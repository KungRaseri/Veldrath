using System.Text.Json.Serialization;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Budget configuration loaded from general/budget-config.json.
/// Controls how item budget is calculated and allocated across components.
/// </summary>
public class BudgetConfig
{
  /// <summary>Gets or sets the metadata.</summary>
  [JsonPropertyName("metadata")]
  public BudgetMetadata? Metadata { get; set; }

  /// <summary>Gets or sets the budget allocation.</summary>
  [JsonPropertyName("budgetAllocation")]
  public BudgetAllocation Allocation { get; set; } = new();

  /// <summary>Gets or sets the cost formulas.</summary>
  [JsonPropertyName("costFormulas")]
  public CostFormulas Formulas { get; set; } = new();

  /// <summary>Gets or sets the pattern costs.</summary>
  [JsonPropertyName("patternCosts")]
  public Dictionary<string, int> PatternCosts { get; set; } = new();

  /// <summary>Gets or sets the minimum costs.</summary>
  [JsonPropertyName("minimumCosts")]
  public MinimumCosts MinimumCosts { get; set; } = new();

  /// <summary>Gets or sets the budget ranges.</summary>
  [JsonPropertyName("budgetRanges")]
  public Dictionary<string, BudgetRange> BudgetRanges { get; set; } = new();

  /// <summary>Gets or sets the source multipliers.</summary>
  [JsonPropertyName("sourceMultipliers")]
  public SourceMultipliers SourceMultipliers { get; set; } = new();
}

/// <summary>
/// Budget metadata.
/// </summary>
public class BudgetMetadata
{
  /// <summary>Gets or sets the budget configuration description.</summary>
  [JsonPropertyName("description")]
  public string Description { get; set; } = string.Empty;

  /// <summary>Gets or sets the budget configuration version.</summary>
  [JsonPropertyName("version")]
  public string Version { get; set; } = string.Empty;

  /// <summary>Gets or sets the last updated timestamp.</summary>
  [JsonPropertyName("lastUpdated")]
  public string LastUpdated { get; set; } = string.Empty;

  /// <summary>Gets or sets the configuration type.</summary>
  [JsonPropertyName("type")]
  public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Budget allocation percentages for materials vs components.
/// </summary>
public class BudgetAllocation
{
  /// <summary>Gets or sets the percentage allocated to material costs.</summary>
  [JsonPropertyName("materialPercentage")]
  public double MaterialPercentage { get; set; } = 0.30;

  /// <summary>Gets or sets the percentage allocated to component costs.</summary>
  [JsonPropertyName("componentPercentage")]
  public double ComponentPercentage { get; set; } = 0.70;

  /// <summary>Gets or sets the allocation description.</summary>
  [JsonPropertyName("description")]
  public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Collection of cost calculation formulas.
/// </summary>
public class CostFormulas
{
  /// <summary>Gets or sets the material cost formula.</summary>
  [JsonPropertyName("material")]
  public CostFormula Material { get; set; } = new();

  /// <summary>Gets or sets the component cost formula.</summary>
  [JsonPropertyName("component")]
  public CostFormula Component { get; set; } = new();

  /// <summary>Gets or sets the enchantment cost formula.</summary>
  [JsonPropertyName("enchantment")]
  public CostFormula Enchantment { get; set; } = new();

  /// <summary>Gets or sets the material quality cost formula.</summary>
  [JsonPropertyName("materialQuality")]
  public CostFormula MaterialQuality { get; set; } = new();
}

/// <summary>
/// Defines a cost calculation formula.
/// </summary>
public class CostFormula
{
  /// <summary>Gets or sets the formula expression.</summary>
  [JsonPropertyName("formula")]
  public string Formula { get; set; } = string.Empty;

  /// <summary>Gets or sets the formula numerator value.</summary>
  [JsonPropertyName("numerator")]
  public int? Numerator { get; set; }

  /// <summary>Gets or sets the field name used in the formula.</summary>
  [JsonPropertyName("field")]
  public string Field { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional scale field name (for materials).</summary>
  [JsonPropertyName("scaleField")]
  public string? ScaleField { get; set; }

  /// <summary>Gets or sets the formula description.</summary>
  [JsonPropertyName("description")]
  public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Minimum cost values for various item components.
/// </summary>
public class MinimumCosts
{
  /// <summary>Gets or sets the minimum cost for material quality.</summary>
  [JsonPropertyName("materialQuality")]
  public int MaterialQuality { get; set; } = 5;

  /// <summary>Gets or sets the minimum cost for prefix enchantments.</summary>
  [JsonPropertyName("prefix")]
  public int Prefix { get; set; } = 3;

  /// <summary>Gets or sets the minimum cost for suffix enchantments.</summary>
  [JsonPropertyName("suffix")]
  public int Suffix { get; set; } = 3;

  /// <summary>Gets or sets the minimum cost for descriptive modifiers.</summary>
  [JsonPropertyName("descriptive")]
  public int Descriptive { get; set; } = 3;

  /// <summary>Gets or sets the minimum cost for enchantments.</summary>
  [JsonPropertyName("enchantment")]
  public int Enchantment { get; set; } = 15;

  /// <summary>Gets or sets the minimum cost for sockets.</summary>
  [JsonPropertyName("socket")]
  public int Socket { get; set; } = 10;
}

/// <summary>
/// Represents a budget range with minimum and maximum values.
/// </summary>
public class BudgetRange
{
  /// <summary>Gets or sets the minimum budget value.</summary>
  [JsonPropertyName("min")]
  public int Min { get; set; }

  /// <summary>Gets or sets the maximum budget value.</summary>
  [JsonPropertyName("max")]
  public int Max { get; set; }
}

/// <summary>
/// Multipliers for different loot sources (enemies, shops, bosses).
/// </summary>
public class SourceMultipliers
{
  /// <summary>Gets or sets the multiplier per enemy level.</summary>
  [JsonPropertyName("enemyLevelMultiplier")]
  public double EnemyLevelMultiplier { get; set; } = 5.0;

  /// <summary>Gets or sets the base budget for shop tiers.</summary>
  [JsonPropertyName("shopTierBase")]
  public int ShopTierBase { get; set; } = 30;

  /// <summary>Gets or sets the budget multiplier for boss enemies.</summary>
  [JsonPropertyName("bossMultiplier")]
  public double BossMultiplier { get; set; } = 2.5;

  /// <summary>Gets or sets the budget multiplier for elite enemies.</summary>
  [JsonPropertyName("eliteMultiplier")]
  public double EliteMultiplier { get; set; } = 1.5;
}
