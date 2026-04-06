namespace RealmEngine.Shared.Models;

// -- Tile Index Constants ---------------------------------------------------
//
// All indices reference the onebit_packed spritesheet (49 columns x 22 rows,
// 16 px tiles, 0 px spacing).  Formula: index = row * 49 + col.
//
// Layer convention:
//   Layer 0 "base"    - opaque terrain fill.  Use Terrain.*.M constants.  Never -1.
//   Layer 1 "objects" - decorations (flora, ground texture, paths, props).  -1 = empty cell.
//
// Background color: R71 G45 B60 (dark maroon).  All tile pixels are fully opaque.
// The background is the implicit empty area of every tile.
//
// Ground-texture strategy for grass zones:
//   base[]=0 (dark maroon), then scatter Ground.* tiles on the objects layer for
//   visual variety (dead leaves, gravel, foliage patches, grass tufts).

/// <summary>
/// Compile-time tile index constants for the <c>onebit_packed</c> spritesheet
/// (Kenney 1-Bit Pack — 49 columns × 22 rows, 16 px tiles, 0 px spacing).
/// </summary>
/// <remarks>
/// Index formula: <c>index = row * 49 + col</c>.
/// Reference image: <c>scripts/onebit_labeled.png</c> — every tile labelled with its index.
/// </remarks>
public static class TileIndex
{
    // -------------------------------------------------------------------------
    // SPECIAL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dark maroon blank tile (r0c0). Base-layer fill for grass zones and dungeon floors.
    /// </summary>
    public const int Blank = 0;

    /// <summary>
    /// Pending / unresolved placeholder. Renders as magenta at runtime — replace
    /// with a real constant before shipping. Look up the index in
    /// <c>scripts/onebit_labeled.png</c>.
    /// </summary>
    public const int Pending = -2;

    // -------------------------------------------------------------------------
    // GROUND TEXTURE  (objects layer — scattered over a base=0 fill)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ground-texture tiles from row 0.  Scatter on the objects layer over a dark
    /// base to add visual variety without changing the terrain type.
    /// </summary>
    public static class Ground
    {
        /// <summary>Dead leaves / sparse dirt dots.</summary>
        public const int DeadLeaves = 1;    // r0c1

        /// <summary>Light gravel — fine pebble scatter.</summary>
        public const int LightGravel = 2;   // r0c2

        /// <summary>Tighter cobblestone / gravel.</summary>
        public const int Cobblestone = 3;   // r0c3

        /// <summary>Larger tiled stone slabs.</summary>
        public const int StoneTile = 4;     // r0c4

        /// <summary>Light foliage / sparse grass dots.</summary>
        public const int LightFoliage = 5;  // r0c5

        /// <summary>Medium grass and foliage.</summary>
        public const int MedFoliage = 6;    // r0c6

        /// <summary>Dense tileable grass — heaviest ground-texture fill.</summary>
        public const int GrassFill = 7;     // r0c7
    }

    // -------------------------------------------------------------------------
    // TERRAIN  (base layer — always opaque, never -1)
    // -------------------------------------------------------------------------

    /// <summary>Opaque terrain fill tiles used as the map base layer.</summary>
    public static class Terrain
    {
        /// <summary>Green grass terrain zones.</summary>
        public static class Grass
        {
            /// <summary>
            /// Base fill for grassy zones — dark maroon ground (same as <see cref="TileIndex.Blank"/>).
            /// Use <see cref="Ground"/> tiles on the objects layer to add green texture.
            /// </summary>
            public const int M = 0;     // r0c0 — dark maroon; Ground.* objects layer adds grass character
        }

        /// <summary>Stone / dungeon terrain.</summary>
        public static class Stone
        {
            /// <summary>Solid warm-grey stone fill — walls, stone zones, dungeon surrounds.</summary>
            public const int M = 202;   // r4c6 — all-points warm grey (R207 G198 B184)

            /// <summary>Open dungeon floor — same dark background as <see cref="TileIndex.Blank"/>.</summary>
            public const int Floor = 0; // r0c0 — dark maroon walkable floor
        }

        /// <summary>Sandy / coastal terrain.</summary>
        public static class Sand
        {
            /// <summary>Solid brown fill — coastal and desert zones.</summary>
            public const int M = 445;   // r9c4 — all-points brown (R191 G121 B88)
        }
    }

    // -------------------------------------------------------------------------
    // WATER  (base layer)
    // -------------------------------------------------------------------------

    /// <summary>Water terrain base-layer tiles.</summary>
    public static class Water
    {
        /// <summary>Solid deep-water fill.</summary>
        public const int Deep = 207;    // r4c11 — all-points blue (R60 G172 B215)
    }

    // -------------------------------------------------------------------------
    // FLORA  (objects layer)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Flora object tiles placed on the objects layer.
    /// Rows 1 and 2 of the spritesheet contain trees, cacti, ground cover and props.
    /// </summary>
    public static class Flora
    {
        // -- Trees (row 1, indices 49-54) ------------------------------------

        /// <summary>Sparse round tree — lightest canopy.</summary>
        public const int TreeA = 49;        // r1c0

        /// <summary>Round tree, medium density.</summary>
        public const int TreeB = 50;        // r1c1

        /// <summary>Single pine / fir tree (pointed top).</summary>
        public const int Pine = 51;         // r1c2

        /// <summary>Round tree variant C.</summary>
        public const int TreeC = 52;        // r1c3

        /// <summary>Round tree variant D — medium-dense.</summary>
        public const int TreeD = 53;        // r1c4

        /// <summary>Dense round tree — best all-purpose forest fill.</summary>
        public const int TreeE = 54;        // r1c5

        // -- Cacti (row 1, indices 55-56) ------------------------------------

        /// <summary>Single cactus — arid / desert / volcanic zones.</summary>
        public const int Cactus = 55;       // r1c6

        /// <summary>Dual (branching) cactus — taller arid decoration.</summary>
        public const int CactusDual = 56;   // r1c7

        // -- Ground cover and props (row 2, indices 98-105) ------------------

        /// <summary>Tall grass tuft.</summary>
        public const int TallGrass = 98;    // r2c0

        /// <summary>Green vines / leafy creeper.</summary>
        public const int Vines = 99;        // r2c1

        /// <summary>Single climbing vine.</summary>
        public const int ClimbingVine = 100; // r2c2

        /// <summary>Dual pine trees side-by-side.</summary>
        public const int DualPine = 101;    // r2c3

        /// <summary>Large round tree with visible trunk — good swamp/deep-forest anchor.</summary>
        public const int BigTree = 102;     // r2c4

        /// <summary>Rocky boulders / stone outcrop.</summary>
        public const int Boulder = 103;     // r2c5

        /// <summary>Dead / withered vines.</summary>
        public const int DeadVines = 104;   // r2c6

        /// <summary>Green mushroom.</summary>
        public const int Mushroom = 105;    // r2c7
    }

    // -------------------------------------------------------------------------
    // PATHS  (objects layer)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dirt path tiles for roads and trails on the objects layer.
    /// All use brown (R191 G121 B88) over the dark background.
    /// </summary>
    public static class DirtPath
    {
        /// <summary>4-way cross junction — open in all four directions.</summary>
        public const int FourWay = 444;         // r9c3

        /// <summary>Isolated single-tile dirt patch.</summary>
        public const int Circle = 191;          // r3c44

        /// <summary>Straight horizontal segment — exits west and east.</summary>
        public const int StraightH = 192;       // r3c45

        /// <summary>Straight vertical segment — exits north and south.</summary>
        public const int StraightV = 343;       // r7c0

        /// <summary>Corner connecting north and west exits.</summary>
        public const int CornerTL = 292;        // r5c47

        /// <summary>Corner connecting north and east exits.</summary>
        public const int CornerTR = 348;        // r7c5

        /// <summary>Corner connecting south and west exits.</summary>
        public const int CornerBL = 184;        // r3c37

        /// <summary>Corner connecting south and east exits.</summary>
        public const int CornerBR = 260;        // r5c15

        /// <summary>T-junction open south, west, east (closed north).</summary>
        public const int TJunctionLBR = 261;    // r5c16

        /// <summary>T-junction open north, west, east (closed south).</summary>
        public const int TJunctionLTR = 345;    // r7c2

        /// <summary>T-junction open north, south, east (closed west).</summary>
        public const int TJunctionTBR = 299;    // r6c5

        /// <summary>T-junction open north, south, west (closed east).</summary>
        public const int TJunctionLTB = 451;    // r9c10

        /// <summary>Dead-end cap — exit south only.</summary>
        public const int EndTop = 244;          // r4c48

        /// <summary>Dead-end cap — exit north only.</summary>
        public const int EndBottom = 243;       // r4c47

        /// <summary>Dead-end cap — exit west only.</summary>
        public const int EndRight = 359;        // r7c16
    }
}