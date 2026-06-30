using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.Rendering;

/// <summary>
/// ASCII / roguelike text-based tilemap renderer. Renders each tile as a single
/// monospace character using <see cref="DrawingContext.DrawText"/>. Each tile is
/// coloured individually via <see cref="TileRegistry"/> and <see cref="AsciiPalette"/>.
/// Entity positions use <see cref="EntityAppearanceRegistry"/> for per-type visuals.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AsciiMapRenderer : IMapRenderer
{
    private static readonly Typeface AsciiTypeface =
        new("JetBrains Mono, Cascadia Code, Consolas, Courier New", FontStyle.Normal, FontWeight.Normal);

    private double _fontSize = 14.0;
    private double _charWidth;
    private double _charHeight;
    private double _tileSize;

    /// <summary>Cache of per-colour foreground brushes to avoid per-tile allocation.</summary>
    private readonly Dictionary<Color, IBrush> _brushCache = [];

    /// <summary>Initializes a new instance of <see cref="AsciiMapRenderer"/>.</summary>
    public AsciiMapRenderer()
    {
        // Measure a single character to determine the initial display tile size.
        var measureText = new FormattedText(
            "W",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            AsciiTypeface,
            _fontSize,
            Brushes.White);
        _charWidth  = measureText.Width;
        _charHeight = measureText.Height;
        _tileSize = Math.Max(_charWidth, _charHeight);
    }

    /// <inheritdoc/>
    public int DisplayTileSize => (int)Math.Ceiling(_tileSize);

    /// <inheritdoc/>
    public double TileSize
    {
        get => _tileSize;
        set
        {
            var clamped = Math.Clamp(value, 8.0, 128.0);
            if (Math.Abs(_tileSize - clamped) < 0.5) return;
            _tileSize = clamped;

            // Derive font size proportionally from the desired tile size.
            // The original 14pt font produced a ~16px tile, so ratio ≈ pixels / 16 * 14.
            _fontSize = Math.Max(6.0, clamped * 0.875);

            // Recompute character metrics with the new font size.
            var measureText = new FormattedText(
                "W",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                _fontSize,
                Brushes.White);
            _charWidth  = measureText.Width;
            _charHeight = measureText.Height;
        }
    }

    /// <inheritdoc/>
    public void Render(DrawingContext context, RenderState state)
    {
        var vpW = (int)Math.Ceiling(state.Bounds.Width  / _charWidth) + 1;
        var vpH = (int)Math.Ceiling(state.Bounds.Height / _charHeight) + 1;

        // Dark background
        context.FillRectangle(Brushes.Black, new Rect(state.Bounds));

        // Build and draw tile rows (per-tile coloured)
        DrawTileRows(context, state, vpW, vpH);

        // Draw entities on top
        DrawEntities(context, state, vpW, vpH);

        // Grid overlay (tile-boundary lines)
        if (state.ShowGrid)
            DrawGrid(context, state, vpW, vpH);

        // Highlights
        if (state.MapType == "zone")
            DrawExitHighlights(context, state, vpW, vpH);
        else
        {
            DrawZoneEntryHighlights(context, state, vpW, vpH);
            DrawRegionExitHighlights(context, state, vpW, vpH);
            DrawLabels(context, state, vpW, vpH);
        }

        // Fog of war (zone only)
        if (state.MapType == "zone" && state.FogMask.Any(f => f))
            DrawFog(context, state, vpW, vpH);

        // Minimap
        if (state.IsMiniMapOpen)
            DrawMinimap(context, state);
    }

    // ── Tile rows (per-tile colouring) ───────────────────────────────────────

    /// <summary>
    /// Draws every visible tile individually so each can carry its own foreground colour
    /// and optional background fill from <see cref="TileRegistry"/>.
    /// </summary>
    private void DrawTileRows(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        for (var ty = s.CameraY; ty < Math.Min(s.CameraY + vpH, s.MapHeight); ty++)
        {
            for (var tx = s.CameraX; tx < Math.Min(s.CameraX + vpW, s.MapWidth); tx++)
            {
                var idx      = s.MapWidth * ty + tx;
                var tileIndex = CompositeTileIndex(s.Layers, idx);
                if (tileIndex == -1) continue; // fully transparent cell

                var desc     = TileRegistry.Get(tileIndex);
                var screenX  = (tx - s.CameraX) * _charWidth;
                var screenY  = (ty - s.CameraY) * _charHeight;

                // Optional background fill behind the character
                if (desc.Background.HasValue)
                {
                    context.FillRectangle(
                        GetBrush(desc.Background.Value),
                        new Rect(screenX, screenY, _charWidth, _charHeight));
                }

                // Per-tile coloured character
                var tileText = new FormattedText(
                    desc.AsciiChar.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    AsciiTypeface,
                    _fontSize,
                    GetBrush(desc.Foreground));

                context.DrawText(tileText, new Point(screenX, screenY));
            }
        }
    }

    /// <summary>Composites all layers (ordered by ZIndex ascending) into a single tile index.</summary>
    private static int CompositeTileIndex(IReadOnlyList<TileLayerDto> layers, int idx)
    {
        var result = -1;
        foreach (var layer in layers.OrderBy(l => l.ZIndex))
        {
            if (idx < 0 || idx >= layer.Data.Length) continue;
            var ti = layer.Data[idx];
            if (ti != -1) result = ti;
        }
        return result;
    }

    /// <summary>
    /// Returns a cached <see cref="IBrush"/> for the given colour.
    /// Avoids allocating one new brush per tile per frame.
    /// </summary>
    private IBrush GetBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(color);
            _brushCache[color] = brush;
        }
        return brush;
    }

    // ── Entity drawing ────────────────────────────────────────────────────────

    private void DrawEntities(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        foreach (var entity in s.Entities)
        {
            var scrX = (entity.TileX - s.CameraX);
            var scrY = (entity.TileY - s.CameraY);
            if (scrX < 0 || scrX >= vpW) continue;
            if (scrY < 0 || scrY >= vpH) continue;

            var appearance = EntityAppearanceRegistry.Get(entity.SpriteKey, entity.EntityType);
            var isSelf     = s.SelfEntityId.HasValue && entity.EntityId == s.SelfEntityId.Value;

            // Override colour for self-player so it stands out, even if a specific
            // sprite-key appearance was registered.
            IBrush brush;
            if (isSelf && entity.EntityType == "player")
                brush = AsciiPalette.PlayerSelfBrush;
            else if (entity.EntityType == "player")
                brush = AsciiPalette.PlayerOtherBrush;
            else
                brush = GetBrush(appearance.Color);

            var entityText = new FormattedText(
                appearance.Character.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                _fontSize,
                brush);

            var px = scrX * _charWidth;
            var py = scrY * _charHeight;
            context.DrawText(entityText, new Point(px, py));
        }
    }

    // ── Highlights ────────────────────────────────────────────────────────────

    private void DrawExitHighlights(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        foreach (var (x, y) in s.ExitHighlights)
        {
            var sx = x - s.CameraX;
            var sy = y - s.CameraY;
            if (sx < 0 || sx >= vpW || sy < 0 || sy >= vpH) continue;

            var text = new FormattedText(
                "X",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                _fontSize,
                AsciiPalette.ExitHighlightBrush);
            context.DrawText(text, new Point(sx * _charWidth, sy * _charHeight));
        }
    }

    private void DrawZoneEntryHighlights(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        foreach (var (x, y) in s.ZoneEntryHighlights)
        {
            var sx = x - s.CameraX;
            var sy = y - s.CameraY;
            if (sx < 0 || sx >= vpW || sy < 0 || sy >= vpH) continue;

            var text = new FormattedText(
                "▣",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                _fontSize,
                AsciiPalette.ZoneEntryHighlightBrush);
            context.DrawText(text, new Point(sx * _charWidth, sy * _charHeight));
        }
    }

    private void DrawRegionExitHighlights(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        foreach (var (x, y) in s.RegionExitHighlights)
        {
            var sx = x - s.CameraX;
            var sy = y - s.CameraY;
            if (sx < 0 || sx >= vpW || sy < 0 || sy >= vpH) continue;

            var text = new FormattedText(
                "▣",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                _fontSize,
                AsciiPalette.RegionExitHighlightBrush);
            context.DrawText(text, new Point(sx * _charWidth, sy * _charHeight));
        }
    }

    private void DrawLabels(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        foreach (var label in s.Labels)
        {
            if (label.IsHidden) continue;
            var sx = label.TileX - s.CameraX;
            var sy = label.TileY - s.CameraY;
            if (sx < -5 || sx >= vpW + 5 || sy < -1 || sy >= vpH + 1) continue;

            var text = new FormattedText(
                label.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                Math.Max(6.0, _fontSize * 0.857),
                AsciiPalette.LabelBrush);
            var px = (sx - text.Width / _charWidth / 2) * _charWidth;
            var py = (sy - 1) * _charHeight;
            context.DrawText(text, new Point(px, py));
        }
    }

    // ── Grid overlay ───────────────────────────────────────────────────────────

    /// <summary>Draws semi-transparent tile-boundary grid lines across the viewport using character cell dimensions.</summary>
    private void DrawGrid(DrawingContext context, RenderState state, int vpW, int vpH)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);

        // Vertical lines
        for (var col = 0; col <= vpW; col++)
        {
            var x = col * _charWidth;
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, state.Bounds.Height));
        }

        // Horizontal lines
        for (var row = 0; row <= vpH; row++)
        {
            var y = row * _charHeight;
            context.DrawLine(gridPen, new Point(0, y), new Point(state.Bounds.Width, y));
        }
    }

    // ── Fog of war ────────────────────────────────────────────────────────────

    private void DrawFog(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        for (var ty = s.CameraY; ty < Math.Min(s.CameraY + vpH, s.MapHeight); ty++)
        for (var tx = s.CameraX; tx < Math.Min(s.CameraX + vpW, s.MapWidth); tx++)
        {
            var idx = s.MapWidth * ty + tx;
            if (idx < 0 || idx >= s.FogMask.Length) continue;
            if (!s.FogMask[idx] || s.RevealedTiles.Contains($"{tx}:{ty}")) continue;

            var px = (tx - s.CameraX) * _charWidth;
            var py = (ty - s.CameraY) * _charHeight;
            context.FillRectangle(AsciiPalette.FogBrush, new Rect(px, py, _charWidth, _charHeight));
        }
    }

    // ── Minimap ───────────────────────────────────────────────────────────────

    private void DrawMinimap(DrawingContext context, RenderState s)
    {
        const int maxMiniSize = 200;
        const int miniPadding = 8;

        var miniScale = Math.Max(1, maxMiniSize / Math.Max(s.MapWidth, s.MapHeight));
        var miniW     = s.MapWidth  * miniScale;
        var miniH     = s.MapHeight * miniScale;
        var offsetX   = (int)s.Bounds.Width  - miniW - miniPadding;
        var offsetY   = miniPadding;

        // Background
        context.FillRectangle(
            GetBrush(AsciiPalette.MiniBg),
            new Rect(offsetX - 2, offsetY - 2, miniW + 4, miniH + 4));

        var miniFontSize = Math.Max(6, miniScale * 0.8);

        for (var ty = 0; ty < s.MapHeight; ty++)
        for (var tx = 0; tx < s.MapWidth; tx++)
        {
            var idx    = ty * s.MapWidth + tx;
            var fogged = s.FogMask.Length > 0 && idx < s.FogMask.Length
                         && s.FogMask[idx] && !s.RevealedTiles.Contains($"{tx}:{ty}");

            if (fogged && s.MapType == "zone") continue;

            var blocked = idx < s.CollisionMask.Length && s.CollisionMask[idx];
            var ch = blocked ? '#' : '.';
            var color = blocked ? AsciiPalette.MiniWall : AsciiPalette.MiniFloor;

            var text = new FormattedText(
                ch.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                miniFontSize,
                GetBrush(color));
            context.DrawText(text,
                new Point(offsetX + tx * miniScale, offsetY + ty * miniScale));
        }

        // Viewport outline
        var vpPen = new Pen(Brushes.White, 1);
        context.DrawRectangle(null, vpPen,
            new Rect(
                offsetX + s.CameraX * miniScale,
                offsetY + s.CameraY * miniScale,
                Math.Min(s.ViewportWidthTiles  * miniScale, miniW),
                Math.Min(s.ViewportHeightTiles * miniScale, miniH)));

        // Entity dots on minimap
        foreach (var entity in s.Entities)
        {
            if (entity.TileX < 0 || entity.TileX >= s.MapWidth ||
                entity.TileY < 0 || entity.TileY >= s.MapHeight) continue;

            var isSelf   = s.SelfEntityId.HasValue && entity.EntityId == s.SelfEntityId.Value;
            var dotColor = isSelf ? AsciiPalette.MiniSelfDot : AsciiPalette.MiniOtherDot;

            var dot = new FormattedText(
                "*",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                miniFontSize,
                GetBrush(dotColor));
            context.DrawText(dot,
                new Point(offsetX + entity.TileX * miniScale, offsetY + entity.TileY * miniScale));
        }
    }
}
