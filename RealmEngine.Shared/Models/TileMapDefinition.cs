namespace RealmEngine.Shared.Models;

/// <summary>A tile that transitions the player to another zone when stepped on.</summary>
public class ExitTileDefinition
{
    /// <summary>Column of the exit tile.</summary>
    public int TileX { get; set; }

    /// <summary>Row of the exit tile.</summary>
    public int TileY { get; set; }

    /// <summary>Destination zone identifier.</summary>
    public string ToZoneId { get; set; } = string.Empty;
}

/// <summary>A default spawn position for players entering the zone for the first time.</summary>
public class SpawnPointDefinition
{
    /// <summary>Column of the spawn tile.</summary>
    public int TileX { get; set; }

    /// <summary>Row of the spawn tile.</summary>
    public int TileY { get; set; }
}

/// <summary>
/// A zone entry object placed on a region map's <c>zones</c> object group.
/// Stepping onto this tile triggers the zone entry prompt.
/// </summary>
public class ZoneObjectDefinition
{
    /// <summary>Slug of the zone this object leads into.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>Human-readable display name shown in the entry dialog.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Minimum recommended character level, or <see langword="null"/> if unrestricted.</summary>
    public int? MinLevel { get; set; }

    /// <summary>Maximum recommended character level, or <see langword="null"/> if unrestricted.</summary>
    public int? MaxLevel { get; set; }

    /// <summary>Tile column of the entry object.</summary>
    public int TileX { get; set; }

    /// <summary>Tile row of the entry object.</summary>
    public int TileY { get; set; }
}

/// <summary>
/// A region exit object placed on a region map's <c>region_exits</c> object group.
/// Stepping onto this tile triggers the region travel prompt.
/// </summary>
public class RegionExitDefinition
{
    /// <summary>Slug of the adjacent region this exit leads to.</summary>
    public string ToRegionId { get; set; } = string.Empty;

    /// <summary>Tile column of the exit object.</summary>
    public int TileX { get; set; }

    /// <summary>Tile row of the exit object.</summary>
    public int TileY { get; set; }
}
