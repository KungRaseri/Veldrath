using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Services.Harvesting;

/// <summary>
/// Service for calculating harvest yields, node depletion, and XP rewards.
/// </summary>
public class HarvestCalculatorService
{
    private readonly ILogger<HarvestCalculatorService> _logger;
    private readonly HarvestingConfig _config;

    /// <summary>
    /// Initializes a new instance of the HarvestCalculatorService class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Harvesting configuration.</param>
    public HarvestCalculatorService(
        ILogger<HarvestCalculatorService> logger,
        HarvestingConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Calculate the final yield amount based on node, skill, tool, and critical status.
    /// </summary>
    public int CalculateYield(HarvestableNode node, int skillRank, int toolTier, bool isCritical)
    {
        // Base yield from node
        var baseYield = node.BaseYield;

        // Skill bonus: +0.3% per rank (default skillScaling = 0.003)
        var skillMultiplier = 1.0 + (skillRank * _config.YieldCalculation.SkillScaling);

        // Tool bonus: +10% per tier above minimum (capped at +30%)
        var toolBonus = Math.Min(
            (toolTier - node.MinToolTier) * _config.YieldCalculation.ToolBonusPerTier,
            _config.YieldCalculation.MaxToolBonus
        );
        var toolMultiplier = 1.0 + Math.Max(0, toolBonus);

        // Critical multiplier: 2.0x for critical harvests
        var critMultiplier = isCritical ? _config.YieldCalculation.CriticalMultiplier : 1.0;

        // Exhausted penalty: -50% yield if node is exhausted
        var exhaustedPenalty = node.GetNodeState() == NodeState.Exhausted 
            ? _config.YieldCalculation.ExhaustedPenalty 
            : 1.0;

        // Calculate final yield
        var finalYield = baseYield * skillMultiplier * toolMultiplier * critMultiplier * exhaustedPenalty;

        var result = (int)Math.Max(1, Math.Round(finalYield));

        _logger.LogDebug(
            "Yield calculation: base={Base} skill={SkillMult:F2} tool={ToolMult:F2} crit={CritMult:F1} exhausted={ExhaustedMult:F1} = {Final}",
            baseYield, skillMultiplier, toolMultiplier, critMultiplier, exhaustedPenalty, result
        );

        return result;
    }

    /// <summary>
    /// Calculate node health depletion amount for a harvest action.
    /// </summary>
    public int CalculateDepletion(HarvestableNode node, int skillRank, int toolTier, bool hasNoTool)
    {
        var baseDepletion = _config.NodeHealth.BaseDepletion;

        // Tool tier modifier: better tools = less damage
        var toolModifier = toolTier switch
        {
            1 => 1.0,
            2 => 0.9,
            3 => 0.8,
            4 => 0.7,
            5 => 0.6,
            _ => 1.0
        };

        // Skill damage reduction: -0.2% per rank, capped at 50% reduction
        var skillReduction = 1.0 - Math.Min(0.5, skillRank * 0.002);

        // No tool penalty: 2x depletion
        var noToolMultiplier = hasNoTool ? _config.ToolRequirements.NoToolDepletionMultiplier : 1.0;

        var depletion = baseDepletion * toolModifier / skillReduction * noToolMultiplier;

        var result = (int)Math.Round(depletion);

        _logger.LogDebug(
            "Depletion calculation: base={Base} tool={ToolMod:F2} skill={SkillRed:F2} noTool={NoToolMult:F1} = {Final}",
            baseDepletion, toolModifier, skillReduction, noToolMultiplier, result
        );

        return result;
    }

    /// <summary>
    /// Calculate skill XP awarded for a harvest action.
    /// </summary>
    public int CalculateSkillXP(HarvestableNode node, int toolTier, bool isCritical)
    {
        var baseXP = _config.SkillXP.BaseXP;

        // Tier multiplier based on material tier
        var tierMultiplier = node.MaterialTier switch
        {
            "common" => _config.SkillXP.TierMultipliers.Common,
            "uncommon" => _config.SkillXP.TierMultipliers.Uncommon,
            "rare" => _config.SkillXP.TierMultipliers.Rare,
            "epic" => _config.SkillXP.TierMultipliers.Epic,
            "legendary" => _config.SkillXP.TierMultipliers.Legendary,
            _ => 1.0
        };

        // Tool quality bonus: +10% per tool tier
        var toolBonus = 1.0 + (toolTier * _config.SkillXP.ToolQualityBonus);

        // Critical bonus: +50% XP
        var critBonus = isCritical ? _config.SkillXP.CriticalBonus : 1.0;

        var xp = baseXP * tierMultiplier * toolBonus * critBonus;

        var result = (int)Math.Round(xp);

        _logger.LogDebug(
            "XP calculation: base={Base} tier={TierMult:F1} tool={ToolBonus:F2} crit={CritBonus:F1} = {Final}",
            baseXP, tierMultiplier, toolBonus, critBonus, result
        );

        return result;
    }

    /// <summary>
    /// Calculate tool durability loss for a harvest action.
    /// </summary>
    public int CalculateDurabilityLoss(HarvestableNode node, int toolTier, bool isCritical)
    {
        var baseLoss = _config.DurabilityLoss.BaseLoss;

        // Node resistance based on tier
        var nodeResistance = node.MaterialTier switch
        {
            "common" => _config.DurabilityLoss.NodeResistance.Common,
            "uncommon" => _config.DurabilityLoss.NodeResistance.Uncommon,
            "rare" => _config.DurabilityLoss.NodeResistance.Rare,
            "epic" => _config.DurabilityLoss.NodeResistance.Epic,
            "legendary" => _config.DurabilityLoss.NodeResistance.Legendary,
            _ => 1.0
        };

        // Tool hardness based on tier
        var toolHardness = toolTier switch
        {
            1 => _config.DurabilityLoss.ToolHardness.Tier1,
            2 => _config.DurabilityLoss.ToolHardness.Tier2,
            3 => _config.DurabilityLoss.ToolHardness.Tier3,
            4 => _config.DurabilityLoss.ToolHardness.Tier4,
            5 => _config.DurabilityLoss.ToolHardness.Tier5,
            _ => 0.5
        };

        // Critical harvest reduces durability loss by 50%
        var critReduction = isCritical ? _config.CriticalHarvest.DurabilityReduction : 1.0;

        var durabilityLoss = baseLoss * (nodeResistance / toolHardness) * critReduction;

        return (int)Math.Max(1, Math.Round(durabilityLoss));
    }
}

/// <summary>
/// Configuration data model for harvesting system.
/// </summary>
public class HarvestingConfig
{
    /// <summary>
    /// Node health and depletion configuration.
    /// </summary>
    public NodeHealthConfig NodeHealth { get; set; } = new();
    
    /// <summary>
    /// Yield calculation configuration.
    /// </summary>
    public YieldCalculationConfig YieldCalculation { get; set; } = new();
    
    /// <summary>
    /// Critical harvest configuration.
    /// </summary>
    public CriticalHarvestConfig CriticalHarvest { get; set; } = new();
    
    /// <summary>
    /// Tool requirements configuration.
    /// </summary>
    public ToolRequirementsConfig ToolRequirements { get; set; } = new();
    
    /// <summary>
    /// Skill XP configuration.
    /// </summary>
    public SkillXPConfig SkillXP { get; set; } = new();
    
    /// <summary>
    /// Durability loss configuration.
    /// </summary>
    public DurabilityLossConfig DurabilityLoss { get; set; } = new();
}

/// <summary>
/// Configuration for node health and depletion mechanics.
/// </summary>
public class NodeHealthConfig
{
    /// <summary>
    /// Base health depletion per harvest action.
    /// </summary>
    public int BaseDepletion { get; set; } = 20;
    
    /// <summary>
    /// Health percentage thresholds for node states.
    /// </summary>
    public Dictionary<string, double> HealthThresholds { get; set; } = new();
    
    /// <summary>
    /// Node respawn rate.
    /// </summary>
    public int RespawnRate { get; set; } = 1;
    
    /// <summary>
    /// Time unit for respawn rate (e.g., "minutes", "hours").
    /// </summary>
    public string RespawnUnit { get; set; } = "minutes";
}

/// <summary>
/// Configuration for yield calculation mechanics.
/// </summary>
public class YieldCalculationConfig
{
    /// <summary>
    /// Skill scaling factor per skill rank.
    /// </summary>
    public double SkillScaling { get; set; } = 0.003;
    
    /// <summary>
    /// Bonus percentage per tool tier.
    /// </summary>
    public double ToolBonusPerTier { get; set; } = 0.10;
    
    /// <summary>
    /// Maximum tool bonus percentage.
    /// </summary>
    public double MaxToolBonus { get; set; } = 0.30;
    
    /// <summary>
    /// Critical harvest yield multiplier.
    /// </summary>
    public double CriticalMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// Yield penalty for exhausted nodes.
    /// </summary>
    public double ExhaustedPenalty { get; set; } = 0.5;
}

/// <summary>
/// Configuration for critical harvest mechanics.
/// </summary>
public class CriticalHarvestConfig
{
    /// <summary>
    /// Base critical harvest chance.
    /// </summary>
    public double BaseChance { get; set; } = 0.05;
    
    /// <summary>
    /// Critical chance increase per skill rank.
    /// </summary>
    public double SkillScaling { get; set; } = 0.0005;
    
    /// <summary>
    /// Critical chance increase per tool tier.
    /// </summary>
    public double ToolBonusPerTier { get; set; } = 0.01;
    
    /// <summary>
    /// Bonus critical chance for rich nodes.
    /// </summary>
    public double RichNodeBonus { get; set; } = 0.05;
    
    /// <summary>
    /// Chance to get bonus materials on critical harvest.
    /// </summary>
    public double BonusMaterialChance { get; set; } = 0.25;
    
    /// <summary>
    /// Chance to get rare drops on critical harvest.
    /// </summary>
    public double RareDropChance { get; set; } = 0.10;
    
    /// <summary>
    /// Durability loss reduction on critical harvest.
    /// </summary>
    public double DurabilityReduction { get; set; } = 0.5;
    
    /// <summary>
    /// XP bonus multiplier for critical harvest.
    /// </summary>
    public double XpBonus { get; set; } = 1.5;
}

/// <summary>
/// Configuration for tool requirements.
/// </summary>
public class ToolRequirementsConfig
{
    /// <summary>
    /// Whether to enforce minimum tool tier requirements.
    /// </summary>
    public bool EnforceMinimum { get; set; } = true;
    
    /// <summary>
    /// Yield penalty when harvesting without proper tool.
    /// </summary>
    public double NoToolPenalty { get; set; } = 0.5;
    
    /// <summary>
    /// Node depletion multiplier when harvesting without tool.
    /// </summary>
    public double NoToolDepletionMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// Whether common materials can be harvested without tools.
    /// </summary>
    public bool AllowNoToolForCommon { get; set; } = true;
}

/// <summary>
/// Configuration for skill XP rewards.
/// </summary>
public class SkillXPConfig
{
    /// <summary>
    /// Base XP reward per harvest action.
    /// </summary>
    public int BaseXP { get; set; } = 10;
    
    /// <summary>
    /// XP multipliers by material tier.
    /// </summary>
    public TierMultipliers TierMultipliers { get; set; } = new();
    
    /// <summary>
    /// Bonus XP percentage per tool tier.
    /// </summary>
    public double ToolQualityBonus { get; set; } = 0.1;
    
    /// <summary>
    /// XP multiplier for critical harvests.
    /// </summary>
    public double CriticalBonus { get; set; } = 1.5;
}

/// <summary>
/// XP multipliers for different material tiers.
/// </summary>
public class TierMultipliers
{
    /// <summary>
    /// XP multiplier for common materials.
    /// </summary>
    public double Common { get; set; } = 1.0;
    
    /// <summary>
    /// XP multiplier for uncommon materials.
    /// </summary>
    public double Uncommon { get; set; } = 2.0;
    
    /// <summary>
    /// XP multiplier for rare materials.
    /// </summary>
    public double Rare { get; set; } = 3.0;
    
    /// <summary>
    /// XP multiplier for epic materials.
    /// </summary>
    public double Epic { get; set; } = 4.0;
    
    /// <summary>
    /// XP multiplier for legendary materials.
    /// </summary>
    public double Legendary { get; set; } = 5.0;
}

/// <summary>
/// Configuration for tool durability loss.
/// </summary>
public class DurabilityLossConfig
{
    /// <summary>
    /// Base durability loss per harvest action.
    /// </summary>
    public int BaseLoss { get; set; } = 1;
    
    /// <summary>
    /// Node resistance values by material tier.
    /// </summary>
    public NodeResistanceConfig NodeResistance { get; set; } = new();
    
    /// <summary>
    /// Tool hardness values by tool tier.
    /// </summary>
    public ToolHardnessConfig ToolHardness { get; set; } = new();
}

/// <summary>
/// Node resistance values affecting durability loss.
/// </summary>
public class NodeResistanceConfig
{
    /// <summary>
    /// Resistance multiplier for common nodes.
    /// </summary>
    public double Common { get; set; } = 1.0;
    
    /// <summary>
    /// Resistance multiplier for uncommon nodes.
    /// </summary>
    public double Uncommon { get; set; } = 1.2;
    
    /// <summary>
    /// Resistance multiplier for rare nodes.
    /// </summary>
    public double Rare { get; set; } = 1.5;
    
    /// <summary>
    /// Resistance multiplier for epic nodes.
    /// </summary>
    public double Epic { get; set; } = 1.8;
    
    /// <summary>
    /// Resistance multiplier for legendary nodes.
    /// </summary>
    public double Legendary { get; set; } = 2.0;
}

/// <summary>
/// Tool hardness values affecting durability loss.
/// </summary>
public class ToolHardnessConfig
{
    /// <summary>
    /// Hardness factor for tier 1 tools.
    /// </summary>
    public double Tier1 { get; set; } = 0.5;
    
    /// <summary>
    /// Hardness factor for tier 2 tools.
    /// </summary>
    public double Tier2 { get; set; } = 0.7;
    
    /// <summary>
    /// Hardness factor for tier 3 tools.
    /// </summary>
    public double Tier3 { get; set; } = 1.0;
    
    /// <summary>
    /// Hardness factor for tier 4 tools.
    /// </summary>
    public double Tier4 { get; set; } = 1.5;
    
    /// <summary>
    /// Hardness factor for tier 5 tools.
    /// </summary>
    public double Tier5 { get; set; } = 2.0;
}
