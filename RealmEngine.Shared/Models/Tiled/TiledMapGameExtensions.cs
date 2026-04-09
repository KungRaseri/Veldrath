using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Models.Tiled;

/// <summary>
/// Engine-specific game logic helpers layered on top of a <see cref="TiledMap"/>.
/// </summary>
/// <remarks>
/// These helpers interpret map and tileset data according to the conventions used by RealmEngine:
/// <list type="bullet">
///   <item>Collision is derived from per-tile <c>objectgroup</c> shapes defined in the tileset (any tile that
///     has at least one collision object is considered solid). This is the native Tiled approach —
///     collision shapes are painted once on the tileset and apply automatically to every map.</item>
///   <item>Exit tiles live in an <c>objectgroup</c> layer named <c>"exits"</c>. Each object has
///     <c>type="exit"</c> and a custom string property <c>toZoneId</c>. Pixel coordinates are
///     converted to tile coordinates by dividing by the map's <c>tilewidth</c> / <c>tileheight</c>.</item>
///   <item>Spawn points live in an <c>objectgroup</c> layer named <c>"spawns"</c>. Point objects
///     (or rectangle/any objects) with <c>type="spawn"</c> are used.</item>
///   <item>Fog is controlled by a map-level custom property <c>fogMode</c>: <c>"none"</c> (default,
///     no fog) or <c>"full"</c> (entire map starts hidden).</item>
/// </list>
/// </remarks>
public static class TiledMapGameExtensions
{
    // GID flip-bit mask. The upper 3 bits of a GID encode horizontal/vertical/anti-diagonal flip.
    // Strip them to get the real GID before comparing with tileset tile IDs.
    private const uint GidFlipMask = 0xE000_0000u;

    // ── Collision ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the set of local tile IDs (0-based within the first tileset) that are considered
    /// solid, based on which tiles have one or more collision shapes in their <c>objectgroup</c>.
    /// </summary>
    /// <param name="map">The loaded <see cref="TiledMap"/> with resolved tileset data.</param>
    /// <returns>
    /// A <see cref="HashSet{T}"/> of local tile IDs that block movement.
    /// Returns an empty set when the map has no tilesets.
    /// </returns>
    public static HashSet<int> BuildSolidTileIds(this TiledMap map)
    {
        var solid = new HashSet<int>();
        foreach (var tileset in map.Tilesets)
        {
            foreach (var tile in tileset.Tiles)
            {
                if (tile.ObjectGroup?.Objects is { Count: > 0 })
                    solid.Add(tile.Id);
            }
        }
        return solid;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the tile at (<paramref name="x"/>, <paramref name="y"/>) blocks
    /// movement. Out-of-bounds coordinates always return <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Collision is determined by checking every tile layer at the given cell against the tileset's
    /// per-tile collision shapes. A cell is solid as soon as any layer contains a solid tile.
    /// GID flip bits (horizontal/vertical/anti-diagonal) are stripped before the lookup.
    /// </remarks>
    /// <param name="map">The loaded <see cref="TiledMap"/>.</param>
    /// <param name="x">Tile column (0-based).</param>
    /// <param name="y">Tile row (0-based).</param>
    public static bool IsBlocked(this TiledMap map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return true;

        var solid    = map.BuildSolidTileIds();
        var firstGid = map.Tilesets.Count > 0 ? map.Tilesets[0].FirstGid : 1;

        foreach (var layer in map.Layers)
        {
            if (layer.Type != "tilelayer" || layer.Data is null) continue;

            var idx = y * map.Width + x;
            if (idx >= layer.Data.Count) continue;

            var rawGid  = (uint)layer.Data[idx];
            var gid     = (int)(rawGid & ~GidFlipMask);
            if (gid == 0) continue; // empty tile — not solid

            var localId = gid - firstGid;
            if (solid.Contains(localId)) return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a flat <see cref="bool"/> array of length <c>Width × Height</c> where each index
    /// <c>y * Width + x</c> is <see langword="true"/> when the tile is solid.
    /// </summary>
    public static bool[] GetCollisionMask(this TiledMap map)
    {
        var mask = new bool[map.Width * map.Height];
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width;  x++)
            mask[y * map.Width + x] = map.IsBlocked(x, y);
        return mask;
    }

    // ── Fog ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the fog mode for this map from the <c>fogMode</c> custom property.
    /// <c>"full"</c> means all tiles start hidden; <c>"none"</c> (or absent) means no fog.
    /// </summary>
    public static string GetFogMode(this TiledMap map) =>
        map.Properties
           .Find(p => p.Name == "fogMode")
           ?.AsString() ?? "none";

    /// <summary>
    /// Builds a flat <see cref="bool"/> array of length <c>Width × Height</c> where each element is
    /// <see langword="true"/> when the tile is hidden under fog of war on zone entry.
    /// Controlled by the map-level <c>fogMode</c> custom property: <c>"full"</c> = all hidden,
    /// <c>"none"</c> = all revealed.
    /// </summary>
    public static bool[] GetFogMask(this TiledMap map)
    {
        var fogFull = map.GetFogMode() == "full";
        var mask    = new bool[map.Width * map.Height];
        if (fogFull) Array.Fill(mask, true);
        return mask;
    }

    // ── Exits ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all exit tile definitions from the objectgroup layer named <c>"exits"</c>.
    /// Each object must have <c>type="exit"</c>. The optional custom string property <c>toZoneId</c>
    /// identifies the destination zone; omitting it (or leaving it empty) signals an exit back to
    /// the region map rather than into another zone.
    /// Pixel coordinates are converted to tile coordinates using the map's tile dimensions.
    /// </summary>
    public static IReadOnlyList<ExitTileDefinition> GetExitTiles(this TiledMap map)
    {
        var layer = map.Layers.Find(l =>
            l.Type == "objectgroup" &&
            l.Name.Equals("exits", StringComparison.OrdinalIgnoreCase));

        if (layer is null) return [];

        var exits = new List<ExitTileDefinition>();
        foreach (var obj in layer.Objects)
        {
            if (!obj.Type.Equals("exit", StringComparison.OrdinalIgnoreCase)) continue;

            var toZoneId = obj.Properties
                              .Find(p => p.Name == "toZoneId")
                              ?.AsString() ?? string.Empty;

            exits.Add(new ExitTileDefinition
            {
                TileX    = (int)(obj.X / map.TileWidth),
                TileY    = (int)(obj.Y / map.TileHeight),
                ToZoneId = toZoneId,
            });
        }

        return exits;
    }

    // ── Spawn points ─────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts all spawn point definitions from the objectgroup layer named <c>"spawns"</c>.
    /// Objects should have <c>type="spawn"</c>; any object in that layer is accepted if the type is
    /// absent or empty. Pixel coordinates are converted to tile coordinates.
    /// </summary>
    public static IReadOnlyList<SpawnPointDefinition> GetSpawnPoints(this TiledMap map)
    {
        var layer = map.Layers.Find(l =>
            l.Type == "objectgroup" &&
            l.Name.Equals("spawns", StringComparison.OrdinalIgnoreCase));

        if (layer is null) return [];

        var spawns = new List<SpawnPointDefinition>();
        foreach (var obj in layer.Objects)
        {
            // Accept objects without a type too — if you put something in "spawns" it's a spawn.
            if (!string.IsNullOrEmpty(obj.Type) &&
                !obj.Type.Equals("spawn", StringComparison.OrdinalIgnoreCase))
                continue;

            spawns.Add(new SpawnPointDefinition
            {
                TileX = (int)(obj.X / map.TileWidth),
                TileY = (int)(obj.Y / map.TileHeight),
            });
        }

        return spawns;
    }

    // ── Property helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the value of a named string property on the map's custom properties list,
    /// or <paramref name="defaultValue"/> when the property is absent or not a string.
    /// </summary>
    public static string? GetStringProperty(this TiledMap map, string name, string? defaultValue = null) =>
        map.Properties.Find(p => p.Name == name)?.AsString(defaultValue) ?? defaultValue;

    /// <summary>
    /// Returns the value of a named integer property on the map's custom properties list,
    /// or <paramref name="defaultValue"/> when the property is absent or not an integer.
    /// </summary>
    public static int GetIntProperty(this TiledMap map, string name, int defaultValue = 0) =>
        map.Properties.Find(p => p.Name == name)?.AsInt(defaultValue) ?? defaultValue;

    /// <summary>
    /// Returns the <c>zoneId</c> custom property, used to identify which engine zone this map belongs to
    /// (e.g. <c>"fenwick-crossing"</c>). Returns an empty string when absent.
    /// </summary>
    public static string GetZoneId(this TiledMap map) =>
        map.GetStringProperty("zoneId") ?? string.Empty;

    /// <summary>
    /// Returns the <c>tilesetKey</c> custom property used to look up the spritesheet in
    /// <c>TilemapAssets.All</c> (e.g. <c>"onebit_packed"</c>). Returns an empty string when absent.
    /// </summary>
    public static string GetTilesetKey(this TiledMap map) =>
        map.GetStringProperty("tilesetKey") ?? string.Empty;

    /// <summary>
    /// Returns the <c>regionId</c> custom property, used to identify which region this map belongs to
    /// (e.g. <c>"thornveil"</c>). Returns an empty string when absent.
    /// </summary>
    /// <returns>The region identifier, or an empty string if the <c>regionId</c> property is absent.</returns>
    public static string GetRegionId(this TiledMap map) =>
        map.GetStringProperty("regionId") ?? string.Empty;

    // ── Region map: zone entries ─────────────────────────────────────────────

    /// <summary>
    /// Extracts all zone-entry definitions from the objectgroup layer named <c>"zones"</c>.
    /// Each object's <c>name</c> attribute is the zone slug.
    /// Optional custom properties: <c>displayName</c> (string), <c>minLevel</c> (int), <c>maxLevel</c> (int).
    /// Pixel coordinates are converted to tile coordinates using the map's tile dimensions.
    /// </summary>
    /// <returns>A read-only list of zone-entry definitions. Returns an empty list if the <c>zones</c> objectgroup layer is absent.</returns>
    public static IReadOnlyList<ZoneObjectDefinition> GetZoneEntries(this TiledMap map)
    {
        var layer = map.Layers.Find(l =>
            l.Type == "objectgroup" &&
            l.Name.Equals("zones", StringComparison.OrdinalIgnoreCase));

        if (layer is null) return [];

        var entries = new List<ZoneObjectDefinition>();
        foreach (var obj in layer.Objects)
        {
            if (string.IsNullOrEmpty(obj.Name)) continue;

            entries.Add(new ZoneObjectDefinition
            {
                TileX       = (int)(obj.X / map.TileWidth),
                TileY       = (int)(obj.Y / map.TileHeight),
                ZoneSlug    = obj.Name,
                DisplayName = obj.Properties.Find(p => p.Name == "displayName")?.AsString() ?? obj.Name,
                MinLevel    = obj.Properties.Find(p => p.Name == "minLevel")?.AsInt(0)      ?? 0,
                MaxLevel    = obj.Properties.Find(p => p.Name == "maxLevel")?.AsInt(0)      ?? 0,
            });
        }

        return entries;
    }

    // ── Region map: region exits ─────────────────────────────────────────────

    /// <summary>
    /// Extracts all region-exit definitions from the objectgroup layer named <c>"region_exits"</c>.
    /// Each object's <c>name</c> attribute is the target region slug.
    /// Pixel coordinates are converted to tile coordinates using the map's tile dimensions.
    /// </summary>
    /// <returns>A read-only list of region-exit definitions. Returns an empty list if the <c>region_exits</c> objectgroup layer is absent.</returns>
    public static IReadOnlyList<RegionExitDefinition> GetRegionExits(this TiledMap map)
    {
        var layer = map.Layers.Find(l =>
            l.Type == "objectgroup" &&
            l.Name.Equals("region_exits", StringComparison.OrdinalIgnoreCase));

        if (layer is null) return [];

        var exits = new List<RegionExitDefinition>();
        foreach (var obj in layer.Objects)
        {
            if (string.IsNullOrEmpty(obj.Name)) continue;

            exits.Add(new RegionExitDefinition
            {
                TileX          = (int)(obj.X / map.TileWidth),
                TileY          = (int)(obj.Y / map.TileHeight),
                TargetRegionId = obj.Name,
            });
        }

        return exits;
    }

    // ── Region map: zone labels ────────────────────────────────────────────────

    /// <summary>
    /// Extracts all zone-label definitions from the objectgroup layer named <c>"labels"</c>.
    /// Point objects only. The object's <c>name</c> is the display text; the optional
    /// <c>zoneSlug</c> custom property links the label to a zone entry.
    /// </summary>
    /// <returns>A read-only list of label definitions. Returns an empty list if the <c>labels</c> objectgroup layer is absent.</returns>
    public static IReadOnlyList<ZoneLabelDefinition> GetZoneLabels(this TiledMap map)
    {
        var layer = map.Layers.Find(l =>
            l.Type == "objectgroup" &&
            l.Name.Equals("labels", StringComparison.OrdinalIgnoreCase));

        if (layer is null) return [];

        var labels = new List<ZoneLabelDefinition>();
        foreach (var obj in layer.Objects)
        {
            if (obj.Point != true || string.IsNullOrEmpty(obj.Name)) continue;

            labels.Add(new ZoneLabelDefinition
            {
                TileX    = (int)(obj.X / map.TileWidth),
                TileY    = (int)(obj.Y / map.TileHeight),
                Text     = obj.Name,
                ZoneSlug  = obj.Properties.Find(p => p.Name == "zoneSlug")?.AsString() ?? string.Empty,
                IsHidden  = obj.Properties.Find(p => p.Name == "isHidden")?.AsBool() ?? false,
            });
        }

        return labels;
    }

    // ── Region map: paths ───────────────────────────────────────────────────

    /// <summary>
    /// Extracts all path definitions from the objectgroup layer named <c>"paths"</c>.
    /// Polyline objects only. Points are converted from pixel coordinates to tile coordinates.
    /// The polyline points are relative to the object origin; this method returns absolute tile positions.
    /// </summary>
    /// <returns>A read-only list of path definitions. Returns an empty list if the <c>paths</c> objectgroup layer is absent.</returns>
    public static IReadOnlyList<RegionPathDefinition> GetRegionPaths(this TiledMap map)
    {
        var layer = map.Layers.Find(l =>
            l.Type == "objectgroup" &&
            l.Name.Equals("paths", StringComparison.OrdinalIgnoreCase));

        if (layer is null) return [];

        var paths = new List<RegionPathDefinition>();
        foreach (var obj in layer.Objects)
        {
            if (obj.Polyline is null || obj.Polyline.Count == 0) continue;

            var points = obj.Polyline
                .Select(p => new RegionPathPoint(
                    TileX: (float)((obj.X + p.X) / map.TileWidth),
                    TileY: (float)((obj.Y + p.Y) / map.TileHeight)))
                .ToList();

            paths.Add(new RegionPathDefinition { Name = obj.Name, Points = points });
        }

        return paths;
    }

    /// <summary>
    /// Returns the <see cref="TiledTileset.FirstGid"/> of the first tileset in the map,
    /// which is 1 for all standard single-tileset maps. Returns 1 when no tilesets are present.
    /// </summary>
    public static int GetFirstGid(this TiledMap map) =>
        map.Tilesets.Count > 0 ? map.Tilesets[0].FirstGid : 1;

    // ── Layer data conversion ─────────────────────────────────────────────────

    /// <summary>
    /// Converts a Tiled GID data array to an engine tile-index array:
    /// <list type="bullet">
    ///   <item>GID 0 → <c>-1</c> (transparent/empty cell).</item>
    ///   <item>GID N → <c>N − firstGid</c> (0-based spritesheet index used by the client renderer).</item>
    ///   <item>GID flip bits are stripped before conversion.</item>
    /// </list>
    /// </summary>
    /// <param name="gidData">Raw GID data from <see cref="TiledLayer.Data"/>.</param>
    /// <param name="firstGid">
    /// The <see cref="TiledTileset.FirstGid"/> of the tileset that owns these tiles.
    /// Pass <c>1</c> for the standard single-tileset case.
    /// </param>
    public static int[] ToEngineLayerData(IReadOnlyList<int> gidData, int firstGid)
    {
        var result = new int[gidData.Count];
        for (var i = 0; i < gidData.Count; i++)
        {
            var raw = (uint)gidData[i];
            var gid = (int)(raw & ~GidFlipMask);
            result[i] = gid == 0 ? -1 : gid - firstGid;
        }
        return result;
    }

}
