using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.Rendering;

/// <summary>
/// ASCII / roguelike text-based tilemap renderer. Renders each tile as a single
/// monospace character using <see cref="DrawingContext.DrawText"/>. Entity positions
/// are rendered as coloured characters (<c>@</c> for players, <c>E</c> for enemies).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AsciiMapRenderer : IMapRenderer
{
    private const double FontSize = 14.0;
    private static readonly Typeface AsciiTypeface =
        new("JetBrains Mono, Cascadia Code, Consolas, Courier New", FontStyle.Normal, FontWeight.Normal);

    private readonly double _charWidth;
    private readonly double _charHeight;

    // ── Entity colours ──────────────────────────────────────────────────────

    private static readonly IBrush SelfPlayerBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212));  // cyan
    private static readonly IBrush OtherPlayerBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128)); // green
    private static readonly IBrush EnemyBrush       = new SolidColorBrush(Color.FromRgb(239, 68, 68));  // red

    // ── Fog ──────────────────────────────────────────────────────────────────

    private static readonly IBrush FogBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)); // dark slate

    // ── Highlight colours ────────────────────────────────────────────────────

    private static readonly IBrush ExitHighlightBrush       = new SolidColorBrush(Color.FromRgb(250, 204, 21)); // yellow
    private static readonly IBrush ZoneEntryHighlightBrush  = new SolidColorBrush(Color.FromRgb(34, 197, 94));  // green
    private static readonly IBrush RegionExitHighlightBrush = new SolidColorBrush(Color.FromRgb(249, 115, 22)); // orange

    // ── Label brush ──────────────────────────────────────────────────────────

    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // slate 300

    /// <summary>Initializes a new instance of <see cref="AsciiMapRenderer"/>.</summary>
    public AsciiMapRenderer()
    {
        // Measure a single character to determine the display tile size.
        var measureText = new FormattedText(
            "W",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            AsciiTypeface,
            FontSize,
            Brushes.White);
        _charWidth  = measureText.Width;
        _charHeight = measureText.Height;
    }

    /// <inheritdoc/>
    public int DisplayTileSize => (int)Math.Max(_charWidth, _charHeight);

    /// <inheritdoc/>
    public void Render(DrawingContext context, RenderState state)
    {
        var vpW = (int)Math.Ceiling(state.Bounds.Width  / _charWidth) + 1;
        var vpH = (int)Math.Ceiling(state.Bounds.Height / _charHeight) + 1;

        // Dark background
        context.FillRectangle(Brushes.Black, new Rect(state.Bounds));

        // Build and draw tile rows
        DrawTileRows(context, state, vpW, vpH);

        // Draw entities on top
        DrawEntities(context, state, vpW, vpH);

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

    // ── Tile rows ──────────────────────────────────────────────────────────

    private void DrawTileRows(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        var sb = new StringBuilder(vpW);
        var darkBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // default tile colour

        for (var ty = s.CameraY; ty < Math.Min(s.CameraY + vpH, s.MapHeight); ty++)
        {
            sb.Clear();
            for (var tx = s.CameraX; tx < Math.Min(s.CameraX + vpW, s.MapWidth); tx++)
            {
                var idx = s.MapWidth * ty + tx;
                var tileIndex = CompositeTileIndex(s.Layers, idx);
                sb.Append(TileAsciiMap.GetChar(tileIndex));
            }

            if (sb.Length == 0) continue;

            var rowText = new FormattedText(
                sb.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                FontSize,
                darkBrush);

            var x = 0.0;
            var y = (ty - s.CameraY) * _charHeight;
            context.DrawText(rowText, new Point(x, y));
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

    // ── Entity drawing ─────────────────────────────────────────────────────

    private void DrawEntities(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        foreach (var entity in s.Entities)
        {
            var scrX = (entity.TileX - s.CameraX);
            var scrY = (entity.TileY - s.CameraY);
            if (scrX < 0 || scrX >= vpW) continue;
            if (scrY < 0 || scrY >= vpH) continue;

            var isSelf = s.SelfEntityId.HasValue && entity.EntityId == s.SelfEntityId.Value;
            var (ch, brush) = entity.EntityType switch
            {
                "player" => ('@', isSelf ? SelfPlayerBrush : OtherPlayerBrush),
                _        => ('E', EnemyBrush),
            };

            var entityText = new FormattedText(
                ch.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                AsciiTypeface,
                FontSize,
                brush);

            var px = scrX * _charWidth;
            var py = scrY * _charHeight;
            context.DrawText(entityText, new Point(px, py));
        }
    }

    // ── Highlights ─────────────────────────────────────────────────────────

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
                FontSize,
                ExitHighlightBrush);
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
                FontSize,
                ZoneEntryHighlightBrush);
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
                FontSize,
                RegionExitHighlightBrush);
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
                FontSize - 2,
                LabelBrush);
            var px = (sx - text.Width / _charWidth / 2) * _charWidth;
            var py = (sy - 1) * _charHeight;
            context.DrawText(text, new Point(px, py));
        }
    }

    // ── Fog of war ─────────────────────────────────────────────────────────

    private void DrawFog(DrawingContext context, RenderState s, int vpW, int vpH)
    {
        var fogChar = new FormattedText(
            " ",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            AsciiTypeface,
            FontSize,
            Brushes.Black);

        for (var ty = s.CameraY; ty < Math.Min(s.CameraY + vpH, s.MapHeight); ty++)
        for (var tx = s.CameraX; tx < Math.Min(s.CameraX + vpW, s.MapWidth); tx++)
        {
            var idx = s.MapWidth * ty + tx;
            if (idx < 0 || idx >= s.FogMask.Length) continue;
            if (!s.FogMask[idx] || s.RevealedTiles.Contains($"{tx}:{ty}")) continue;

            var px = (tx - s.CameraX) * _charWidth;
            var py = (ty - s.CameraY) * _charHeight;
            context.FillRectangle(FogBrush, new Rect(px, py, _charWidth, _charHeight));
        }
    }

    // ── Minimap ────────────────────────────────────────────────────────────

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
        var miniBg = new SolidColorBrush(Color.FromArgb(210, 8, 10, 20));
        context.FillRectangle(miniBg,
            new Rect(offsetX - 2, offsetY - 2, miniW + 4, miniH + 4));

        var miniFontSize = Math.Max(6, miniScale * 0.8);
        var miniTypeface = AsciiTypeface;

        for (var ty = 0; ty < s.MapHeight; ty++)
        for (var tx = 0; tx < s.MapWidth; tx++)
        {
            var idx    = ty * s.MapWidth + tx;
            var fogged = s.FogMask.Length > 0 && idx < s.FogMask.Length
                         && s.FogMask[idx] && !s.RevealedTiles.Contains($"{tx}:{ty}");

            if (fogged && s.MapType == "zone") continue;

            var blocked = idx < s.CollisionMask.Length && s.CollisionMask[idx];
            var ch = blocked ? '#' : '.';
            var color = blocked
                ? new SolidColorBrush(Color.FromRgb(30, 30, 40))
                : new SolidColorBrush(Color.FromRgb(80, 80, 100));

            var text = new FormattedText(
                ch.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                miniTypeface,
                miniFontSize,
                color);
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

            var isSelf = s.SelfEntityId.HasValue && entity.EntityId == s.SelfEntityId.Value;
            var dotColor = isSelf
                ? new SolidColorBrush(Color.FromRgb(6, 182, 212))
                : new SolidColorBrush(Color.FromRgb(30, 144, 255));

            var dot = new FormattedText(
                "*",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                miniTypeface,
                miniFontSize,
                dotColor);
            context.DrawText(dot,
                new Point(offsetX + entity.TileX * miniScale, offsetY + entity.TileY * miniScale));
        }
    }
}
