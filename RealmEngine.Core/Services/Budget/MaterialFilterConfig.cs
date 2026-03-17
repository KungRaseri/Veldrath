using System.Text.Json.Serialization;

namespace RealmEngine.Core.Services.Budget;

/// <summary>
/// Material filter configuration loaded from configuration/material-filters.json
/// </summary>
public class MaterialFilterConfig
{
    /// <summary>Gets or sets the metadata.</summary>
    [JsonPropertyName("metadata")]
    public MaterialFilterMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the default filters for unknown types.</summary>
    [JsonPropertyName("defaults")]
    public Dictionary<string, DefaultMaterialFilter> Defaults { get; set; } = new();

    /// <summary>Gets or sets the category-based material filters.</summary>
    [JsonPropertyName("categories")]
    public Dictionary<string, CategoryMaterialFilter> Categories { get; set; } = new();
}

/// <summary>
/// Metadata for material filter configuration
/// </summary>
public class MaterialFilterMetadata
{
    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the version.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Gets or sets the last updated date.</summary>
    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; set; }

    /// <summary>Gets or sets the configuration type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets additional notes.</summary>
    [JsonPropertyName("notes")]
    public List<string>? Notes { get; set; }
}

/// <summary>
/// Default material filter for unknown types
/// </summary>
public class DefaultMaterialFilter
{
    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the allowed material types.</summary>
    [JsonPropertyName("allowedMaterials")]
    public List<string> AllowedMaterials { get; set; } = new();
}

/// <summary>
/// Category-level material filter with type-specific overrides
/// </summary>
public class CategoryMaterialFilter
{
    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the default material types for this category.</summary>
    [JsonPropertyName("defaultMaterials")]
    public List<string> DefaultMaterials { get; set; } = new();

    /// <summary>Gets or sets the property-based material matches.</summary>
    [JsonPropertyName("propertyMatches")]
    public List<PropertyMaterialMatch>? PropertyMatches { get; set; }

    /// <summary>Gets or sets the type-specific material filters.</summary>
    [JsonPropertyName("types")]
    public Dictionary<string, TypeMaterialFilter> Types { get; set; } = new();
}

/// <summary>
/// Property-based material matching (e.g., armorClass=light → fabrics)
/// </summary>
public class PropertyMaterialMatch
{
    /// <summary>Gets or sets the property name to match.</summary>
    [JsonPropertyName("property")]
    public string Property { get; set; } = string.Empty;

    /// <summary>Gets or sets the condition (e.g., "exists").</summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    /// <summary>Gets or sets the expected value.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the allowed material types.</summary>
    [JsonPropertyName("allowedMaterials")]
    public List<string> AllowedMaterials { get; set; } = new();
}

/// <summary>
/// Type-specific material filter
/// </summary>
public class TypeMaterialFilter
{
    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the allowed material types.</summary>
    [JsonPropertyName("allowedMaterials")]
    public List<string> AllowedMaterials { get; set; } = new();
}
