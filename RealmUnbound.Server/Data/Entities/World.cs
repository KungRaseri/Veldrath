namespace RealmUnbound.Server.Data.Entities;

/// <summary>The top-level world container that groups all geographic regions of Draveth.</summary>
public class World
{
    /// <summary>Slug identifier (e.g. "draveth").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the world.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Flavour description shown in lore screens and loading text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Current in-world era (e.g. "The Age of Embers").</summary>
    public string Era { get; set; } = string.Empty;

    // Navigation
    /// <summary>All regions that exist within this world.</summary>
    public ICollection<Region> Regions { get; set; } = [];
}
