using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Services.Harvesting;

/// <summary>
/// Service for calculating and determining critical harvest outcomes.
/// </summary>
public class CriticalHarvestService
{
    private readonly ILogger<CriticalHarvestService> _logger;
    private readonly HarvestingConfig _config;
    private readonly Random _random;

    public CriticalHarvestService(
        ILogger<CriticalHarvestService> logger,
        HarvestingConfig config)
    {
        _logger = logger;
        _config = config;
        _random = new Random();
    }

    /// <summary>
    /// Calculate the critical harvest chance based on skill, tool, and node properties.
    /// </summary>
    public double CalculateCriticalChance(int skillRank, int toolTier, bool isRichNode)
    {
        var baseChance = _config.CriticalHarvest.BaseChance;

        // Skill scaling: +0.05% per rank (max +12.5% at rank 250)
        var skillBonus = skillRank * _config.CriticalHarvest.SkillScaling;

        // Tool bonus: +1% per tier
        var toolBonus = toolTier * _config.CriticalHarvest.ToolBonusPerTier;

        // Rich node bonus: +5%
        var richBonus = isRichNode ? _config.CriticalHarvest.RichNodeBonus : 0;

        var totalChance = baseChance + skillBonus + toolBonus + richBonus;

        _logger.LogDebug(
            "Critical chance: base={Base:P1} skill={SkillBonus:P1} tool={ToolBonus:P1} rich={RichBonus:P1} = {Total:P1}",
            baseChance, skillBonus, toolBonus, richBonus, totalChance
        );

        return Math.Min(1.0, totalChance); // Cap at 100%
    }

    /// <summary>
    /// Roll for a critical harvest.
    /// </summary>
    public bool RollCritical(int skillRank, int toolTier, bool isRichNode)
    {
        var chance = CalculateCriticalChance(skillRank, toolTier, isRichNode);
        var roll = _random.NextDouble();

        var isCritical = roll < chance;

        if (isCritical)
        {
            _logger.LogInformation(
                "Critical harvest! (rolled {Roll:F3} < {Chance:F3})",
                roll, chance
            );
        }

        return isCritical;
    }

    /// <summary>
    /// Determine if a bonus material should drop from a critical harvest.
    /// </summary>
    public bool ShouldDropBonusMaterial()
    {
        var chance = _config.CriticalHarvest.BonusMaterialChance;
        return _random.NextDouble() < chance;
    }

    /// <summary>
    /// Determine if a rare material should drop from a critical harvest.
    /// </summary>
    public bool ShouldDropRareMaterial()
    {
        var chance = _config.CriticalHarvest.RareDropChance;
        return _random.NextDouble() < chance;
    }

    /// <summary>
    /// Generate a higher tier material name for bonus drops.
    /// </summary>
    public string GetBonusMaterialTier(string currentTier)
    {
        return currentTier switch
        {
            "common" => "uncommon",
            "uncommon" => "rare",
            "rare" => "epic",
            "epic" => "legendary",
            "legendary" => "legendary", // Can't go higher
            _ => "common"
        };
    }
}
