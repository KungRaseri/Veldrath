namespace RealmForge.Models;

/// <summary>
/// Represents a referenceable item from the JSON data files
/// </summary>
public class ReferenceInfo
{
    /// <summary>
    /// The domain this item belongs to (e.g., "items", "enemies", "abilities")
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// The path within the domain (e.g., "weapons/swords", "humanoid")
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The category for organization (e.g., "Weapons", "Enemies")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The item's unique name identifier
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI (may be formatted from name)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Rarity weight for the item (if applicable)
    /// </summary>
    public int RarityWeight { get; set; }

    /// <summary>
    /// Full file path to the catalog file containing this item
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The complete reference string in JSON v5.1 format
    /// Example: @items/weapons/swords:iron-longsword
    /// </summary>
    public string ReferenceString => $"@{Domain}/{Path}:{Name}";

    /// <summary>
    /// Additional metadata from the JSON file (level, description, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a category for organizing references in the UI
/// </summary>
public class ReferenceCategory
{
    /// <summary>
    /// Category identifier (e.g., "items", "enemies")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the category
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Icon name for the category (MaterialDesign icon)
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Number of items in this category
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Subcategories within this category
    /// </summary>
    public List<string> Subcategories { get; set; } = new();
}
