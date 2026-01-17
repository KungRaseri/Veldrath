using System.Text.Json.Serialization;

namespace RealmEngine.Shared.Data.Models;

/// <summary>
/// Root catalog data for classes/catalog.json (v4.1 format).
/// </summary>
public class ClassCatalogData
{
    /// <summary>Gets or sets the metadata.</summary>
    [JsonPropertyName("metadata")]
    public CatalogMetadata Metadata { get; set; } = new();

    /// <summary>Gets or sets the class types.</summary>
    [JsonPropertyName("class_types")]
    public Dictionary<string, ClassTypeCategory> ClassTypes { get; set; } = new();
}

/// <summary>
/// Category of classes (warrior, mage, rogue, etc.) with metadata and items.
/// </summary>
public class ClassTypeCategory
{
    /// <summary>Gets or sets the metadata.</summary>
    [JsonPropertyName("metadata")]
    public ClassCategoryMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the items.</summary>
    [JsonPropertyName("items")]
    public List<ClassItemData> Items { get; set; } = new();
}

/// <summary>
/// Metadata for a class category.
/// </summary>
public class ClassCategoryMetadata
{
    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary stats.</summary>
    [JsonPropertyName("primaryStats")]
    public List<string> PrimaryStats { get; set; } = new();

    /// <summary>Gets or sets the armor proficiency.</summary>
    [JsonPropertyName("armorProficiency")]
    public List<string> ArmorProficiency { get; set; } = new();
}

/// <summary>
/// Individual class data from JSON.
/// </summary>
public class ClassItemData
{
    /// <summary>Gets or sets the slug.</summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the rarity weight.</summary>
    [JsonPropertyName("rarityWeight")]
    public int RarityWeight { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether this is a subclass.</summary>
    [JsonPropertyName("isSubclass")]
    public bool IsSubclass { get; set; } = false;

    /// <summary>Gets or sets the parent class.</summary>
    [JsonPropertyName("parentClass")]
    public string? ParentClass { get; set; }

    /// <summary>Gets or sets the starting stats.</summary>
    [JsonPropertyName("startingStats")]
    public ClassStartingStats? StartingStats { get; set; }

    /// <summary>Gets or sets the starting ability IDs.</summary>
    [JsonPropertyName("startingAbilities")]
    public List<string>? StartingAbilityIds { get; set; }

    /// <summary>Gets or sets the traits.</summary>
    [JsonPropertyName("traits")]
    public Dictionary<string, object>? Traits { get; set; }

    /// <summary>Gets or sets the progression.</summary>
    [JsonPropertyName("progression")]
    public Dictionary<string, object>? Progression { get; set; }
}

/// <summary>
/// Starting stats from JSON.
/// </summary>
public class ClassStartingStats
{
    /// <summary>Gets or sets the health.</summary>
    [JsonPropertyName("health")]
    public int Health { get; set; } = 100;

    /// <summary>Gets or sets the mana.</summary>
    [JsonPropertyName("mana")]
    public int Mana { get; set; } = 50;

    /// <summary>Gets or sets the strength.</summary>
    [JsonPropertyName("strength")]
    public int Strength { get; set; } = 10;

    /// <summary>Gets or sets the dexterity.</summary>
    [JsonPropertyName("dexterity")]
    public int Dexterity { get; set; } = 10;

    /// <summary>Gets or sets the constitution.</summary>
    [JsonPropertyName("constitution")]
    public int Constitution { get; set; } = 10;

    /// <summary>Gets or sets the intelligence.</summary>
    [JsonPropertyName("intelligence")]
    public int Intelligence { get; set; } = 10;

    /// <summary>Gets or sets the wisdom.</summary>
    [JsonPropertyName("wisdom")]
    public int Wisdom { get; set; } = 10;

    /// <summary>Gets or sets the charisma.</summary>
    [JsonPropertyName("charisma")]
    public int Charisma { get; set; } = 10;
}
