namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a suffix modifier for an item name (e.g., "of the Bear", "of Speed", "of Protection").
/// Suffixes appear after the base item name and provide various bonuses.
/// </summary>
public class ItemSuffix : IItemComponent
{
    /// <summary>
    /// Gets or sets the name of the suffix (e.g., "of the Bear", "of Speed").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rarity weight of this suffix (higher = more common).
    /// </summary>
    public int RarityWeight { get; set; }

    /// <summary>
    /// Gets or sets the description of this suffix.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the traits provided by this suffix.
    /// Includes attribute bonuses, resistances, special effects, etc.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
}
