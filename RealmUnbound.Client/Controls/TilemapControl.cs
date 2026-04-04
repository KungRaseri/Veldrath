using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Tilemap;

namespace RealmUnbound.Client.Controls;

/// <summary>
/// Real-time tile map canvas. Renders the zone's tile layers, entities, and fog of war
/// using Avalonia's <see cref="DrawingContext"/>.
/// Tile scale is fixed at 3× the native 16 px tile size = 48 px display tiles.
/// Input: WASD, arrow keys, and numpad trigger a movement request via <see cref="TilemapViewModel"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class TilemapControl : Control
{
    private const int DisplayTileSize = 48; // 16 px × 3

    private static readonly IBrush FogBrush       = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
    private static readonly IBrush SelfPlayerBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255)); // DodgerBlue — own character
    private static readonly IBrush PlayerBrush     = Brushes.LimeGreen;  // other players
    private static readonly IBrush EnemyBrush      = Brushes.OrangeRed;
    private static readonly IBrush EntityPen       = Brushes.White;

    // Avalonia styled property for the ViewModel
    /// <summary>ViewModel property for the tilemap control.</summary>
    public static readonly StyledProperty<TilemapViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<TilemapControl, TilemapViewModel?>(nameof(ViewModel));

    /// <summary>The tilemap view model driving this control.</summary>
    public TilemapViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private readonly TileTextureCache _cache = new();
    private DispatcherTimer? _timer;

    static TilemapControl()
    {
        AffectsRender<TilemapControl>(ViewModelProperty);
    }

    /// <summary>Initializes a new instance of <see cref="TilemapControl"/>.</summary>
    public TilemapControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();

        // Refresh the canvas at ~30 fps for smooth movement feel
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();
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
        vm.RequestMoveCommand.Execute((toX, toY, dir)).Subscribe();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var vm = ViewModel;
        if (vm?.TileMapData is null)
        {
            // No map loaded yet — draw a dark placeholder
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            var noMapText = new FormattedText("Loading zone map...", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, 18, Brushes.Gray);
            context.DrawText(noMapText, new Point(Bounds.Width / 2 - noMapText.Width / 2, Bounds.Height / 2));
            return;
        }

        var map       = vm.TileMapData;
        var sheet     = _cache.GetSheet(map.TilesetKey);
        var camX      = vm.CameraX;
        var camY      = vm.CameraY;
        var vpWidthTiles  = (int)Math.Ceiling(Bounds.Width  / DisplayTileSize) + 1;
        var vpHeightTiles = (int)Math.Ceiling(Bounds.Height / DisplayTileSize) + 1;

        // ── Tile layers ──────────────────────────────────────────────────────
        foreach (var layer in map.Layers)
        {
            for (var ty = camY; ty < Math.Min(camY + vpHeightTiles, map.Height); ty++)
            for (var tx = camX; tx < Math.Min(camX + vpWidthTiles,  map.Width);  tx++)
            {
                var idx = map.Width * ty + tx;
                if (idx < 0 || idx >= layer.Data.Length) continue;

                var tileIndex = layer.Data[idx];
                if (tileIndex < 0) continue; // transparent

                var destX = (tx - camX) * DisplayTileSize;
                var destY = (ty - camY) * DisplayTileSize;
                var dest  = new Rect(destX, destY, DisplayTileSize, DisplayTileSize);

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
                    // Fallback: colored debug tile
                    context.FillRectangle(Brushes.DimGray, dest);
                }
            }
        }

        // ── Entities ─────────────────────────────────────────────────────────
        foreach (var entity in vm.Entities)
        {
            var scrX = (entity.TileX - camX) * DisplayTileSize;
            var scrY = (entity.TileY - camY) * DisplayTileSize;
            if (scrX < -DisplayTileSize || scrX > Bounds.Width  + DisplayTileSize) continue;
            if (scrY < -DisplayTileSize || scrY > Bounds.Height + DisplayTileSize) continue;

            var rect = new Rect(scrX + 4, scrY + 4, DisplayTileSize - 8, DisplayTileSize - 8);
            IBrush fill;
            if (entity.EntityType == "player")
                fill = vm.SelfEntityId.HasValue && entity.EntityId == vm.SelfEntityId.Value
                    ? SelfPlayerBrush
                    : PlayerBrush;
            else
                fill = EnemyBrush;
            context.FillRectangle(fill, rect, 4f);
        }

        // ── Fog of war ────────────────────────────────────────────────────────
        if (map.FogMask.Any(f => f)) // Only applies if fog is enabled on this map
        {
            for (var ty = camY; ty < Math.Min(camY + vpHeightTiles, map.Height); ty++)
            for (var tx = camX; tx < Math.Min(camX + vpWidthTiles,  map.Width);  tx++)
            {
                var idx = map.Width * ty + tx;
                if (idx < 0 || idx >= map.FogMask.Length) continue;
                if (!map.FogMask[idx] || vm.RevealedTiles.Contains($"{tx}:{ty}")) continue;

                var destX = (tx - camX) * DisplayTileSize;
                var destY = (ty - camY) * DisplayTileSize;
                context.FillRectangle(FogBrush, new Rect(destX, destY, DisplayTileSize, DisplayTileSize));
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        _cache.Dispose();
        base.OnDetachedFromLogicalTree(e);
    }
}
