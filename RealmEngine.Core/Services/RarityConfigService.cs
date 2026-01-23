using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using Serilog;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading rarity tier configuration from JSON.
/// Provides tier definitions, colors, and drop chances for rarity system.
/// </summary>
public class RarityConfigService
{
    private readonly GameDataCache _dataCache;
    private RarityConfig? _cachedConfig;

    /// <summary>
    /// Initializes a new instance of the RarityConfigService class.
    /// </summary>
    /// <param name="dataCache">The game data cache service.</param>
    public RarityConfigService(GameDataCache dataCache)
    {
        _dataCache = dataCache;
    }

    /// <summary>
    /// Load rarity configuration from configuration/rarity.json.
    /// </summary>
    public RarityConfig LoadConfig()
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        var configFile = _dataCache.GetFile("configuration/rarity.json");
        
        if (configFile == null)
        {
            Log.Warning("Rarity config not found, using defaults");
            return GetDefaultConfig();
        }

        try
        {
            var tiers = new List<RarityTierDefinition>();
            var tiersArray = configFile.JsonData["tiers"] as JArray;

            if (tiersArray != null)
            {
                foreach (var tierToken in tiersArray)
                {
                    var name = tierToken["name"]?.ToString() ?? "Unknown";
                    var range = tierToken["rarityWeightRange"] as JObject;
                    var color = tierToken["color"]?.ToString() ?? "#FFFFFF";
                    var dropChance = tierToken["dropChance"]?.Value<double>() ?? 0.0;

                    if (range != null)
                    {
                        tiers.Add(new RarityTierDefinition
                        {
                            Name = name,
                            MinWeight = range["min"]?.Value<int>() ?? 0,
                            MaxWeight = range["max"]?.Value<int>() ?? 100,
                            Color = color,
                            DropChance = dropChance
                        });
                    }
                }
            }

            _cachedConfig = new RarityConfig { Tiers = tiers };
            Log.Information("Loaded {Count} rarity tiers from configuration", tiers.Count);
            return _cachedConfig;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading rarity config, using defaults");
            return GetDefaultConfig();
        }
    }

    /// <summary>
    /// Get rarity tier for a given rarityWeight value.
    /// </summary>
    public RarityTierDefinition GetTierForWeight(int rarityWeight)
    {
        var config = LoadConfig();
        
        foreach (var tier in config.Tiers)
        {
            if (rarityWeight >= tier.MinWeight && rarityWeight <= tier.MaxWeight)
            {
                return tier;
            }
        }

        // Default to first tier if no match
        return config.Tiers.FirstOrDefault() ?? GetDefaultConfig().Tiers[0];
    }

    /// <summary>
    /// Get color for a given rarityWeight value.
    /// </summary>
    public string GetColorForWeight(int rarityWeight)
    {
        return GetTierForWeight(rarityWeight).Color;
    }

    /// <summary>
    /// Clear the cached configuration.
    /// </summary>
    public void ClearCache()
    {
        _cachedConfig = null;
    }

    private static RarityConfig GetDefaultConfig()
    {
        return new RarityConfig
        {
            Tiers = new List<RarityTierDefinition>
            {
                new() { Name = "Common", MinWeight = 50, MaxWeight = 100, Color = "#FFFFFF", DropChance = 0.5 },
                new() { Name = "Uncommon", MinWeight = 30, MaxWeight = 49, Color = "#1EFF00", DropChance = 0.25 },
                new() { Name = "Rare", MinWeight = 15, MaxWeight = 29, Color = "#0070DD", DropChance = 0.15 },
                new() { Name = "Epic", MinWeight = 5, MaxWeight = 14, Color = "#A335EE", DropChance = 0.08 },
                new() { Name = "Legendary", MinWeight = 1, MaxWeight = 4, Color = "#FF8000", DropChance = 0.02 }
            }
        };
    }
}

/// <summary>
/// Rarity configuration with tier definitions.
/// </summary>
public class RarityConfig
{
    /// <summary>Gets or sets the rarity tiers.</summary>
    public List<RarityTierDefinition> Tiers { get; set; } = new();
}

/// <summary>
/// Rarity tier definition from configuration.
/// </summary>
public class RarityTierDefinition
{
    /// <summary>Gets or sets the tier name.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the minimum rarityWeight.</summary>
    public int MinWeight { get; set; }
    
    /// <summary>Gets or sets the maximum rarityWeight.</summary>
    public int MaxWeight { get; set; }
    
    /// <summary>Gets or sets the display color (hex).</summary>
    public string Color { get; set; } = "#FFFFFF";
    
    /// <summary>Gets or sets the drop chance probability.</summary>
    public double DropChance { get; set; }
}
