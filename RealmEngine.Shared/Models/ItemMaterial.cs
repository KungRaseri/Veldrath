namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a material used in item crafting (Iron, Mithril, Oak, Dragonhide, etc.).
/// Materials provide base attributes and durability modifiers.
/// </summary>
public class ItemMaterial : IItemComponent
{
    /// <summary>
    /// Gets or sets the name of the material (e.g., "Iron", "Mithril").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rarity weight of this material (higher = more common).
    /// </summary>
    public int RarityWeight { get; set; }

    /// <summary>
    /// Gets or sets the description of this material.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cost scale multiplier for refined/exotic materials.
    /// Defaults to 1.0. Values > 1.0 represent processing premiums or artificial scarcity.
    /// </summary>
    public double CostScale { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the material category (metal, wood, leather, etc.).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets the traits provided by this material.
    /// Includes durability bonuses, weight modifiers, attribute bonuses, etc.
    /// </summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
}
