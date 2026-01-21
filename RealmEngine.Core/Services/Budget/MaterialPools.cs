using Newtonsoft.Json;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Material pool configuration loaded from general/material-pools.json.
/// Defines which materials can drop from which sources.
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
/// Supports both old structure (metals dictionary) and new structure (rarity tiers).
/// </summary>
public class MaterialPool
{
    /// <summary>Gets or sets the pool description.</summary>
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the metal materials with their selection properties (LEGACY).</summary>
    [JsonProperty("metals")]
    public Dictionary<string, MaterialPoolEntry>? Metals { get; set; }

    /// <summary>Gets or sets common tier materials.</summary>
    [JsonProperty("common")]
    public List<MaterialReference>? Common { get; set; }

    /// <summary>Gets or sets uncommon tier materials.</summary>
    [JsonProperty("uncommon")]
    public List<MaterialReference>? Uncommon { get; set; }

    /// <summary>Gets or sets rare tier materials.</summary>
    [JsonProperty("rare")]
    public List<MaterialReference>? Rare { get; set; }

    /// <summary>Gets or sets epic tier materials.</summary>
    [JsonProperty("epic")]
    public List<MaterialReference>? Epic { get; set; }

    /// <summary>Gets or sets legendary tier materials.</summary>
    [JsonProperty("legendary")]
    public List<MaterialReference>? Legendary { get; set; }

    /// <summary>
    /// Get all materials from a specific rarity tier.
    /// </summary>
    public List<MaterialReference> GetTier(string tierName)
    {
        return tierName.ToLower() switch
        {
            "common" => Common ?? new List<MaterialReference>(),
            "uncommon" => Uncommon ?? new List<MaterialReference>(),
            "rare" => Rare ?? new List<MaterialReference>(),
            "epic" => Epic ?? new List<MaterialReference>(),
            "legendary" => Legendary ?? new List<MaterialReference>(),
            _ => new List<MaterialReference>()
        };
    }

    /// <summary>
    /// Get all materials across all rarity tiers.
    /// </summary>
    public List<MaterialReference> GetAllMaterials()
    {
        var all = new List<MaterialReference>();
        if (Common != null) all.AddRange(Common);
        if (Uncommon != null) all.AddRange(Uncommon);
        if (Rare != null) all.AddRange(Rare);
        if (Epic != null) all.AddRange(Epic);
        if (Legendary != null) all.AddRange(Legendary);
        return all;
    }
}

/// <summary>
/// Reference to a material item in the new structure.
/// </summary>
public class MaterialReference
{
    /// <summary>Gets or sets the reference to the material item.</summary>
    [JsonProperty("itemRef")]
    public string ItemRef { get; set; } = string.Empty;

    /// <summary>Gets or sets the rarity weight for weighted random selection.</summary>
    [JsonProperty("rarityWeight")]
    public int RarityWeight { get; set; }
}

/// <summary>
/// Entry defining a material's selection properties in a pool (LEGACY).
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
