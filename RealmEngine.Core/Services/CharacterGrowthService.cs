using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading character stat growth configuration from the database.
/// </summary>
public class CharacterGrowthService
{
    private readonly GameConfigService _configService;
    private GrowthStatsConfig? _config;

    public CharacterGrowthService(GameConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    private GrowthStatsConfig LoadConfig()
    {
        if (_config != null)
            return _config;

        try
        {
            var rawJson = _configService.GetData("growth-stats");
            if (rawJson == null)
            {
                _logger.LogWarning("growth-stats not found in database, using defaults");
                _config = GetDefaultConfig();
                return _config;
            }

            var doc = JsonSerializer.Deserialize<GrowthStatsDoc>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _config = new GrowthStatsConfig
            {
                Version = doc?.Metadata?.Version ?? "4.0",
                DerivedStats = MapDerivedStats(doc?.DerivedStats),
                StatCaps = MapStatCaps(doc?.StatCaps),
                ClassGrowthMultipliers = MapClassGrowthMultipliers(doc?.ClassGrowthMultipliers),
                StatPointAllocation = MapStatPointAllocation(doc?.StatPointAllocation),
                RespecSystem = MapRespecSystem(doc?.RespecSystem)
            };

            _logger.LogInformation("✅ Loaded growth stats config (version {Version}) with {Count} class multipliers",
                _config.Version, _config.ClassGrowthMultipliers.Count);
            return _config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load growth-stats.json, using defaults");
            _config = GetDefaultConfig();
            return _config;
        }
    }

    /// <summary>
    /// Gets growth multipliers for a specific class using JSON reference
    /// </summary>
    /// <param name="classReference">Class reference (e.g., "@classes/warrior:Fighter")</param>
    public ClassGrowthMultiplier? GetClassMultipliers(string classReference)
    {
        var config = LoadConfig();
        return config.ClassGrowthMultipliers
            .FirstOrDefault(m => m.ClassRef.Equals(classReference, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Calculates derived stat value using formula
    /// </summary>
    /// <param name="statName">Derived stat name (health, mana, etc.)</param>
    /// <param name="baseValue">Base value</param>
    /// <param name="primaryStat">Primary stat value (str, dex, etc.)</param>
    /// <param name="level">Character level</param>
    /// <param name="classGrowthValue">Class-specific growth per level</param>
    public double CalculateDerivedStat(string statName, double baseValue, double primaryStat, int level, double classGrowthValue)
    {
        var config = LoadConfig();
        
        if (!config.DerivedStats.TryGetValue(statName.ToLowerInvariant(), out var derivedStat))
        {
            _logger.LogWarning("Unknown derived stat: {StatName}", statName);
            return baseValue;
        }

        // Parse and evaluate formula
        // Simplified evaluation - in production, use a proper expression evaluator
        return statName.ToLowerInvariant() switch
        {
            "health" => baseValue + (primaryStat * 10) + (level * classGrowthValue),
            "mana" => baseValue + (primaryStat * 5) + (level * classGrowthValue),
            "physicaldamage" => primaryStat * 0.5,
            "magicaldamage" => primaryStat * 0.5,
            "criticalchance" => 0.05 + (primaryStat * 0.001),
            "criticaldamage" => 1.5 + (primaryStat * 0.002),
            "armor" => primaryStat * 2,
            "magicresist" => primaryStat * 2,
            "dodge" => 0.05 + (primaryStat * 0.001),
            "block" => 0.05 + (primaryStat * 0.0005),
            _ => baseValue
        };
    }

    /// <summary>
    /// Applies soft cap diminishing returns to a stat value
    /// </summary>
    public double ApplySoftCap(string statName, double value)
    {
        var config = LoadConfig();
        var softCaps = config.StatCaps.SoftCaps;

        if (!softCaps.StatLimits.TryGetValue(statName.ToLowerInvariant(), out var softCap))
            return value;

        if (value <= softCap)
            return value;

        // Apply diminishing returns after soft cap
        var excess = value - softCap;
        var diminished = excess * softCaps.DiminishingFactor;
        return softCap + diminished;
    }

    /// <summary>
    /// Enforces hard cap on a stat value
    /// </summary>
    public double ApplyHardCap(string statName, double value)
    {
        var config = LoadConfig();
        var hardCaps = config.StatCaps.HardCaps;

        if (!hardCaps.StatLimits.TryGetValue(statName.ToLowerInvariant(), out var hardCap))
            return value;

        return Math.Min(value, hardCap);
    }

    /// <summary>
    /// Calculates respec cost based on level
    /// </summary>
    public int CalculateRespecCost(int level)
    {
        var config = LoadConfig();
        if (!config.RespecSystem.Enabled)
            return 0;

        // Simple formula: level * 100
        return level * 100;
    }

    /// <summary>
    /// Clears the cached configuration, forcing reload on next access
    /// </summary>
    public void ClearCache()
    {
        _config = null;
        _logger.LogDebug("Growth stats config cache cleared");
    }

    #region Private Parsing Methods

    private static Dictionary<string, DerivedStat> MapDerivedStats(Dictionary<string, DerivedStatDoc>? doc) =>
        doc?.ToDictionary(kvp => kvp.Key, kvp => new DerivedStat { Formula = kvp.Value.Formula ?? "", Description = kvp.Value.Description ?? "" })
        ?? [];

    private static StatCaps MapStatCaps(StatCapsDoc? doc) =>
        doc == null ? GetDefaultStatCaps() : new StatCaps
        {
            SoftCaps = MapCapTier(doc.SoftCaps, 0.5),
            HardCaps = MapCapTier(doc.HardCaps, 1.0)
        };

    private static CapTier MapCapTier(CapTierDoc? doc, double defaultDiminishing)
    {
        if (doc == null)
            return new CapTier { Description = "", StatLimits = [], DiminishingFactor = defaultDiminishing };

        var statLimits = new Dictionary<string, int>();
        if (doc.Extra != null)
        {
            foreach (var (key, value) in doc.Extra)
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intVal))
                    statLimits[key] = intVal;
            }
        }

        return new CapTier
        {
            Description = doc.Description ?? "",
            StatLimits = statLimits,
            DiminishingFactor = doc.DiminishingFactor ?? defaultDiminishing
        };
    }

    private static List<ClassGrowthMultiplier> MapClassGrowthMultipliers(ClassGrowthMultipliersDoc? doc) =>
        doc?.Growth?
            .Where(item => !string.IsNullOrEmpty(item.ClassRef))
            .Select(item => new ClassGrowthMultiplier { ClassRef = item.ClassRef!, Multipliers = item.Multipliers ?? [] })
            .ToList() ?? [];

    private static StatPointAllocation MapStatPointAllocation(StatPointAllocationDoc? doc) =>
        doc == null
            ? new StatPointAllocation { PointsPerLevel = 5, MinimumPerStat = 1, MaximumPerStat = 3 }
            : new StatPointAllocation
            {
                PointsPerLevel = doc.PointsPerLevel ?? 5,
                Description = doc.Description,
                MinimumPerStat = doc.MinimumPerStat ?? 1,
                MaximumPerStat = doc.MaximumPerStat ?? 3
            };

    private static RespecSystem MapRespecSystem(RespecSystemDoc? doc) =>
        doc == null
            ? new RespecSystem { Enabled = true, CostFormula = "level * 100" }
            : new RespecSystem { Enabled = doc.Enabled ?? true, CostFormula = doc.CostFormula ?? "level * 100", Description = doc.Description };

    private sealed record GrowthStatsDoc(
        [property: JsonPropertyName("metadata")] MetadataDoc? Metadata,
        [property: JsonPropertyName("derivedStats")] Dictionary<string, DerivedStatDoc>? DerivedStats,
        [property: JsonPropertyName("statCaps")] StatCapsDoc? StatCaps,
        [property: JsonPropertyName("classGrowthMultipliers")] ClassGrowthMultipliersDoc? ClassGrowthMultipliers,
        [property: JsonPropertyName("statPointAllocation")] StatPointAllocationDoc? StatPointAllocation,
        [property: JsonPropertyName("respecSystem")] RespecSystemDoc? RespecSystem);

    private sealed record MetadataDoc([property: JsonPropertyName("version")] string? Version);

    private sealed record DerivedStatDoc(
        [property: JsonPropertyName("formula")] string? Formula,
        [property: JsonPropertyName("description")] string? Description);

    private sealed record StatCapsDoc(
        [property: JsonPropertyName("softCaps")] CapTierDoc? SoftCaps,
        [property: JsonPropertyName("hardCaps")] CapTierDoc? HardCaps);

    private sealed class CapTierDoc
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("diminishingFactor")]
        public double? DiminishingFactor { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed record ClassGrowthMultipliersDoc([property: JsonPropertyName("growth")] List<ClassGrowthItemDoc>? Growth);

    private sealed record ClassGrowthItemDoc(
        [property: JsonPropertyName("classRef")] string? ClassRef,
        [property: JsonPropertyName("multipliers")] Dictionary<string, double>? Multipliers);

    private sealed record StatPointAllocationDoc(
        [property: JsonPropertyName("pointsPerLevel")] int? PointsPerLevel,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("minimumPerStat")] int? MinimumPerStat,
        [property: JsonPropertyName("maximumPerStat")] int? MaximumPerStat);

    private sealed record RespecSystemDoc(
        [property: JsonPropertyName("enabled")] bool? Enabled,
        [property: JsonPropertyName("costFormula")] string? CostFormula,
        [property: JsonPropertyName("description")] string? Description);

    #endregion

    #region Default Configuration

    private GrowthStatsConfig GetDefaultConfig()
    {
        return new GrowthStatsConfig
        {
            Version = "4.0",
            DerivedStats = new Dictionary<string, DerivedStat>
            {
                ["health"] = new DerivedStat { Formula = "base + (constitution * 10) + (level * classHealthGrowth)", Description = "Total HP" },
                ["mana"] = new DerivedStat { Formula = "base + (intelligence * 5) + (wisdom * 5) + (level * classManaGrowth)", Description = "Total mana" }
            },
            StatCaps = GetDefaultStatCaps(),
            ClassGrowthMultipliers = new List<ClassGrowthMultiplier>(),
            StatPointAllocation = new StatPointAllocation { PointsPerLevel = 5, MinimumPerStat = 1, MaximumPerStat = 3 },
            RespecSystem = new RespecSystem { Enabled = true, CostFormula = "level * 100" }
        };
    }

    private static StatCaps GetDefaultStatCaps()
    {
        return new StatCaps
        {
            SoftCaps = new CapTier
            {
                Description = "Diminishing returns after these values",
                StatLimits = new Dictionary<string, int>
                {
                    ["strength"] = 100,
                    ["dexterity"] = 100,
                    ["constitution"] = 100,
                    ["intelligence"] = 100,
                    ["wisdom"] = 100,
                    ["charisma"] = 100
                },
                DiminishingFactor = 0.5
            },
            HardCaps = new CapTier
            {
                Description = "Maximum values",
                StatLimits = new Dictionary<string, int>
                {
                    ["strength"] = 200,
                    ["dexterity"] = 200,
                    ["constitution"] = 200,
                    ["intelligence"] = 200,
                    ["wisdom"] = 200,
                    ["charisma"] = 200
                },
                DiminishingFactor = 1.0
            }
        };
    }

    #endregion
}

#region Configuration Model Classes

/// <summary>
/// Complete growth stats configuration
/// </summary>
public class GrowthStatsConfig
{
    /// <summary>Configuration version</summary>
    public required string Version { get; set; }
    
    /// <summary>Derived stat formulas</summary>
    public required Dictionary<string, DerivedStat> DerivedStats { get; set; }
    
    /// <summary>Stat caps configuration</summary>
    public required StatCaps StatCaps { get; set; }
    
    /// <summary>Class growth multipliers (uses JSON references to @classes)</summary>
    public required List<ClassGrowthMultiplier> ClassGrowthMultipliers { get; set; }
    
    /// <summary>Stat point allocation per level</summary>
    public required StatPointAllocation StatPointAllocation { get; set; }
    
    /// <summary>Respec system configuration</summary>
    public required RespecSystem RespecSystem { get; set; }
}

/// <summary>Derived stat formula</summary>
public class DerivedStat
{
    /// <summary>Calculation formula</summary>
    public required string Formula { get; set; }
    
    /// <summary>Description</summary>
    public required string Description { get; set; }
}

/// <summary>Stat caps (soft and hard)</summary>
public class StatCaps
{
    /// <summary>Soft caps with diminishing returns</summary>
    public required CapTier SoftCaps { get; set; }
    
    /// <summary>Hard caps (absolute maximum)</summary>
    public required CapTier HardCaps { get; set; }
}

/// <summary>Cap tier definition</summary>
public class CapTier
{
    /// <summary>Description</summary>
    public required string Description { get; set; }
    
    /// <summary>Stat limits by name</summary>
    public required Dictionary<string, int> StatLimits { get; set; }
    
    /// <summary>Diminishing factor for soft caps</summary>
    public required double DiminishingFactor { get; set; }
}

/// <summary>Class growth multipliers using JSON reference</summary>
public class ClassGrowthMultiplier
{
    /// <summary>Class reference (e.g., "@classes/warrior:Fighter")</summary>
    public required string ClassRef { get; set; }
    
    /// <summary>Growth multipliers by stat name</summary>
    public required Dictionary<string, double> Multipliers { get; set; }
}

/// <summary>Stat point allocation configuration</summary>
public class StatPointAllocation
{
    /// <summary>Points granted per level</summary>
    public required int PointsPerLevel { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
    
    /// <summary>Minimum points per stat</summary>
    public required int MinimumPerStat { get; set; }
    
    /// <summary>Maximum points per stat</summary>
    public required int MaximumPerStat { get; set; }
}

/// <summary>Respec system configuration</summary>
public class RespecSystem
{
    /// <summary>Whether respec is enabled</summary>
    public required bool Enabled { get; set; }
    
    /// <summary>Cost formula</summary>
    public required string CostFormula { get; set; }
    
    /// <summary>Description</summary>
    public string? Description { get; set; }
}

#endregion
