namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a quality tier for an item (Fine, Superior, Exceptional, Masterwork, Legendary).
/// Quality modifies item stats based on item type (weapon vs armor have different bonuses).
/// </summary>
public class ItemQuality : IItemComponent
{
    /// <summary>
    /// Gets or sets the name of the quality (e.g., "Fine", "Masterwork").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rarity weight of this quality (higher = more common).
    /// </summary>
    public int RarityWeight { get; set; }

    /// <summary>
    /// Gets or sets the description of this quality.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type-specific traits (weapon vs armor).
    /// Allows quality to provide different bonuses based on item type.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> ItemTypeTraits { get; set; } = new();

    /// <summary>
    /// Gets the traits provided by this quality.
    /// Returns traits specific to the item type if available.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
}
