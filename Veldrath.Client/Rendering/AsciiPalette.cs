using Avalonia.Media;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Named palette of <see cref="Color"/> and <see cref="IBrush"/> slots used by the ASCII renderer.
/// Extracted from hardcoded literals in <see cref="AsciiMapRenderer"/> and the old <c>TileAsciiMap</c>.
/// Replaceable for theming (dark mode, high-contrast, colourblind palettes).
/// </summary>
public static class AsciiPalette
{
    // ── Tile colours ──────────────────────────────────────────────────────────

    /// <summary>Ground — dead leaves / sparse dirt dots.</summary>
    public static Color GroundDeadLeaves => Color.FromRgb(74, 222, 128);

    /// <summary>Ground — fine pebble scatter.</summary>
    public static Color GroundGravel => Color.FromRgb(148, 163, 184);

    /// <summary>Ground — tighter cobblestone.</summary>
    public static Color GroundCobblestone => Color.FromRgb(148, 163, 184);

    /// <summary>Ground — larger stone slabs.</summary>
    public static Color GroundStoneTile => Color.FromRgb(148, 163, 184);

    /// <summary>Ground — light foliage / sparse grass dots.</summary>
    public static Color GroundLightFoliage => Color.FromRgb(34, 197, 94);

    /// <summary>Ground — medium grass and foliage.</summary>
    public static Color GroundMedFoliage => Color.FromRgb(34, 197, 94);

    /// <summary>Ground — dense tileable grass fill.</summary>
    public static Color GroundGrassFill => Color.FromRgb(74, 222, 128);

    /// <summary>Terrain — solid warm-grey stone wall.</summary>
    public static Color TerrainStone => Color.FromRgb(148, 163, 184);

    /// <summary>Terrain — solid brown sand fill.</summary>
    public static Color TerrainSand => Color.FromRgb(251, 191, 36);

    /// <summary>Water — deep blue water fill.</summary>
    public static Color WaterDeep => Color.FromRgb(96, 165, 250);

    /// <summary>Flora — green tree canopies (all tree variants).</summary>
    public static Color FloraTree => Color.FromRgb(34, 197, 94);

    /// <summary>Flora — green cactus (arid / desert).</summary>
    public static Color FloraCactus => Color.FromRgb(34, 197, 94);

    /// <summary>Flora — ground cover (grass tufts, vines, living).</summary>
    public static Color FloraGroundCover => Color.FromRgb(34, 197, 94);

    /// <summary>Flora — dead vines / withered plants.</summary>
    public static Color FloraDead => Color.FromRgb(148, 163, 184);

    /// <summary>Flora — rocky boulders.</summary>
    public static Color FloraBoulder => Color.FromRgb(148, 163, 184);

    /// <summary>Flora — green mushrooms.</summary>
    public static Color FloraMushroom => Color.FromRgb(34, 197, 94);

    /// <summary>Paths — tan dirt path / road tiles.</summary>
    public static Color PathTan => Color.FromRgb(212, 165, 116);

    /// <summary>Special — blank dark-maroon tile (base layer fill).</summary>
    public static Color Blank => Color.FromRgb(74, 222, 128);

    /// <summary>Debug — magenta for unknown / unmapped tile indices.</summary>
    public static Color DebugUnknown => Color.FromRgb(255, 0, 255);

    // ── Entity colours ────────────────────────────────────────────────────────

    /// <summary>Self player entity — cyan.</summary>
    public static Color PlayerSelf => Color.FromRgb(6, 182, 212);

    /// <summary>Other player entity — green.</summary>
    public static Color PlayerOther => Color.FromRgb(74, 222, 128);

    /// <summary>Enemy entity — red.</summary>
    public static Color Enemy => Color.FromRgb(239, 68, 68);

    /// <summary>NPC entity — amber.</summary>
    public static Color Npc => Color.FromRgb(250, 204, 21);

    // ── UI / overlay colours ──────────────────────────────────────────────────

    /// <summary>Exit tile highlight — yellow.</summary>
    public static Color ExitHighlight => Color.FromRgb(250, 204, 21);

    /// <summary>Zone-entry highlight — green.</summary>
    public static Color ZoneEntryHighlight => Color.FromRgb(34, 197, 94);

    /// <summary>Region-exit highlight — orange.</summary>
    public static Color RegionExitHighlight => Color.FromRgb(249, 115, 22);

    /// <summary>Zone label text — slate 300.</summary>
    public static Color LabelText => Color.FromRgb(203, 213, 225);

    /// <summary>Fog overlay — dark slate.</summary>
    public static Color FogOverlay => Color.FromRgb(30, 41, 59);

    /// <summary>Default tile text colour — slate 400.</summary>
    public static Color DefaultTileText => Color.FromRgb(148, 163, 184);

    // ── Minimap colours ───────────────────────────────────────────────────────

    /// <summary>Minimap background — dark transparent.</summary>
    public static Color MiniBg => Color.FromArgb(210, 8, 10, 20);

    /// <summary>Minimap wall — dark grey.</summary>
    public static Color MiniWall => Color.FromRgb(30, 30, 40);

    /// <summary>Minimap floor — medium grey.</summary>
    public static Color MiniFloor => Color.FromRgb(80, 80, 100);

    /// <summary>Minimap self player dot — cyan.</summary>
    public static Color MiniSelfDot => Color.FromRgb(6, 182, 212);

    /// <summary>Minimap other entity dot — blue.</summary>
    public static Color MiniOtherDot => Color.FromRgb(30, 144, 255);

    // ── Cached brushes (immutable — safe for static sharing) ──────────────────

    /// <summary>Self player entity brush (cyan).</summary>
    public static IBrush PlayerSelfBrush { get; } = new SolidColorBrush(PlayerSelf);

    /// <summary>Other player entity brush (green).</summary>
    public static IBrush PlayerOtherBrush { get; } = new SolidColorBrush(PlayerOther);

    /// <summary>Enemy entity brush (red).</summary>
    public static IBrush EnemyBrush { get; } = new SolidColorBrush(Enemy);

    /// <summary>NPC entity brush (amber).</summary>
    public static IBrush NpcBrush { get; } = new SolidColorBrush(Npc);

    /// <summary>Exit highlight brush (yellow).</summary>
    public static IBrush ExitHighlightBrush { get; } = new SolidColorBrush(ExitHighlight);

    /// <summary>Zone-entry highlight brush (green).</summary>
    public static IBrush ZoneEntryHighlightBrush { get; } = new SolidColorBrush(ZoneEntryHighlight);

    /// <summary>Region-exit highlight brush (orange).</summary>
    public static IBrush RegionExitHighlightBrush { get; } = new SolidColorBrush(RegionExitHighlight);

    /// <summary>Label text brush (slate 300).</summary>
    public static IBrush LabelBrush { get; } = new SolidColorBrush(LabelText);

    /// <summary>Fog overlay brush (dark slate).</summary>
    public static IBrush FogBrush { get; } = new SolidColorBrush(FogOverlay);
}
