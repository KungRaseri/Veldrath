using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;
using Serilog;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading item catalogs from JSON files.
/// Reads weapon, armor, and consumable definitions for shop inventory generation.
/// </summary>
public class ItemCatalogLoader
{
    private readonly GameDataCache _dataCache;
    private readonly Dictionary<string, List<ItemTemplate>> _cache = new();

    /// <summary>
    /// Initializes a new instance of the ItemCatalogLoader class.
    /// </summary>
    /// <param name="dataCache">The game data cache service.</param>
    public ItemCatalogLoader(GameDataCache dataCache)
    {
        _dataCache = dataCache;
    }

    /// <summary>
    /// Load items from a specific catalog file.
    /// </summary>
    /// <param name="category">The item category (weapons, armor, consumables).</param>
    /// <param name="rarityFilter">Optional rarity filter (Common, Uncommon, Rare, Epic, Legendary).</param>
    /// <returns>List of item templates matching the category and rarity.</returns>
    public List<ItemTemplate> LoadCatalog(string category, ItemRarity? rarityFilter = null)
    {
        var cacheKey = $"{category}_{rarityFilter}";
        
        if (_cache.ContainsKey(cacheKey))
        {
            return _cache[cacheKey];
        }

        var catalogPath = $"items/{category}/catalog.json";
        var catalogFile = _dataCache.GetFile(catalogPath);
        
        if (catalogFile == null)
        {
            Log.Warning("Catalog file not found: {CatalogPath}", catalogPath);
            return new List<ItemTemplate>();
        }

        try
        {
            var catalog = catalogFile.JsonData;
            var items = new List<ItemTemplate>();

            // Parse catalog structure
            var typeKey = category switch
            {
                "weapons" => "weapon_types",
                "armor" => "armor_types",
                "consumables" => "consumable_types",
                _ => null
            };

            if (typeKey == null || catalog[typeKey] == null)
            {
                Log.Warning("Unknown category or missing type key: {Category}", category);
                return new List<ItemTemplate>();
            }

            var types = (JObject)catalog[typeKey]!;

            foreach (var typeProperty in types.Properties())
            {
                var typeName = typeProperty.Name;
                var typeData = (JObject)typeProperty.Value;
                var typeProperties = typeData["properties"];
                var typeItems = typeData["items"] as JArray;

                if (typeItems == null) continue;

                foreach (var itemToken in typeItems)
                {
                    var item = itemToken.ToObject<ItemCatalogEntry>();
                    if (item == null) continue;

                    // Apply rarity filter if specified
                    if (rarityFilter.HasValue)
                    {
                        var itemRarity = ParseRarity(item.Rarity);
                        if (itemRarity != rarityFilter.Value)
                            continue;
                    }

                    // Create item template
                    var template = new ItemTemplate
                    {
                        Name = item.Name,
                        Category = category,
                        Type = typeName,
                        RarityWeight = item.RarityWeight,
                        BasePrice = item.Value,
                        Rarity = ParseRarity(item.Rarity)
                    };

                    items.Add(template);
                }
            }

            _cache[cacheKey] = items;
            Log.Information("Loaded {Count} items from {Category} catalog", items.Count, category);
            return items;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading catalog {Category}", category);
            return new List<ItemTemplate>();
        }
    }

    /// <summary>
    /// Load multiple categories at once.
    /// </summary>
    /// <param name="categories">List of categories to load.</param>
    /// <param name="rarityFilter">Optional rarity filter.</param>
    /// <returns>Combined list of item templates.</returns>
    public List<ItemTemplate> LoadMultipleCategories(List<string> categories, ItemRarity? rarityFilter = null)
    {
        var allItems = new List<ItemTemplate>();
        
        foreach (var category in categories)
        {
            allItems.AddRange(LoadCatalog(category, rarityFilter));
        }

        return allItems;
    }

    /// <summary>
    /// Parse rarity value from catalog (number or string).
    /// </summary>
    private ItemRarity ParseRarity(int rarityValue)
    {
        return rarityValue switch
        {
            >= 75 => ItemRarity.Common,
            >= 50 => ItemRarity.Uncommon,
            >= 25 => ItemRarity.Rare,
            >= 10 => ItemRarity.Epic,
            _ => ItemRarity.Legendary
        };
    }

    /// <summary>
    /// Clear the internal cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
}

/// <summary>
/// Represents a simplified item template for shop generation.
/// </summary>
public class ItemTemplate
{
    /// <summary>Gets or sets the item name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the item category (weapons, armor, consumables).</summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the item type within category (longsword, helmet, potion).</summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the rarity weight for selection.</summary>
    public int RarityWeight { get; set; }
    
    /// <summary>Gets or sets the base price in gold.</summary>
    public int BasePrice { get; set; }
    
    /// <summary>Gets or sets the item rarity.</summary>
    public ItemRarity Rarity { get; set; }
}

/// <summary>
/// Internal class for deserializing catalog entries.
/// </summary>
internal class ItemCatalogEntry
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;
    
    [JsonProperty("rarity")]
    public int Rarity { get; set; }
    
    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }
    
    [JsonProperty("value")]
    public int Value { get; set; }
}
