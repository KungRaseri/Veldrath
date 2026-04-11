namespace Veldrath.Server.Data.Entities;

/// <summary>A named geographic region within the world, containing several adjacent zones.</summary>
public class Region
{
    /// <summary>Slug identifier (e.g. "thornveil").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the region.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Flavour description shown on region discovery and maps.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Dominant terrain / biome archetype.</summary>
    public RegionType Type { get; set; } = RegionType.Forest;

    /// <summary>Minimum recommended character level for this region (0 = no restriction).</summary>
    public int MinLevel { get; set; }

    /// <summary>Maximum recommended character level for this region.</summary>
    public int MaxLevel { get; set; }

    /// <summary>True for the starting region that new characters are placed into.</summary>
    public bool IsStarter { get; set; }

    /// <summary>False for regions that are unlocked through progression or quests.</summary>
    public bool IsDiscoverable { get; set; } = true;

    // FK
    /// <summary>The world this region belongs to.</summary>
    public string WorldId { get; set; } = string.Empty;

    // Navigation
    /// <summary>Parent world.</summary>
    public World World { get; set; } = null!;

    /// <summary>All zones contained within this region.</summary>
    public ICollection<Zone> Zones { get; set; } = [];

    /// <summary>Directed connections to adjacent regions (outbound from this region).</summary>
    public ICollection<RegionConnection> Connections { get; set; } = [];
}

/// <summary>Terrain / biome archetype of a region.</summary>
public enum RegionType
{
    /// <summary>Dense woodland and ancient forest.</summary>
    Forest,

    /// <summary>Open highland moors and bogland.</summary>
    Highland,

    /// <summary>Sea-facing cliffs and coastal terrain.</summary>
    Coastal,

    /// <summary>Volcanic plains and ash wastes.</summary>
    Volcanic,

    /// <summary>Rolling farmland and pastoral countryside on the edge of the wild.</summary>
    Countryside,
}
