using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>Service for loading and caching item catalog data from the content database.</summary>
public class ItemDataService
{
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly ILogger<ItemDataService> _logger;
    private readonly Dictionary<string, List<ItemTemplate>> _cache = new();

    /// <summary>Initializes a new instance of <see cref="ItemDataService"/>.</summary>
    /// <param name="dbFactory">Factory used to create database contexts.</param>
    /// <param name="logger">Logger instance.</param>
    public ItemDataService(IDbContextFactory<ContentDbContext> dbFactory, ILogger<ItemDataService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>Loads and caches all items in the specified category, optionally filtered by rarity.</summary>
    /// <param name="category">The TypeKey category to load (e.g. "sword", "helmet").</param>
    /// <param name="rarityFilter">Optional rarity filter; returns all rarities when <see langword="null"/>.</param>
    /// <returns>Matching item templates, or an empty list on database failure.</returns>
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
            _logger.LogInformation("Loaded {Count} items from {Category} catalog", items.Count, category);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading catalog {Category}", category);
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
            results.Add(new ItemTemplate { Slug = w.Slug, Name = w.DisplayName ?? w.TypeKey, Category = category, Type = w.WeaponType, RarityWeight = w.RarityWeight, BasePrice = w.Stats.Value ?? 0, Rarity = rarity });
        }

        foreach (var a in db.Armors.AsNoTracking().Where(a => a.IsActive && a.TypeKey == category).ToList())
        {
            var rarity = GetRarity(a.RarityWeight);
            if (rarityFilter.HasValue && rarity != rarityFilter.Value) continue;
            results.Add(new ItemTemplate { Slug = a.Slug, Name = a.DisplayName ?? a.TypeKey, Category = category, Type = a.ArmorType, RarityWeight = a.RarityWeight, BasePrice = a.Stats.Value ?? 0, Rarity = rarity });
        }

        foreach (var i in db.Items.AsNoTracking().Where(i => i.IsActive && i.TypeKey == category).ToList())
        {
            var rarity = GetRarity(i.RarityWeight);
            if (rarityFilter.HasValue && rarity != rarityFilter.Value) continue;
            results.Add(new ItemTemplate { Slug = i.Slug, Name = i.DisplayName ?? i.TypeKey, Category = category, Type = i.ItemType, RarityWeight = i.RarityWeight, BasePrice = i.Stats.Value ?? 0, Rarity = rarity });
        }

        return results;
    }

    /// <summary>Loads catalog items from multiple categories in a single pass.</summary>
    /// <param name="categories">List of category TypeKeys to load.</param>
    /// <param name="rarityFilter">Optional rarity filter applied to each category.</param>
    /// <returns>Aggregated item templates from all requested categories.</returns>
    public List<ItemTemplate> LoadMultipleCategories(List<string> categories, ItemRarity? rarityFilter = null)
    {
        var allItems = new List<ItemTemplate>();
        foreach (var category in categories)
            allItems.AddRange(LoadCatalog(category, rarityFilter));
        return allItems;
    }

    /// <summary>Clears all cached catalog data, forcing a fresh database load on the next request.</summary>
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

/// <summary>Lightweight item descriptor used by <see cref="ItemDataService"/> and the generator pipeline.</summary>
public class ItemTemplate
{
    /// <summary>Gets or sets the unique slug identifier for this item.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name shown to players.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the TypeKey category (e.g. "sword", "helmet").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the item sub-type within the category.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the rarity weight used for weighted-selection and probability calculations.</summary>
    public int RarityWeight { get; set; }

    /// <summary>Gets or sets the base gold value of the item before modifiers.</summary>
    public int BasePrice { get; set; }

    /// <summary>Gets or sets the resolved rarity tier for this item.</summary>
    public ItemRarity Rarity { get; set; }
}