namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a prefix modifier for an item name (e.g., "Flaming", "Sharp", "Vorpal").
/// Prefixes appear before the base item name and provide various bonuses.
/// </summary>
public class ItemPrefix : IItemComponent
{
    /// <summary>
    /// Gets or sets the name of the prefix (e.g., "Flaming", "Sharp").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rarity weight of this prefix (higher = more common).
    /// </summary>
    public int RarityWeight { get; set; }

    /// <summary>
    /// Gets or sets the description of this prefix.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the traits provided by this prefix.
    /// Includes damage bonuses, special effects, attribute bonuses, etc.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
}
