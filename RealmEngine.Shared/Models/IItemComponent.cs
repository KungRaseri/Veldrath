namespace RealmEngine.Shared.Models;

/// <summary>
/// Base interface for all item components (Quality, Material, Prefix, Suffix).
/// Components provide traits and contribute to the item's display name and attributes.
/// </summary>
public interface IItemComponent : ITraitable
{
    /// <summary>
    /// Gets the name of the component (e.g., "Fine", "Iron", "Flaming", "of the Bear").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the rarity weight of this component (higher = more common).
    /// Used for selection probability and budget calculations.
    /// </summary>
    int RarityWeight { get; }

    /// <summary>
    /// Gets the description of this component.
    /// </summary>
    string Description { get; }
}
