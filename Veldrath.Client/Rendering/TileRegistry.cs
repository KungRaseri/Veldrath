using Avalonia.Media;
using RealmEngine.Shared.Models;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Registry mapping tile indices to <see cref="TileDescriptor"/> values.
/// Populated at static-init time from the <see cref="TileIndex"/> constants.
/// Both renderers use this as the single source of truth for tile identity.
/// Replaces the old <see cref="TileAsciiMap"/> class.
/// </summary>
public static class TileRegistry
{
    private static readonly Dictionary<int, TileDescriptor> Descriptors = [];
    private static readonly TileDescriptor Unknown = new(-999, "Unknown", TileCategory.Unknown, '?', AsciiPalette.DebugUnknown, null);

    static TileRegistry()
    {
        RegisterSpecial();
        RegisterGround();
        RegisterTerrain();
        RegisterWater();
        RegisterFlora();
        RegisterPaths();
    }

    /// <summary>
    /// Looks up the <see cref="TileDescriptor"/> for a tile index.
    /// Returns an "Unknown" descriptor for unmapped indices so callers never receive a default struct.
    /// </summary>
    /// <param name="tileIndex">The spritesheet tile index.</param>
    /// <returns>The descriptor for the tile, or an unknown placeholder.</returns>
    public static TileDescriptor Get(int tileIndex) =>
        Descriptors.TryGetValue(tileIndex, out var d) ? d : Unknown;

    private static void Register(int tileIndex, string name, TileCategory category, char asciiChar, Color foreground, Color? background = null) =>
        Descriptors[tileIndex] = new TileDescriptor(tileIndex, name, category, asciiChar, foreground, background);

    // ── Special ────────────────────────────────────────────────────────────────

    private static void RegisterSpecial()
    {
        Register(-1,                  "Empty",    TileCategory.Empty,   ' ',  AsciiPalette.DefaultTileText);
        Register(TileIndex.Blank,     "Blank",    TileCategory.Special, '.',  AsciiPalette.Blank);
        Register(TileIndex.Pending,   "Pending",  TileCategory.Special, '?',  AsciiPalette.DebugUnknown);
    }

    // ── Ground Textures ────────────────────────────────────────────────────────

    private static void RegisterGround()
    {
        Register(TileIndex.Ground.DeadLeaves,    "Dead Leaves",     TileCategory.Ground, ',',  AsciiPalette.GroundDeadLeaves);
        Register(TileIndex.Ground.LightGravel,   "Light Gravel",    TileCategory.Ground, ':',  AsciiPalette.GroundGravel);
        Register(TileIndex.Ground.Cobblestone,   "Cobblestone",     TileCategory.Ground, ';',  AsciiPalette.GroundCobblestone);
        Register(TileIndex.Ground.StoneTile,     "Stone Tile",      TileCategory.Ground, '=',  AsciiPalette.GroundStoneTile);
        Register(TileIndex.Ground.LightFoliage,  "Light Foliage",   TileCategory.Ground, '"',  AsciiPalette.GroundLightFoliage);
        Register(TileIndex.Ground.MedFoliage,    "Medium Foliage",  TileCategory.Ground, '\'', AsciiPalette.GroundMedFoliage);
        Register(TileIndex.Ground.GrassFill,     "Grass Fill",      TileCategory.Ground, '"',  AsciiPalette.GroundGrassFill);
    }

    // ── Terrain ────────────────────────────────────────────────────────────────

    private static void RegisterTerrain()
    {
        // Background colours give terrain tiles a solid cell fill for readability.
        Register(TileIndex.Terrain.Stone.M, "Stone Wall", TileCategory.Terrain, '#', AsciiPalette.TerrainStone, Color.FromRgb(40, 40, 50));
        Register(TileIndex.Terrain.Sand.M,  "Sand",       TileCategory.Terrain, '~', AsciiPalette.TerrainSand,  Color.FromRgb(60, 50, 30));
    }

    // ── Water ──────────────────────────────────────────────────────────────────

    private static void RegisterWater()
    {
        Register(TileIndex.Water.Deep, "Deep Water", TileCategory.Water, '≈', AsciiPalette.WaterDeep, Color.FromRgb(10, 30, 60));
    }

    // ── Flora ──────────────────────────────────────────────────────────────────

    private static void RegisterFlora()
    {
        // Trees
        Register(TileIndex.Flora.TreeA,    "Sparse Tree",     TileCategory.Flora, 'T',  AsciiPalette.FloraTree);
        Register(TileIndex.Flora.TreeB,    "Tree B",          TileCategory.Flora, 'T',  AsciiPalette.FloraTree);
        Register(TileIndex.Flora.Pine,     "Pine Tree",       TileCategory.Flora, '▲', AsciiPalette.FloraTree);
        Register(TileIndex.Flora.TreeC,    "Tree C",          TileCategory.Flora, 'T',  AsciiPalette.FloraTree);
        Register(TileIndex.Flora.TreeD,    "Tree D",          TileCategory.Flora, 'T',  AsciiPalette.FloraTree);
        Register(TileIndex.Flora.TreeE,    "Dense Tree",      TileCategory.Flora, 'T',  AsciiPalette.FloraTree);

        // Cacti
        Register(TileIndex.Flora.Cactus,     "Cactus",        TileCategory.Flora, 'Y', AsciiPalette.FloraCactus);
        Register(TileIndex.Flora.CactusDual, "Dual Cactus",   TileCategory.Flora, 'Y', AsciiPalette.FloraCactus);

        // Ground cover
        Register(TileIndex.Flora.TallGrass,    "Tall Grass",     TileCategory.Flora, 'i', AsciiPalette.FloraGroundCover);
        Register(TileIndex.Flora.Vines,        "Vines",          TileCategory.Flora, 'v', AsciiPalette.FloraGroundCover);
        Register(TileIndex.Flora.ClimbingVine, "Climbing Vine",  TileCategory.Flora, 'v', AsciiPalette.FloraGroundCover);
        Register(TileIndex.Flora.DualPine,     "Dual Pine",      TileCategory.Flora, '▲', AsciiPalette.FloraTree);
        Register(TileIndex.Flora.BigTree,      "Big Tree",       TileCategory.Flora, 'T',  AsciiPalette.FloraTree);
        Register(TileIndex.Flora.Boulder,      "Boulder",        TileCategory.Flora, 'O', AsciiPalette.FloraBoulder);
        Register(TileIndex.Flora.DeadVines,    "Dead Vines",     TileCategory.Flora, 'v', AsciiPalette.FloraDead);
        Register(TileIndex.Flora.Mushroom,     "Mushroom",       TileCategory.Flora, '*', AsciiPalette.FloraMushroom);
    }

    // ── Dirt Paths ─────────────────────────────────────────────────────────────

    private static void RegisterPaths()
    {
        Register(TileIndex.DirtPath.FourWay,      "Path Crossroads",   TileCategory.Path, '╬', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.Circle,       "Path Circle",       TileCategory.Path, 'o', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.StraightH,    "Path Horizontal",   TileCategory.Path, '═', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.StraightV,    "Path Vertical",     TileCategory.Path, '║', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.CornerTL,     "Path Corner NW",    TileCategory.Path, '╝', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.CornerTR,     "Path Corner NE",    TileCategory.Path, '╚', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.CornerBL,     "Path Corner SW",    TileCategory.Path, '╗', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.CornerBR,     "Path Corner SE",    TileCategory.Path, '╔', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.TJunctionLBR, "Path T-Junction N", TileCategory.Path, '╩', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.TJunctionLTR, "Path T-Junction S", TileCategory.Path, '╦', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.TJunctionTBR, "Path T-Junction W", TileCategory.Path, '╣', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.TJunctionLTB, "Path T-Junction E", TileCategory.Path, '╠', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.EndTop,       "Path Dead End N",   TileCategory.Path, '╨', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.EndBottom,    "Path Dead End S",   TileCategory.Path, '╥', AsciiPalette.PathTan);
        Register(TileIndex.DirtPath.EndRight,     "Path Dead End W",   TileCategory.Path, '╡', AsciiPalette.PathTan);
    }
}
