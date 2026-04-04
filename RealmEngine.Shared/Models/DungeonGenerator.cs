using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Procedural dungeon map generator using Binary Space Partitioning (BSP).
/// Produces a <see cref="TileMapDefinition"/> suitable for a dungeon zone.
/// </summary>
public static class DungeonGenerator
{
    // Tile indices within the roguelike_base spritesheet (57 columns × 31 rows, 16 px, 1 px spacing).
    // Layer 0 "base"    — Stone.Floor (7) everywhere (opaque ground visible under all cells).
    // Layer 1 "objects" — Stone.M (920) for wall cells; -1 (transparent) for carved floor cells.
    private const int TileWall  = TileIndex.Terrain.Stone.M;     // 920 — solid centre stone
    private const int TileFloor = TileIndex.Terrain.Stone.Floor; // 7   — lighter dungeon floor

    private const int MinRoomW  = 5;
    private const int MinRoomH  = 5;
    private const int MaxRoomW  = 10;
    private const int MaxRoomH  = 8;
    private const int MinRooms  = 6;
    private const int MaxRooms  = 10;

    // Dungeon dimensions scale with depth so deeper levels feel larger and more complex.
    // Level 1: 40×30  Level 2: 50×38  Level 3: 60×45  Level 4+: 70×50 (capped)
    private static (int Width, int Height) DimensionsForLevel(int level) => level switch
    {
        1 => (40, 30),
        2 => (50, 38),
        3 => (60, 45),
        _ => (70, 50),
    };

    /// <summary>
    /// Generates a deterministic dungeon <see cref="TileMapDefinition"/> for the given zone identifier.
    /// The same <paramref name="zoneId"/> always produces the same layout.
    /// Map dimensions scale with dungeon depth: level 1 = 40×30, level 2 = 50×38, level 3 = 60×45, level 4+ = 70×50.
    /// </summary>
    /// <param name="zoneId">Dungeon zone identifier (e.g. <c>"dungeon-1"</c>).</param>
    /// <param name="seed">
    /// Optional explicit seed. When 0 (default) the seed is derived from <paramref name="zoneId"/>'s hash code,
    /// ensuring deterministic but zone-unique layouts.
    /// </param>
    /// <returns>A fully-formed <see cref="TileMapDefinition"/> with rooms, corridors, spawn points, and one exit tile.</returns>
    public static TileMapDefinition Generate(string zoneId, int seed = 0)
    {
        var level = ParseLevel(zoneId);
        var (mapW, mapH) = DimensionsForLevel(level);

        var rng = new Random(seed == 0 ? zoneId.GetHashCode() : seed);

        // Build flat ground layer (all walls) — used for carving, then split into 2 layers
        var groundData = new int[mapW * mapH];
        Array.Fill(groundData, TileWall);

        var collisionMask = new bool[mapW * mapH];
        Array.Fill(collisionMask, true); // everything starts blocked

        var fogMask = new bool[mapW * mapH];
        Array.Fill(fogMask, true); // dungeons start fully hidden

        // Generate rooms via BSP partitioning
        var rooms = GenerateRooms(rng, mapW, mapH);

        // Carve floor tiles for each room
        foreach (var room in rooms)
            CarveRoom(room, groundData, collisionMask, mapW);

        // Connect rooms with L-shaped corridors
        for (var i = 0; i < rooms.Count - 1; i++)
            CarveCorridorBetween(rooms[i], rooms[i + 1], groundData, collisionMask, rng, mapW, mapH);

        // Build 2-layer data from the carved groundData:
        //   Layer 0 "base"    — Stone.Floor everywhere (opaque, never -1)
        //   Layer 1 "objects" — Stone.M where walls remain; -1 where floor was carved
        var baseData    = new int[mapW * mapH];
        var objectsData = new int[mapW * mapH];
        Array.Fill(baseData, TileFloor);
        for (var i = 0; i < groundData.Length; i++)
            objectsData[i] = groundData[i] == TileWall ? TileWall : -1;

        // Spawn points: two positions in the first room's centre
        var firstRoom = rooms[0];
        var spawnX    = firstRoom.X + firstRoom.W / 2;
        var spawnY    = firstRoom.Y + firstRoom.H / 2;
        var spawnPoints = new List<SpawnPointDefinition>
        {
            new() { TileX = spawnX,     TileY = spawnY },
            new() { TileX = spawnX + 1, TileY = spawnY },
        };

        // Exit tile: floor tile on the border of the last room
        var lastRoom = rooms[^1];
        var exitTile = new ExitTileDefinition
        {
            TileX    = lastRoom.X + lastRoom.W / 2,
            TileY    = lastRoom.Y + lastRoom.H - 1,
            ToZoneId = DeeperZoneId(zoneId),
        };

        return new TileMapDefinition
        {
            ZoneId        = zoneId,
            TilesetKey    = "roguelike_base",
            Width         = mapW,
            Height        = mapH,
            TileSize      = 16,
            Layers        =
            [
                new TileLayerDefinition { Name = "base",    Data = baseData    },
                new TileLayerDefinition { Name = "objects", Data = objectsData },
            ],
            CollisionMask = collisionMask,
            FogMask       = fogMask,
            SpawnPoints   = spawnPoints,
            ExitTiles     = [exitTile],
        };
    }

    // ── Room generation ───────────────────────────────────────────────────────

    private static List<Rect> GenerateRooms(Random rng, int mapW, int mapH)
    {
        var count = rng.Next(MinRooms, MaxRooms + 1);
        var rooms = new List<Rect>(count);
        const int maxAttempts = 200;

        for (var i = 0; i < count; i++)
        {
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var w = rng.Next(MinRoomW, MaxRoomW + 1);
                var h = rng.Next(MinRoomH, MaxRoomH + 1);
                var x = rng.Next(1, mapW - w - 1);
                var y = rng.Next(1, mapH - h - 1);
                var candidate = new Rect(x, y, w, h);

                if (!rooms.Any(r => r.Overlaps(candidate, padding: 1)))
                {
                    rooms.Add(candidate);
                    break;
                }
            }
        }

        return rooms;
    }

    private static void CarveRoom(Rect room, int[] ground, bool[] collision, int mapW)
    {
        for (var y = room.Y; y < room.Y + room.H; y++)
        for (var x = room.X; x < room.X + room.W; x++)
        {
            var idx = y * mapW + x;
            ground[idx]    = TileFloor;
            collision[idx] = false;
        }
    }

    private static void CarveCorridorBetween(Rect a, Rect b, int[] ground, bool[] collision, Random rng, int mapW, int mapH)
    {
        // Centre points
        var (ax, ay) = (a.X + a.W / 2, a.Y + a.H / 2);
        var (bx, by) = (b.X + b.W / 2, b.Y + b.H / 2);

        // Randomly choose horizontal-first or vertical-first
        if (rng.Next(2) == 0)
        {
            CarveHorizontalTunnel(ay, ax, bx, ground, collision, mapW, mapH);
            CarveVerticalTunnel(bx, ay, by, ground, collision, mapW, mapH);
        }
        else
        {
            CarveVerticalTunnel(ax, ay, by, ground, collision, mapW, mapH);
            CarveHorizontalTunnel(by, ax, bx, ground, collision, mapW, mapH);
        }
    }

    private static void CarveHorizontalTunnel(int y, int x1, int x2, int[] ground, bool[] collision, int mapW, int mapH)
    {
        var (minX, maxX) = x1 < x2 ? (x1, x2) : (x2, x1);
        for (var x = minX; x <= maxX; x++)
            CarveTile(x, y, ground, collision, mapW, mapH);
    }

    private static void CarveVerticalTunnel(int x, int y1, int y2, int[] ground, bool[] collision, int mapW, int mapH)
    {
        var (minY, maxY) = y1 < y2 ? (y1, y2) : (y2, y1);
        for (var y = minY; y <= maxY; y++)
            CarveTile(x, y, ground, collision, mapW, mapH);
    }

    private static void CarveTile(int x, int y, int[] ground, bool[] collision, int mapW, int mapH)
    {
        if (x < 0 || x >= mapW || y < 0 || y >= mapH) return;
        var idx = y * mapW + x;
        ground[idx]    = TileFloor;
        collision[idx] = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Parses the numeric level suffix from a dungeon zone ID (e.g. <c>"dungeon-3"</c> → 3). Returns 1 for unrecognised IDs.</summary>
    private static int ParseLevel(string zoneId)
    {
        const string prefix = "dungeon-";
        if (zoneId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(zoneId[prefix.Length..], out var level))
            return level;
        return 1;
    }

    /// <summary>
    /// Derives the next-level dungeon zone ID from the current one.
    /// E.g. <c>"dungeon-1"</c> → <c>"dungeon-2"</c>; unknown format → <c>"dungeon-1"</c>.
    /// </summary>
    private static string DeeperZoneId(string zoneId)
    {
        const string prefix = "dungeon-";
        if (zoneId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(zoneId[prefix.Length..], out var level))
            return $"{prefix}{level + 1}";
        return "dungeon-1";
    }

    private readonly record struct Rect(int X, int Y, int W, int H)
    {
        internal bool Overlaps(Rect other, int padding = 0) =>
            X - padding < other.X + other.W + padding &&
            X + W + padding > other.X - padding &&
            Y - padding < other.Y + other.H + padding &&
            Y + H + padding > other.Y - padding;
    }
}
