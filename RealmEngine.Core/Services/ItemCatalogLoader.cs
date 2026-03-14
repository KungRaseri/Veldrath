using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using Serilog;

namespace RealmEngine.Core.Services;

public class ItemCatalogLoader
{
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly Dictionary<string, List<ItemTemplate>> _cache = new();

    public ItemCatalogLoader(IDbContextFactory<ContentDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public List<ItemTemplate> LoadCatalog(string category, ItemRarity? rarityFilter = null)
    {
        var cacheKey = $"{category}_{rarityFilter}";

        if (_cache.ContainsKey(cacheKey))
            return _cache[cacheKey];

        try
        {
            using var db = _dbFactory.CreateDbContext();
            var items = QueryCategory(db, category, rarityFilter);
            _cache[cacheKey] = items;
            Log.Information("Loaded {Count} items from {Category} catalog", items.Count, category);
            return items;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading catalog {Category}", category);
            return [];
        }
    }

    private static List<ItemTemplate> QueryCategory(ContentDbContext db, string category, ItemRarity? rarityFilter)
    {
        var results = new List<ItemTemplate>();

        foreach (var w in db.Weapons.AsNoTracking().Where(w => w.IsActive && w.TypeKey == category).ToList())
        {
            var rarity = GetRarity(w.RarityWeight);
            if (rarityFilter.HasValue && rarity != rarityFilter.Value) continue;
            results.Add(new ItemTemplate { Name = w.DisplayName ?? w.TypeKey, Category = category, Type = w.WeaponType, RarityWeight = w.RarityWeight, BasePrice = w.Stats.Value ?? 0, Rarity = rarity });
        }

        foreach (var a in db.Armors.AsNoTracking().Where(a => a.IsActive && a.TypeKey == category).ToList())
        {
            var rarity = GetRarity(a.RarityWeight);
            if (rarityFilter.HasValue && rarity != rarityFilter.Value) continue;
            results.Add(new ItemTemplate { Name = a.DisplayName ?? a.TypeKey, Category = category, Type = a.ArmorType, RarityWeight = a.RarityWeight, BasePrice = a.Stats.Value ?? 0, Rarity = rarity });
        }

        foreach (var i in db.Items.AsNoTracking().Where(i => i.IsActive && i.TypeKey == category).ToList())
        {
            var rarity = GetRarity(i.RarityWeight);
            if (rarityFilter.HasValue && rarity != rarityFilter.Value) continue;
            results.Add(new ItemTemplate { Name = i.DisplayName ?? i.TypeKey, Category = category, Type = i.ItemType, RarityWeight = i.RarityWeight, BasePrice = i.Stats.Value ?? 0, Rarity = rarity });
        }

        return results;
    }

    public List<ItemTemplate> LoadMultipleCategories(List<string> categories, ItemRarity? rarityFilter = null)
    {
        var allItems = new List<ItemTemplate>();
        foreach (var category in categories)
            allItems.AddRange(LoadCatalog(category, rarityFilter));
        return allItems;
    }

    public void ClearCache() => _cache.Clear();

    private static ItemRarity GetRarity(int rarityWeight) => rarityWeight switch
    {
        >= 75 => ItemRarity.Common,
        >= 50 => ItemRarity.Uncommon,
        >= 25 => ItemRarity.Rare,
        >= 10 => ItemRarity.Epic,
        _ => ItemRarity.Legendary
    };
}

public class ItemTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int RarityWeight { get; set; }
    public int BasePrice { get; set; }
    public ItemRarity Rarity { get; set; }
}