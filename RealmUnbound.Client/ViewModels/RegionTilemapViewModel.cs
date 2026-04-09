using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using RealmUnbound.Contracts.Tilemap;
using Serilog;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Zone-entry prompt state set when the player steps onto a zone-object tile on the region map.</summary>
/// <param name="ZoneId">The zone identifier to enter.</param>
/// <param name="DisplayName">Human-readable zone name shown in the entry prompt.</param>
public record ZoneEntryInfo(string ZoneId, string DisplayName);

/// <summary>Snapshot of another player's position on the region map.</summary>
/// <param name="CharacterId">Unique character identifier.</param>
/// <param name="TileX">Current tile column.</param>
/// <param name="TileY">Current tile row.</param>
public record RegionPlayerState(Guid CharacterId, int TileX, int TileY);

/// <summary>
/// View model for the region-map canvas. Holds the region map data, local player position,
/// other players' positions, camera origin, and commands for movement, zone entry, and region exit.
/// </summary>
public class RegionTilemapViewModel : ViewModelBase
{
    private RegionMapDto? _mapData;
    private int _cameraX;
    private int _cameraY;
    private int _playerTileX;
    private int _playerTileY;
    private ZoneEntryInfo? _pendingZoneEntry;
    private string? _pendingRegionExit;

    // ── Map data ─────────────────────────────────────────────────────────────

    /// <summary>The current region's map definition, received from the server via <c>RegionMap</c>.</summary>
    public RegionMapDto? MapData
    {
        get => _mapData;
        set
        {
            this.RaiseAndSetIfChanged(ref _mapData, value);
            this.RaisePropertyChanged(nameof(HasMap));
        }
    }

    /// <summary>Whether a region map has been loaded.</summary>
    public bool HasMap => _mapData is not null;

    // ── Camera (tile-space origin of the viewport top-left corner) ────────────

    /// <summary>Tile column of the top-left corner of the viewport.</summary>
    public int CameraX
    {
        get => _cameraX;
        set => this.RaiseAndSetIfChanged(ref _cameraX, value);
    }

    /// <summary>Tile row of the top-left corner of the viewport.</summary>
    public int CameraY
    {
        get => _cameraY;
        set => this.RaiseAndSetIfChanged(ref _cameraY, value);
    }

    /// <summary>
    /// Number of tile columns visible in the viewport. Updated by the map control on resize.
    /// Defaults to 26 (1248 px ÷ 48 px/tile).
    /// </summary>
    public int ViewportWidthTiles  { get; set; } = 26;

    /// <summary>
    /// Number of tile rows visible in the viewport. Updated by the map control on resize.
    /// Defaults to 17 (816 px ÷ 48 px/tile).
    /// </summary>
    public int ViewportHeightTiles { get; set; } = 17;

    // ── Local player position ─────────────────────────────────────────────────

    /// <summary>Current tile column of the local player character on the region map.</summary>
    public int PlayerTileX
    {
        get => _playerTileX;
        set => this.RaiseAndSetIfChanged(ref _playerTileX, value);
    }

    /// <summary>Current tile row of the local player character on the region map.</summary>
    public int PlayerTileY
    {
        get => _playerTileY;
        set => this.RaiseAndSetIfChanged(ref _playerTileY, value);
    }

    // ── Other players ─────────────────────────────────────────────────────────

    /// <summary>Snapshot positions of all other players currently on this region map.</summary>
    public ObservableCollection<RegionPlayerState> OtherPlayers { get; } = [];

    // ── Zone / region prompts ─────────────────────────────────────────────────

    /// <summary>
    /// Set when the local player steps onto a zone-object tile.
    /// The UI should show a confirmation prompt; clearing this cancels the entry.
    /// </summary>
    public ZoneEntryInfo? PendingZoneEntry
    {
        get => _pendingZoneEntry;
        set => this.RaiseAndSetIfChanged(ref _pendingZoneEntry, value);
    }

    /// <summary>
    /// Set to the target region ID when the local player steps onto a region-exit tile.
    /// The UI should show a confirmed prompt; clearing this cancels the transition.
    /// </summary>
    public string? PendingRegionExit
    {
        get => _pendingRegionExit;
        set => this.RaiseAndSetIfChanged(ref _pendingRegionExit, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Command executed when the player requests a move to an adjacent tile on the region map.</summary>
    public ReactiveCommand<(int ToX, int ToY, string Direction), Unit> RequestMoveCommand { get; }

    /// <summary>Command executed when the player confirms they want to enter the nearby zone.</summary>
    public ReactiveCommand<Unit, Unit> ConfirmZoneEntryCommand { get; }

    /// <summary>Command executed when the player dismisses the zone-entry prompt.</summary>
    public ReactiveCommand<Unit, Unit> CancelZoneEntryCommand { get; }

    /// <summary>Command executed when the player confirms they want to cross the nearby region exit.</summary>
    public ReactiveCommand<Unit, Unit> ConfirmRegionExitCommand { get; }

    /// <summary>Command executed when the player dismisses the region-exit prompt.</summary>
    public ReactiveCommand<Unit, Unit> CancelRegionExitCommand { get; }

    /// <summary>Initializes a new instance of <see cref="RegionTilemapViewModel"/>.</summary>
    /// <param name="onMove">
    /// Callback invoked when the player requests a tile move on the region map.
    /// Receives <c>(toX, toY, direction)</c> and must forward it to the server hub.
    /// </param>
    /// <param name="onEnterZone">
    /// Callback invoked when the player confirms zone entry.
    /// Receives the <c>zoneId</c> to enter and must call <c>EnterZone</c> on the hub.
    /// </param>
    /// <param name="onTransitionRegion">
    /// Callback invoked when the player confirms a region transition.
    /// Receives the <c>targetRegionId</c> and must call <c>TransitionToRegion</c> on the hub.
    /// </param>
    public RegionTilemapViewModel(
        Func<int, int, string, System.Threading.Tasks.Task> onMove,
        Func<string, System.Threading.Tasks.Task> onEnterZone,
        Func<string, System.Threading.Tasks.Task> onTransitionRegion)
    {
        RequestMoveCommand = ReactiveCommand.CreateFromTask<(int ToX, int ToY, string Direction)>(
            args => onMove(args.ToX, args.ToY, args.Direction));
        RequestMoveCommand.ThrownExceptions.Subscribe(ex =>
            Log.Warning(ex, "RegionMap move command failed — server connection may be unavailable"));

        var hasZoneEntry   = this.WhenAnyValue(x => x.PendingZoneEntry).Select(v => v is not null);
        var hasRegionExit  = this.WhenAnyValue(x => x.PendingRegionExit).Select(v => v is not null);

        ConfirmZoneEntryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var entry = PendingZoneEntry;
            if (entry is null) return;
            PendingZoneEntry = null;
            await onEnterZone(entry.ZoneId);
        }, hasZoneEntry);
        ConfirmZoneEntryCommand.ThrownExceptions.Subscribe(ex =>
            Log.Warning(ex, "ConfirmZoneEntry failed"));

        CancelZoneEntryCommand = ReactiveCommand.Create(() => { PendingZoneEntry = null; });

        ConfirmRegionExitCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var toRegionId = PendingRegionExit;
            if (toRegionId is null) return;
            PendingRegionExit = null;
            await onTransitionRegion(toRegionId);
        }, hasRegionExit);
        ConfirmRegionExitCommand.ThrownExceptions.Subscribe(ex =>
            Log.Warning(ex, "ConfirmRegionExit failed"));

        CancelRegionExitCommand = ReactiveCommand.Create(() => { PendingRegionExit = null; });
    }

    // ── Entity helpers ────────────────────────────────────────────────────────

    /// <summary>Adds or updates another player's position on the region map.</summary>
    public void UpsertPlayer(Guid characterId, int tileX, int tileY)
    {
        var existing = OtherPlayers.FirstOrDefault(p => p.CharacterId == characterId);
        if (existing is not null)
            OtherPlayers.Remove(existing);
        OtherPlayers.Add(new RegionPlayerState(characterId, tileX, tileY));
    }

    /// <summary>Removes a player from the region map (e.g. when they enter a zone or disconnect).</summary>
    public void RemovePlayer(Guid characterId)
    {
        var existing = OtherPlayers.FirstOrDefault(p => p.CharacterId == characterId);
        if (existing is not null)
            OtherPlayers.Remove(existing);
    }

    /// <summary>
    /// Centers the camera on the given tile coordinate using the current viewport dimensions.
    /// The camera is clamped so it never scrolls past the map boundary.
    /// </summary>
    public void CenterCameraOn(int tileX, int tileY)
        => CenterCameraOn(tileX, tileY, ViewportWidthTiles, ViewportHeightTiles);

    /// <summary>
    /// Centers the camera on the given tile coordinate with explicit viewport dimensions.
    /// The camera is clamped so it never scrolls past the map boundary.
    /// </summary>
    public void CenterCameraOn(int tileX, int tileY, int viewportWidthTiles, int viewportHeightTiles)
    {
        if (_mapData is null) return;

        var cx = tileX - viewportWidthTiles  / 2;
        var cy = tileY - viewportHeightTiles / 2;

        CameraX = Math.Clamp(cx, 0, Math.Max(0, _mapData.Width  - viewportWidthTiles));
        CameraY = Math.Clamp(cy, 0, Math.Max(0, _mapData.Height - viewportHeightTiles));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the tile at <paramref name="x"/>, <paramref name="y"/>
    /// is statically impassable according to the current map's collision mask.
    /// Returns <see langword="false"/> when no map is loaded, letting the server decide.
    /// </summary>
    public bool IsBlocked(int x, int y)
    {
        if (_mapData is null) return false;
        if (x < 0 || y < 0 || x >= _mapData.Width || y >= _mapData.Height) return true;
        var idx = y * _mapData.Width + x;
        return idx < _mapData.CollisionMask.Length && _mapData.CollisionMask[idx];
    }
}
