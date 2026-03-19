using System.Text.Json;
using System.Text.Json.Serialization;
using RealmEngine.Data.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading harvesting system configuration from the database.
/// </summary>
public class HarvestingConfigService
{
    private readonly GameConfigService _configService;
    private readonly ILogger<HarvestingConfigService> _logger;
    private HarvestingConfig? _cachedConfig;

    /// <summary>Initializes a new instance of <see cref="HarvestingConfigService"/>.</summary>
    /// <param name="configService">Service used to load raw config JSON.</param>
    /// <param name="logger">Logger instance.</param>
    public HarvestingConfigService(GameConfigService configService, ILogger<HarvestingConfigService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Load harvesting configuration from configuration/harvesting-config.json.
    /// </summary>
    public HarvestingConfig LoadConfig()
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        var json = _configService.GetData("harvesting-config");

        if (json == null)
        {
            _logger.LogWarning("Harvesting config not found in database, using defaults");
            return GetDefaultConfig();
        }

        try
        {
            var doc = JsonSerializer.Deserialize<HarvestingConfigDoc>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _cachedConfig = MapToConfig(doc);
            _logger.LogInformation("Loaded harvesting configuration from file");
            return _cachedConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading harvesting config, using defaults");
            return GetDefaultConfig();
        }
    }

    /// <summary>
    /// Clear the cached configuration.
    /// </summary>
    public void ClearCache()
    {
        _cachedConfig = null;
    }

    private static HarvestingConfig GetDefaultConfig() => new();

    private sealed record HarvestingConfigDoc(
        [property: JsonPropertyName("nodeHealth")] NodeHealthDoc? NodeHealth,
        [property: JsonPropertyName("yieldCalculation")] YieldCalcDoc? YieldCalculation,
        [property: JsonPropertyName("criticalHarvest")] CriticalHarvestDoc? CriticalHarvest,
        [property: JsonPropertyName("toolRequirements")] ToolReqsDoc? ToolRequirements,
        [property: JsonPropertyName("skillXP")] SkillXpDoc? SkillXP,
        [property: JsonPropertyName("durabilityLoss")] DurabilityLossDoc? DurabilityLoss);

    private sealed record NodeHealthDoc(
        [property: JsonPropertyName("baseDepletion")] int BaseDepletion,
        [property: JsonPropertyName("healthThresholds")] HealthThresholdsDoc? HealthThresholds,
        [property: JsonPropertyName("respawnRate")] int RespawnRate);

    private sealed record HealthThresholdsDoc(
        [property: JsonPropertyName("healthy")] double Healthy,
        [property: JsonPropertyName("depleted")] double Depleted,
        [property: JsonPropertyName("exhausted")] double Exhausted);

    private sealed record YieldCalcDoc(
        [property: JsonPropertyName("skillScaling")] double SkillScaling,
        [property: JsonPropertyName("toolBonusPerTier")] double ToolBonusPerTier,
        [property: JsonPropertyName("maxToolBonus")] double MaxToolBonus,
        [property: JsonPropertyName("criticalMultiplier")] double CriticalMultiplier,
        [property: JsonPropertyName("exhaustedPenalty")] double ExhaustedPenalty);

    private sealed record CriticalHarvestDoc(
        [property: JsonPropertyName("baseChance")] double BaseChance,
        [property: JsonPropertyName("skillScaling")] double SkillScaling,
        [property: JsonPropertyName("toolBonusPerTier")] double ToolBonusPerTier,
        [property: JsonPropertyName("richNodeBonus")] double RichNodeBonus,
        [property: JsonPropertyName("bonusMaterialChance")] double BonusMaterialChance,
        [property: JsonPropertyName("rareDropChance")] double RareDropChance,
        [property: JsonPropertyName("durabilityReduction")] double DurabilityReduction,
        [property: JsonPropertyName("xpBonus")] double XpBonus);

    private sealed record ToolReqsDoc(
        [property: JsonPropertyName("enforceMinimum")] bool EnforceMinimum,
        [property: JsonPropertyName("noToolPenalty")] double NoToolPenalty,
        [property: JsonPropertyName("noToolDepletionMultiplier")] double NoToolDepletionMultiplier,
        [property: JsonPropertyName("allowNoToolForCommon")] bool AllowNoToolForCommon);

    private sealed record SkillXpDoc(
        [property: JsonPropertyName("baseXP")] int BaseXP,
        [property: JsonPropertyName("toolQualityBonus")] double ToolQualityBonus,
        [property: JsonPropertyName("tierMultipliers")] Dictionary<string, double>? TierMultipliers);

    private sealed record DurabilityLossDoc(
        [property: JsonPropertyName("baseLoss")] int BaseLoss,
        [property: JsonPropertyName("nodeResistance")] Dictionary<string, double>? NodeResistance,
        [property: JsonPropertyName("toolHardness")] Dictionary<string, double>? ToolHardness);

    private static HarvestingConfig MapToConfig(HarvestingConfigDoc? doc)
    {
        var config = new HarvestingConfig();
        if (doc == null) return config;

        if (doc.NodeHealth is { } nh)
        {
            config.BaseDepletion = nh.BaseDepletion;
            config.RespawnRate = nh.RespawnRate;
            if (nh.HealthThresholds is { } ht)
            {
                config.HealthyThreshold = ht.Healthy;
                config.DepletedThreshold = ht.Depleted;
                config.ExhaustedThreshold = ht.Exhausted;
            }
        }

        if (doc.YieldCalculation is { } yc)
        {
            config.SkillScaling = yc.SkillScaling;
            config.ToolBonusPerTier = yc.ToolBonusPerTier;
            config.MaxToolBonus = yc.MaxToolBonus;
            config.CriticalMultiplier = yc.CriticalMultiplier;
            config.ExhaustedPenalty = yc.ExhaustedPenalty;
        }

        if (doc.CriticalHarvest is { } ch)
        {
            config.CriticalBaseChance = ch.BaseChance;
            config.CriticalSkillScaling = ch.SkillScaling;
            config.CriticalToolBonusPerTier = ch.ToolBonusPerTier;
            config.RichNodeBonus = ch.RichNodeBonus;
            config.BonusMaterialChance = ch.BonusMaterialChance;
            config.RareDropChance = ch.RareDropChance;
            config.CriticalDurabilityReduction = ch.DurabilityReduction;
            config.CriticalXPBonus = ch.XpBonus;
        }

        if (doc.ToolRequirements is { } tr)
        {
            config.EnforceMinimumTool = tr.EnforceMinimum;
            config.NoToolPenalty = tr.NoToolPenalty;
            config.NoToolDepletionMultiplier = tr.NoToolDepletionMultiplier;
            config.AllowNoToolForCommon = tr.AllowNoToolForCommon;
        }

        if (doc.SkillXP is { } sx)
        {
            config.BaseXP = sx.BaseXP;
            config.ToolQualityBonus = sx.ToolQualityBonus;
            if (sx.TierMultipliers is { } tm)
                config.TierXPMultipliers = tm;
        }

        if (doc.DurabilityLoss is { } dl)
        {
            config.BaseDurabilityLoss = dl.BaseLoss;
            if (dl.NodeResistance is { } nr)
                config.NodeResistance = nr;
            if (dl.ToolHardness is { } th)
                config.ToolHardness = th;
        }

        return config;
    }
}

/// <summary>
/// Harvesting system configuration.
/// </summary>
public class HarvestingConfig
{
    // Node Health
    /// <summary>Gets or sets base node health depletion per harvest.</summary>
    public int BaseDepletion { get; set; } = 20;
    
    /// <summary>Gets or sets healthy node threshold (0.0-1.0).</summary>
    public double HealthyThreshold { get; set; } = 0.8;
    
    /// <summary>Gets or sets depleted node threshold (0.0-1.0).</summary>
    public double DepletedThreshold { get; set; } = 0.4;
    
    /// <summary>Gets or sets exhausted node threshold (0.0-1.0).</summary>
    public double ExhaustedThreshold { get; set; } = 0.1;
    
    /// <summary>Gets or sets node respawn rate in minutes.</summary>
    public int RespawnRate { get; set; } = 1;

    // Yield Calculation
    /// <summary>Gets or sets skill scaling factor for yield.</summary>
    public double SkillScaling { get; set; } = 0.003;
    
    /// <summary>Gets or sets tool bonus per tier.</summary>
    public double ToolBonusPerTier { get; set; } = 0.1;
    
    /// <summary>Gets or sets maximum tool bonus.</summary>
    public double MaxToolBonus { get; set; } = 0.3;
    
    /// <summary>Gets or sets critical harvest yield multiplier.</summary>
    public double CriticalMultiplier { get; set; } = 2.0;
    
    /// <summary>Gets or sets yield penalty for exhausted nodes.</summary>
    public double ExhaustedPenalty { get; set; } = 0.5;

    // Critical Harvest
    /// <summary>Gets or sets base critical harvest chance.</summary>
    public double CriticalBaseChance { get; set; } = 0.05;
    
    /// <summary>Gets or sets critical chance skill scaling.</summary>
    public double CriticalSkillScaling { get; set; } = 0.0005;
    
    /// <summary>Gets or sets critical chance tool bonus per tier.</summary>
    public double CriticalToolBonusPerTier { get; set; } = 0.01;
    
    /// <summary>Gets or sets critical chance bonus for rich nodes.</summary>
    public double RichNodeBonus { get; set; } = 0.05;
    
    /// <summary>Gets or sets bonus material drop chance on critical.</summary>
    public double BonusMaterialChance { get; set; } = 0.25;
    
    /// <summary>Gets or sets rare drop chance on critical.</summary>
    public double RareDropChance { get; set; } = 0.1;
    
    /// <summary>Gets or sets durability loss reduction on critical.</summary>
    public double CriticalDurabilityReduction { get; set; } = 0.5;
    
    /// <summary>Gets or sets XP bonus multiplier on critical.</summary>
    public double CriticalXPBonus { get; set; } = 1.5;

    // Tool Requirements
    /// <summary>Gets or sets whether to enforce minimum tool tier.</summary>
    public bool EnforceMinimumTool { get; set; } = true;
    
    /// <summary>Gets or sets yield penalty for missing tool.</summary>
    public double NoToolPenalty { get; set; } = 0.5;
    
    /// <summary>Gets or sets depletion multiplier for missing tool.</summary>
    public double NoToolDepletionMultiplier { get; set; } = 2.0;
    
    /// <summary>Gets or sets whether common nodes allow no tool.</summary>
    public bool AllowNoToolForCommon { get; set; } = true;

    // Skill XP
    /// <summary>Gets or sets base XP per harvest.</summary>
    public int BaseXP { get; set; } = 10;
    
    /// <summary>Gets or sets tool quality XP bonus per tier.</summary>
    public double ToolQualityBonus { get; set; } = 0.1;
    
    /// <summary>Gets or sets XP multipliers by node tier.</summary>
    public Dictionary<string, double> TierXPMultipliers { get; set; } = new()
    {
        { "common", 1.0 },
        { "uncommon", 2.0 },
        { "rare", 3.0 },
        { "epic", 4.0 },
        { "legendary", 5.0 }
    };

    // Durability Loss
    /// <summary>Gets or sets base durability loss per harvest.</summary>
    public int BaseDurabilityLoss { get; set; } = 1;
    
    /// <summary>Gets or sets node resistance by tier.</summary>
    public Dictionary<string, double> NodeResistance { get; set; } = new()
    {
        { "common", 1.0 },
        { "uncommon", 1.2 },
        { "rare", 1.5 },
        { "epic", 1.8 },
        { "legendary", 2.0 }
    };
    
    /// <summary>Gets or sets tool hardness by tier.</summary>
    public Dictionary<string, double> ToolHardness { get; set; } = new()
    {
        { "tier1", 0.5 },
        { "tier2", 0.7 },
        { "tier3", 1.0 },
        { "tier4", 1.5 },
        { "tier5", 2.0 }
    };
}
