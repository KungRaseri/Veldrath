using Newtonsoft.Json.Linq;
using RealmEngine.Data.Services;
using Serilog;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading harvesting system configuration from the database.
/// </summary>
public class HarvestingConfigService
{
    private readonly GameConfigService _configService;
    private HarvestingConfig? _cachedConfig;

    public HarvestingConfigService(GameConfigService configService)
    {
        _configService = configService;
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
            Log.Warning("Harvesting config not found in database, using defaults");
            return GetDefaultConfig();
        }

        try
        {
            var config = new HarvestingConfig();
            var jobj = JObject.Parse(json);

            // Node Health
            var nodeHealth = jobj["nodeHealth"] as JObject;
            if (nodeHealth != null)
            {
                config.BaseDepletion = nodeHealth["baseDepletion"]?.Value<int>() ?? 20;
                config.HealthyThreshold = nodeHealth["healthThresholds"]?["healthy"]?.Value<double>() ?? 0.8;
                config.DepletedThreshold = nodeHealth["healthThresholds"]?["depleted"]?.Value<double>() ?? 0.4;
                config.ExhaustedThreshold = nodeHealth["healthThresholds"]?["exhausted"]?.Value<double>() ?? 0.1;
                config.RespawnRate = nodeHealth["respawnRate"]?.Value<int>() ?? 1;
            }

            // Yield Calculation
            var yieldCalc = jobj["yieldCalculation"] as JObject;
            if (yieldCalc != null)
            {
                config.SkillScaling = yieldCalc["skillScaling"]?.Value<double>() ?? 0.003;
                config.ToolBonusPerTier = yieldCalc["toolBonusPerTier"]?.Value<double>() ?? 0.1;
                config.MaxToolBonus = yieldCalc["maxToolBonus"]?.Value<double>() ?? 0.3;
                config.CriticalMultiplier = yieldCalc["criticalMultiplier"]?.Value<double>() ?? 2.0;
                config.ExhaustedPenalty = yieldCalc["exhaustedPenalty"]?.Value<double>() ?? 0.5;
            }

            // Critical Harvest
            var criticalHarvest = jobj["criticalHarvest"] as JObject;
            if (criticalHarvest != null)
            {
                config.CriticalBaseChance = criticalHarvest["baseChance"]?.Value<double>() ?? 0.05;
                config.CriticalSkillScaling = criticalHarvest["skillScaling"]?.Value<double>() ?? 0.0005;
                config.CriticalToolBonusPerTier = criticalHarvest["toolBonusPerTier"]?.Value<double>() ?? 0.01;
                config.RichNodeBonus = criticalHarvest["richNodeBonus"]?.Value<double>() ?? 0.05;
                config.BonusMaterialChance = criticalHarvest["bonusMaterialChance"]?.Value<double>() ?? 0.25;
                config.RareDropChance = criticalHarvest["rareDropChance"]?.Value<double>() ?? 0.1;
                config.CriticalDurabilityReduction = criticalHarvest["durabilityReduction"]?.Value<double>() ?? 0.5;
                config.CriticalXPBonus = criticalHarvest["xpBonus"]?.Value<double>() ?? 1.5;
            }

            // Tool Requirements
            var toolReqs = jobj["toolRequirements"] as JObject;
            if (toolReqs != null)
            {
                config.EnforceMinimumTool = toolReqs["enforceMinimum"]?.Value<bool>() ?? true;
                config.NoToolPenalty = toolReqs["noToolPenalty"]?.Value<double>() ?? 0.5;
                config.NoToolDepletionMultiplier = toolReqs["noToolDepletionMultiplier"]?.Value<double>() ?? 2.0;
                config.AllowNoToolForCommon = toolReqs["allowNoToolForCommon"]?.Value<bool>() ?? true;
            }

            // Skill XP
            var skillXP = jobj["skillXP"] as JObject;
            if (skillXP != null)
            {
                config.BaseXP = skillXP["baseXP"]?.Value<int>() ?? 10;
                config.ToolQualityBonus = skillXP["toolQualityBonus"]?.Value<double>() ?? 0.1;
                
                var tierMultipliers = skillXP["tierMultipliers"] as JObject;
                if (tierMultipliers != null)
                {
                    config.TierXPMultipliers["common"] = tierMultipliers["common"]?.Value<double>() ?? 1.0;
                    config.TierXPMultipliers["uncommon"] = tierMultipliers["uncommon"]?.Value<double>() ?? 2.0;
                    config.TierXPMultipliers["rare"] = tierMultipliers["rare"]?.Value<double>() ?? 3.0;
                    config.TierXPMultipliers["epic"] = tierMultipliers["epic"]?.Value<double>() ?? 4.0;
                    config.TierXPMultipliers["legendary"] = tierMultipliers["legendary"]?.Value<double>() ?? 5.0;
                }
            }

            // Durability Loss
            var durability = jobj["durabilityLoss"] as JObject;
            if (durability != null)
            {
                config.BaseDurabilityLoss = durability["baseLoss"]?.Value<int>() ?? 1;
                
                var nodeResistance = durability["nodeResistance"] as JObject;
                if (nodeResistance != null)
                {
                    config.NodeResistance["common"] = nodeResistance["common"]?.Value<double>() ?? 1.0;
                    config.NodeResistance["uncommon"] = nodeResistance["uncommon"]?.Value<double>() ?? 1.2;
                    config.NodeResistance["rare"] = nodeResistance["rare"]?.Value<double>() ?? 1.5;
                    config.NodeResistance["epic"] = nodeResistance["epic"]?.Value<double>() ?? 1.8;
                    config.NodeResistance["legendary"] = nodeResistance["legendary"]?.Value<double>() ?? 2.0;
                }
                
                var toolHardness = durability["toolHardness"] as JObject;
                if (toolHardness != null)
                {
                    config.ToolHardness["tier1"] = toolHardness["tier1"]?.Value<double>() ?? 0.5;
                    config.ToolHardness["tier2"] = toolHardness["tier2"]?.Value<double>() ?? 0.7;
                    config.ToolHardness["tier3"] = toolHardness["tier3"]?.Value<double>() ?? 1.0;
                    config.ToolHardness["tier4"] = toolHardness["tier4"]?.Value<double>() ?? 1.5;
                    config.ToolHardness["tier5"] = toolHardness["tier5"]?.Value<double>() ?? 2.0;
                }
            }

            _cachedConfig = config;
            Log.Information("Loaded harvesting configuration from file");
            return _cachedConfig;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading harvesting config, using defaults");
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

    private static HarvestingConfig GetDefaultConfig()
    {
        return new HarvestingConfig();
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
