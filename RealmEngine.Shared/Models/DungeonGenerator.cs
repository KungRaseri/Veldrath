using System.Text.Json;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Procedural dungeon map generator using Binary Space Partitioning (BSP).
/// Produces a <see cref="TiledMap"/> suitable for a dungeon zone.
/// </summary>
public static class DungeonGenerator
{
    // Tile indices within the onebit_packed spritesheet (49 columns × 22 rows, 16 px, 0 px spacing).
    // Layer 0 "base"    — Stone.Floor (7) everywhere (opaque ground visible under all cells).
    // Layer 1 "objects" — Stone.M (920) for wall cells; -1 (transparent) for carved floor cells.
    private const int TileWall  = TileIndex.Terrain.Stone.M;     // 920 — solid centre stone
    private const int TileFloor = TileIndex.Terrain.Stone.Floor; // 7   — lighter dungeon floor

    // GIDs are 1-based: GID = engine index + 1
    private const int Gid0     = 0;                // empty / transparent
    private const int GidWall  = TileWall  + 1;   // 921
    private const int GidFloor = TileFloor + 1;   //   8

    private const int FirstGid  = 1;
    private const int TileSize  = 16;

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
    /// Generates a deterministic dungeon <see cref="TiledMap"/> for the given zone identifier.
    /// The same <paramref name="zoneId"/> always produces the same layout.
    /// Map dimensions scale with dungeon depth: level 1 = 40×30, level 2 = 50×38, level 3 = 60×45, level 4+ = 70×50.
    /// The returned map contains an inline tileset with collision shapes for the wall tile so that
    /// <see cref="TiledMapGameExtensions.IsBlocked"/> works without loading any external file.
    /// </summary>
    /// <param name="zoneId">Dungeon zone identifier (e.g. <c>"dungeon-1"</c>).</param>
    /// <param name="seed">
    /// Optional explicit seed. When 0 (default) the seed is derived from <paramref name="zoneId"/>'s hash code,
    /// ensuring deterministic but zone-unique layouts.
    /// </param>
    /// <returns>A fully-formed <see cref="TiledMap"/> with rooms, corridors, spawn points, and one exit tile.</returns>
    public static TiledMap Generate(string zoneId, int seed = 0)
    {
        var level = ParseLevel(zoneId);
        var (mapW, mapH) = DimensionsForLevel(level);

        var rng = new Random(seed == 0 ? zoneId.GetHashCode() : seed);

        // Build flat GID arrays: start with all wall GIDs
        var baseGids    = new int[mapW * mapH];
        var objectsGids = new int[mapW * mapH];

        Array.Fill(baseGids, GidFloor); // base layer is always floor
        Array.Fill(objectsGids, GidWall); // objects layer starts all-wall; carved = Gid0

        // Generate rooms via BSP partitioning
        var rooms = GenerateRooms(rng, mapW, mapH);

        // Carve floor tiles for each room
        foreach (var room in rooms)
            CarveRoom(room, objectsGids, mapW);

        // Connect rooms with L-shaped corridors
        for (var i = 0; i < rooms.Count - 1; i++)
            CarveCorridorBetween(rooms[i], rooms[i + 1], objectsGids, rng, mapW, mapH);

        // Spawn points: two positions in the first room's centre
        var firstRoom = rooms[0];
        var spawnX    = firstRoom.X + firstRoom.W / 2;
        var spawnY    = firstRoom.Y + firstRoom.H / 2;

        // Exit tile: floor tile on the border of the last room
        var lastRoom = rooms[^1];
        var exitX    = lastRoom.X + lastRoom.W / 2;
        var exitY    = lastRoom.Y + lastRoom.H - 1;

        return new TiledMap
        {
            Width      = mapW,
            Height     = mapH,
            TileWidth  = TileSize,
            TileHeight = TileSize,
            Properties =
            [
                MakeStringProp("zoneId",      zoneId),
                MakeStringProp("tilesetKey",  "onebit_packed"),
                MakeStringProp("fogMode",     "full"),
            ],
            Tilesets = [BuildInlineTileset()],
            Layers   =
            [
                new TiledLayer
                {
                    Id     = 1,
                    Type   = "tilelayer",
                    Name   = "base",
                    Width  = mapW,
                    Height = mapH,
                    Data   = [..baseGids],
                },
                new TiledLayer
                {
                    Id     = 2,
                    Type   = "tilelayer",
                    Name   = "objects",
                    Width  = mapW,
                    Height = mapH,
                    Data   = [..objectsGids],
                },
                new TiledLayer
                {
                    Id        = 3,
                    Type      = "objectgroup",
                    Name      = "spawns",
                    DrawOrder = "topdown",
                    Objects   =
                    [
                        new TiledObject { Id = 1, Type = "spawn", X = spawnX * TileSize, Y = spawnY * TileSize, Point = true },
                        new TiledObject { Id = 2, Type = "spawn", X = (spawnX + 1) * TileSize, Y = spawnY * TileSize, Point = true },
                    ],
                },
                new TiledLayer
                {
                    Id        = 4,
                    Type      = "objectgroup",
                    Name      = "exits",
                    DrawOrder = "topdown",
                    Objects   =
                    [
                        new TiledObject
                        {
                            Id     = 3,
                            Type   = "exit",
                            X      = exitX * TileSize,
                            Y      = exitY * TileSize,
                            Width  = TileSize,
                            Height = TileSize,
                            Properties = [MakeStringProp("toZoneId", DeeperZoneId(zoneId))],
                        },
                    ],
                },
            ],
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

    private static void CarveRoom(Rect room, int[] objectsGids, int mapW)
    {
        for (var y = room.Y; y < room.Y + room.H; y++)
        for (var x = room.X; x < room.X + room.W; x++)
            objectsGids[y * mapW + x] = Gid0;
    }

    private static void CarveCorridorBetween(Rect a, Rect b, int[] objectsGids, Random rng, int mapW, int mapH)
    {
        var (ax, ay) = (a.X + a.W / 2, a.Y + a.H / 2);
        var (bx, by) = (b.X + b.W / 2, b.Y + b.H / 2);

        if (rng.Next(2) == 0)
        {
            CarveHorizontalTunnel(ay, ax, bx, objectsGids, mapW, mapH);
            CarveVerticalTunnel(bx, ay, by, objectsGids, mapW, mapH);
        }
        else
        {
            CarveVerticalTunnel(ax, ay, by, objectsGids, mapW, mapH);
            CarveHorizontalTunnel(by, ax, bx, objectsGids, mapW, mapH);
        }
    }

    private static void CarveHorizontalTunnel(int y, int x1, int x2, int[] objectsGids, int mapW, int mapH)
    {
        var (minX, maxX) = x1 < x2 ? (x1, x2) : (x2, x1);
        for (var x = minX; x <= maxX; x++)
            CarveTile(x, y, objectsGids, mapW, mapH);
    }

    private static void CarveVerticalTunnel(int x, int y1, int y2, int[] objectsGids, int mapW, int mapH)
    {
        var (minY, maxY) = y1 < y2 ? (y1, y2) : (y2, y1);
        for (var y = minY; y <= maxY; y++)
            CarveTile(x, y, objectsGids, mapW, mapH);
    }

    private static void CarveTile(int x, int y, int[] objectsGids, int mapW, int mapH)
    {
        if (x < 0 || x >= mapW || y < 0 || y >= mapH) return;
        objectsGids[y * mapW + x] = Gid0;
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

    // Builds a minimal inline TiledTileset that includes only the wall tile's collision shape.
    // Engine collision detection (BuildSolidTileIds) only needs tiles with non-empty ObjectGroups.
    private static TiledTileset BuildInlineTileset() => new()
    {
        FirstGid   = FirstGid,
        Name       = "onebit",
        TileWidth  = TileSize,
        TileHeight = TileSize,
        Spacing    = 2,
        Columns    = 49,
        TileCount  = 1078,
        Image      = "../sheets/onebit_packed.png",
        ImageWidth  = 800,
        ImageHeight = 368,
        Tiles =
        [
            // Wall tile — has a full-tile collision box → marked solid by BuildSolidTileIds
            new TiledTileDefinition
            {
                Id          = TileWall,  // local ID 920 → GID 921
                ObjectGroup = new TiledLayer
                {
                    Type      = "objectgroup",
                    Name      = "collision",
                    DrawOrder = "index",
                    Objects   = [new TiledObject { Id = 1, X = 0, Y = 0, Width = TileSize, Height = TileSize }],
                },
            },
        ],
    };

    private static TiledProperty MakeStringProp(string name, string value) => new()
    {
        Name  = name,
        Type  = "string",
        Value = JsonSerializer.SerializeToElement(value),
    };

    private readonly record struct Rect(int X, int Y, int W, int H)
    {
        internal bool Overlaps(Rect other, int padding = 0) =>
            X - padding < other.X + other.W + padding &&
            X + W + padding > other.X - padding &&
            Y - padding < other.Y + other.H + padding &&
            Y + H + padding > other.Y - padding;
    }
}
