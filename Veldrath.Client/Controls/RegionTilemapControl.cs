using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Client.Rendering;
using Veldrath.Client.ViewModels;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.Controls;

/// <summary>
/// Region-map canvas. Delegates rendering to an <see cref="IMapRenderer"/>.
/// Input: WASD / arrow keys trigger movement; E key confirms a pending zone-entry or region-crossing.
/// </summary>
[ExcludeFromCodeCoverage]
public class RegionTilemapControl : Control
{
    /// <summary>Styled property for the tilemap view model.</summary>
    public static readonly StyledProperty<RegionTilemapViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<RegionTilemapControl, RegionTilemapViewModel?>(nameof(ViewModel));

    /// <summary>The region tilemap view model driving this control.</summary>
    public RegionTilemapViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private readonly MapRendererResolver? _rendererResolver;
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
        _rendererResolver = App.Services?.GetService<MapRendererResolver>();
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

        var displayTileSize = _rendererResolver?.Current.DisplayTileSize ?? 48;
        vm.ViewportWidthTiles  = Math.Max(1, (int)(Bounds.Width  / displayTileSize));
        vm.ViewportHeightTiles = Math.Max(1, (int)(Bounds.Height / displayTileSize));

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

        if (_rendererResolver is null) return;
        var state = BuildRenderState(vm);
        _rendererResolver.Current.Render(context, state);
    }

    private RenderState BuildRenderState(RegionTilemapViewModel vm)
    {
        var map = vm.RegionMapData!;
        return new RenderState(
            Bounds: Bounds.Size,
            CameraX: vm.CameraX, CameraY: vm.CameraY,
            ViewportWidthTiles: vm.ViewportWidthTiles,
            ViewportHeightTiles: vm.ViewportHeightTiles,
            MapWidth: map.Width, MapHeight: map.Height,
            Layers: map.Layers,
            CollisionMask: map.CollisionMask,
            FogMask: [],
            RevealedTiles: vm.RevealedTiles,
            ExitHighlights: [],
            ZoneEntryHighlights: map.ZoneEntries.Select(z => (z.TileX, z.TileY)).ToList(),
            RegionExitHighlights: map.RegionExits.Select(r => (r.TileX, r.TileY)).ToList(),
            Entities: vm.Entities.Select(e => new RenderEntity(
                e.EntityId, e.EntityType, e.SpriteKey, e.TileX, e.TileY, e.Direction)).ToList(),
            SelfEntityId: vm.SelfEntityId,
            Labels: map.Labels.Select(l => new RenderLabel(l.TileX, l.TileY, l.Text, l.IsHidden)).ToList(),
            IsMiniMapOpen: vm.IsMiniMapOpen,
            TilesetKey: map.TilesetKey,
            MapType: "region");
    }
}
