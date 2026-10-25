using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that consumes one unit of a usable item (potion, elixir, scroll) from the character's
/// bag, applies its effect to the character's attributes, and persists the result.
/// </summary>
/// <remarks>
/// Only consumables with a recognised <see cref="Item.Effect"/> can be used; equippable items must go
/// through <see cref="EquipItemHubCommand"/> instead.
/// </remarks>
public record UseItemHubCommand : IRequest<UseItemHubResult>
{
    /// <summary>Gets the ID of the character using the item.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>Gets the item-reference slug to consume (e.g. <c>"health_potion"</c>).</summary>
    public required string ItemRef { get; init; }
}

/// <summary>Result returned by <see cref="UseItemHubCommandHandler"/>.</summary>
public record UseItemHubResult
{
    /// <summary>Gets a value indicating whether the item was consumed.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the item-reference slug that was consumed.</summary>
    public string ItemRef { get; init; } = string.Empty;

    /// <summary>Gets the number of units left in the bag after consuming one.</summary>
    public int RemainingQuantity { get; init; }

    /// <summary>Gets the character's current health after the effect.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's maximum health.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets the character's current mana after the effect.</summary>
    public int CurrentMana { get; init; }

    /// <summary>Gets the character's maximum mana.</summary>
    public int MaxMana { get; init; }

    /// <summary>Gets a player-facing description of what the item did.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handles <see cref="UseItemHubCommand"/> by loading the character and its bag, resolving the catalog
/// item, applying the consumable effect, decrementing the stack, and persisting both blobs.
/// </summary>
public class UseItemHubCommandHandler : IRequestHandler<UseItemHubCommand, UseItemHubResult>
{
    // Keys within the character Attributes JSON blob
    internal const string KeyCurrentHealth = "CurrentHealth";
    internal const string KeyMaxHealth = "MaxHealth";
    internal const string KeyCurrentMana = "CurrentMana";
    internal const string KeyMaxMana = "MaxMana";

    internal const string EffectHeal = "heal";
    internal const string EffectRestore = "restore";
    internal const string EffectHealOverTime = "heal_overtime";
    internal const string EffectCurePoison = "cure_poison";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<UseItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="UseItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="itemRepository">Repository used to resolve item-reference slugs to catalog items.</param>
    /// <param name="logger">Logger instance.</param>
    public UseItemHubCommandHandler(
        ICharacterRepository characterRepo,
        IItemRepository itemRepository,
        ILogger<UseItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _itemRepository = itemRepository;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the consumption outcome.</summary>
    /// <param name="request">The command containing the character ID and item ref.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="UseItemHubResult"/> describing the outcome.</returns>
    public async Task<UseItemHubResult> Handle(
        UseItemHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemRef))
            return Fail("An item reference is required.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        var inventory = ParseInventory(character.InventoryBlob);
        var entryIndex = inventory.FindIndex(
            entry => string.Equals(entry.ItemRef, request.ItemRef, StringComparison.OrdinalIgnoreCase));

        if (entryIndex < 0)
            return Fail($"You do not have '{request.ItemRef}' in your bag.");

        Item? catalogItem;
        try
        {
            catalogItem = await _itemRepository.GetBySlugAsync(request.ItemRef);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to resolve item '{ItemRef}' from the catalog.", request.ItemRef);
            catalogItem = null;
        }

        if (catalogItem is null)
            return Fail($"'{request.ItemRef}' is not a usable item.");

        var attributes = ParseAttributes(character.Attributes);
        var health = attributes.GetValueOrDefault(KeyCurrentHealth, 0);
        var maxHealth = attributes.GetValueOrDefault(KeyMaxHealth, 0);
        var mana = attributes.GetValueOrDefault(KeyCurrentMana, 0);
        var maxMana = attributes.GetValueOrDefault(KeyMaxMana, 0);

        var applied = ApplyEffect(catalogItem, ref health, maxHealth, ref mana, maxMana, out var message);
        if (!applied)
            return Fail(message);

        var entry = inventory[entryIndex];
        var remaining = entry.Quantity - 1;
        if (remaining > 0)
            inventory[entryIndex] = entry with { Quantity = remaining };
        else
            inventory.RemoveAt(entryIndex);

        attributes[KeyCurrentHealth] = health;
        attributes[KeyCurrentMana] = mana;

        character.InventoryBlob = JsonSerializer.Serialize(inventory);
        character.Attributes = JsonSerializer.Serialize(attributes);
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} used '{ItemRef}'; {Remaining} left",
            request.CharacterId,
            request.ItemRef,
            remaining);

        return new UseItemHubResult
        {
            Success = true,
            ItemRef = request.ItemRef,
            RemainingQuantity = Math.Max(0, remaining),
            CurrentHealth = health,
            MaxHealth = maxHealth,
            CurrentMana = mana,
            MaxMana = maxMana,
            Message = message,
        };
    }

    /// <summary>
    /// Applies a consumable's effect to the supplied health/mana values.
    /// </summary>
    /// <param name="item">Catalog item being consumed.</param>
    /// <param name="health">Current health, updated in place.</param>
    /// <param name="maxHealth">Maximum health.</param>
    /// <param name="mana">Current mana, updated in place.</param>
    /// <param name="maxMana">Maximum mana.</param>
    /// <param name="message">Player-facing description of the effect.</param>
    /// <returns><see langword="true"/> when the effect was applied.</returns>
    private static bool ApplyEffect(
        Item item,
        ref int health,
        int maxHealth,
        ref int mana,
        int maxMana,
        out string message)
    {
        var name = string.IsNullOrWhiteSpace(item.Name) ? item.Slug : item.Name;
        var power = Math.Max(0, item.Power);
        var effect = item.Effect?.Trim().ToLowerInvariant();

        switch (effect)
        {
            case EffectHeal:
                {
                    if (power == 0)
                    {
                        message = $"The {name} has no healing power.";
                        return false;
                    }

                    var before = health;
                    health = maxHealth > 0 ? Math.Min(maxHealth, health + power) : health + power;
                    message = $"You drink the {name} and recover {health - before} health.";
                    return true;
                }

            case EffectHealOverTime:
                {
                    if (power == 0)
                    {
                        message = $"The {name} has no healing power.";
                        return false;
                    }

                    var restored = Math.Max(1, power / 2);
                    var before = health;
                    health = maxHealth > 0 ? Math.Min(maxHealth, health + restored) : health + restored;
                    message = $"The {name} mends you for {health - before} health and keeps working.";
                    return true;
                }

            case EffectRestore:
                {
                    if (power == 0)
                    {
                        message = $"The {name} has no restorative power.";
                        return false;
                    }

                    var before = mana;
                    mana = maxMana > 0 ? Math.Min(maxMana, mana + power) : mana + power;
                    message = $"You drink the {name} and recover {mana - before} mana.";
                    return true;
                }

            case EffectCurePoison:
                message = $"You use the {name}; the poison in your veins fades.";
                return true;

            case null or "":
                message = $"The {name} cannot be used.";
                return false;

            default:
                message = $"The {name} cannot be used ({effect}).";
                return false;
        }
    }

    private List<InventoryItemDto> ParseInventory(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<InventoryItemDto>>(blob, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialise inventory blob; treating as empty.");
            return [];
        }
    }

    private Dictionary<string, int> ParseAttributes(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "{}")
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(blob, JsonOptions)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialise attributes blob; treating as empty.");
            return [];
        }
    }

    private static UseItemHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
