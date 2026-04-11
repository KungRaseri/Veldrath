using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
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

    /// <summary>Gets the slot name (e.g. <c>"MainHand"</c>, <c>"Head"</c>). Must be a value in <see cref="EquipItemHubCommandHandler.ValidSlots"/>.</summary>
    public required string Slot { get; init; }

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
        "MainHand", "OffHand", "Head", "Chest", "Legs", "Feet", "Ring", "Amulet",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<EquipItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EquipItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public EquipItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<EquipItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the equip outcome.</summary>
    /// <param name="request">The command containing character ID, slot, and optional item ref.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EquipItemHubResult"/> describing the outcome.</returns>
    public async Task<EquipItemHubResult> Handle(
        EquipItemHubCommand request,
        CancellationToken cancellationToken)
    {
        if (!ValidSlots.Contains(request.Slot))
            return Fail($"Invalid equipment slot '{request.Slot}'.");

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

        // Normalize slot to canonical casing from ValidSlots
        var canonicalSlot = ValidSlots.First(s => s.Equals(request.Slot, StringComparison.OrdinalIgnoreCase));

        if (request.ItemRef is null)
            equipment.Remove(canonicalSlot);
        else
            equipment[canonicalSlot] = request.ItemRef;

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
            Success          = true,
            Slot             = canonicalSlot,
            ItemRef          = request.ItemRef,
            AllEquippedItems = new Dictionary<string, string>(equipment, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static EquipItemHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
