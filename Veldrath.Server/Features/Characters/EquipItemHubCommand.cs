using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that equips or unequips an item in a named equipment slot for a character.
/// Equipment is stored separately in <see cref="Data.Entities.Character.EquipmentBlob"/> as a
/// <c>Dictionary&lt;string, string&gt;</c> JSON object keyed by slot name.
/// Pass <see langword="null"/> for <see cref="ItemRef"/> to clear the slot.
/// </summary>
public record EquipItemHubCommand : IRequest<EquipItemHubResult>
{
    /// <summary>Gets the ID of the character equipping the item.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>
    /// Gets the slot name (e.g. <c>"MainHand"</c>, <c>"Head"</c>), or <see langword="null"/> to let
    /// the handler derive it from the item's catalog type. When set it must be a value in
    /// <see cref="EquipItemHubCommandHandler.ValidSlots"/>.
    /// </summary>
    public string? Slot { get; init; }

    /// <summary>Gets the item-reference slug to place in the slot, or <see langword="null"/> to unequip.</summary>
    public string? ItemRef { get; init; }
}

/// <summary>Result returned by <see cref="EquipItemHubCommandHandler"/>.</summary>
public record EquipItemHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the slot that was updated.</summary>
    public string Slot { get; init; } = string.Empty;

    /// <summary>Gets the item-reference slug now in the slot, or <see langword="null"/> if the slot is empty.</summary>
    public string? ItemRef { get; init; }

    /// <summary>Gets the full equipment map (slot → item-ref) after the update.</summary>
    public Dictionary<string, string> AllEquippedItems { get; init; } = [];

    /// <summary>
    /// Gets the slots that were emptied as a side effect of this equip (currently the off hand when a
    /// two-handed weapon is equipped in the main hand).
    /// </summary>
    public IReadOnlyList<string> ClearedSlots { get; init; } = [];
}

/// <summary>
/// Handles <see cref="EquipItemHubCommand"/> by loading the server-side character,
/// deserialising the equipment JSON blob, setting or clearing the specified slot,
/// persisting the change, and returning the updated equipment state.
/// </summary>
public class EquipItemHubCommandHandler : IRequestHandler<EquipItemHubCommand, EquipItemHubResult>
{
    /// <summary>The set of valid slot names accepted by this handler.</summary>
    public static readonly IReadOnlySet<string> ValidSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        EquipmentSlots.MainHand,
        EquipmentSlots.OffHand,
        EquipmentSlots.Head,
        EquipmentSlots.Shoulders,
        EquipmentSlots.Chest,
        EquipmentSlots.Bracers,
        EquipmentSlots.Gloves,
        EquipmentSlots.Belt,
        EquipmentSlots.Legs,
        EquipmentSlots.Feet,
        EquipmentSlots.Ring,
        EquipmentSlots.Amulet,
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<EquipItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EquipItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="itemRepository">Repository used to resolve the slot when the caller omits it.</param>
    /// <param name="logger">Logger instance.</param>
    public EquipItemHubCommandHandler(
        ICharacterRepository characterRepo,
        IItemRepository itemRepository,
        ILogger<EquipItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _itemRepository = itemRepository;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the equip outcome.</summary>
    /// <param name="request">The command containing character ID, slot, and optional item ref.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EquipItemHubResult"/> describing the outcome.</returns>
    public async Task<EquipItemHubResult> Handle(
        EquipItemHubCommand request,
        CancellationToken cancellationToken)
    {
        string? canonicalSlot;
        if (!string.IsNullOrWhiteSpace(request.Slot))
        {
            canonicalSlot = ValidSlots.FirstOrDefault(
                slot => slot.Equals(request.Slot, StringComparison.OrdinalIgnoreCase));

            if (canonicalSlot is null)
                return Fail($"Invalid equipment slot '{request.Slot}'.");
        }
        else
        {
            if (request.ItemRef is null)
                return Fail("A slot is required to unequip an item.");

            canonicalSlot = await DeriveSlotFromItemAsync(request.ItemRef, cancellationToken);
            if (canonicalSlot is null)
                return Fail($"'{request.ItemRef}' cannot be equipped.");
        }

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        var equipment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(character.EquipmentBlob) && character.EquipmentBlob != "{}")
        {
            try
            {
                equipment = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    character.EquipmentBlob, JsonOptions) ?? equipment;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deserialise equipment blob for character {Id}; treating as empty.",
                    character.Id);
            }
        }

        var clearedSlots = new List<string>();
        if (request.ItemRef is null)
        {
            equipment.Remove(canonicalSlot);
        }
        else
        {
            // Two-handed weapons own both hands: equipping one frees the off hand, and an occupied
            // two-handed main hand blocks off-hand items until it is unequipped.
            if (canonicalSlot == EquipmentSlots.MainHand)
            {
                var incoming = await LookupItemAsync(request.ItemRef, cancellationToken);
                if (incoming?.IsTwoHanded == true && equipment.Remove(EquipmentSlots.OffHand))
                    clearedSlots.Add(EquipmentSlots.OffHand);
            }
            else if (canonicalSlot == EquipmentSlots.OffHand
                     && equipment.TryGetValue(EquipmentSlots.MainHand, out var mainHandRef)
                     && !string.IsNullOrWhiteSpace(mainHandRef))
            {
                var mainHand = await LookupItemAsync(mainHandRef, cancellationToken);
                if (mainHand?.IsTwoHanded == true)
                {
                    return Fail(
                        $"'{mainHandRef}' occupies both hands — unequip it before using an off-hand item.");
                }
            }

            equipment[canonicalSlot] = request.ItemRef;
        }

        character.EquipmentBlob = JsonSerializer.Serialize(equipment);
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} {Action} '{ItemRef}' in slot {Slot}",
            request.CharacterId,
            request.ItemRef is null ? "cleared" : "equipped",
            request.ItemRef ?? "(none)",
            canonicalSlot);

        return new EquipItemHubResult
        {
            Success = true,
            Slot = canonicalSlot,
            ItemRef = request.ItemRef,
            AllEquippedItems = new Dictionary<string, string>(equipment, StringComparer.OrdinalIgnoreCase),
            ClearedSlots = clearedSlots,
        };
    }

    /// <summary>
    /// Derives the canonical slot for an item from its catalog type.
    /// </summary>
    /// <param name="itemRef">Item-reference slug being equipped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The canonical slot name, or <see langword="null"/> when the item is unknown or not equippable.</returns>
    private async Task<string?> DeriveSlotFromItemAsync(
        string itemRef,
        CancellationToken cancellationToken)
    {
        var item = await LookupItemAsync(itemRef, cancellationToken);
        return item is null ? null : EquipmentSlots.ForItem(item);
    }

    /// <summary>Loads a catalog item by slug, returning <see langword="null"/> on a miss or failure.</summary>
    /// <param name="itemRef">Item-reference slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catalog item, or <see langword="null"/>.</returns>
    private async Task<Item?> LookupItemAsync(string itemRef, CancellationToken cancellationToken)
    {
        try
        {
            return await _itemRepository.GetBySlugAsync(itemRef);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve item '{ItemRef}' from the catalog.",
                itemRef);
            return null;
        }
    }

    private static EquipItemHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
