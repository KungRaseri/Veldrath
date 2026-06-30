using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Veldrath.Assets.Manifest;

namespace Veldrath.Client.Rendering;

/// <summary>
/// Sprite-based tilemap renderer. Draws tile layers from spritesheets, entity sprites,
/// fog of war, minimap overlay, and map-specific highlights (exits, labels).
/// Handles both zone maps (<c>MapType = "zone"</c>) and region maps (<c>MapType = "region"</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class SpriteMapRenderer : IMapRenderer
{
    /// <summary>Current tile size in pixels. Updated by <see cref="TileSize"/> setter.</summary>
    private int _tileSize = 48;

    /// <inheritdoc/>
    public int DisplayTileSize => _tileSize;

    /// <inheritdoc/>
    public double TileSize
    {
        get => _tileSize;
        set => _tileSize = (int)Math.Clamp(value, 16.0, 128.0);
    }

    // ── Zone map brushes / pens ──────────────────────────────────────────────

    private static readonly IBrush FogBrush       = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
    private static readonly IBrush SelfPlayerBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255)); // DodgerBlue
    private static readonly IBrush PlayerBrush     = Brushes.LimeGreen;
    private static readonly IBrush EnemyBrush      = Brushes.OrangeRed;
    private static readonly IBrush EntityPen       = Brushes.White;

    private static readonly IBrush ExitFillBrush  = new SolidColorBrush(Color.FromArgb(80, 255, 220, 0));
    private static readonly IPen   ExitBorderPen  = new Pen(Brushes.Yellow, 2f);

    // ── Region map brushes / pens ────────────────────────────────────────────

    private static readonly IBrush ZoneEntryFillBrush = new SolidColorBrush(Color.FromArgb(80, 50, 205, 50));
    private static readonly IPen   ZoneEntryBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 205, 50)), 2f);

    private static readonly IBrush RegionExitFillBrush = new SolidColorBrush(Color.FromArgb(80, 255, 140, 0));
    private static readonly IPen   RegionExitBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 0)), 2f);

    private static readonly IBrush PendingBrush = new SolidColorBrush(Color.FromRgb(255, 0, 255));

    // ── Minimap brushes / pens ───────────────────────────────────────────────
    // Zone minimap
    private static readonly IBrush MiniWallBrush   = new SolidColorBrush(Color.FromRgb(30,  30,  40));
    private static readonly IBrush MiniFloorBrush  = new SolidColorBrush(Color.FromRgb(80,  80, 100));
    private static readonly IBrush MiniExitBrush   = new SolidColorBrush(Color.FromRgb(255, 220,   0));
    private static readonly IBrush MiniBgBrush     = new SolidColorBrush(Color.FromArgb(210,   8,  10,  20));
    private static readonly IBrush MiniFogBrush    = new SolidColorBrush(Color.FromRgb(20,  20,  28));
    private static readonly IPen   MiniVpPen       = new Pen(Brushes.White, 1);

    // Region minimap
    private static readonly IBrush MiniZoneEntryBrush  = new SolidColorBrush(Color.FromRgb(50, 205,  50));
    private static readonly IBrush MiniRegionExitBrush = new SolidColorBrush(Color.FromRgb(255, 140,   0));
    private static readonly IBrush MiniPlayerBrush     = new SolidColorBrush(Color.FromRgb(30, 144, 255));

    // ── Label typeface ────────────────────────────────────────────────────────

    private static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    // ── Caches ────────────────────────────────────────────────────────────────

    private readonly TileTextureCache _tileCache;
    private readonly EntityTextureCache _entityCache;

    /// <summary>Initializes a new instance of <see cref="SpriteMapRenderer"/>.</summary>
    public SpriteMapRenderer(TileTextureCache tileCache, EntityTextureCache entityCache)
    {
        _tileCache = tileCache;
        _entityCache = entityCache;
    }

    /// <inheritdoc/>
    public void Render(DrawingContext context, RenderState state)
    {
        var sheet = _tileCache.GetSheet(state.TilesetKey);
        var vpW  = (int)Math.Ceiling(state.Bounds.Width  / (double)DisplayTileSize) + 1;
        var vpH  = (int)Math.Ceiling(state.Bounds.Height / (double)DisplayTileSize) + 1;

        if (state.MapType == "zone")
            RenderZone(context, state, sheet, vpW, vpH);
        else
            RenderRegion(context, state, sheet, vpW, vpH);
    }

    // ── Zone rendering ──────────────────────────────────────────────────────

    private void RenderZone(DrawingContext context, RenderState s, Bitmap? sheet, int vpW, int vpH)
    {
        // Tile layers below entities (ZIndex < EntityZIndex)
        DrawTileLayers(context, s.Layers.Where(l => l.ZIndex < EntityZIndex), sheet, s, vpW, vpH);

        // Exit tile highlights
        foreach (var (x, y) in s.ExitHighlights)
        {
            if (!InViewport(x, y, s.CameraX, s.CameraY, vpW, vpH)) continue;
            var (ex, ey) = ToScreen(x, y, s.CameraX, s.CameraY);
            context.DrawRectangle(ExitFillBrush, ExitBorderPen,
                new Rect(ex + 1, ey + 1, DisplayTileSize - 2, DisplayTileSize - 2));
        }

        // Entities
        DrawEntities(context, s, withSprites: true);

        // Tile layers above entities (ZIndex >= EntityZIndex)
        DrawTileLayers(context, s.Layers.Where(l => l.ZIndex >= EntityZIndex), sheet, s, vpW, vpH);

        // Grid overlay (tile-boundary lines)
        if (s.ShowGrid)
            DrawGrid(context, s);

        // Fog of war
        if (s.FogMask.Any(f => f))
        {
            for (var ty = s.CameraY; ty < Math.Min(s.CameraY + vpH, s.MapHeight); ty++)
            for (var tx = s.CameraX; tx < Math.Min(s.CameraX + vpW, s.MapWidth);  tx++)
            {
                var idx = s.MapWidth * ty + tx;
                if (idx < 0 || idx >= s.FogMask.Length) continue;
                if (!s.FogMask[idx] || s.RevealedTiles.Contains($"{tx}:{ty}")) continue;

                var destX = (tx - s.CameraX) * DisplayTileSize;
                var destY = (ty - s.CameraY) * DisplayTileSize;
                context.FillRectangle(FogBrush, new Rect(destX, destY, DisplayTileSize, DisplayTileSize));
            }
        }

        // Minimap
        if (s.IsMiniMapOpen)
            DrawZoneMinimap(context, s);
    }

    // ── Region rendering ────────────────────────────────────────────────────

    private void RenderRegion(DrawingContext context, RenderState s, Bitmap? sheet, int vpW, int vpH)
    {
        // Tile layers
        DrawTileLayers(context, s.Layers.OrderBy(l => l.ZIndex), sheet, s, vpW, vpH);

        // Zone-entry highlights (green)
        foreach (var (x, y) in s.ZoneEntryHighlights)
        {
            if (!InViewport(x, y, s.CameraX, s.CameraY, vpW, vpH)) continue;
            var (ex, ey) = ToScreen(x, y, s.CameraX, s.CameraY);
            context.DrawRectangle(ZoneEntryFillBrush, ZoneEntryBorderPen,
                new Rect(ex + 1, ey + 1, DisplayTileSize - 2, DisplayTileSize - 2));
        }

        // Region-exit highlights (orange)
        foreach (var (x, y) in s.RegionExitHighlights)
        {
            if (!InViewport(x, y, s.CameraX, s.CameraY, vpW, vpH)) continue;
            var (ex, ey) = ToScreen(x, y, s.CameraX, s.CameraY);
            context.DrawRectangle(RegionExitFillBrush, RegionExitBorderPen,
                new Rect(ex + 1, ey + 1, DisplayTileSize - 2, DisplayTileSize - 2));
        }

        // Player entities
        DrawEntities(context, s, withSprites: false);

        // Grid overlay (tile-boundary lines)
        if (s.ShowGrid)
            DrawGrid(context, s);

        // Zone labels
        foreach (var label in s.Labels)
        {
            if (label.IsHidden) continue;
            if (!InViewport(label.TileX, label.TileY, s.CameraX, s.CameraY, vpW + 4, vpH + 2)) continue;
            var (lx, ly) = ToScreen(label.TileX, label.TileY, s.CameraX, s.CameraY);
            var ft = new FormattedText(
                label.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                11,
                Brushes.White);
            // Shadow for readability
            context.DrawText(ft, new Point(lx - ft.Width / 2 + 1, ly - 14 + 1));
            context.DrawText(ft, new Point(lx - ft.Width / 2, ly - 14));
        }

        // Minimap
        if (s.IsMiniMapOpen)
            DrawRegionMinimap(context, s);
    }

    // ── Shared tile layer drawing ──────────────────────────────────────────

    /// <summary>
    /// Layers with <see cref="Contracts.Tilemap.TileLayerDto.ZIndex"/> below this value draw under entities;
    /// layers at or above this value draw over entities (roofs, canopies).
    /// </summary>
    private const int EntityZIndex = 2;

    private void DrawTileLayers(
        DrawingContext context,
        IEnumerable<global::Veldrath.Contracts.Tilemap.TileLayerDto> layers,
        Bitmap? sheet,
        RenderState s,
        int vpW, int vpH)
    {
        foreach (var layer in layers)
        {
            for (var ty = s.CameraY; ty < Math.Min(s.CameraY + vpH, s.MapHeight); ty++)
            for (var tx = s.CameraX; tx < Math.Min(s.CameraX + vpW, s.MapWidth);  tx++)
            {
                var idx = s.MapWidth * ty + tx;
                if (idx < 0 || idx >= layer.Data.Length) continue;

                var tileIndex = layer.Data[idx];
                if (tileIndex < -2) continue;

                var dest = new Rect(
                    (tx - s.CameraX) * DisplayTileSize,
                    (ty - s.CameraY) * DisplayTileSize,
                    DisplayTileSize, DisplayTileSize);

                if (tileIndex == -1) continue;

                if (tileIndex == -2)
                {
                    context.FillRectangle(PendingBrush, dest);
                    continue;
                }

                if (sheet is not null)
                {
                    var srcRect = TileTextureCache.GetSourceRect(s.TilesetKey, tileIndex);
                    if (srcRect.HasValue)
                        context.DrawImage(sheet, srcRect.Value, dest);
                    else
                        context.FillRectangle(Brushes.DimGray, dest);
                }
                else
                {
                    context.FillRectangle(Brushes.DimGray, dest);
                }
            }
        }
    }

    // ── Entity drawing ─────────────────────────────────────────────────────

    private void DrawEntities(DrawingContext context, RenderState s, bool withSprites)
    {
        foreach (var entity in s.Entities)
        {
            var scrX = (entity.TileX - s.CameraX) * DisplayTileSize;
            var scrY = (entity.TileY - s.CameraY) * DisplayTileSize;
            if (scrX < -DisplayTileSize || scrX > s.Bounds.Width  + DisplayTileSize) continue;
            if (scrY < -DisplayTileSize || scrY > s.Bounds.Height + DisplayTileSize) continue;

            if (withSprites)
            {
                // Try sprite sheet; fall back to coloured box when no sheet is registered.
                var spriteSheet = _entityCache.GetSheet(entity.SpriteKey);
                var srcRect     = EntityTextureCache.GetSourceRect(entity.SpriteKey, entity.Direction);

                if (spriteSheet is not null && srcRect.HasValue)
                {
                    var si    = EntitySpriteAssets.All[entity.SpriteKey];
                    var scale = (double)DisplayTileSize / si.FrameWidth;
                    var dw    = DisplayTileSize;
                    var dh    = (int)(si.FrameHeight * scale);
                    var dy    = scrY + DisplayTileSize - dh; // bottom-align in tile
                    context.DrawImage(spriteSheet, srcRect.Value, new Rect(scrX, dy, dw, dh));
                    continue;
                }
            }

            // Coloured box fallback
            IBrush fill = entity.EntityType switch
            {
                "player" => s.SelfEntityId.HasValue && entity.EntityId == s.SelfEntityId.Value
                    ? SelfPlayerBrush : PlayerBrush,
                _ => EnemyBrush,
            };

            if (withSprites)
            {
                context.FillRectangle(fill,
                    new Rect(scrX + 4, scrY + 4, DisplayTileSize - 8, DisplayTileSize - 8), 4f);
            }
            else
            {
                context.FillRectangle(fill,
                    new Rect(scrX + 6, scrY + 6, DisplayTileSize - 12, DisplayTileSize - 12), 4f);
            }
        }
    }

    // ── Zone minimap ───────────────────────────────────────────────────────

    private void DrawZoneMinimap(DrawingContext context, RenderState s)
    {
        const int maxMiniSize = 200;
        const int miniPadding = 8;

        var miniScale = Math.Max(1, maxMiniSize / Math.Max(s.MapWidth, s.MapHeight));
        var miniW     = s.MapWidth  * miniScale;
        var miniH     = s.MapHeight * miniScale;
        var offsetX   = (int)s.Bounds.Width  - miniW - miniPadding;
        var offsetY   = miniPadding;

        context.FillRectangle(MiniBgBrush,
            new Rect(offsetX - 2, offsetY - 2, miniW + 4, miniH + 4));

        for (var ty = 0; ty < s.MapHeight; ty++)
        for (var tx = 0; tx < s.MapWidth;  tx++)
        {
            var idx    = ty * s.MapWidth + tx;
            var fogged = idx < s.FogMask.Length && s.FogMask[idx] && !s.RevealedTiles.Contains($"{tx}:{ty}");
            IBrush tile;
            if (fogged)
                tile = MiniFogBrush;
            else
            {
                var blocked = idx < s.CollisionMask.Length && s.CollisionMask[idx];
                tile = blocked ? MiniWallBrush : MiniFloorBrush;
            }
            context.FillRectangle(tile,
                new Rect(offsetX + tx * miniScale, offsetY + ty * miniScale, miniScale, miniScale));
        }

        // Exit tiles (yellow)
        foreach (var (x, y) in s.ExitHighlights)
        {
            var exitIdx = y * s.MapWidth + x;
            var exitFogged = exitIdx < s.FogMask.Length && s.FogMask[exitIdx] && !s.RevealedTiles.Contains($"{x}:{y}");
            if (!exitFogged)
                context.FillRectangle(MiniExitBrush,
                    new Rect(offsetX + x * miniScale, offsetY + y * miniScale,
                             miniScale, miniScale));
        }

        // Viewport outline
        context.DrawRectangle(null, MiniVpPen,
            new Rect(
                offsetX + s.CameraX * miniScale,
                offsetY + s.CameraY * miniScale,
                Math.Min(s.ViewportWidthTiles  * miniScale, miniW),
                Math.Min(s.ViewportHeightTiles * miniScale, miniH)));

        // Entity dots
        foreach (var entity in s.Entities)
        {
            if (entity.TileX < 0 || entity.TileX >= s.MapWidth  ||
                entity.TileY < 0 || entity.TileY >= s.MapHeight) continue;

            IBrush dot;
            if (entity.EntityType == "player")
                dot = s.SelfEntityId.HasValue && entity.EntityId == s.SelfEntityId.Value
                    ? SelfPlayerBrush : PlayerBrush;
            else
                dot = EnemyBrush;

            var dotSize = Math.Max(2, miniScale);
            context.FillRectangle(dot,
                new Rect(offsetX + entity.TileX * miniScale, offsetY + entity.TileY * miniScale,
                         dotSize, dotSize));
        }
    }

    // ── Region minimap ─────────────────────────────────────────────────────

    private void DrawRegionMinimap(DrawingContext context, RenderState s)
    {
        const int maxMiniSize = 200;
        const int miniPadding = 8;

        var miniScale = Math.Max(1, maxMiniSize / Math.Max(s.MapWidth, s.MapHeight));
        var miniW     = s.MapWidth  * miniScale;
        var miniH     = s.MapHeight * miniScale;
        var offsetX   = (int)s.Bounds.Width  - miniW - miniPadding;
        var offsetY   = miniPadding;

        context.FillRectangle(MiniBgBrush,
            new Rect(offsetX - 2, offsetY - 2, miniW + 4, miniH + 4));

        // Terrain tiles
        for (var ty = 0; ty < s.MapHeight; ty++)
        for (var tx = 0; tx < s.MapWidth;  tx++)
        {
            var idx     = ty * s.MapWidth + tx;
            var blocked = idx < s.CollisionMask.Length && s.CollisionMask[idx];
            context.FillRectangle(
                blocked ? MiniWallBrush : MiniFloorBrush,
                new Rect(offsetX + tx * miniScale, offsetY + ty * miniScale, miniScale, miniScale));
        }

        // Zone-entry dots (green)
        foreach (var (x, y) in s.ZoneEntryHighlights)
            context.FillRectangle(MiniZoneEntryBrush,
                new Rect(offsetX + x * miniScale, offsetY + y * miniScale,
                         Math.Max(2, miniScale), Math.Max(2, miniScale)));

        // Region-exit dots (orange)
        foreach (var (x, y) in s.RegionExitHighlights)
            context.FillRectangle(MiniRegionExitBrush,
                new Rect(offsetX + x * miniScale, offsetY + y * miniScale,
                         Math.Max(2, miniScale), Math.Max(2, miniScale)));

        // Viewport outline
        context.DrawRectangle(null, MiniVpPen,
            new Rect(
                offsetX + s.CameraX * miniScale,
                offsetY + s.CameraY * miniScale,
                Math.Min(s.ViewportWidthTiles  * miniScale, miniW),
                Math.Min(s.ViewportHeightTiles * miniScale, miniH)));

        // Entity dots
        foreach (var entity in s.Entities)
        {
            if (entity.TileX < 0 || entity.TileX >= s.MapWidth  ||
                entity.TileY < 0 || entity.TileY >= s.MapHeight) continue;
            var dotSize = Math.Max(2, miniScale);
            context.FillRectangle(MiniPlayerBrush,
                new Rect(offsetX + entity.TileX * miniScale, offsetY + entity.TileY * miniScale,
                         dotSize, dotSize));
        }
    }

    // ── Grid overlay ─────────────────────────────────────────────────────────

    /// <summary>Draws semi-transparent tile-boundary grid lines across the entire viewport.</summary>
    private void DrawGrid(DrawingContext context, RenderState state)
    {
        var tileSize = DisplayTileSize;
        var viewWidth = state.Bounds.Width;
        var viewHeight = state.Bounds.Height;

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);

        // Vertical lines
        for (var x = 0.0; x <= viewWidth; x += tileSize)
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, viewHeight));

        // Horizontal lines
        for (var y = 0.0; y <= viewHeight; y += tileSize)
            context.DrawLine(gridPen, new Point(0, y), new Point(viewWidth, y));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool InViewport(int tileX, int tileY, int camX, int camY, int vpW, int vpH) =>
        tileX >= camX && tileX < camX + vpW && tileY >= camY && tileY < camY + vpH;

    private (int screenX, int screenY) ToScreen(int tileX, int tileY, int camX, int camY) =>
        ((tileX - camX) * _tileSize, (tileY - camY) * _tileSize);
}
