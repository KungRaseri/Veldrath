using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>Inventory slot entry as stored in the character's <c>InventoryBlob</c> JSON.</summary>
/// <param name="ItemRef">Item-reference slug (e.g. <c>"iron_sword"</c>).</param>
/// <param name="Quantity">Stack size (always ≥ 1).</param>
/// <param name="Durability">Current durability (0–100), or <see langword="null"/> for stackable items.</param>
public record InventoryItemDto(string ItemRef, int Quantity, int? Durability);

/// <summary>Hub command that fetches the inventory of the active character.</summary>
/// <param name="CharacterId">ID of the character whose inventory to return.</param>
public record GetInventoryHubCommand(Guid CharacterId) : IRequest<GetInventoryHubResult>;

/// <summary>Result returned by <see cref="GetInventoryHubCommandHandler"/>.</summary>
public record GetInventoryHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the items in the character's bag. Items currently equipped are excluded so the
    /// same item never appears in both the inventory list and the equipment panel.
    /// </summary>
    public IReadOnlyList<InventoryItemDto> Items { get; init; } = [];

    /// <summary>
    /// Gets the full catalog details for <see cref="Items"/>, in the same order.
    /// Used by clients that render item names, rarity, and type.
    /// </summary>
    public IReadOnlyList<Item> ItemDetails { get; init; } = [];

    /// <summary>Gets the currently equipped item references keyed by canonical slot name.</summary>
    public IReadOnlyDictionary<string, string> EquippedRefs { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the currently equipped items with full catalog details keyed by canonical slot name.
    /// Sent under a separate property so string-based and item-based clients can both bind it.
    /// </summary>
    public IReadOnlyDictionary<string, Item> Equipped { get; init; } =
        new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Handles <see cref="GetInventoryHubCommand"/> by loading the server-side character,
/// deserialising its <c>InventoryBlob</c> and <c>EquipmentBlob</c> JSON, and resolving
/// catalog details for every referenced item.
/// Items that are currently equipped are removed from the bag list so clients do not
/// display the same item twice, and the equipment map is returned alongside the bag.
/// </summary>
public class GetInventoryHubCommandHandler : IRequestHandler<GetInventoryHubCommand, GetInventoryHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly IItemRepository _itemRepository;
    private readonly ILogger<GetInventoryHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetInventoryHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load characters.</param>
    /// <param name="itemRepository">Repository used to resolve item-reference slugs to catalog items.</param>
    /// <param name="logger">Logger instance.</param>
    public GetInventoryHubCommandHandler(
        ICharacterRepository characterRepo,
        IItemRepository itemRepository,
        ILogger<GetInventoryHubCommandHandler> logger)
    {
        _characterRepo  = characterRepo;
        _itemRepository = itemRepository;
        _logger         = logger;
    }

    /// <summary>Handles the command and returns the character's current inventory.</summary>
    /// <param name="request">The command containing the character ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GetInventoryHubResult"/> with the bag and equipment snapshots.</returns>
    public async Task<GetInventoryHubResult> Handle(
        GetInventoryHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        var items     = ParseInventory(character.InventoryBlob);
        var equipment = ParseEquipment(character.EquipmentBlob);

        var equippedRefs = equipment.Values
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Equipped items live in the equipment blob. Remove them from the bag view so the
        // same item is not listed in both the inventory and equipment panels.
        var bagItems = items
            .Where(item => !equippedRefs.Contains(item.ItemRef))
            .ToList();

        var itemDetails = new List<Item>(bagItems.Count);
        foreach (var entry in bagItems)
            itemDetails.Add(await ResolveItemAsync(entry.ItemRef, entry.Quantity, slot: null, cancellationToken));

        var equippedItems = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slot, itemRef) in equipment)
            equippedItems[slot] = await ResolveItemAsync(itemRef, quantity: 1, slot, cancellationToken);

        return new GetInventoryHubResult
        {
            Success      = true,
            Items        = bagItems,
            ItemDetails  = itemDetails,
            EquippedRefs = equipment,
            Equipped     = equippedItems,
        };
    }

    private async Task<Item> ResolveItemAsync(
        string itemRef,
        int quantity,
        string? slot,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _itemRepository.GetBySlugAsync(itemRef);
            if (item is not null)
            {
                item.Quantity = quantity;
                return item;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve item '{ItemRef}' from the catalog; using fallback details.", itemRef);
        }

        return BuildFallbackItem(itemRef, quantity, slot);
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
            _logger.LogWarning(
                ex,
                "Failed to deserialise inventory blob; treating as empty.");
            return [];
        }
    }

    private Dictionary<string, string> ParseEquipment(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "{}")
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var equipment = JsonSerializer.Deserialize<Dictionary<string, string>>(blob, JsonOptions);
            return equipment is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(equipment, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialise equipment blob; treating as empty.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Item BuildFallbackItem(string slug, int quantity, string? slot)
    {
        var isShield = IsShield(slug);
        return new Item
        {
            Slug        = slug,
            Name        = HumanizeSlug(slug),
            Quantity    = quantity,
            Type        = InferItemType(slug, slot),
            TypeKey     = isShield ? "shield" : null,
            ArmorType   = isShield ? "shield" : null,
            StackSize   = 1,
            IsStackable = false,
        };
    }

    private static ItemType InferItemType(string slug, string? slot)
    {
        if (IsShield(slug))
            return ItemType.Shield;

        return slot switch
        {
            "MainHand" => ItemType.Weapon,
            "Head"     => ItemType.Helmet,
            "Chest"    => ItemType.Chest,
            "Legs"     => ItemType.Legs,
            "Feet"     => ItemType.Boots,
            "Ring"     => ItemType.Ring,
            "Amulet"   => ItemType.Necklace,
            _          => InferItemTypeFromSlug(slug),
        };
    }

    private static ItemType InferItemTypeFromSlug(string slug)
    {
        if (slug.Contains("shield", StringComparison.OrdinalIgnoreCase))
            return ItemType.Shield;
        if (slug.Contains("sword", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("axe", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("bow", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("dagger", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("staff", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("mace", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("hammer", StringComparison.OrdinalIgnoreCase)
            || slug.Contains("spear", StringComparison.OrdinalIgnoreCase))
            return ItemType.Weapon;

        return ItemType.Consumable;
    }

    private static bool IsShield(string slug) =>
        slug.Contains("shield", StringComparison.OrdinalIgnoreCase);

    private static string HumanizeSlug(string slug)
    {
        var leaf = slug.Split(':', '/').LastOrDefault() ?? slug;
        var words = leaf.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0
                ? word
                : char.ToUpperInvariant(word[0]) + word[1..]);
        return string.Join(' ', words);
    }

    private static GetInventoryHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
