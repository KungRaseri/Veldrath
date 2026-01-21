using Newtonsoft.Json;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Enemy type configuration loaded from enemies/enemy-types.json.
/// Defines default material pools and budget modifiers per enemy type.
/// </summary>
public class EnemyTypes
{
    /// <summary>Gets or sets the enemy types metadata.</summary>
    [JsonProperty("metadata")]
    public EnemyTypeMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the dictionary of enemy type configurations.</summary>
    [JsonProperty("types")]
    public Dictionary<string, EnemyTypeConfig> Types { get; set; } = new();
}

/// <summary>
/// Metadata for enemy types configuration.
/// </summary>
public class EnemyTypeMetadata
{
    /// <summary>Gets or sets the configuration description.</summary>
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the configuration version.</summary>
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
/// Configuration for a specific enemy type including budget multipliers.
/// </summary>
public class EnemyTypeConfig
{
    /// <summary>Gets or sets the budget multiplier for this enemy type.</summary>
    [JsonProperty("budgetMultiplier")]
    public double BudgetMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the optional material percentage override.</summary>
    [JsonProperty("materialPercentage")]
    public double? MaterialPercentage { get; set; }

    /// <summary>Gets or sets the description of this enemy type.</summary>
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;
}
