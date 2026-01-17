using Newtonsoft.Json;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Material pool configuration loaded from general/material-pools.json.
/// Defines which materials can drop from which enemy types.
/// </summary>
public class MaterialPools
{
    /// <summary>Gets or sets the material pools metadata.</summary>
    [JsonProperty("metadata")]
    public MaterialPoolMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the dictionary of material pools by pool name.</summary>
    [JsonProperty("pools")]
    public Dictionary<string, MaterialPool> Pools { get; set; } = new();
}

/// <summary>
/// Metadata for material pools configuration.
/// </summary>
public class MaterialPoolMetadata
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
/// Defines a pool of materials that can be selected from.
/// </summary>
public class MaterialPool
{
    /// <summary>Gets or sets the pool description.</summary>
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the metal materials with their selection properties.</summary>
    [JsonProperty("metals")]
    public Dictionary<string, MaterialPoolEntry> Metals { get; set; } = new();
}

/// <summary>
/// Entry defining a material's selection properties in a pool.
/// </summary>
public class MaterialPoolEntry
{
    /// <summary>Gets or sets the rarity weight for weighted random selection.</summary>
    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }

    /// <summary>Gets or sets the optional level requirement range [min, max].</summary>
    [JsonProperty("levelRequirementRange")]
    public int[]? LevelRequirementRange { get; set; }
}
