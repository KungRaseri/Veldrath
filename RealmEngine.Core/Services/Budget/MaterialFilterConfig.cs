using Newtonsoft.Json;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Material filter configuration loaded from configuration/material-filters.json
/// </summary>
public class MaterialFilterConfig
{
    /// <summary>Gets or sets the metadata.</summary>
    [JsonProperty("metadata")]
    public MaterialFilterMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the default filters for unknown types.</summary>
    [JsonProperty("defaults")]
    public Dictionary<string, DefaultMaterialFilter> Defaults { get; set; } = new();

    /// <summary>Gets or sets the category-based material filters.</summary>
    [JsonProperty("categories")]
    public Dictionary<string, CategoryMaterialFilter> Categories { get; set; } = new();
}

/// <summary>
/// Metadata for material filter configuration
/// </summary>
public class MaterialFilterMetadata
{
    /// <summary>Gets or sets the description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the version.</summary>
    [JsonProperty("version")]
    public string? Version { get; set; }

    /// <summary>Gets or sets the last updated date.</summary>
    [JsonProperty("lastUpdated")]
    public string? LastUpdated { get; set; }

    /// <summary>Gets or sets the configuration type.</summary>
    [JsonProperty("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets additional notes.</summary>
    [JsonProperty("notes")]
    public List<string>? Notes { get; set; }
}

/// <summary>
/// Default material filter for unknown types
/// </summary>
public class DefaultMaterialFilter
{
    /// <summary>Gets or sets the description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the allowed material types.</summary>
    [JsonProperty("allowedMaterials")]
    public List<string> AllowedMaterials { get; set; } = new();
}

/// <summary>
/// Category-level material filter with type-specific overrides
/// </summary>
public class CategoryMaterialFilter
{
    /// <summary>Gets or sets the description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the default material types for this category.</summary>
    [JsonProperty("defaultMaterials")]
    public List<string> DefaultMaterials { get; set; } = new();

    /// <summary>Gets or sets the property-based material matches.</summary>
    [JsonProperty("propertyMatches")]
    public List<PropertyMaterialMatch>? PropertyMatches { get; set; }

    /// <summary>Gets or sets the type-specific material filters.</summary>
    [JsonProperty("types")]
    public Dictionary<string, TypeMaterialFilter> Types { get; set; } = new();
}

/// <summary>
/// Property-based material matching (e.g., armorClass=light → fabrics)
/// </summary>
public class PropertyMaterialMatch
{
    /// <summary>Gets or sets the property name to match.</summary>
    [JsonProperty("property")]
    public string Property { get; set; } = string.Empty;

    /// <summary>Gets or sets the condition (e.g., "exists").</summary>
    [JsonProperty("condition")]
    public string? Condition { get; set; }

    /// <summary>Gets or sets the expected value.</summary>
    [JsonProperty("value")]
    public string? Value { get; set; }

    /// <summary>Gets or sets the description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the allowed material types.</summary>
    [JsonProperty("allowedMaterials")]
    public List<string> AllowedMaterials { get; set; } = new();
}

/// <summary>
/// Type-specific material filter
/// </summary>
public class TypeMaterialFilter
{
    /// <summary>Gets or sets the description.</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the allowed material types.</summary>
    [JsonProperty("allowedMaterials")]
    public List<string> AllowedMaterials { get; set; } = new();
}
