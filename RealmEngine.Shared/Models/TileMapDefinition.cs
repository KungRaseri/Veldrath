namespace RealmEngine.Shared.Models;

// ── Tile Map Definition ────────────────────────────────────────────────────

/// <summary>
/// Full authoritative tilemap definition for a zone.
/// Loaded from a JSON asset file or generated procedurally for dungeons.
/// </summary>
public class TileMapDefinition
{
    /// <summary>Zone identifier this map belongs to (e.g. <c>"fenwick-crossing"</c>).</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Tileset key used to look up the spritesheet in <c>TilemapAssets.All</c>.
    /// All zones use <c>"roguelike_base"</c>. Tile index constants are in <see cref="TileIndex"/>.
    /// </summary>
    public string TilesetKey { get; set; } = string.Empty;

    /// <summary>Map width in tiles.</summary>
    public int Width { get; set; }

    /// <summary>Map height in tiles.</summary>
    public int Height { get; set; }

    /// <summary>Native tile size in pixels. All shipped zones use 16.</summary>
    public int TileSize { get; set; } = 16;

    /// <summary>Ordered render layers (ground first, decoration on top).</summary>
    public List<TileLayerDefinition> Layers { get; set; } = [];

    /// <summary>
    /// Flat array of length <c>Width × Height</c>.
    /// <see langword="true"/> at row-major index <c>y * Width + x</c> means the tile is solid.
    /// </summary>
    public bool[] CollisionMask { get; set; } = [];

    /// <summary>
    /// Flat array of length <c>Width × Height</c>.
    /// <see langword="true"/> at row-major index <c>y * Width + x</c> means the tile starts hidden under fog of war.
    /// </summary>
    public bool[] FogMask { get; set; } = [];

    /// <summary>Tiles that trigger a zone transition when stepped on.</summary>
    public List<ExitTileDefinition> ExitTiles { get; set; } = [];

    /// <summary>Default player spawn positions for this map.</summary>
    public List<SpawnPointDefinition> SpawnPoints { get; set; } = [];

    /// <summary>Returns the collision state at the given tile coordinates, or <see langword="true"/> if out of bounds.</summary>
    public bool IsBlocked(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return true;
        return CollisionMask[y * Width + x];
    }

    /// <summary>Returns the fog state at the given tile coordinates.</summary>
    public bool IsInFog(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
        return FogMask[y * Width + x];
    }
}

/// <summary>A single render layer within a <see cref="TileMapDefinition"/>.</summary>
public class TileLayerDefinition
{
    /// <summary>Logical layer name (e.g. <c>"base"</c>, <c>"objects"</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Flat tile-index array of length <c>Width × Height</c>, row-major.
    /// Each value is the spritesheet tile index: <c>row * columns + col</c>. -1 means transparent.
    /// </summary>
    public int[] Data { get; set; } = [];

    /// <summary>
    /// Render z-order for this layer. Layers with <c>ZIndex &lt; 2</c> paint below entities;
    /// layers with <c>ZIndex &gt;= 2</c> paint above entities (roofs, canopies).
    /// Convention: 0 = base terrain, 1 = objects/ground clutter, 2 = roof/canopy.
    /// </summary>
    public int ZIndex { get; set; } = 0;
}

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
