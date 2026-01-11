using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Queries.GetPlayerInventory;

/// <summary>
/// Handler for retrieving player inventory with filtering and sorting.
/// </summary>
public class GetPlayerInventoryQueryHandler : IRequestHandler<GetPlayerInventoryQuery, InventoryQueryResult>
{
    private readonly SaveGameService _saveGameService;
    private readonly ILogger<GetPlayerInventoryQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPlayerInventoryQueryHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GetPlayerInventoryQueryHandler(
        SaveGameService saveGameService,
        ILogger<GetPlayerInventoryQueryHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the player inventory query with filtering and sorting.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inventory query result.</returns>
    public Task<InventoryQueryResult> Handle(GetPlayerInventoryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character?.Inventory == null)
            {
                return Task.FromResult(new InventoryQueryResult
                {
                    Success = false,
                    ErrorMessage = "No active save game or inventory found"
                });
            }

            var equippedIds = GetEquippedItemIds(saveGame.Character);
            var items = saveGame.Character.Inventory
                .Select(item => CreateInventoryItemInfo(item, equippedIds))
                .ToList();

            // Apply filters
            items = ApplyFilters(items, request);

            // Apply sorting
            items = ApplySorting(items, request);

            // Generate summary
            var summary = GenerateSummary(saveGame.Character.Inventory, equippedIds);

            _logger.LogInformation("Retrieved {Count} inventory items (filtered from {Total})",
                items.Count, saveGame.Character.Inventory.Count);

            return Task.FromResult(new InventoryQueryResult
            {
                Success = true,
                Items = items,
                Summary = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving player inventory");
            return Task.FromResult(new InventoryQueryResult
            {
                Success = false,
                ErrorMessage = $"Failed to retrieve inventory: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Gets a set of all equipped item IDs from the character.
    /// </summary>
    private static HashSet<string> GetEquippedItemIds(Character character)
    {
        var equipped = new HashSet<string>();
        
        if (character.EquippedMainHand != null) equipped.Add(character.EquippedMainHand.Id);
        if (character.EquippedOffHand != null) equipped.Add(character.EquippedOffHand.Id);
        if (character.EquippedHelmet != null) equipped.Add(character.EquippedHelmet.Id);
        if (character.EquippedShoulders != null) equipped.Add(character.EquippedShoulders.Id);
        if (character.EquippedChest != null) equipped.Add(character.EquippedChest.Id);
        if (character.EquippedBracers != null) equipped.Add(character.EquippedBracers.Id);
        if (character.EquippedGloves != null) equipped.Add(character.EquippedGloves.Id);
        if (character.EquippedBelt != null) equipped.Add(character.EquippedBelt.Id);
        if (character.EquippedLegs != null) equipped.Add(character.EquippedLegs.Id);
        if (character.EquippedBoots != null) equipped.Add(character.EquippedBoots.Id);
        if (character.EquippedNecklace != null) equipped.Add(character.EquippedNecklace.Id);
        if (character.EquippedRing1 != null) equipped.Add(character.EquippedRing1.Id);
        if (character.EquippedRing2 != null) equipped.Add(character.EquippedRing2.Id);

        return equipped;
    }

    /// <summary>
    /// Creates an InventoryItemInfo DTO from an Item model.
    /// </summary>
    private static InventoryItemInfo CreateInventoryItemInfo(Item item, HashSet<string> equippedIds)
    {
        return new InventoryItemInfo
        {
            Id = item.Id,
            Name = item.Name,
            ItemType = item.Type.ToString(),
            Rarity = item.Rarity.ToString(),
            Value = item.Price,
            Quantity = 1, // Note: Item.Quantity property exists but inventory system treats each as unique for now
            TotalValue = item.Price,
            IsEquipped = equippedIds.Contains(item.Id),
            IsSocketable = item.Sockets?.Any() == true,
            SocketCount = item.Sockets?.Sum(kvp => kvp.Value.Count),
            Prefixes = item.Prefixes?.Select(p => p.Value).ToList() ?? new List<string>(),
            Suffixes = item.Suffixes?.Select(s => s.Value).ToList() ?? new List<string>()
        };
    }

    /// <summary>
    /// Applies filters from the query to the item list.
    /// </summary>
    private static List<InventoryItemInfo> ApplyFilters(List<InventoryItemInfo> items, GetPlayerInventoryQuery request)
    {
        if (!string.IsNullOrEmpty(request.ItemTypeFilter))
        {
            items = items.Where(i => i.ItemType.Equals(request.ItemTypeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(request.RarityFilter))
        {
            items = items.Where(i => i.Rarity.Equals(request.RarityFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (request.MinValue.HasValue)
        {
            items = items.Where(i => i.Value >= request.MinValue.Value).ToList();
        }

        if (request.MaxValue.HasValue)
        {
            items = items.Where(i => i.Value <= request.MaxValue.Value).ToList();
        }

        return items;
    }

    /// <summary>
    /// Applies sorting from the query to the item list.
    /// </summary>
    private static List<InventoryItemInfo> ApplySorting(List<InventoryItemInfo> items, GetPlayerInventoryQuery request)
    {
        if (string.IsNullOrEmpty(request.SortBy))
        {
            return items;
        }

        var sorted = request.SortBy.ToLowerInvariant() switch
        {
            "name" => items.OrderBy(i => i.Name),
            "value" => items.OrderBy(i => i.Value),
            "rarity" => items.OrderBy(i => i.Rarity),
            "type" => items.OrderBy(i => i.ItemType),
            _ => items.AsEnumerable()
        };

        return request.SortDescending
            ? sorted.Reverse().ToList()
            : sorted.ToList();
    }

    /// <summary>
    /// Generates summary statistics for the inventory.
    /// </summary>
    private static InventorySummary GenerateSummary(List<Item> inventory, HashSet<string> equippedIds)
    {
        var itemsByType = new Dictionary<string, int>();
        var itemsByRarity = new Dictionary<string, int>();
        int totalValue = 0;

        foreach (var item in inventory)
        {
            // Count by type
            var typeStr = item.Type.ToString();
            if (!itemsByType.ContainsKey(typeStr))
            {
                itemsByType[typeStr] = 0;
            }
            itemsByType[typeStr]++;

            // Count by rarity
            var rarityStr = item.Rarity.ToString();
            if (!itemsByRarity.ContainsKey(rarityStr))
            {
                itemsByRarity[rarityStr] = 0;
            }
            itemsByRarity[rarityStr]++;

            // Sum value
            totalValue += item.Price;
        }

        return new InventorySummary
        {
            TotalItems = inventory.Count,
            UniqueItems = inventory.Count, // Each item is unique; grouping by name would require inventory refactor
            TotalValue = totalValue,
            EquippedItems = equippedIds.Count,
            ItemsByType = itemsByType,
            ItemsByRarity = itemsByRarity
        };
    }
}
