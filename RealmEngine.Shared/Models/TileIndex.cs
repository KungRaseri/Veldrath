namespace RealmEngine.Shared.Models;

// ── Tile Index Constants ───────────────────────────────────────────────────
//
// All indices reference the roguelike_base spritesheet (57 columns × 31 rows,
// 16 px tiles, 1 px spacing).  Formula: index = row * 57 + col.
//
// Layer convention:
//   Layer 0 "base"    — opaque terrain fill.  Use Terrain.*.M or Terrain.*.Fill1.  Never -1.
//   Layer 1 "objects" — transparent decorations (flora, paths, props).  Use -1 for empty cells.

/// <summary>
/// Compile-time tile index constants for the <c>roguelike_base</c> spritesheet
/// (Kenney Roguelike Base Pack — 57 columns × 31 rows, 16 px tiles, 1 px spacing).
/// </summary>
/// <remarks>
/// Index formula: <c>index = row * 57 + col</c>.
/// <para>
/// All object/decoration tiles have transparent backgrounds and belong on
/// <em>Layer 1 "objects"</em>.  Opaque terrain fills belong on <em>Layer 0 "base"</em>.
/// </para>
/// </remarks>
public static class TileIndex
{
    // ── Blank ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Confirmed blank / empty tile. Use <c>-1</c> on object layer for transparency;
    /// use this constant where an explicit opaque-blank is needed.
    /// </summary>
    public const int Blank = 285;

    // ── Terrain ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Opaque terrain fill and 9-patch border tiles.
    /// The <c>M</c> (middle-centre) constant is the canonical solid base-layer fill.
    /// Border tiles (TL/T/TR/ML/MR/BL/B/BR) are used where two terrain types meet.
    /// </summary>
    public static class Terrain
    {
        /// <summary>
        /// Green grass terrain.
        /// Use <see cref="M"/> (915) or <see cref="Fill1"/> (5) for the base layer.
        /// </summary>
        public static class Grass
        {
            /// <summary>Flat grass fill variant 1 — lighter tone.</summary>
            public const int Fill1 = 5;

            /// <summary>Flat grass fill variant 2 — slightly darker tone.</summary>
            public const int Fill2 = 62;

            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int TL = 857;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int T  = 858;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int TR = 859;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int ML = 914;
            /// <summary>Solid grass centre — canonical base-layer fill for grassy zones.</summary>
            public const int M  = 915;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int MR = 916;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int BL = 971;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int B  = 972;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int BR = 973;
        }

        /// <summary>
        /// Grey stone / dungeon floor terrain.
        /// Use <see cref="M"/> (920) for the base layer in stone-floored zones.
        /// </summary>
        public static class Stone
        {
            /// <summary>Plain stone floor.</summary>
            public const int Floor    = 7;

            /// <summary>Plain stone floor — alternate shade.</summary>
            public const int FloorAlt = 64;

            /// <summary>Cobblestone floor.</summary>
            public const int Cobble   = 9;

            /// <summary>Tiled stone floor.</summary>
            public const int Tiled    = 120;

            /// <summary>Tiled stone floor — alternate shade.</summary>
            public const int TiledAlt = 177;

            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int TL = 862;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int T  = 863;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int TR = 864;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int ML = 919;
            /// <summary>Solid stone centre — canonical base-layer fill for stone/dungeon zones.</summary>
            public const int M  = 920;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int MR = 921;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int BL = 976;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int B  = 977;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int BR = 978;
        }

        /// <summary>
        /// Sandy / coastal terrain.
        /// Use <see cref="M"/> (1262) for the base layer in desert or coastal zones.
        /// </summary>
        public static class Sand
        {
            /// <summary>Flat sand fill variant 1.</summary>
            public const int Fill1 = 8;

            /// <summary>Flat sand fill variant 2 — alternate shade.</summary>
            public const int Fill2 = 65;

            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int TL = 1204;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int T  = 1205;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int TR = 1206;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int ML = 1261;
            /// <summary>Solid sand centre — canonical base-layer fill for sandy/coastal zones.</summary>
            public const int M  = 1262;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int MR = 1263;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int BL = 1318;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int B  = 1319;
            /// <inheritdoc cref="TileIndex" path="//summary"/>
            public const int BR = 1320;
        }
    }

    // ── Flora ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plant and tree object tiles. Transparent background — objects layer only.
    /// </summary>
    public static class Flora
    {
        /// <summary>Round oak-style tree variants.</summary>
        public static class OakTree
        {
            /// <summary>Light green oak tree.</summary>
            public const int LightGreen = 526;

            /// <summary>Autumn orange oak tree.</summary>
            public const int Orange     = 527;

            /// <summary>Dark green oak tree.</summary>
            public const int DarkGreen  = 528;
        }

        /// <summary>Tall pine / conifer tree variants.</summary>
        public static class PineTree
        {
            /// <summary>Light green pine tree.</summary>
            public const int LightGreen = 529;

            /// <summary>Autumn orange pine tree.</summary>
            public const int Orange     = 530;

            /// <summary>Dark green pine tree.</summary>
            public const int DarkGreen  = 531;
        }

        /// <summary>Round bush / shrub variants.</summary>
        public static class Bush
        {
            /// <summary>Light green bush.</summary>
            public const int LightGreen = 532;

            /// <summary>Autumn orange bush.</summary>
            public const int Orange     = 533;

            /// <summary>Dark green bush.</summary>
            public const int DarkGreen  = 534;
        }

        /// <summary>Fruit-bearing tree.</summary>
        public const int FruitTree = 536;

        /// <summary>Dead / bare tree (no leaves).</summary>
        public const int DeadTree  = 654;
    }

    // ── Paths ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dirt path tiles for roads and trails. Transparent background — objects layer only.
    /// </summary>
    public static class DirtPath
    {
        /// <summary>4-way cross junction.</summary>
        public const int FourWay = 691;

        /// <summary>Isolated single-tile dirt circle.</summary>
        public const int Circle  = 692;

        // End caps — path terminates in this direction
        /// <summary>Dead-end cap facing left.</summary>
        public const int EndLeft   = 690;
        /// <summary>Dead-end cap facing right.</summary>
        public const int EndRight  = 689;
        /// <summary>Dead-end cap facing up.</summary>
        public const int EndTop    = 633;
        /// <summary>Dead-end cap facing down.</summary>
        public const int EndBottom = 632;

        // T-junctions — open in three directions
        /// <summary>T-junction open Left, Bottom, Right.</summary>
        public const int TJunctionLBR = 404;
        /// <summary>T-junction open Left, Top, Bottom.</summary>
        public const int TJunctionLTB = 405;
        /// <summary>T-junction open Top, Bottom, Right.</summary>
        public const int TJunctionTBR = 461;
        /// <summary>T-junction open Left, Top, Right.</summary>
        public const int TJunctionLTR = 462;

        // Rounded corners — path curves through this tile
        /// <summary>Rounded corner connecting Top and Left.</summary>
        public const int CornerTL = 406;
        /// <summary>Rounded corner connecting Top and Right.</summary>
        public const int CornerTR = 407;
        /// <summary>Rounded corner connecting Bottom and Left.</summary>
        public const int CornerBL = 463;
        /// <summary>Rounded corner connecting Bottom and Right.</summary>
        public const int CornerBR = 464;

        // Straight starters — short straight segment pairs
        /// <summary>Short horizontal path segment (top/bottom entry).</summary>
        public const int StraightH = 408;
        /// <summary>Short vertical path segment (left/right entry).</summary>
        public const int StraightV = 465;
    }

    // ── Props ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decorative prop tiles. Transparent background — objects layer only.
    /// Multi-tile props must be placed as a group using all named positions.
    /// </summary>
    public static class Props
    {
        /// <summary>
        /// Water fountain — a 2-column × 3-row prop.
        /// Place the six tiles as a block: (TL,TR) / (ML,M,MR) / (BL,B,BR).
        /// </summary>
        public static class WaterFountain
        {
            /// <summary>Top-right tile of the fountain.</summary>
            public const int TR = 228;
            /// <summary>Top-left tile of the fountain.</summary>
            public const int TL = 229;
            /// <summary>Middle-left tile of the fountain.</summary>
            public const int ML = 230;
            /// <summary>Middle-centre tile of the fountain.</summary>
            public const int M  = 231;
            /// <summary>Middle-right tile of the fountain.</summary>
            public const int MR = 232;
            /// <summary>Bottom-left tile of the fountain.</summary>
            public const int BL = 287;
            /// <summary>Bottom-centre tile of the fountain.</summary>
            public const int B  = 288;
            /// <summary>Bottom-right tile of the fountain.</summary>
            public const int BR = 289;
        }
    }
}
