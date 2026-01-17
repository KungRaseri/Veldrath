using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Calculates budget costs for materials, components, and enchantments.
/// Uses formulas from budget-config.json.
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
}
