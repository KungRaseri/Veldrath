using System.Diagnostics.CodeAnalysis;
using System.Reactive;
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

    // Exit tile highlight (semi-transparent yellow fill + solid border)
    private static readonly IBrush ExitFillBrush  = new SolidColorBrush(Color.FromArgb(80, 255, 220, 0));
    private static readonly IPen   ExitBorderPen  = new Pen(Brushes.Yellow, 2f);

    // Minimap palette — pre-allocated so no brush is created per-frame
    private static readonly IBrush MiniWallBrush  = new SolidColorBrush(Color.FromRgb(30,  30,  40));
    private static readonly IBrush MiniFloorBrush = new SolidColorBrush(Color.FromRgb(80,  80, 100));
    private static readonly IBrush MiniExitBrush  = new SolidColorBrush(Color.FromRgb(255, 220,   0));
    private static readonly IBrush MiniBgBrush    = new SolidColorBrush(Color.FromArgb(210,   8,  10,  20));
    private static readonly IBrush MiniFogBrush   = new SolidColorBrush(Color.FromRgb(20,  20,  28));
    private static readonly IPen   MiniVpPen      = new Pen(Brushes.White, 1);

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

    private readonly TileTextureCache   _cache        = new();
    private readonly EntityTextureCache _entityCache  = new();
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
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var vm = ViewModel;
        if (vm is null) return;

        // Compute how many tiles fit in the new bounds and push into the VM so that
        // CenterCameraOn() always has correct, window-size-aware viewport dimensions.
        vm.ViewportWidthTiles  = Math.Max(1, (int)(Bounds.Width  / DisplayTileSize));
        vm.ViewportHeightTiles = Math.Max(1, (int)(Bounds.Height / DisplayTileSize));

        // Re-centre immediately so the camera adjusts on window resize without waiting
        // for the next character move.
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

        // ── Client-side collision pre-check ───────────────────────────────────
        // Skips the server round-trip when the tile is statically blocked.
        // The server still validates as the authoritative fallback.
        if (vm.IsBlocked(toX, toY)) return;

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

        // ── Exit tile highlights ─────────────────────────────────────────────
        // Tinted fill + yellow border on every exit tile within the viewport
        // so the player can see where zone transitions are.
        foreach (var exit in map.ExitTiles)
        {
            if (exit.TileX < camX || exit.TileX >= camX + vpWidthTiles)  continue;
            if (exit.TileY < camY || exit.TileY >= camY + vpHeightTiles) continue;
            var ex = (exit.TileX - camX) * DisplayTileSize;
            var ey = (exit.TileY - camY) * DisplayTileSize;
            context.DrawRectangle(ExitFillBrush, ExitBorderPen,
                new Rect(ex + 1, ey + 1, DisplayTileSize - 2, DisplayTileSize - 2));
        }

        // ── Entities ─────────────────────────────────────────────────────────
        foreach (var entity in vm.Entities)
        {
            var scrX = (entity.TileX - camX) * DisplayTileSize;
            var scrY = (entity.TileY - camY) * DisplayTileSize;
            if (scrX < -DisplayTileSize || scrX > Bounds.Width  + DisplayTileSize) continue;
            if (scrY < -DisplayTileSize || scrY > Bounds.Height + DisplayTileSize) continue;

            // Try sprite sheet first; fall back to coloured box when no sheet is registered.
            var spriteSheet = _entityCache.GetSheet(entity.SpriteKey);
            var srcRect     = EntityTextureCache.GetSourceRect(entity.SpriteKey, entity.Direction);

            if (spriteSheet is not null && srcRect.HasValue)
            {
                // Draw sprite scaled into the tile.  Bottom-align: sprites taller than the
                // tile (e.g. 48×64 monsters) extend above the tile top for a natural look.
                var si    = RealmUnbound.Assets.Manifest.EntitySpriteAssets.All[entity.SpriteKey];
                var scale = (double)DisplayTileSize / si.FrameWidth;  // scale to fill tile width
                var dw    = DisplayTileSize;
                var dh    = (int)(si.FrameHeight * scale);
                var dy    = scrY + DisplayTileSize - dh;               // bottom-align in tile
                context.DrawImage(spriteSheet, srcRect.Value, new Rect(scrX, dy, dw, dh));
            }
            else
            {
                // Coloured box fallback — visible whenever a sprite key has no registered sheet.
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

        // ── Mini-map overlay ─────────────────────────────────────────────────
        // Rendered on top of everything; toggled with M or the footer button.
        if (vm.IsMiniMapOpen)
            DrawMinimap(context, vm, map);
    }

    private void DrawMinimap(DrawingContext context, TilemapViewModel vm, TileMapDto map)
    {
        const int maxMiniSize = 200;
        const int miniPadding = 8;

        var miniScale = Math.Max(1, maxMiniSize / Math.Max(map.Width, map.Height));
        var miniW     = map.Width  * miniScale;
        var miniH     = map.Height * miniScale;
        var offsetX   = (int)Bounds.Width  - miniW - miniPadding;
        var offsetY   = miniPadding;

        // Tinted background panel
        context.FillRectangle(MiniBgBrush,
            new Rect(offsetX - 2, offsetY - 2, miniW + 4, miniH + 4));

        // Terrain: blocked = dark wall colour, open = lighter floor colour; fogged tiles are hidden
        for (var ty = 0; ty < map.Height; ty++)
        for (var tx = 0; tx < map.Width;  tx++)
        {
            var idx     = ty * map.Width + tx;
            var fogged  = idx < map.FogMask.Length && map.FogMask[idx] && !vm.RevealedTiles.Contains($"{tx}:{ty}");
            IBrush tile;
            if (fogged)
                tile = MiniFogBrush;
            else
            {
                var blocked = idx < map.CollisionMask.Length && map.CollisionMask[idx];
                tile = blocked ? MiniWallBrush : MiniFloorBrush;
            }
            context.FillRectangle(tile,
                new Rect(offsetX + tx * miniScale, offsetY + ty * miniScale, miniScale, miniScale));
        }

        // Exit tiles (yellow) — only shown for revealed tiles
        foreach (var exit in map.ExitTiles)
        {
            var exitIdx = exit.TileY * map.Width + exit.TileX;
            var exitFogged = exitIdx < map.FogMask.Length && map.FogMask[exitIdx] && !vm.RevealedTiles.Contains($"{exit.TileX}:{exit.TileY}");
            if (!exitFogged)
                context.FillRectangle(MiniExitBrush,
                    new Rect(offsetX + exit.TileX * miniScale, offsetY + exit.TileY * miniScale,
                             miniScale, miniScale));
        }

        // Viewport rectangle (white outline shows what's on screen)
        context.DrawRectangle(null, MiniVpPen,
            new Rect(
                offsetX + vm.CameraX * miniScale,
                offsetY + vm.CameraY * miniScale,
                Math.Min(vm.ViewportWidthTiles  * miniScale, miniW),
                Math.Min(vm.ViewportHeightTiles * miniScale, miniH)));

        // Entity dots
        foreach (var entity in vm.Entities)
        {
            if (entity.TileX < 0 || entity.TileX >= map.Width  ||
                entity.TileY < 0 || entity.TileY >= map.Height) continue;

            IBrush dot;
            if (entity.EntityType == "player")
                dot = vm.SelfEntityId.HasValue && entity.EntityId == vm.SelfEntityId.Value
                    ? SelfPlayerBrush : PlayerBrush;
            else
                dot = EnemyBrush;

            var dotSize = Math.Max(2, miniScale);
            context.FillRectangle(dot,
                new Rect(offsetX + entity.TileX * miniScale, offsetY + entity.TileY * miniScale,
                         dotSize, dotSize));
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        _cache.Dispose();
        _entityCache.Dispose();
        base.OnDetachedFromLogicalTree(e);
    }
}
