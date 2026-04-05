namespace RealmEngine.Shared.Models;

// -- Tile Index Constants ---------------------------------------------------
//
// All indices reference the roguelike_base spritesheet (57 columns x 31 rows,
// 16 px tiles, 1 px spacing).  Formula: index = row * 57 + col.
//
// Layer convention:
//   Layer 0 "base"    - opaque terrain fill.  Use Terrain.*.M or Fill constants.  Never -1.
//   Layer 1 "objects" - transparent decorations (flora, paths, props).  Use -1 for empty cells.

/// <summary>
/// Compile-time tile index constants for the <c>roguelike_base</c> spritesheet
/// (Kenney Roguelike Base Pack - 57 columns x 31 rows, 16 px tiles, 1 px spacing).
/// </summary>
/// <remarks>
/// Index formula: <c>index = row * 57 + col</c>.
/// <para>
/// All object/decoration tiles have transparent backgrounds and belong on
/// <em>Layer 1 "objects"</em>.  Opaque terrain fills belong on <em>Layer 0 "base"</em>.
/// </para>
/// <para>
/// Unverified constants use <c>-1</c> as a placeholder so they compile but render nothing
/// until filled in from <c>scripts/roguelike_base_labeled.png</c>.
/// </para>
/// </remarks>
public static class TileIndex
{
    // -- Blank ----------------------------------------------------------------

    /// <summary>
    /// Confirmed blank / empty tile. Use <c>-1</c> on object layer for transparency;
    /// use this constant where an explicit opaque-blank is needed.
    /// </summary>
    public const int Blank = 285; // <- verified

    // -------------------------------------------------------------------------
    // TERRAIN  (base layer - always opaque)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opaque terrain fill and 9-patch border tiles.
    /// The <c>M</c> (middle-centre) constant is the canonical solid base-layer fill.
    /// Border tiles (TL/T/TR/ML/MR/BL/B/BR) are used where two terrain types meet.
    /// </summary>
    public static class Terrain
    {
        /// <summary>Green grass terrain. Use <see cref="M"/> for the base layer.</summary>
        public static class Grass
        {
            /// <summary>Flat grass fill variant 1 - lighter tone.</summary>
            public const int Fill1 = 5;   // <- verified
            /// <summary>Flat grass fill variant 2 - slightly darker tone.</summary>
            public const int Fill2 = 62;  // <- verified
            /// <summary>Top-left border tile.</summary>
            public const int TL = 857;    // TODO: needs-verify
            /// <summary>Top-centre border tile.</summary>
            public const int T  = 858;    // TODO: needs-verify
            /// <summary>Top-right border tile.</summary>
            public const int TR = 859;    // TODO: needs-verify
            /// <summary>Middle-left border tile.</summary>
            public const int ML = 914;    // TODO: needs-verify
            /// <summary>Solid grass centre - canonical base-layer fill for grassy zones.</summary>
            public const int M  = 915;    // <- verified
            /// <summary>Middle-right border tile.</summary>
            public const int MR = 916;    // TODO: needs-verify
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = 971;    // TODO: needs-verify
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = 972;    // TODO: needs-verify
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = 973;    // TODO: needs-verify
        }

        /// <summary>Grey stone / dungeon floor terrain. Use <see cref="M"/> for the base layer.</summary>
        public static class Stone
        {
            /// <summary>Plain stone floor.</summary>
            public const int Floor    = 7;   // TODO: needs-verify
            /// <summary>Plain stone floor - alternate shade.</summary>
            public const int FloorAlt = 64;  // TODO: needs-verify
            /// <summary>Cobblestone floor.</summary>
            public const int Cobble   = 9;   // TODO: needs-verify
            /// <summary>Tiled stone floor.</summary>
            public const int Tiled    = 120; // TODO: needs-verify
            /// <summary>Tiled stone floor - alternate shade.</summary>
            public const int TiledAlt = 177; // TODO: needs-verify
            /// <summary>Top-left border tile.</summary>
            public const int TL = 862;  // TODO: needs-verify
            /// <summary>Top-centre border tile.</summary>
            public const int T  = 863;  // TODO: needs-verify
            /// <summary>Top-right border tile.</summary>
            public const int TR = 864;  // TODO: needs-verify
            /// <summary>Middle-left border tile.</summary>
            public const int ML = 919;  // TODO: needs-verify
            /// <summary>Solid stone centre - canonical base-layer fill for stone/dungeon zones.</summary>
            public const int M  = 920;  // <- verified
            /// <summary>Middle-right border tile.</summary>
            public const int MR = 921;  // TODO: needs-verify
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = 976;  // TODO: needs-verify
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = 977;  // TODO: needs-verify
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = 978;  // TODO: needs-verify
        }

        /// <summary>Sandy / coastal terrain. Use <see cref="M"/> for the base layer.</summary>
        public static class Sand
        {
            /// <summary>Flat sand fill variant 1.</summary>
            public const int Fill1 = 8;   // TODO: needs-verify
            /// <summary>Flat sand fill variant 2 - alternate shade.</summary>
            public const int Fill2 = 65;  // TODO: needs-verify
            /// <summary>Top-left border tile.</summary>
            public const int TL = 1204;   // TODO: needs-verify
            /// <summary>Top-centre border tile.</summary>
            public const int T  = 1205;   // TODO: needs-verify
            /// <summary>Top-right border tile.</summary>
            public const int TR = 1206;   // TODO: needs-verify
            /// <summary>Middle-left border tile.</summary>
            public const int ML = 1261;   // TODO: needs-verify
            /// <summary>Solid sand centre - canonical base-layer fill for sandy/coastal zones.</summary>
            public const int M  = 1262;   // <- verified
            /// <summary>Middle-right border tile.</summary>
            public const int MR = 1263;   // TODO: needs-verify
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = 1318;   // TODO: needs-verify
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = 1319;   // TODO: needs-verify
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = 1320;   // TODO: needs-verify
        }

        /// <summary>Bare dirt / earth terrain. Use <see cref="M"/> for the base layer.</summary>
        public static class Dirt
        {
            /// <summary>Flat dirt fill.</summary>
            public const int Fill1 = -1; // TODO: Needs value
            /// <summary>Solid dirt centre - canonical base-layer fill for dirt terrain.</summary>
            public const int M  = -1; // TODO: Needs value
            /// <summary>Top-left border tile.</summary>
            public const int TL = -1; // TODO: Needs value
            /// <summary>Top-centre border tile.</summary>
            public const int T  = -1; // TODO: Needs value
            /// <summary>Top-right border tile.</summary>
            public const int TR = -1; // TODO: Needs value
            /// <summary>Middle-left border tile.</summary>
            public const int ML = -1; // TODO: Needs value
            /// <summary>Middle-right border tile.</summary>
            public const int MR = -1; // TODO: Needs value
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = -1; // TODO: Needs value
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = -1; // TODO: Needs value
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = -1; // TODO: Needs value
        }

        /// <summary>Swamp / muddy terrain. Use <see cref="M"/> for the base layer.</summary>
        public static class Mud
        {
            /// <summary>Flat mud fill.</summary>
            public const int Fill1 = -1; // TODO: Needs value
            /// <summary>Solid mud centre - canonical base-layer fill for swamp/fen zones.</summary>
            public const int M  = -1; // TODO: Needs value
            /// <summary>Top-left border tile.</summary>
            public const int TL = -1; // TODO: Needs value
            /// <summary>Top-centre border tile.</summary>
            public const int T  = -1; // TODO: Needs value
            /// <summary>Top-right border tile.</summary>
            public const int TR = -1; // TODO: Needs value
            /// <summary>Middle-left border tile.</summary>
            public const int ML = -1; // TODO: Needs value
            /// <summary>Middle-right border tile.</summary>
            public const int MR = -1; // TODO: Needs value
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = -1; // TODO: Needs value
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = -1; // TODO: Needs value
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = -1; // TODO: Needs value
        }

        /// <summary>Snow / tundra terrain. Use <see cref="M"/> for the base layer.</summary>
        public static class Snow
        {
            /// <summary>Flat snow fill.</summary>
            public const int Fill1 = -1; // TODO: Needs value
            /// <summary>Solid snow centre - canonical base-layer fill.</summary>
            public const int M  = -1; // TODO: Needs value
            /// <summary>Top-left border tile.</summary>
            public const int TL = -1; // TODO: Needs value
            /// <summary>Top-centre border tile.</summary>
            public const int T  = -1; // TODO: Needs value
            /// <summary>Top-right border tile.</summary>
            public const int TR = -1; // TODO: Needs value
            /// <summary>Middle-left border tile.</summary>
            public const int ML = -1; // TODO: Needs value
            /// <summary>Middle-right border tile.</summary>
            public const int MR = -1; // TODO: Needs value
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = -1; // TODO: Needs value
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = -1; // TODO: Needs value
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = -1; // TODO: Needs value
        }

        /// <summary>Dark obsidian / magma-rock terrain. Use <see cref="M"/> for the base layer in volcanic zones.</summary>
        public static class DarkRock
        {
            /// <summary>Flat dark rock fill.</summary>
            public const int Fill1 = -1; // TODO: Needs value
            /// <summary>Solid dark rock centre.</summary>
            public const int M  = -1; // TODO: Needs value
            /// <summary>Top-left border tile.</summary>
            public const int TL = -1; // TODO: Needs value
            /// <summary>Top-centre border tile.</summary>
            public const int T  = -1; // TODO: Needs value
            /// <summary>Top-right border tile.</summary>
            public const int TR = -1; // TODO: Needs value
            /// <summary>Middle-left border tile.</summary>
            public const int ML = -1; // TODO: Needs value
            /// <summary>Middle-right border tile.</summary>
            public const int MR = -1; // TODO: Needs value
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = -1; // TODO: Needs value
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = -1; // TODO: Needs value
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = -1; // TODO: Needs value
        }

        /// <summary>Wooden plank / interior floor. Use <see cref="M"/> for the base layer in buildings.</summary>
        public static class WoodFloor
        {
            /// <summary>Flat wood plank fill.</summary>
            public const int Fill1 = -1; // TODO: Needs value
            /// <summary>Solid wood floor centre.</summary>
            public const int M  = -1; // TODO: Needs value
            /// <summary>Top-left border tile.</summary>
            public const int TL = -1; // TODO: Needs value
            /// <summary>Top-centre border tile.</summary>
            public const int T  = -1; // TODO: Needs value
            /// <summary>Top-right border tile.</summary>
            public const int TR = -1; // TODO: Needs value
            /// <summary>Middle-left border tile.</summary>
            public const int ML = -1; // TODO: Needs value
            /// <summary>Middle-right border tile.</summary>
            public const int MR = -1; // TODO: Needs value
            /// <summary>Bottom-left border tile.</summary>
            public const int BL = -1; // TODO: Needs value
            /// <summary>Bottom-centre border tile.</summary>
            public const int B  = -1; // TODO: Needs value
            /// <summary>Bottom-right border tile.</summary>
            public const int BR = -1; // TODO: Needs value
        }
    }

    // -------------------------------------------------------------------------
    // WATER  (base-layer deep water + object-layer edge/shore overlays)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Water terrain tiles. Deep water goes on the base layer.
    /// Shore/edge blending tiles go on the objects layer over the adjacent terrain.
    /// </summary>
    public static class Water
    {
        /// <summary>Solid deep water fill - canonical base-layer tile for water zones.</summary>
        public const int Deep  = -1; // TODO: Needs value
        /// <summary>Shallow water fill variant.</summary>
        public const int Shallow = -1; // TODO: Needs value

        /// <summary>Shore/edge overlay tiles - place on the objects layer over the land base.</summary>
        public static class Shore
        {
            /// <summary>Shore edge facing north (water to the north).</summary>
            public const int N  = -1; // TODO: Needs value
            /// <summary>Shore edge facing south.</summary>
            public const int S  = -1; // TODO: Needs value
            /// <summary>Shore edge facing east.</summary>
            public const int E  = -1; // TODO: Needs value
            /// <summary>Shore edge facing west.</summary>
            public const int W  = -1; // TODO: Needs value
            /// <summary>Inner corner - water in NE quadrant only.</summary>
            public const int CornerNE = -1; // TODO: Needs value
            /// <summary>Inner corner - water in NW quadrant only.</summary>
            public const int CornerNW = -1; // TODO: Needs value
            /// <summary>Inner corner - water in SE quadrant only.</summary>
            public const int CornerSE = -1; // TODO: Needs value
            /// <summary>Inner corner - water in SW quadrant only.</summary>
            public const int CornerSW = -1; // TODO: Needs value
        }
    }

    // -------------------------------------------------------------------------
    // FLORA  (objects layer - transparent background)
    // -------------------------------------------------------------------------

    /// <summary>Plant and tree object tiles. Transparent background - objects layer only.</summary>
    public static class Flora
    {
        /// <summary>Round oak-style tree variants.</summary>
        public static class OakTree
        {
            /// <summary>Light green oak tree.</summary>
            public const int LightGreen = 526; // TODO: needs-verify
            /// <summary>Autumn orange oak tree.</summary>
            public const int Orange     = 527; // TODO: needs-verify
            /// <summary>Dark green oak tree.</summary>
            public const int DarkGreen  = 528; // TODO: needs-verify
        }

        /// <summary>Tall pine / conifer tree variants.</summary>
        public static class PineTree
        {
            /// <summary>Light green pine tree.</summary>
            public const int LightGreen = 529; // TODO: needs-verify
            /// <summary>Autumn orange pine tree.</summary>
            public const int Orange     = 530; // TODO: needs-verify
            /// <summary>Dark green pine tree.</summary>
            public const int DarkGreen  = 531; // TODO: needs-verify
        }

        /// <summary>Round bush / shrub variants.</summary>
        public static class Bush
        {
            /// <summary>Light green bush.</summary>
            public const int LightGreen = 532; // TODO: needs-verify
            /// <summary>Autumn orange bush.</summary>
            public const int Orange     = 533; // TODO: needs-verify
            /// <summary>Dark green bush.</summary>
            public const int DarkGreen  = 534; // TODO: needs-verify
        }

        /// <summary>Fruit-bearing tree.</summary>
        public const int FruitTree = 536; // TODO: needs-verify

        /// <summary>Dead / bare tree (no leaves).</summary>
        public const int DeadTree  = 654; // TODO: needs-verify

        /// <summary>Tree stump.</summary>
        public const int Stump = -1; // TODO: Needs value

        /// <summary>Small cactus.</summary>
        public const int CactusSmall = -1; // TODO: Needs value

        /// <summary>Large cactus.</summary>
        public const int CactusLarge = -1; // TODO: Needs value

        /// <summary>Tall grass / weeds.</summary>
        public const int TallGrass = -1; // TODO: Needs value

        /// <summary>Tall grass variant 2.</summary>
        public const int TallGrass2 = -1; // TODO: Needs value

        /// <summary>Small flower - colour variant 1.</summary>
        public const int FlowerA = -1; // TODO: Needs value

        /// <summary>Small flower - colour variant 2.</summary>
        public const int FlowerB = -1; // TODO: Needs value

        /// <summary>Small flower - colour variant 3.</summary>
        public const int FlowerC = -1; // TODO: Needs value

        /// <summary>Large mushroom.</summary>
        public const int MushroomLarge = -1; // TODO: Needs value

        /// <summary>Small mushroom cluster.</summary>
        public const int MushroomSmall = -1; // TODO: Needs value

        /// <summary>Lily pad (water surface).</summary>
        public const int LilyPad = -1; // TODO: Needs value

        /// <summary>Seaweed / aquatic plant.</summary>
        public const int Seaweed = -1; // TODO: Needs value

        /// <summary>Vine / ivy hanging vertical.</summary>
        public const int VineV = -1; // TODO: Needs value

        /// <summary>Crop / wheat stalk.</summary>
        public const int Wheat = -1; // TODO: Needs value

        /// <summary>Palm tree.</summary>
        public const int PalmTree = -1; // TODO: Needs value
    }

    // -------------------------------------------------------------------------
    // PATHS  (objects layer - transparent background)
    // -------------------------------------------------------------------------

    /// <summary>Dirt path tiles for roads and trails. Transparent background - objects layer only.</summary>
    public static class DirtPath
    {
        /// <summary>4-way cross junction.</summary>
        public const int FourWay = 691;    // TODO: needs-verify
        /// <summary>Isolated single-tile dirt patch / circle.</summary>
        public const int Circle  = 692;    // TODO: needs-verify

        // -- End caps - path terminates in this direction ----------------------
        /// <summary>Dead-end cap, open facing right (road coming from the left).</summary>
        public const int EndLeft   = 690;  // TODO: needs-verify
        /// <summary>Dead-end cap, open facing left (road coming from the right).</summary>
        public const int EndRight  = 689;  // TODO: needs-verify
        /// <summary>Dead-end cap, open facing down (road coming from above).</summary>
        public const int EndTop    = 633;  // TODO: needs-verify
        /// <summary>Dead-end cap, open facing up (road coming from below).</summary>
        public const int EndBottom = 632;  // TODO: needs-verify

        // -- T-junctions - open in three directions ----------------------------
        /// <summary>T-junction open Left, Bottom, Right (closed at Top).</summary>
        public const int TJunctionLBR = 404; // TODO: needs-verify
        /// <summary>T-junction open Left, Top, Bottom (closed at Right).</summary>
        public const int TJunctionLTB = 405; // TODO: needs-verify
        /// <summary>T-junction open Top, Bottom, Right (closed at Left).</summary>
        public const int TJunctionTBR = 461; // TODO: needs-verify
        /// <summary>T-junction open Left, Top, Right (closed at Bottom).</summary>
        public const int TJunctionLTR = 462; // TODO: needs-verify

        // -- Rounded corners ---------------------------------------------------
        /// <summary>Corner connecting Top exit and Left exit.</summary>
        public const int CornerTL = 406; // TODO: needs-verify
        /// <summary>Corner connecting Top exit and Right exit.</summary>
        public const int CornerTR = 407; // TODO: needs-verify
        /// <summary>Corner connecting Bottom exit and Left exit.</summary>
        public const int CornerBL = 463; // TODO: needs-verify
        /// <summary>Corner connecting Bottom exit and Right exit.</summary>
        public const int CornerBR = 464; // TODO: needs-verify

        // -- Straight segments -------------------------------------------------
        /// <summary>Straight horizontal segment (exits left and right).</summary>
        public const int StraightH = 408; // TODO: needs-verify
        /// <summary>Straight vertical segment (exits top and bottom).</summary>
        public const int StraightV = 465; // TODO: needs-verify
    }

    /// <summary>Stone / cobblestone road tiles. Transparent background - objects layer only.</summary>
    public static class StonePath
    {
        /// <summary>4-way cross junction.</summary>
        public const int FourWay   = -1; // TODO: Needs value
        /// <summary>Dead-end cap, open facing right.</summary>
        public const int EndLeft   = -1; // TODO: Needs value
        /// <summary>Dead-end cap, open facing left.</summary>
        public const int EndRight  = -1; // TODO: Needs value
        /// <summary>Dead-end cap, open facing down.</summary>
        public const int EndTop    = -1; // TODO: Needs value
        /// <summary>Dead-end cap, open facing up.</summary>
        public const int EndBottom = -1; // TODO: Needs value
        /// <summary>T-junction open Left, Bottom, Right.</summary>
        public const int TJunctionLBR = -1; // TODO: Needs value
        /// <summary>T-junction open Left, Top, Bottom.</summary>
        public const int TJunctionLTB = -1; // TODO: Needs value
        /// <summary>T-junction open Top, Bottom, Right.</summary>
        public const int TJunctionTBR = -1; // TODO: Needs value
        /// <summary>T-junction open Left, Top, Right.</summary>
        public const int TJunctionLTR = -1; // TODO: Needs value
        /// <summary>Corner connecting Top and Left.</summary>
        public const int CornerTL  = -1; // TODO: Needs value
        /// <summary>Corner connecting Top and Right.</summary>
        public const int CornerTR  = -1; // TODO: Needs value
        /// <summary>Corner connecting Bottom and Left.</summary>
        public const int CornerBL  = -1; // TODO: Needs value
        /// <summary>Corner connecting Bottom and Right.</summary>
        public const int CornerBR  = -1; // TODO: Needs value
        /// <summary>Straight horizontal segment.</summary>
        public const int StraightH = -1; // TODO: Needs value
        /// <summary>Straight vertical segment.</summary>
        public const int StraightV = -1; // TODO: Needs value
    }

    // -------------------------------------------------------------------------
    // FENCES  (objects layer)
    // -------------------------------------------------------------------------

    /// <summary>Wooden fence tiles. Transparent background - objects layer only.</summary>
    public static class WoodFence
    {
        /// <summary>Horizontal fence segment.</summary>
        public const int H         = -1; // TODO: Needs value
        /// <summary>Vertical fence segment.</summary>
        public const int V         = -1; // TODO: Needs value
        /// <summary>Corner connecting Top and Left.</summary>
        public const int CornerTL  = -1; // TODO: Needs value
        /// <summary>Corner connecting Top and Right.</summary>
        public const int CornerTR  = -1; // TODO: Needs value
        /// <summary>Corner connecting Bottom and Left.</summary>
        public const int CornerBL  = -1; // TODO: Needs value
        /// <summary>Corner connecting Bottom and Right.</summary>
        public const int CornerBR  = -1; // TODO: Needs value
        /// <summary>T-junction open Left, Bottom, Right.</summary>
        public const int TJunctionLBR = -1; // TODO: Needs value
        /// <summary>T-junction open Top, Bottom, Right.</summary>
        public const int TJunctionTBR = -1; // TODO: Needs value
        /// <summary>T-junction open Left, Top, Bottom.</summary>
        public const int TJunctionLTB = -1; // TODO: Needs value
        /// <summary>T-junction open Left, Top, Right.</summary>
        public const int TJunctionLTR = -1; // TODO: Needs value
        /// <summary>Gate (closed).</summary>
        public const int GateClosed = -1; // TODO: Needs value
        /// <summary>Gate (open).</summary>
        public const int GateOpen   = -1; // TODO: Needs value
        /// <summary>4-way cross junction.</summary>
        public const int FourWay    = -1; // TODO: Needs value
    }

    // -------------------------------------------------------------------------
    // BRIDGES  (objects layer)
    // -------------------------------------------------------------------------

    /// <summary>Bridge tiles placed on water. Transparent background - objects layer only.</summary>
    public static class Bridge
    {
        /// <summary>Horizontal bridge - left end.</summary>
        public const int HLeft   = -1; // TODO: Needs value
        /// <summary>Horizontal bridge - middle segment.</summary>
        public const int HMid    = -1; // TODO: Needs value
        /// <summary>Horizontal bridge - right end.</summary>
        public const int HRight  = -1; // TODO: Needs value
        /// <summary>Vertical bridge - top end.</summary>
        public const int VTop    = -1; // TODO: Needs value
        /// <summary>Vertical bridge - middle segment.</summary>
        public const int VMid    = -1; // TODO: Needs value
        /// <summary>Vertical bridge - bottom end.</summary>
        public const int VBottom = -1; // TODO: Needs value
    }

    // -------------------------------------------------------------------------
    // BUILDINGS  (objects layer - walls, roofs, doors, windows)
    // -------------------------------------------------------------------------

    /// <summary>Wooden building exterior tiles. Transparent background - objects layer only.</summary>
    public static class WoodWall
    {
        /// <summary>Plain wall top edge.</summary>
        public const int Top        = -1; // TODO: Needs value
        /// <summary>Plain wall left edge.</summary>
        public const int Left       = -1; // TODO: Needs value
        /// <summary>Plain wall right edge.</summary>
        public const int Right      = -1; // TODO: Needs value
        /// <summary>Plain wall bottom edge.</summary>
        public const int Bottom     = -1; // TODO: Needs value
        /// <summary>Outer corner - top-left.</summary>
        public const int CornerTL   = -1; // TODO: Needs value
        /// <summary>Outer corner - top-right.</summary>
        public const int CornerTR   = -1; // TODO: Needs value
        /// <summary>Outer corner - bottom-left.</summary>
        public const int CornerBL   = -1; // TODO: Needs value
        /// <summary>Outer corner - bottom-right.</summary>
        public const int CornerBR   = -1; // TODO: Needs value
        /// <summary>Wall face (south-facing, visible front).</summary>
        public const int FaceS      = -1; // TODO: Needs value
        /// <summary>Wall face with window.</summary>
        public const int WindowS    = -1; // TODO: Needs value
        /// <summary>Wall face with door (closed).</summary>
        public const int DoorS      = -1; // TODO: Needs value
        /// <summary>Wall face with door (open).</summary>
        public const int DoorOpenS  = -1; // TODO: Needs value
    }

    /// <summary>Stone building exterior tiles. Transparent background - objects layer only.</summary>
    public static class StoneWall
    {
        /// <summary>Plain wall top edge.</summary>
        public const int Top        = -1; // TODO: Needs value
        /// <summary>Plain wall left edge.</summary>
        public const int Left       = -1; // TODO: Needs value
        /// <summary>Plain wall right edge.</summary>
        public const int Right      = -1; // TODO: Needs value
        /// <summary>Plain wall bottom edge.</summary>
        public const int Bottom     = -1; // TODO: Needs value
        /// <summary>Outer corner - top-left.</summary>
        public const int CornerTL   = -1; // TODO: Needs value
        /// <summary>Outer corner - top-right.</summary>
        public const int CornerTR   = -1; // TODO: Needs value
        /// <summary>Outer corner - bottom-left.</summary>
        public const int CornerBL   = -1; // TODO: Needs value
        /// <summary>Outer corner - bottom-right.</summary>
        public const int CornerBR   = -1; // TODO: Needs value
        /// <summary>Wall face (south-facing, visible front).</summary>
        public const int FaceS      = -1; // TODO: Needs value
        /// <summary>Wall face with window.</summary>
        public const int WindowS    = -1; // TODO: Needs value
        /// <summary>Wall face with door (closed).</summary>
        public const int DoorS      = -1; // TODO: Needs value
        /// <summary>Wall face with door (open).</summary>
        public const int DoorOpenS  = -1; // TODO: Needs value
    }

    /// <summary>Roof tiles. Transparent background - place on a layer above entities (ZIndex &gt;= 2).</summary>
    public static class Roof
    {
        /// <summary>Thatched roof - ridge (top centre).</summary>
        public const int ThatchRidge    = -1; // TODO: Needs value
        /// <summary>Thatched roof - left slope.</summary>
        public const int ThatchLeft     = -1; // TODO: Needs value
        /// <summary>Thatched roof - right slope.</summary>
        public const int ThatchRight    = -1; // TODO: Needs value
        /// <summary>Thatched roof - left gable end-cap.</summary>
        public const int ThatchGableL   = -1; // TODO: Needs value
        /// <summary>Thatched roof - right gable end-cap.</summary>
        public const int ThatchGableR   = -1; // TODO: Needs value
        /// <summary>Tiled roof - ridge (top centre).</summary>
        public const int TileRidge      = -1; // TODO: Needs value
        /// <summary>Tiled roof - left slope.</summary>
        public const int TileLeft       = -1; // TODO: Needs value
        /// <summary>Tiled roof - right slope.</summary>
        public const int TileRight      = -1; // TODO: Needs value
        /// <summary>Tiled roof - left gable end-cap.</summary>
        public const int TileGableL     = -1; // TODO: Needs value
        /// <summary>Tiled roof - right gable end-cap.</summary>
        public const int TileGableR     = -1; // TODO: Needs value
    }

    // -------------------------------------------------------------------------
    // DUNGEON  (objects layer - walls, floors, doors, special features)
    // -------------------------------------------------------------------------

    /// <summary>Dungeon / cave wall and tunnel tiles. Transparent background - objects layer only.</summary>
    public static class Dungeon
    {
        /// <summary>Dungeon wall - solid front face.</summary>
        public const int WallFront    = -1; // TODO: Needs value
        /// <summary>Dungeon wall - top edge.</summary>
        public const int WallTop      = -1; // TODO: Needs value
        /// <summary>Dungeon wall - left edge.</summary>
        public const int WallLeft     = -1; // TODO: Needs value
        /// <summary>Dungeon wall - right edge.</summary>
        public const int WallRight    = -1; // TODO: Needs value
        /// <summary>Dungeon wall - corner top-left.</summary>
        public const int WallCornerTL = -1; // TODO: Needs value
        /// <summary>Dungeon wall - corner top-right.</summary>
        public const int WallCornerTR = -1; // TODO: Needs value
        /// <summary>Dungeon wall - corner bottom-left.</summary>
        public const int WallCornerBL = -1; // TODO: Needs value
        /// <summary>Dungeon wall - corner bottom-right.</summary>
        public const int WallCornerBR = -1; // TODO: Needs value
        /// <summary>Dungeon wall with cracks / damage.</summary>
        public const int WallCracked  = -1; // TODO: Needs value

        /// <summary>Wooden door - closed (horizontal passage).</summary>
        public const int DoorWoodClosed = -1; // TODO: Needs value
        /// <summary>Wooden door - open (horizontal passage).</summary>
        public const int DoorWoodOpen   = -1; // TODO: Needs value
        /// <summary>Iron door - closed.</summary>
        public const int DoorIronClosed = -1; // TODO: Needs value
        /// <summary>Iron door - open.</summary>
        public const int DoorIronOpen   = -1; // TODO: Needs value
        /// <summary>Iron bars / portcullis.</summary>
        public const int Bars           = -1; // TODO: Needs value

        /// <summary>Stone stairs going up.</summary>
        public const int StairsUp   = -1; // TODO: Needs value
        /// <summary>Stone stairs going down.</summary>
        public const int StairsDown = -1; // TODO: Needs value
        /// <summary>Ladder going up.</summary>
        public const int LadderUp   = -1; // TODO: Needs value
        /// <summary>Ladder going down.</summary>
        public const int LadderDown = -1; // TODO: Needs value
        /// <summary>Pit / trapdoor opening.</summary>
        public const int Pit        = -1; // TODO: Needs value

        /// <summary>Wall-mounted torch (south-facing wall).</summary>
        public const int TorchWallS = -1; // TODO: Needs value
        /// <summary>Wall-mounted torch (east-facing wall).</summary>
        public const int TorchWallE = -1; // TODO: Needs value
        /// <summary>Floor-standing torch / brazier.</summary>
        public const int TorchFloor = -1; // TODO: Needs value
    }

    // -------------------------------------------------------------------------
    // PROPS  (objects layer - decorations and interactive objects)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Decorative and interactive prop tiles. Transparent background - objects layer only.
    /// Multi-tile props must be placed as a group using all named positions.
    /// </summary>
    public static class Props
    {
        // -- Water / outdoor structures ----------------------------------------

        /// <summary>
        /// Water fountain - 3-column - 3-row prop.
        /// Layout: (TL, T, TR) / (ML, M, MR) / (BL, B, BR).
        /// </summary>
        public static class WaterFountain
        {
            /// <summary>Top-left tile.</summary>
            public const int TL = 229; // TODO: needs-verify
            /// <summary>Top-centre tile.</summary>
            public const int T  = -1;  // TODO: Needs value  (check if 3-wide or 2-wide)
            /// <summary>Top-right tile.</summary>
            public const int TR = 228; // TODO: needs-verify
            /// <summary>Middle-left tile.</summary>
            public const int ML = 230; // TODO: needs-verify
            /// <summary>Middle-centre tile.</summary>
            public const int M  = 231; // TODO: needs-verify
            /// <summary>Middle-right tile.</summary>
            public const int MR = 232; // TODO: needs-verify
            /// <summary>Bottom-left tile.</summary>
            public const int BL = 287; // TODO: needs-verify
            /// <summary>Bottom-centre tile.</summary>
            public const int B  = 288; // TODO: needs-verify
            /// <summary>Bottom-right tile.</summary>
            public const int BR = 289; // TODO: needs-verify
        }

        /// <summary>Stone well - 2-tile tall prop: (Top) / (Base).</summary>
        public static class Well
        {
            /// <summary>Top tile of the well (rope, bucket, roof).</summary>
            public const int Top  = -1; // TODO: Needs value
            /// <summary>Base tile of the well (stone surround).</summary>
            public const int Base = -1; // TODO: Needs value
        }

        /// <summary>Wooden signpost / directional sign.</summary>
        public const int Sign = -1; // TODO: Needs value

        /// <summary>Market stall awning - left end.</summary>
        public const int StallLeft  = -1; // TODO: Needs value
        /// <summary>Market stall awning - middle section.</summary>
        public const int StallMid   = -1; // TODO: Needs value
        /// <summary>Market stall awning - right end.</summary>
        public const int StallRight = -1; // TODO: Needs value

        // -- Containers --------------------------------------------------------

        /// <summary>Closed wooden chest.</summary>
        public const int ChestClosed = -1; // TODO: Needs value
        /// <summary>Open wooden chest (empty or looted).</summary>
        public const int ChestOpen   = -1; // TODO: Needs value
        /// <summary>Locked iron chest.</summary>
        public const int ChestLocked = -1; // TODO: Needs value
        /// <summary>Small closed chest.</summary>
        public const int ChestSmall  = -1; // TODO: Needs value
        /// <summary>Sealed barrel.</summary>
        public const int BarrelSealed  = -1; // TODO: Needs value
        /// <summary>Open barrel.</summary>
        public const int BarrelOpen    = -1; // TODO: Needs value
        /// <summary>Broken / empty barrel.</summary>
        public const int BarrelBroken  = -1; // TODO: Needs value
        /// <summary>Wooden crate.</summary>
        public const int Crate  = -1; // TODO: Needs value
        /// <summary>Burlap sack.</summary>
        public const int Sack   = -1; // TODO: Needs value
        /// <summary>Small pot / vase.</summary>
        public const int PotSmall  = -1; // TODO: Needs value
        /// <summary>Large pot / vase.</summary>
        public const int PotLarge  = -1; // TODO: Needs value

        // -- Lights ------------------------------------------------------------

        /// <summary>Floor-standing lamp post / lantern.</summary>
        public const int LampPost = -1; // TODO: Needs value
        /// <summary>Lit campfire.</summary>
        public const int Campfire = -1; // TODO: Needs value
        /// <summary>Unlit campfire / fire pit.</summary>
        public const int CampfireUnlit = -1; // TODO: Needs value
        /// <summary>Candle / candlestick.</summary>
        public const int Candle   = -1; // TODO: Needs value
        /// <summary>Brazier (iron fire basket).</summary>
        public const int Brazier  = -1; // TODO: Needs value

        // -- Crafting and workstations -----------------------------------------

        /// <summary>Blacksmith anvil.</summary>
        public const int Anvil      = -1; // TODO: Needs value
        /// <summary>Forge / furnace.</summary>
        public const int Forge      = -1; // TODO: Needs value
        /// <summary>Cauldron / cooking pot.</summary>
        public const int Cauldron   = -1; // TODO: Needs value
        /// <summary>Crafting workbench / table.</summary>
        public const int Workbench  = -1; // TODO: Needs value
        /// <summary>Alchemy / potion table.</summary>
        public const int AlchemyTable = -1; // TODO: Needs value

        // -- Furniture ---------------------------------------------------------

        /// <summary>Wooden table - horizontal (wider than tall).</summary>
        public const int TableH        = -1; // TODO: Needs value
        /// <summary>Wooden table - vertical (taller than wide).</summary>
        public const int TableV        = -1; // TODO: Needs value
        /// <summary>Chair facing south (seat visible from the front).</summary>
        public const int ChairS        = -1; // TODO: Needs value
        /// <summary>Chair facing north.</summary>
        public const int ChairN        = -1; // TODO: Needs value
        /// <summary>Chair facing east.</summary>
        public const int ChairE        = -1; // TODO: Needs value
        /// <summary>Chair facing west.</summary>
        public const int ChairW        = -1; // TODO: Needs value
        /// <summary>Bed - top tile (pillow end).</summary>
        public const int BedTop        = -1; // TODO: Needs value
        /// <summary>Bed - bottom tile (foot end).</summary>
        public const int BedBottom     = -1; // TODO: Needs value
        /// <summary>Bookshelf - filled with books.</summary>
        public const int Bookshelf     = -1; // TODO: Needs value
        /// <summary>Shelf - with items or empty.</summary>
        public const int Shelf         = -1; // TODO: Needs value
        /// <summary>Tall wardrobe / cabinet.</summary>
        public const int Wardrobe      = -1; // TODO: Needs value

        // -- Structures and monuments ------------------------------------------

        /// <summary>Stone pillar - top cap.</summary>
        public const int PillarTop    = -1; // TODO: Needs value
        /// <summary>Stone pillar - middle shaft segment.</summary>
        public const int PillarMid    = -1; // TODO: Needs value
        /// <summary>Stone pillar - base.</summary>
        public const int PillarBase   = -1; // TODO: Needs value
        /// <summary>Stone statue.</summary>
        public const int Statue       = -1; // TODO: Needs value
        /// <summary>Altar / offering stone.</summary>
        public const int Altar        = -1; // TODO: Needs value
        /// <summary>Pedestal (for holding an item or orb).</summary>
        public const int Pedestal     = -1; // TODO: Needs value
        /// <summary>Gravestone / tombstone style A.</summary>
        public const int GravestoneA  = -1; // TODO: Needs value
        /// <summary>Gravestone / tombstone style B.</summary>
        public const int GravestoneB  = -1; // TODO: Needs value
        /// <summary>Grave cross.</summary>
        public const int GraveCross   = -1; // TODO: Needs value

        // -- Ground decorations ------------------------------------------------

        /// <summary>Small rock.</summary>
        public const int RockSmall  = -1; // TODO: Needs value
        /// <summary>Medium rock.</summary>
        public const int RockMedium = -1; // TODO: Needs value
        /// <summary>Large boulder.</summary>
        public const int RockLarge  = -1; // TODO: Needs value
        /// <summary>Rock cluster / rubble pile.</summary>
        public const int RockPile   = -1; // TODO: Needs value
        /// <summary>Crystal formation (small).</summary>
        public const int CrystalSmall = -1; // TODO: Needs value
        /// <summary>Crystal formation (large).</summary>
        public const int CrystalLarge = -1; // TODO: Needs value
        /// <summary>Bone / skeleton remains.</summary>
        public const int Bones      = -1; // TODO: Needs value
        /// <summary>Skull.</summary>
        public const int Skull      = -1; // TODO: Needs value
        /// <summary>Hay bale.</summary>
        public const int HayBale    = -1; // TODO: Needs value
        /// <summary>Log (chopped wood).</summary>
        public const int Log        = -1; // TODO: Needs value
        /// <summary>Wood pile (stacked logs).</summary>
        public const int WoodPile   = -1; // TODO: Needs value

        // -- Interactable / mechanical -----------------------------------------

        /// <summary>Pressure plate (untriggered).</summary>
        public const int PressurePlate      = -1; // TODO: Needs value
        /// <summary>Lever (up / off position).</summary>
        public const int LeverOff           = -1; // TODO: Needs value
        /// <summary>Lever (down / on position).</summary>
        public const int LeverOn            = -1; // TODO: Needs value
        /// <summary>Wall button / switch.</summary>
        public const int Button             = -1; // TODO: Needs value
        /// <summary>Mine cart.</summary>
        public const int MineCart           = -1; // TODO: Needs value
        /// <summary>Mine cart rail - horizontal.</summary>
        public const int RailH              = -1; // TODO: Needs value
        /// <summary>Mine cart rail - vertical.</summary>
        public const int RailV              = -1; // TODO: Needs value
        /// <summary>Mine cart rail - corner.</summary>
        public const int RailCorner         = -1; // TODO: Needs value
    }
}
