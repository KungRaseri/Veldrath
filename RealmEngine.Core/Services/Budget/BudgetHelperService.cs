using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Helper service for calculating budgets across different game systems.
/// Provides consistent budget calculations for enemy loot, chests, shops, crafting, etc.
/// </summary>
public class BudgetHelperService
{
    private readonly BudgetCalculator _budgetCalculator;
    private readonly ILogger<BudgetHelperService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetHelperService"/> class.
    /// </summary>
    /// <param name="budgetCalculator">The budget calculator service.</param>
    /// <param name="logger">The logger.</param>
    public BudgetHelperService(
        BudgetCalculator budgetCalculator,
        ILogger<BudgetHelperService> logger)
    {
        _budgetCalculator = budgetCalculator ?? throw new ArgumentNullException(nameof(budgetCalculator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get item generation budget for an enemy's loot drop.
    /// Uses enemy type multiplier and level scaling.
    /// </summary>
    /// <param name="enemyType">The enemy type (goblin, dragon, etc.)</param>
    /// <param name="level">The enemy level</param>
    /// <returns>Budget value for item generation</returns>
    public int GetBudgetForEnemyLoot(string enemyType, int level)
    {
        // Calculate base budget from level
        var baseBudget = _budgetCalculator.CalculateBaseBudget(level);
        
        // Apply enemy type multiplier (would need MaterialPoolService to get actual multiplier)
        // For now, use simple logic: boss types get 1.5x, elite gets 1.3x
        var multiplier = enemyType.ToLowerInvariant() switch
        {
            var t when t.Contains("boss") || t.Contains("dragon") => 1.5,
            var t when t.Contains("elite") || t.Contains("knight") => 1.3,
            _ => 1.0
        };
        
        var budget = (int)(baseBudget * multiplier);
        _logger.LogDebug("Enemy loot budget: {Budget} (type={Type}, level={Level})", 
            budget, enemyType, level);
        return budget;
    }

    /// <summary>
    /// Get item generation budget for a chest or container.
    /// Chest rarity determines budget multiplier.
    /// </summary>
    /// <param name="chestRarity">The chest rarity tier</param>
    /// <param name="locationLevel">The location level where chest is found</param>
    /// <returns>Budget value for item generation</returns>
    public int GetBudgetForChest(RarityTier chestRarity, int locationLevel)
    {
        // Base budget similar to enemy, but with chest rarity multiplier
        var basePerLevel = 5;
        var levelBudget = locationLevel * basePerLevel;

        var rarityMultiplier = chestRarity switch
        {
            RarityTier.Common => 0.8,
            RarityTier.Uncommon => 1.2,
            RarityTier.Rare => 1.8,
            RarityTier.Epic => 2.5,
            RarityTier.Legendary => 4.0,
            _ => 1.0
        };

        var budget = (int)(levelBudget * rarityMultiplier);
        _logger.LogDebug("Chest loot budget: {Budget} (rarity={Rarity}, location_level={Level})", 
            budget, chestRarity, locationLevel);
        
        return Math.Max(10, budget); // Minimum 10 budget
    }

    /// <summary>
    /// Get item generation budget for a shop merchant's generated inventory.
    /// Based on merchant wealth and item tier.
    /// </summary>
    /// <param name="merchantWealth">The merchant's wealth level (1-100)</param>
    /// <param name="itemTier">The tier of item to generate (1=basic, 2=advanced, 3=masterwork)</param>
    /// <returns>Budget value for item generation</returns>
    public int GetBudgetForShopItem(int merchantWealth, int itemTier)
    {
        // Shop items scale with merchant wealth
        // Tier determines the base multiplier
        var tierMultiplier = itemTier switch
        {
            1 => 0.5,  // Basic items (low budget)
            2 => 1.0,  // Advanced items (medium budget)
            3 => 2.0,  // Masterwork items (high budget)
            _ => 1.0
        };

        var budget = (int)(merchantWealth * tierMultiplier);
        _logger.LogDebug("Shop item budget: {Budget} (wealth={Wealth}, tier={Tier})", 
            budget, merchantWealth, itemTier);
        
        return Math.Max(20, budget); // Minimum 20 budget (shops don't sell trash)
    }

    /// <summary>
    /// Get quality tier modifier for crafted items based on player skill vs recipe requirement.
    /// </summary>
    /// <param name="playerSkill">The player's crafting skill level</param>
    /// <param name="recipeRequirement">The recipe's required skill level</param>
    /// <param name="criticalSuccess">Whether a critical success occurred</param>
    /// <returns>Quality tier bonus (0-3)</returns>
    public int GetCraftingQualityBonus(int playerSkill, int recipeRequirement, bool criticalSuccess = false)
    {
        var skillDifference = playerSkill - recipeRequirement;
        
        // Every 10 skill points above requirement = +1 quality tier
        var qualityBonus = skillDifference / 10;
        
        // Critical success adds +1 tier
        if (criticalSuccess)
        {
            qualityBonus += 1;
            _logger.LogDebug("Critical crafting success! +1 quality tier");
        }

        // Cap at 3 tiers (Common → Rare)
        qualityBonus = Math.Clamp(qualityBonus, 0, 3);
        
        _logger.LogDebug("Crafting quality bonus: {Bonus} (skill={PlayerSkill}, req={Requirement}, crit={Critical})", 
            qualityBonus, playerSkill, recipeRequirement, criticalSuccess);
        
        return qualityBonus;
    }

    /// <summary>
    /// Calculate crafting success chance based on player skill vs recipe difficulty.
    /// </summary>
    /// <param name="playerSkill">The player's crafting skill level</param>
    /// <param name="recipeDifficulty">The recipe's difficulty level</param>
    /// <returns>Success chance percentage (0-100)</returns>
    public int GetCraftingSuccessChance(int playerSkill, int recipeDifficulty)
    {
        // Base success chance formula:
        // 50% + (playerSkill - recipeDifficulty) * 2%
        // Range: 0% to 100%
        
        var baseChance = 50;
        var skillDifference = playerSkill - recipeDifficulty;
        var successChance = baseChance + (skillDifference * 2);
        
        successChance = Math.Clamp(successChance, 5, 99); // Always 5-99% (never guaranteed/impossible)
        
        _logger.LogDebug("Crafting success chance: {Chance}% (skill={PlayerSkill}, difficulty={Difficulty})", 
            successChance, playerSkill, recipeDifficulty);
        
        return successChance;
    }

    /// <summary>
    /// Calculate critical success chance for crafting.
    /// </summary>
    /// <param name="playerSkill">The player's crafting skill level</param>
    /// <param name="recipeDifficulty">The recipe's difficulty level</param>
    /// <returns>Critical success chance percentage (0-20)</returns>
    public int GetCraftingCriticalChance(int playerSkill, int recipeDifficulty)
    {
        // Critical chance increases with skill
        // Base 5% + 1% per 10 skill points above difficulty
        var skillDifference = Math.Max(0, playerSkill - recipeDifficulty);
        var criticalChance = 5 + (skillDifference / 10);
        
        criticalChance = Math.Clamp(criticalChance, 5, 20); // Max 20% critical
        
        _logger.LogDebug("Crafting critical chance: {Chance}% (skill={PlayerSkill}, difficulty={Difficulty})", 
            criticalChance, playerSkill, recipeDifficulty);
        
        return criticalChance;
    }

    /// <summary>
    /// Calculate failure severity based on how far below success threshold the roll was.
    /// </summary>
    /// <param name="successRoll">The roll result (1-100)</param>
    /// <param name="successChance">The required success chance</param>
    /// <returns>Failure severity: 0=success, 1=marginal, 2=moderate, 3=critical</returns>
    public int GetCraftingFailureSeverity(int successRoll, int successChance)
    {
        // Success - no failure
        if (successRoll <= successChance)
            return 0;

        // Calculate how far below success threshold
        var failureMargin = successRoll - successChance;

        // Tiered failure system:
        // 1-10 points below = Marginal failure
        // 11-30 points below = Moderate failure  
        // 31+ points below = Critical failure
        return failureMargin switch
        {
            <= 10 => 1, // Marginal
            <= 30 => 2, // Moderate
            _ => 3      // Critical
        };
    }

    /// <summary>
    /// Get quality reduction for a failure severity level.
    /// </summary>
    /// <param name="failureSeverity">The failure severity (1-3)</param>
    /// <returns>Number of quality tiers to reduce</returns>
    public int GetQualityReductionForFailure(int failureSeverity)
    {
        return failureSeverity switch
        {
            1 => 1, // Marginal: -1 tier
            2 => 2, // Moderate: -2 tiers
            3 => 3, // Critical: -3 tiers
            _ => 0  // Success: no reduction
        };
    }

    /// <summary>
    /// Get material refund percentage for a failure severity level.
    /// </summary>
    /// <param name="failureSeverity">The failure severity (1-3)</param>
    /// <returns>Percentage of materials to refund (0-50)</returns>
    public int GetMaterialRefundPercentage(int failureSeverity)
    {
        return failureSeverity switch
        {
            3 => 50, // Critical failure: 50% refund
            _ => 0   // All other cases: no refund
        };
    }

    /// <summary>
    /// Get budget range for quest reward items based on quest difficulty.
    /// </summary>
    /// <param name="questLevel">The quest level</param>
    /// <param name="difficulty">Quest difficulty (easy, normal, hard, legendary)</param>
    /// <returns>Tuple of (minBudget, maxBudget)</returns>
    public (int minBudget, int maxBudget) GetBudgetRangeForQuestReward(int questLevel, string difficulty)
    {
        var basePerLevel = 5;
        var levelBudget = questLevel * basePerLevel;

        var (minMultiplier, maxMultiplier) = difficulty.ToLowerInvariant() switch
        {
            "easy" => (0.8, 1.2),
            "normal" => (1.0, 1.5),
            "hard" => (1.5, 2.5),
            "legendary" => (3.0, 5.0),
            _ => (1.0, 1.5)
        };

        var minBudget = (int)(levelBudget * minMultiplier);
        var maxBudget = (int)(levelBudget * maxMultiplier);

        _logger.LogDebug("Quest reward budget range: {Min}-{Max} (level={Level}, difficulty={Difficulty})", 
            minBudget, maxBudget, questLevel, difficulty);

        return (Math.Max(15, minBudget), Math.Max(30, maxBudget));
    }

    /// <summary>
    /// Get budget for special event items (world boss, holiday event, etc.).
    /// </summary>
    /// <param name="eventType">Type of event (world_boss, holiday, dungeon_completion, etc.)</param>
    /// <param name="participantLevel">Average participant level</param>
    /// <returns>Budget for event reward item</returns>
    public int GetBudgetForEventReward(string eventType, int participantLevel)
    {
        var basePerLevel = 8; // Events are more generous than regular loot
        var levelBudget = participantLevel * basePerLevel;

        var eventMultiplier = eventType.ToLowerInvariant() switch
        {
            "world_boss" => 4.0,
            "dungeon_completion" => 2.5,
            "holiday_event" => 2.0,
            "achievement" => 1.8,
            "pvp_tournament" => 3.0,
            _ => 2.0
        };

        var budget = (int)(levelBudget * eventMultiplier);
        _logger.LogDebug("Event reward budget: {Budget} (type={Type}, level={Level})", 
            budget, eventType, participantLevel);

        return Math.Max(50, budget); // Events give minimum decent loot
    }

    /// <summary>
    /// Calculate budget for upgrading/reforging an existing item.
    /// Based on current item value and upgrade tier.
    /// </summary>
    /// <param name="currentItemValue">The current item's budget value</param>
    /// <param name="upgradeTier">The upgrade tier (1=minor, 2=major, 3=legendary)</param>
    /// <returns>Additional budget to add to item</returns>
    public int GetBudgetForItemUpgrade(int currentItemValue, int upgradeTier)
    {
        var upgradeMultiplier = upgradeTier switch
        {
            1 => 0.25, // +25% value
            2 => 0.50, // +50% value
            3 => 1.00, // +100% value (double)
            _ => 0.25
        };

        var additionalBudget = (int)(currentItemValue * upgradeMultiplier);
        _logger.LogDebug("Item upgrade budget: +{Budget} (current={Current}, tier={Tier})", 
            additionalBudget, currentItemValue, upgradeTier);

        return Math.Max(5, additionalBudget);
    }
}
