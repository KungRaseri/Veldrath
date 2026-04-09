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

/// <summary>A zone-entry point on a region map, placed in the <c>zones</c> objectgroup layer.</summary>
public class ZoneObjectDefinition
{
    /// <summary>Tile column of the zone-entry object centre.</summary>
    public int TileX { get; set; }

    /// <summary>Tile row of the zone-entry object centre.</summary>
    public int TileY { get; set; }

    /// <summary>Slug of the zone this entry leads to (e.g. <c>"fenwick-crossing"</c>).</summary>
    public string ZoneSlug { get; set; } = string.Empty;

    /// <summary>Human-readable display name for the zone (from the <c>displayName</c> object property).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Minimum character level suggested for this zone (from the <c>minLevel</c> object property). 0 when unset.</summary>
    public int MinLevel { get; set; }

    /// <summary>Maximum character level suggested for this zone (from the <c>maxLevel</c> object property). 0 when unset.</summary>
    public int MaxLevel { get; set; }
}

/// <summary>A border crossing on a region map that leads to an adjacent region, placed in the <c>region_exits</c> objectgroup layer.</summary>
public class RegionExitDefinition
{
    /// <summary>Tile column of the region-exit object centre.</summary>
    public int TileX { get; set; }

    /// <summary>Tile row of the region-exit object centre.</summary>
    public int TileY { get; set; }

    /// <summary>Slug of the target region this crossing leads to (e.g. <c>"greymoor"</c>).</summary>
    public string TargetRegionId { get; set; } = string.Empty;
}

/// <summary>A label overlay on a region map, placed in the <c>labels</c> objectgroup layer.</summary>
public class ZoneLabelDefinition
{
    /// <summary>Tile column of the label anchor point.</summary>
    public int TileX { get; set; }

    /// <summary>Tile row of the label anchor point.</summary>
    public int TileY { get; set; }

    /// <summary>Display text for the label.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Zone slug this label refers to. Empty for region-exit labels.</summary>
    public string ZoneSlug { get; set; } = string.Empty;
}

/// <summary>A road or path on a region map, placed in the <c>paths</c> objectgroup layer.</summary>
public class RegionPathDefinition
{
    /// <summary>Unique name of the path.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ordered tile-space points that make up the polyline.</summary>
    public IReadOnlyList<RegionPathPoint> Points { get; set; } = [];
}

/// <summary>A tile-space point on a <see cref="RegionPathDefinition"/>.</summary>
/// <param name="TileX">Tile column.</param>
/// <param name="TileY">Tile row.</param>
public record RegionPathPoint(float TileX, float TileY);
