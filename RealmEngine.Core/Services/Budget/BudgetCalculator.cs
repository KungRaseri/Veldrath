using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Calculates budget costs for materials, components, and enchantments.
/// Uses formulas from budget_config.json.
/// </summary>
public class BudgetCalculator
{
    private readonly BudgetConfig _config;
    private readonly ILogger<BudgetCalculator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetCalculator"/> class.
    /// </summary>
    /// <param name="config">The budget configuration.</param>
    /// <param name="logger">The logger.</param>
    public BudgetCalculator(BudgetConfig config, ILogger<BudgetCalculator> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculate the base budget for an item based on enemy level.
    /// </summary>
    public int CalculateBaseBudget(int enemyLevel, bool isBoss = false, bool isElite = false)
    {
        var baseBudget = enemyLevel * _config.SourceMultipliers.EnemyLevelMultiplier;

        if (isBoss)
        {
            baseBudget *= _config.SourceMultipliers.BossMultiplier;
        }
        else if (isElite)
        {
            baseBudget *= _config.SourceMultipliers.EliteMultiplier;
        }

        return (int)Math.Round(baseBudget);
    }

    /// <summary>
    /// Apply quality modifier to base budget (multiplicative).
    /// </summary>
    public int ApplyQualityModifier(int baseBudget, double qualityModifier)
    {
        var adjustedBudget = baseBudget * (1.0 + qualityModifier);
        return (int)Math.Round(adjustedBudget);
    }

    /// <summary>
    /// Calculate material budget allocation.
    /// </summary>
    public int CalculateMaterialBudget(int totalBudget, double? materialPercentageOverride = null)
    {
        var percentage = materialPercentageOverride ?? _config.Allocation.MaterialPercentage;
        return (int)Math.Round(totalBudget * percentage);
    }

    /// <summary>
    /// Calculate component budget (remaining after material allocation).
    /// </summary>
    public int CalculateComponentBudget(int totalBudget, int materialBudget)
    {
        return totalBudget - materialBudget;
    }

    /// <summary>
    /// Calculate the cost of a material using inverse formula with optional cost scale.
    /// Formula: cost = (numerator / rarityWeight) × costScale
    /// </summary>
    public int CalculateMaterialCost(JToken material)
    {
        var rarityWeight = GetIntProperty(material, "rarityWeight", 0);
        if (rarityWeight <= 0)
        {
            _logger.LogWarning("Material {MaterialName} has invalid rarityWeight: {Weight}", 
                GetStringProperty(material, "name"), rarityWeight);
            return 999999; // Unaffordable
        }

        var numerator = _config.Formulas.Material.Numerator ?? 6000;
        var costScale = GetDoubleProperty(material, "costScale", 1.0);
        var cost = ((double)numerator / rarityWeight) * costScale;
        
        return (int)Math.Round(cost);
    }

    /// <summary>
    /// Calculate the cost of a component using inverse formula: numerator / rarityWeight.
    /// </summary>
    public int CalculateComponentCost(JToken component)
    {
        var rarityWeight = GetIntProperty(component, "rarityWeight", 0);
        if (rarityWeight <= 0)
        {
            _logger.LogWarning("Component {ComponentName} has invalid rarityWeight: {Weight}", 
                GetStringProperty(component, "value"), rarityWeight);
            return 999999; // Unaffordable
        }

        var numerator = _config.Formulas.Component.Numerator ?? 100;
        var cost = (double)numerator / rarityWeight;
        return (int)Math.Round(cost);
    }

    /// <summary>
    /// Calculate the cost of an enchantment using premium formula: numerator / rarityWeight.
    /// </summary>
    public int CalculateEnchantmentCost(JToken enchantment)
    {
        var rarityWeight = GetIntProperty(enchantment, "rarityWeight", 0);
        if (rarityWeight <= 0)
        {
            _logger.LogWarning("Enchantment {EnchantmentName} has invalid rarityWeight: {Weight}", 
                GetStringProperty(enchantment, "value"), rarityWeight);
            return 999999; // Unaffordable
        }

        var numerator = _config.Formulas.Enchantment.Numerator ?? 130;
        var cost = (double)numerator / rarityWeight;
        return (int)Math.Round(cost);
    }

    /// <summary>
    /// Calculate the cost of a material quality modifier using inverse formula.
    /// </summary>
    public int CalculateQualityCost(JToken quality)
    {
        var rarityWeight = GetIntProperty(quality, "rarityWeight", 0);
        if (rarityWeight <= 0)
        {
            return 999999; // Unaffordable
        }

        var numerator = _config.Formulas.MaterialQuality.Numerator ?? 150;
        var cost = (double)numerator / rarityWeight;
        return (int)Math.Round(cost);
    }

    /// <summary>
    /// Get pattern overhead cost.
    /// </summary>
    public int GetPatternCost(string patternString)
    {
        if (_config.PatternCosts.TryGetValue(patternString, out var cost))
        {
            return cost;
        }
        
        // Default to 0 for unknown patterns
        return 0;
    }

    /// <summary>
    /// Check if an item is affordable within budget.
    /// </summary>
    public bool CanAfford(int currentBudget, int cost)
    {
        return currentBudget >= cost;
    }

    private static string GetStringProperty(JToken token, string propertyName)
    {
        return token[propertyName]?.Value<string>() ?? string.Empty;
    }

    private static int GetIntProperty(JToken token, string propertyName, int defaultValue)
    {
        return token[propertyName]?.Value<int>() ?? defaultValue;
    }

    private static double GetDoubleProperty(JToken token, string propertyName, double defaultValue)
    {
        try
        {
            var value = token[propertyName];
            return value?.Value<double>() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Get a detailed budget breakdown for UI display.
    /// Shows how budget is allocated across materials and components.
    /// </summary>
    /// <param name="totalBudget">Total budget to break down</param>
    /// <param name="materialPercentageOverride">Optional material percentage override</param>
    /// <returns>Dictionary of allocation type to budget amount</returns>
    public Dictionary<string, int> GetBudgetBreakdown(int totalBudget, double? materialPercentageOverride = null)
    {
        var materialBudget = CalculateMaterialBudget(totalBudget, materialPercentageOverride);
        var componentBudget = CalculateComponentBudget(totalBudget, materialBudget);

        return new Dictionary<string, int>
        {
            ["Total"] = totalBudget,
            ["Materials"] = materialBudget,
            ["Components"] = componentBudget,
            ["MaterialPercentage"] = (int)Math.Round((double)materialBudget / totalBudget * 100)
        };
    }

    /// <summary>
    /// Check if an item is affordable within a budget.
    /// Useful for greying out unaffordable items in UI.
    /// </summary>
    /// <param name="itemCost">Cost of the item</param>
    /// <param name="availableBudget">Available budget</param>
    /// <returns>True if affordable</returns>
    public static bool IsAffordable(int itemCost, int availableBudget)
    {
        return itemCost <= availableBudget;
    }

    /// <summary>
    /// Get affordability status as a display string.
    /// Examples: "Affordable", "15 over budget", "Perfect fit"
    /// </summary>
    /// <param name="itemCost">Cost of the item</param>
    /// <param name="availableBudget">Available budget</param>
    /// <returns>User-friendly status string</returns>
    public static string GetAffordabilityDisplay(int itemCost, int availableBudget)
    {
        if (itemCost == availableBudget)
            return "Perfect fit";

        if (itemCost < availableBudget)
        {
            var remaining = availableBudget - itemCost;
            return $"Affordable ({remaining} remaining)";
        }

        var overBudget = itemCost - availableBudget;
        return $"{overBudget} over budget";
    }

    /// <summary>
    /// Calculate remaining budget after an item is selected.
    /// </summary>
    /// <param name="totalBudget">Total available budget</param>
    /// <param name="itemCost">Cost of the selected item</param>
    /// <returns>Remaining budget (0 if over budget)</returns>
    public static int GetRemainingBudget(int totalBudget, int itemCost)
    {
        return Math.Max(0, totalBudget - itemCost);
    }

    /// <summary>
    /// Get budget utilization percentage.
    /// Useful for progress bars showing budget usage.
    /// </summary>
    /// <param name="spentBudget">Amount of budget spent</param>
    /// <param name="totalBudget">Total available budget</param>
    /// <returns>Utilization percentage (0-100, can exceed 100)</returns>
    public static double GetBudgetUtilization(int spentBudget, int totalBudget)
    {
        if (totalBudget <= 0) return 0.0;
        return (double)spentBudget / totalBudget * 100.0;
    }

    /// <summary>
    /// Get recommended rarityWeight range for a given budget.
    /// Helps filter affordable items in loot generation.
    /// </summary>
    /// <param name="availableBudget">Available budget</param>
    /// <param name="formulaNumerator">Formula numerator (6000 for materials, 100 for components)</param>
    /// <returns>Tuple of (minWeight, maxWeight)</returns>
    public static (int minWeight, int maxWeight) GetAffordableWeightRange(int availableBudget, int formulaNumerator = 6000)
    {
        if (availableBudget <= 0) return (100, 100); // Only common items

        // Inverse formula: cost = numerator / weight
        // So: weight = numerator / cost
        var maxWeight = 100; // Common tier cap
        var minWeight = Math.Max(1, formulaNumerator / availableBudget);

        return (minWeight, maxWeight);
    }

    /// <summary>
    /// Estimate item quality tier from budget.
    /// Returns a user-friendly tier name.
    /// </summary>
    /// <param name="totalBudget">Total item budget</param>
    /// <returns>Quality tier name</returns>
    public static string GetQualityTierFromBudget(int totalBudget)
    {
        return totalBudget switch
        {
            >= 5000 => "Legendary",
            >= 2000 => "Epic",
            >= 800 => "Rare",
            >= 300 => "Uncommon",
            _ => "Common"
        };
    }

    /// <summary>
    /// Get cost breakdown for multiple items.
    /// Useful for crafting recipes showing total costs.
    /// </summary>
    /// <param name="items">List of (itemName, cost) tuples</param>
    /// <returns>Dictionary of item names to costs, plus "Total" key</returns>
    public static Dictionary<string, int> GetMultiItemCostBreakdown(List<(string name, int cost)> items)
    {
        var breakdown = items.ToDictionary(i => i.name, i => i.cost);
        breakdown["Total"] = items.Sum(i => i.cost);
        return breakdown;
    }
}
