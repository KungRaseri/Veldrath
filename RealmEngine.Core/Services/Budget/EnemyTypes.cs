using System.Text.Json.Serialization;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Enemy type configuration loaded from enemies/enemy-types.json.
/// Defines default material pools and budget modifiers per enemy type.
/// </summary>
public class EnemyTypes
{
    /// <summary>Gets or sets the enemy types metadata.</summary>
    [JsonPropertyName("metadata")]
    public EnemyTypeMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the dictionary of enemy type configurations.</summary>
    [JsonPropertyName("types")]
    public Dictionary<string, EnemyTypeConfig> Types { get; set; } = new();
}

/// <summary>
/// Metadata for enemy types configuration.
/// </summary>
public class EnemyTypeMetadata
{
    /// <summary>Gets or sets the configuration description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the configuration version.</summary>
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
/// Configuration for a specific enemy type including budget multipliers.
/// </summary>
public class EnemyTypeConfig
{
    /// <summary>Gets or sets the budget multiplier for this enemy type.</summary>
    [JsonPropertyName("budgetMultiplier")]
    public double BudgetMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the optional material percentage override.</summary>
    [JsonPropertyName("materialPercentage")]
    public double? MaterialPercentage { get; set; }

    /// <summary>Gets or sets the description of this enemy type.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
