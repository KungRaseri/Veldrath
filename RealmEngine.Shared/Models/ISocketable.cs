namespace RealmEngine.Shared.Models;

/// <summary>
/// Interface for items that can be socketed into equipment.
/// Includes gems, essences, runes, crystals, and orbs.
/// </summary>
public interface ISocketable : ITraitable
{
    /// <summary>
    /// Unique identifier for this socketable item.
    /// </summary>
    string Id { get; set; }

    /// <summary>
    /// Display name of the socketable item.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Type of socket this item fits into.
    /// </summary>
    SocketType SocketType { get; set; }

    /// <summary>
    /// Thematic category for organization (e.g., "red" for gems, "fire" for essences).
    /// Does not restrict socket compatibility - used for organization only.
    /// </summary>
    string? Category { get; set; }

    /// <summary>
    /// Description of the socketable item's effects.
    /// </summary>
    string Description { get; set; }

    /// <summary>
    /// Rarity weight for procedural generation.
    /// </summary>
    int RarityWeight { get; set; }
}
