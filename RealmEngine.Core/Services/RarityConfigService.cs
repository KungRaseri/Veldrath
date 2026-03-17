using System.Text.Json;
using System.Text.Json.Serialization;
using RealmEngine.Data.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading rarity tier configuration from the database.
/// </summary>
public class RarityConfigService
{
    private readonly GameConfigService _configService;
    private RarityConfig? _cachedConfig;

    public RarityConfigService(GameConfigService configService)
    {
        _configService = configService;
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

        var rawJson = _configService.GetData("rarity");

        if (rawJson == null)
        {
            _logger.LogWarning("Rarity config not found in database, using defaults");
            return GetDefaultConfig();
        }

        try
        {
            var doc = JsonSerializer.Deserialize<RarityConfigDoc>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var tiers = doc?.Tiers?.Select(t => new RarityTierDefinition
            {
                Name       = t.Name,
                MinWeight  = t.RarityWeightRange?.Min ?? 0,
                MaxWeight  = t.RarityWeightRange?.Max ?? 100,
                Color      = t.Color,
                DropChance = t.DropChance
            }).ToList() ?? [];

            _cachedConfig = new RarityConfig { Tiers = tiers };
            _logger.LogInformation("Loaded {Count} rarity tiers from configuration", tiers.Count);
            return _cachedConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rarity config, using defaults");
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

    private sealed record RarityConfigDoc([property: JsonPropertyName("tiers")] List<RarityTierDoc>? Tiers);
    private sealed record RarityTierDoc(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("rarityWeightRange")] RarityRangeDoc? RarityWeightRange,
        [property: JsonPropertyName("color")] string Color,
        [property: JsonPropertyName("dropChance")] double DropChance);
    private sealed record RarityRangeDoc(
        [property: JsonPropertyName("min")] int Min,
        [property: JsonPropertyName("max")] int Max);
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
