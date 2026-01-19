using Newtonsoft.Json;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Budget configuration loaded from general/budget_config.json.
/// Controls how item budget is calculated and allocated across components.
/// </summary>
public class BudgetConfig
{
  /// <summary>Gets or sets the metadata.</summary>
  [JsonProperty("metadata")]
  public BudgetMetadata? Metadata { get; set; }

  /// <summary>Gets or sets the budget allocation.</summary>
  [JsonProperty("budgetAllocation")]
  public BudgetAllocation Allocation { get; set; } = new();

  /// <summary>Gets or sets the cost formulas.</summary>
  [JsonProperty("costFormulas")]
  public CostFormulas Formulas { get; set; } = new();

  /// <summary>Gets or sets the pattern costs.</summary>
  [JsonProperty("patternCosts")]
  public Dictionary<string, int> PatternCosts { get; set; } = new();

  /// <summary>Gets or sets the minimum costs.</summary>
  [JsonProperty("minimumCosts")]
  public MinimumCosts MinimumCosts { get; set; } = new();

  /// <summary>Gets or sets the budget ranges.</summary>
  [JsonProperty("budgetRanges")]
  public Dictionary<string, BudgetRange> BudgetRanges { get; set; } = new();

  /// <summary>Gets or sets the source multipliers.</summary>
  [JsonProperty("sourceMultipliers")]
  public SourceMultipliers SourceMultipliers { get; set; } = new();
}

/// <summary>
/// Budget metadata.
/// </summary>
public class BudgetMetadata
{
  /// <summary>Gets or sets the budget configuration description.</summary>
  [JsonProperty("description")]
  public string Description { get; set; } = string.Empty;

  /// <summary>Gets or sets the budget configuration version.</summary>
  [JsonProperty("version")]
  public string Version { get; set; } = string.Empty;

  /// <summary>Gets or sets the last updated timestamp.</summary>
  [JsonProperty("lastUpdated")]
  public string LastUpdated { get; set; } = string.Empty;

  /// <summary>Gets or sets the configuration type.</summary>
  [JsonProperty("type")]
  public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Budget allocation percentages for materials vs components.
/// </summary>
public class BudgetAllocation
{
  /// <summary>Gets or sets the percentage allocated to material costs.</summary>
  [JsonProperty("materialPercentage")]
  public double MaterialPercentage { get; set; } = 0.30;

  /// <summary>Gets or sets the percentage allocated to component costs.</summary>
  [JsonProperty("componentPercentage")]
  public double ComponentPercentage { get; set; } = 0.70;

  /// <summary>Gets or sets the allocation description.</summary>
  [JsonProperty("description")]
  public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Collection of cost calculation formulas.
/// </summary>
public class CostFormulas
{
  /// <summary>Gets or sets the material cost formula.</summary>
  [JsonProperty("material")]
  public CostFormula Material { get; set; } = new();

  /// <summary>Gets or sets the component cost formula.</summary>
  [JsonProperty("component")]
  public CostFormula Component { get; set; } = new();

  /// <summary>Gets or sets the enchantment cost formula.</summary>
  [JsonProperty("enchantment")]
  public CostFormula Enchantment { get; set; } = new();

  /// <summary>Gets or sets the material quality cost formula.</summary>
  [JsonProperty("materialQuality")]
  public CostFormula MaterialQuality { get; set; } = new();
}

/// <summary>
/// Defines a cost calculation formula.
/// </summary>
public class CostFormula
{
  /// <summary>Gets or sets the formula expression.</summary>
  [JsonProperty("formula")]
  public string Formula { get; set; } = string.Empty;

  /// <summary>Gets or sets the formula numerator value.</summary>
  [JsonProperty("numerator")]
  public int? Numerator { get; set; }

  /// <summary>Gets or sets the field name used in the formula.</summary>
  [JsonProperty("field")]
  public string Field { get; set; } = string.Empty;

  /// <summary>Gets or sets the optional scale field name (for materials).</summary>
  [JsonProperty("scaleField")]
  public string? ScaleField { get; set; }

  /// <summary>Gets or sets the formula description.</summary>
  [JsonProperty("description")]
  public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Minimum cost values for various item components.
/// </summary>
public class MinimumCosts
{
  /// <summary>Gets or sets the minimum cost for material quality.</summary>
  [JsonProperty("materialQuality")]
  public int MaterialQuality { get; set; } = 5;

  /// <summary>Gets or sets the minimum cost for prefix enchantments.</summary>
  [JsonProperty("prefix")]
  public int Prefix { get; set; } = 3;

  /// <summary>Gets or sets the minimum cost for suffix enchantments.</summary>
  [JsonProperty("suffix")]
  public int Suffix { get; set; } = 3;

  /// <summary>Gets or sets the minimum cost for descriptive modifiers.</summary>
  [JsonProperty("descriptive")]
  public int Descriptive { get; set; } = 3;

  /// <summary>Gets or sets the minimum cost for enchantments.</summary>
  [JsonProperty("enchantment")]
  public int Enchantment { get; set; } = 15;

  /// <summary>Gets or sets the minimum cost for sockets.</summary>
  [JsonProperty("socket")]
  public int Socket { get; set; } = 10;
}

/// <summary>
/// Represents a budget range with minimum and maximum values.
/// </summary>
public class BudgetRange
{
  /// <summary>Gets or sets the minimum budget value.</summary>
  [JsonProperty("min")]
  public int Min { get; set; }

  /// <summary>Gets or sets the maximum budget value.</summary>
  [JsonProperty("max")]
  public int Max { get; set; }
}

/// <summary>
/// Multipliers for different loot sources (enemies, shops, bosses).
/// </summary>
public class SourceMultipliers
{
  /// <summary>Gets or sets the multiplier per enemy level.</summary>
  [JsonProperty("enemyLevelMultiplier")]
  public double EnemyLevelMultiplier { get; set; } = 5.0;

  /// <summary>Gets or sets the base budget for shop tiers.</summary>
  [JsonProperty("shopTierBase")]
  public int ShopTierBase { get; set; } = 30;

  /// <summary>Gets or sets the budget multiplier for boss enemies.</summary>
  [JsonProperty("bossMultiplier")]
  public double BossMultiplier { get; set; } = 2.5;

  /// <summary>Gets or sets the budget multiplier for elite enemies.</summary>
  [JsonProperty("eliteMultiplier")]
  public double EliteMultiplier { get; set; } = 1.5;
}
