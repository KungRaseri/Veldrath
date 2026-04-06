namespace RealmEngine.Shared.Models;

// -- Tile Index Constants ---------------------------------------------------
//
// All indices reference the onebit_packed spritesheet (49 columns x 22 rows,
// 16 px tiles, 0 px spacing).  Formula: index = row * 49 + col.
//
// Layer convention:
//   Layer 0 "base"    - opaque terrain fill.  Use Terrain.*.M constants.  Never -1.
//   Layer 1 "objects" - decorations (flora, paths, props).  Use -1 for empty cells.
//
// Background color: R71 G45 B60 (dark maroon).  All tile pixels are fully opaque.
// The background is the tile's "empty" area; colored pixels are the tile content.

/// <summary>
/// Compile-time tile index constants for the <c>onebit_packed</c> spritesheet
/// (Kenney 1-Bit Pack — 49 columns x 22 rows, 16 px tiles, 0 px spacing).
/// </summary>
/// <remarks>
/// Index formula: <c>index = row * 49 + col</c>.
/// <para>
/// Base-layer fills use solid opaque tiles.  Object-layer decorations (flora, paths)
/// are placed over the base.  Use <c>-1</c> on the objects layer for empty cells.
/// </para>
/// <para>
/// Reference image: <c>scripts/onebit_labeled.png</c> — each tile labelled with its index.
/// </para>
/// </remarks>
public static class TileIndex
{
    // -- Special ---------------------------------------------------------------

    /// <summary>Solid dark-maroon tile (r0c0). Use where an explicit opaque blank is needed.</summary>
    public const int Blank = 0;

    /// <summary>
    /// Pending / unresolved placeholder. Rendered as magenta at runtime so unfinished
    /// cells are immediately visible. Replace with a real constant once identified.
    /// </summary>
    public const int Pending = -2;

    // -------------------------------------------------------------------------
    // TERRAIN  (base layer — always opaque)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opaque terrain fill tiles. The <c>M</c> constant is the canonical base-layer fill.
    /// No 9-patch border tiles exist in the 1-bit pack — zone edges use flora objects instead.
    /// </summary>
    public static class Terrain
    {
        /// <summary>Green grass terrain.</summary>
        public static class Grass
        {
            /// <summary>Solid grass fill — canonical base-layer fill for grassy zones.</summary>
            public const int M = 265; // r5c20 — 8/9 sample points green (R56 G217 B115)
        }

        /// <summary>Grey stone terrain.</summary>
        public static class Stone
        {
            /// <summary>Solid stone fill — canonical base-layer fill for stone zones and dungeon walls.</summary>
            public const int M = 202; // r4c6 — all-points warm grey (R207 G198 B184)
            /// <summary>Open dungeon floor — dark background tile used under walkable dungeon cells.</summary>
            public const int Floor = 0; // r0c0 — all-points dark maroon (R71 G45 B60)
        }

        /// <summary>Sandy / coastal terrain.</summary>
        public static class Sand
        {
            /// <summary>Solid brown fill — canonical base-layer fill for sandy/coastal zones.</summary>
            public const int M = 445; // r9c4 — all-points brown (R191 G121 B88)
        }
    }

    // -------------------------------------------------------------------------
    // WATER  (base layer — deep water fill)
    // -------------------------------------------------------------------------

    /// <summary>Water terrain tiles.</summary>
    public static class Water
    {
        /// <summary>Solid deep water fill — canonical base-layer tile for water zones.</summary>
        public const int Deep = 207; // r4c11 — all-points blue (R60 G172 B215)
    }

    // -------------------------------------------------------------------------
    // FLORA  (objects layer)
    // -------------------------------------------------------------------------

    /// <summary>Plant and tree object tiles placed on the objects layer.</summary>
    public static class Flora
    {
        /// <summary>Round oak-style tree variants.</summary>
        public static class OakTree
        {
            /// <summary>Sparse light green oak tree.</summary>
            public const int LightGreen = 49;  // r1c0 — 5/9 green
            /// <summary>Medium round oak tree.</summary>
            public const int Orange     = 53;  // r1c4 — 7/9 green (no orange variant; maps to medium round)
            /// <summary>Dense round dark green oak tree.</summary>
            public const int DarkGreen  = 55;  // r1c6 — 7/9 green
        }

        /// <summary>Round bush / shrub variants.</summary>
        public static class Bush
        {
            /// <summary>Light green bush.</summary>
            public const int LightGreen = 50;  // r1c1 — 5/9 green sparse
            /// <summary>Medium bush variant.</summary>
            public const int Orange     = 53;  // r1c4 — round medium (no orange; reuses medium round)
            /// <summary>Dense dark green bush.</summary>
            public const int DarkGreen  = 102; // r2c4 — 6/9 green dense
        }
    }

    // -------------------------------------------------------------------------
    // PATHS  (objects layer)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dirt path tiles for roads and trails placed on the objects layer.
    /// All use brown (R191 G121 B88) over the dark background.
    /// </summary>
    public static class DirtPath
    {
        /// <summary>4-way cross junction — open in all four directions.</summary>
        public const int FourWay = 444; // r9c3 — T+B+L+R brown, hollow centre

        /// <summary>Isolated single-tile dirt patch.</summary>
        public const int Circle = 191; // r3c44 — centre-only brown dot

        // -- Straight segments -----------------------------------------------

        /// <summary>Straight horizontal segment — exits west and east.</summary>
        public const int StraightH = 192; // r3c45 — L+R+M brown, T/B bg

        /// <summary>Straight vertical segment — exits north and south.</summary>
        public const int StraightV = 343; // r7c0 — T+B+M brown, L/R bg

        // -- Corners ---------------------------------------------------------

        /// <summary>Corner connecting north exit and west exit.</summary>
        public const int CornerTL = 292; // r5c47 — T+L+M brown

        /// <summary>Corner connecting north exit and east exit.</summary>
        public const int CornerTR = 348; // r7c5 — T+R brown (hollow centre)

        /// <summary>Corner connecting south exit and west exit.</summary>
        public const int CornerBL = 184; // r3c37 — B+L+M brown

        /// <summary>Corner connecting south exit and east exit.</summary>
        public const int CornerBR = 260; // r5c15 — B+R+M brown

        // -- T-junctions -----------------------------------------------------

        /// <summary>T-junction open south, west, east (closed north).</summary>
        public const int TJunctionLBR = 261; // r5c16 — B+L+R+M brown

        /// <summary>T-junction open north, west, east (closed south).</summary>
        public const int TJunctionLTR = 345; // r7c2 — T+L+R+M brown

        /// <summary>T-junction open north, south, east (closed west).</summary>
        public const int TJunctionTBR = 299; // r6c5 — T+B+R+M brown

        /// <summary>T-junction open north, south, west (closed east).</summary>
        public const int TJunctionLTB = 451; // r9c10 — T+B+L+M brown

        // -- End caps --------------------------------------------------------

        /// <summary>Dead-end cap — exit south only (cap face at north).</summary>
        public const int EndTop = 244; // r4c48 — B+M brown, T/L/R bg

        /// <summary>Dead-end cap — exit north only (cap face at south).</summary>
        public const int EndBottom = 243; // r4c47 — T+M brown, B/L/R bg

        /// <summary>Dead-end cap — exit west only (cap face at east).</summary>
        public const int EndRight = 359; // r7c16 — L+M brown, T/B/R bg
    }
}