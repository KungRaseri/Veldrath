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
    /// Gets every item the character owns, as flat projections. Equipped items are included and
    /// flagged through <see cref="InventoryItemView.IsEquipped"/> so the character sheet can show a
    /// single inventory of record with an "equipped" marker.
    /// </summary>
    public IReadOnlyList<InventoryItemView> Items { get; init; } = [];

    /// <summary>Gets the equipped items keyed by canonical slot name.</summary>
    public IReadOnlyDictionary<string, InventoryItemView> Equipped { get; init; } =
        new Dictionary<string, InventoryItemView>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Handles <see cref="GetInventoryHubCommand"/> by loading the server-side character,
/// deserialising its <c>InventoryBlob</c> and <c>EquipmentBlob</c> JSON, resolving catalog details
/// for every referenced item, and projecting them into flat <see cref="InventoryItemView"/> records.
/// </summary>
/// <remarks>
/// Equipped items stay in the item list (flagged) instead of being filtered out: the web character
/// sheet renders one list, and hiding the equipped rows there made items appear to vanish after
/// equipping.
/// </remarks>
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
        _characterRepo = characterRepo;
        _itemRepository = itemRepository;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the character's current inventory.</summary>
    /// <param name="request">The command containing the character ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GetInventoryHubResult"/> with the item list and equipment snapshot.</returns>
    public async Task<GetInventoryHubResult> Handle(
        GetInventoryHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        var items = ParseInventory(character.InventoryBlob);
        var equipment = ParseEquipment(character.EquipmentBlob);

        var equippedRefs = equipment.Values
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catalogCache = new Dictionary<string, Item?>(StringComparer.OrdinalIgnoreCase);
        var views = new List<InventoryItemView>(items.Count + equipment.Count);
        var seenRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in items)
        {
            var catalogItem = await ResolveCatalogItemAsync(entry.ItemRef, catalogCache, cancellationToken);
            seenRefs.Add(entry.ItemRef);
            views.Add(BuildView(
                entry.ItemRef,
                catalogItem,
                quantity: entry.Quantity,
                durability: entry.Durability,
                isEquipped: equippedRefs.Contains(entry.ItemRef)));
        }

        var equippedViews = new Dictionary<string, InventoryItemView>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slot, itemRef) in equipment)
        {
            if (string.IsNullOrWhiteSpace(itemRef))
                continue;

            var catalogItem = await ResolveCatalogItemAsync(itemRef, catalogCache, cancellationToken);
            var view = BuildView(itemRef, catalogItem, quantity: 1, durability: null, isEquipped: true);
            equippedViews[slot] = view;

            // Starting equipment is written to the equipment blob only, so top it up in the list.
            if (seenRefs.Add(itemRef))
                views.Add(view);
        }

        return new GetInventoryHubResult
        {
            Success = true,
            Items = views,
            Equipped = equippedViews,
        };
    }

    private async Task<Item?> ResolveCatalogItemAsync(
        string itemRef,
        IDictionary<string, Item?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(itemRef, out var cached))
            return cached;

        Item? item = null;
        try
        {
            item = await _itemRepository.GetBySlugAsync(itemRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve item '{ItemRef}' from the catalog; using fallback details.",
                itemRef);
        }

        cache[itemRef] = item;
        return item;
    }

    private static InventoryItemView BuildView(
        string itemRef,
        Item? catalogItem,
        int quantity,
        int? durability,
        bool isEquipped)
    {
        if (catalogItem is null)
            return BuildFallbackView(itemRef, quantity, durability, isEquipped);

        var slot = EquipmentSlots.ForItem(catalogItem);

        return new InventoryItemView(
            ItemRef: itemRef,
            Name: string.IsNullOrWhiteSpace(catalogItem.Name) ? HumanizeSlug(itemRef) : catalogItem.Name,
            Description: string.IsNullOrWhiteSpace(catalogItem.Description) ? null : catalogItem.Description,
            Rarity: catalogItem.Rarity.ToString(),
            Type: catalogItem.Type.ToString(),
            TypeKey: catalogItem.TypeKey,
            Slot: slot,
            Quantity: quantity,
            StackSize: Math.Max(1, catalogItem.StackSize),
            IsStackable: catalogItem.IsStackable,
            IsEquipped: isEquipped,
            Price: catalogItem.Price,
            Weight: catalogItem.Weight,
            Durability: durability);
    }

    private static InventoryItemView BuildFallbackView(
        string itemRef,
        int quantity,
        int? durability,
        bool isEquipped)
    {
        var isShield = IsShield(itemRef);
        var type = InferItemType(itemRef, slot: null);

        return new InventoryItemView(
            ItemRef: itemRef,
            Name: HumanizeSlug(itemRef),
            Description: null,
            Rarity: ItemRarity.Common.ToString(),
            Type: type.ToString(),
            TypeKey: isShield ? "shield" : null,
            Slot: EquipmentSlots.ForItemType(type),
            Quantity: quantity,
            StackSize: 1,
            IsStackable: false,
            IsEquipped: isEquipped,
            Price: 0,
            Weight: 0,
            Durability: durability);
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

    private static ItemType InferItemType(string slug, string? slot)
    {
        if (IsShield(slug))
            return ItemType.Shield;

        return slot switch
        {
            EquipmentSlots.MainHand => ItemType.Weapon,
            EquipmentSlots.Head => ItemType.Helmet,
            EquipmentSlots.Chest => ItemType.Chest,
            EquipmentSlots.Legs => ItemType.Legs,
            EquipmentSlots.Feet => ItemType.Boots,
            EquipmentSlots.Ring => ItemType.Ring,
            EquipmentSlots.Amulet => ItemType.Necklace,
            _ => InferItemTypeFromSlug(slug),
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
