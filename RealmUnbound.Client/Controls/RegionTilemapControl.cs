using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Veldrath.Client.Services;
using Veldrath.Client.ViewModels;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.Controls;

/// <summary>
/// Region-map canvas. Renders the region's tile layers, player entities, zone-entry highlights,
/// region-exit highlights, and a minimap overlay using Avalonia's <see cref="DrawingContext"/>.
/// Tile scale is fixed at 3× the native 16 px tile size = 48 px display tiles.
/// Input: WASD / arrow keys trigger movement; E key confirms a pending zone-entry or region-crossing.
/// </summary>
[ExcludeFromCodeCoverage]
public class RegionTilemapControl : Control
{
    private const int DisplayTileSize = 48; // 16 px × 3

    private static readonly IBrush SelfPlayerBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255));
    private static readonly IBrush PlayerBrush     = Brushes.LimeGreen;

    // Zone-entry tile highlight (green)
    private static readonly IBrush ZoneEntryFillBrush = new SolidColorBrush(Color.FromArgb(80, 50, 205, 50));
    private static readonly IPen   ZoneEntryBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(50, 205, 50)), 2f);

    // Region-exit tile highlight (orange)
    private static readonly IBrush RegionExitFillBrush = new SolidColorBrush(Color.FromArgb(80, 255, 140, 0));
    private static readonly IPen   RegionExitBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 0)), 2f);

    // Pending tile placeholder
    private static readonly IBrush PendingBrush = new SolidColorBrush(Color.FromRgb(255, 0, 255));

    // Minimap palette
    private static readonly IBrush MiniWallBrush      = new SolidColorBrush(Color.FromRgb(30,  30,  40));
    private static readonly IBrush MiniFloorBrush     = new SolidColorBrush(Color.FromRgb(80,  80, 100));
    private static readonly IBrush MiniZoneEntryBrush = new SolidColorBrush(Color.FromRgb(50, 205,  50));
    private static readonly IBrush MiniRegionExitBrush = new SolidColorBrush(Color.FromRgb(255, 140,   0));
    private static readonly IBrush MiniPlayerBrush    = new SolidColorBrush(Color.FromRgb(30, 144, 255));
    private static readonly IBrush MiniBgBrush        = new SolidColorBrush(Color.FromArgb(210, 8, 10, 20));
    private static readonly IPen   MiniVpPen          = new Pen(Brushes.White, 1);

    // Label text typeface
    private static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    /// <summary>Styled property for the tilemap view model.</summary>
    public static readonly StyledProperty<RegionTilemapViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<RegionTilemapControl, RegionTilemapViewModel?>(nameof(ViewModel));

    /// <summary>The region tilemap view model driving this control.</summary>
    public RegionTilemapViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private readonly TileTextureCache _cache = new();
    private DispatcherTimer? _timer;

    static RegionTilemapControl()
    {
        AffectsRender<RegionTilemapControl>(ViewModelProperty);
    }

    /// <summary>Initializes a new instance of <see cref="RegionTilemapControl"/>.</summary>
    public RegionTilemapControl()
    {
        Focusable    = true;
        ClipToBounds = true;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var vm = ViewModel;
        if (vm is null) return;

        vm.ViewportWidthTiles  = Math.Max(1, (int)(Bounds.Width  / DisplayTileSize));
        vm.ViewportHeightTiles = Math.Max(1, (int)(Bounds.Height / DisplayTileSize));

        if (vm.SelfEntityId.HasValue)
        {
            var self = vm.Entities.FirstOrDefault(en => en.EntityId == vm.SelfEntityId.Value);
            if (self is not null)
                vm.CenterCameraOn(self.TileX, self.TileY);
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var vm = ViewModel;
        if (vm is null || !vm.HasMap) return;

        // ── Mini-map toggle (M) ───────────────────────────────────────────────
        if (e.Key == Key.M)
        {
            vm.ToggleMiniMapCommand.Execute(Unit.Default).Subscribe();
            e.Handled = true;
            return;
        }

        // ── Confirm zone-entry or region-crossing (E) ─────────────────────────
        if (e.Key == Key.E)
        {
            vm.ConfirmContextActionCommand.Execute(Unit.Default).Subscribe();
            e.Handled = true;
            return;
        }

        var player = vm.SelfEntityId.HasValue
            ? vm.Entities.FirstOrDefault(en => en.EntityId == vm.SelfEntityId.Value)
            : vm.Entities.FirstOrDefault(en => en.EntityType == "player");
        if (player is null) return;

        var (dx, dy, dir) = e.Key switch
        {
            Key.W or Key.Up    or Key.NumPad8 => ( 0, -1, "up"),
            Key.S or Key.Down  or Key.NumPad2 => ( 0,  1, "down"),
            Key.A or Key.Left  or Key.NumPad4 => (-1,  0, "left"),
            Key.D or Key.Right or Key.NumPad6 => ( 1,  0, "right"),
            _ => (0, 0, ""),
        };

        if (dir == string.Empty) return;
        e.Handled = true;

        var toX = player.TileX + dx;
        var toY = player.TileY + dy;

        if (vm.IsBlocked(toX, toY)) return;

        vm.RequestMoveCommand.Execute((toX, toY, dir)).Subscribe();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var vm = ViewModel;
        if (vm?.RegionMapData is null)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            var noMapText = new FormattedText(
                "Loading region map...",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                18,
                Brushes.Gray);
            context.DrawText(noMapText,
                new Point(Bounds.Width / 2 - noMapText.Width / 2, Bounds.Height / 2));
            return;
        }

        var map   = vm.RegionMapData;
        var sheet = _cache.GetSheet(map.TilesetKey);
        var camX  = vm.CameraX;
        var camY  = vm.CameraY;
        var vpW   = (int)Math.Ceiling(Bounds.Width  / DisplayTileSize) + 1;
        var vpH   = (int)Math.Ceiling(Bounds.Height / DisplayTileSize) + 1;

        // ── Tile layers ──────────────────────────────────────────────────────
        DrawTileLayers(context, map.Layers, sheet, map, camX, camY, vpW, vpH);

        // ── Zone-entry highlights (green) ────────────────────────────────────
        foreach (var entry in map.ZoneEntries)
        {
            if (!InViewport(entry.TileX, entry.TileY, camX, camY, vpW, vpH)) continue;
            var (ex, ey) = ToScreen(entry.TileX, entry.TileY, camX, camY);
            context.DrawRectangle(ZoneEntryFillBrush, ZoneEntryBorderPen,
                new Rect(ex + 1, ey + 1, DisplayTileSize - 2, DisplayTileSize - 2));
        }

        // ── Region-exit highlights (orange) ──────────────────────────────────
        foreach (var exit in map.RegionExits)
        {
            if (!InViewport(exit.TileX, exit.TileY, camX, camY, vpW, vpH)) continue;
            var (ex, ey) = ToScreen(exit.TileX, exit.TileY, camX, camY);
            context.DrawRectangle(RegionExitFillBrush, RegionExitBorderPen,
                new Rect(ex + 1, ey + 1, DisplayTileSize - 2, DisplayTileSize - 2));
        }

        // ── Player entities ──────────────────────────────────────────────────
        foreach (var entity in vm.Entities)
        {
            var (sx, sy) = ToScreen(entity.TileX, entity.TileY, camX, camY);
            if (sx < -DisplayTileSize || sx > Bounds.Width  + DisplayTileSize) continue;
            if (sy < -DisplayTileSize || sy > Bounds.Height + DisplayTileSize) continue;

            IBrush fill = vm.SelfEntityId.HasValue && entity.EntityId == vm.SelfEntityId.Value
                ? SelfPlayerBrush
                : PlayerBrush;
            context.FillRectangle(fill,
                new Rect(sx + 6, sy + 6, DisplayTileSize - 12, DisplayTileSize - 12), 4f);
        }

        // ── Zone labels ───────────────────────────────────────────────────────
        foreach (var label in map.Labels)
        {
            if (label.IsHidden) continue;
            if (!InViewport(label.TileX, label.TileY, camX, camY, vpW + 4, vpH + 2)) continue;
            var (lx, ly) = ToScreen(label.TileX, label.TileY, camX, camY);
            var ft = new FormattedText(
                label.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                11,
                Brushes.White);
            // Draw a dark shadow for readability, then the white label on top
            context.DrawText(ft, new Point(lx - ft.Width / 2 + 1, ly - 14 + 1));
            var ftLight = new FormattedText(
                label.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                11,
                Brushes.White);
            context.DrawText(ftLight, new Point(lx - ftLight.Width / 2, ly - 14));
        }

        // ── Mini-map overlay ─────────────────────────────────────────────────
        if (vm.IsMiniMapOpen)
            DrawMinimap(context, vm, map);
    }

    private static bool InViewport(int tileX, int tileY, int camX, int camY, int vpW, int vpH) =>
        tileX >= camX && tileX < camX + vpW && tileY >= camY && tileY < camY + vpH;

    private static (int screenX, int screenY) ToScreen(int tileX, int tileY, int camX, int camY) =>
        ((tileX - camX) * DisplayTileSize, (tileY - camY) * DisplayTileSize);

    private static void DrawTileLayers(
        DrawingContext context,
        IReadOnlyList<TileLayerDto> layers,
        Bitmap? sheet,
        RegionMapDto map,
        int camX, int camY,
        int vpW, int vpH)
    {
        foreach (var layer in layers.OrderBy(l => l.ZIndex))
        {
            for (var ty = camY; ty < Math.Min(camY + vpH, map.Height); ty++)
            for (var tx = camX; tx < Math.Min(camX + vpW, map.Width);  tx++)
            {
                var idx = map.Width * ty + tx;
                if (idx < 0 || idx >= layer.Data.Length) continue;

                var tileIndex = layer.Data[idx];
                if (tileIndex < -2) continue;

                var dest = new Rect(
                    (tx - camX) * DisplayTileSize,
                    (ty - camY) * DisplayTileSize,
                    DisplayTileSize, DisplayTileSize);

                if (tileIndex == -1) continue;

                if (tileIndex == -2)
                {
                    context.FillRectangle(PendingBrush, dest);
                    continue;
                }

                if (sheet is not null)
                {
                    var srcRect = TileTextureCache.GetSourceRect(map.TilesetKey, tileIndex);
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

    private void DrawMinimap(DrawingContext context, RegionTilemapViewModel vm, RegionMapDto map)
    {
        const int maxMiniSize = 200;
        const int miniPadding = 8;

        var miniScale = Math.Max(1, maxMiniSize / Math.Max(map.Width, map.Height));
        var miniW     = map.Width  * miniScale;
        var miniH     = map.Height * miniScale;
        var offsetX   = (int)Bounds.Width  - miniW - miniPadding;
        var offsetY   = miniPadding;

        context.FillRectangle(MiniBgBrush,
            new Rect(offsetX - 2, offsetY - 2, miniW + 4, miniH + 4));

        // Terrain tiles
        for (var ty = 0; ty < map.Height; ty++)
        for (var tx = 0; tx < map.Width;  tx++)
        {
            var idx     = ty * map.Width + tx;
            var blocked = idx < map.CollisionMask.Length && map.CollisionMask[idx];
            context.FillRectangle(
                blocked ? MiniWallBrush : MiniFloorBrush,
                new Rect(offsetX + tx * miniScale, offsetY + ty * miniScale, miniScale, miniScale));
        }

        // Zone-entry dots (green)
        foreach (var entry in map.ZoneEntries)
            context.FillRectangle(MiniZoneEntryBrush,
                new Rect(offsetX + entry.TileX * miniScale, offsetY + entry.TileY * miniScale,
                         Math.Max(2, miniScale), Math.Max(2, miniScale)));

        // Region-exit dots (orange)
        foreach (var exit in map.RegionExits)
            context.FillRectangle(MiniRegionExitBrush,
                new Rect(offsetX + exit.TileX * miniScale, offsetY + exit.TileY * miniScale,
                         Math.Max(2, miniScale), Math.Max(2, miniScale)));

        // Viewport outline
        context.DrawRectangle(null, MiniVpPen,
            new Rect(
                offsetX + vm.CameraX * miniScale,
                offsetY + vm.CameraY * miniScale,
                Math.Min(vm.ViewportWidthTiles  * miniScale, miniW),
                Math.Min(vm.ViewportHeightTiles * miniScale, miniH)));

        // Player dots (blue)
        foreach (var entity in vm.Entities)
        {
            if (entity.TileX < 0 || entity.TileX >= map.Width  ||
                entity.TileY < 0 || entity.TileY >= map.Height) continue;
            var dotSize = Math.Max(2, miniScale);
            context.FillRectangle(MiniPlayerBrush,
                new Rect(offsetX + entity.TileX * miniScale, offsetY + entity.TileY * miniScale,
                         dotSize, dotSize));
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        _cache.Dispose();
        base.OnDetachedFromLogicalTree(e);
    }
}
