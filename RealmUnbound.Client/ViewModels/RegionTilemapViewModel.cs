using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using RealmUnbound.Contracts.Tilemap;
using Serilog;

namespace RealmUnbound.Client.ViewModels;

/// <summary>
/// View model for the region-map tilemap. Holds the current region map data, visible entity
/// positions, camera origin, fog reveal state, and the command used to request region movement
/// from the server.
/// </summary>
public class RegionTilemapViewModel : ViewModelBase
{
    private RegionMapDto? _regionMapData;
    private int _cameraX;
    private int _cameraY;
    private bool _isMiniMapOpen;
    private Guid? _selfEntityId;

    // ── Map data ─────────────────────────────────────────────────────────────

    /// <summary>The current region's tilemap definition, received from the server via <c>RegionMapData</c>.</summary>
    public RegionMapDto? RegionMapData
    {
        get => _regionMapData;
        set
        {
            this.RaiseAndSetIfChanged(ref _regionMapData, value);
            this.RaisePropertyChanged(nameof(HasMap));
            this.RaisePropertyChanged(nameof(Labels));
            this.RaisePropertyChanged(nameof(Paths));
        }
    }

    /// <summary>Whether a region tilemap has been loaded.</summary>
    public bool HasMap => _regionMapData is not null;

    /// <summary>Zone label overlays for the current region map. Empty when no map is loaded.</summary>
    public IReadOnlyList<ZoneLabelDto> Labels => _regionMapData?.Labels ?? [];

    /// <summary>Road and path polylines for the current region map. Empty when no map is loaded.</summary>
    public IReadOnlyList<RegionPathDto> Paths => _regionMapData?.Paths ?? [];

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
    /// Number of tile columns visible in the viewport. Updated by the tilemap control on resize.
    /// Defaults to 26 (1248 px ÷ 48 px/tile) until the control reports its actual bounds.
    /// </summary>
    public int ViewportWidthTiles  { get; set; } = 26;

    /// <summary>
    /// Number of tile rows visible in the viewport. Updated by the tilemap control on resize.
    /// Defaults to 17 (816 px ÷ 48 px/tile) until the control reports its actual bounds.
    /// </summary>
    public int ViewportHeightTiles { get; set; } = 17;

    // ── Mini-map overlay ─────────────────────────────────────────────────────

    /// <summary>Whether the mini-map overlay is currently open.</summary>
    public bool IsMiniMapOpen
    {
        get => _isMiniMapOpen;
        set => this.RaiseAndSetIfChanged(ref _isMiniMapOpen, value);
    }

    // ── Entity tracking ───────────────────────────────────────────────────────

    /// <summary>
    /// The entity ID of the local player's own character.
    /// Used to distinguish self from other players in rendering and input handling.
    /// </summary>
    public Guid? SelfEntityId
    {
        get => _selfEntityId;
        set => this.RaiseAndSetIfChanged(ref _selfEntityId, value);
    }

    /// <summary>All live entity positions on the region map (players only). Mutated on the UI thread.</summary>
    public ObservableCollection<TileEntityState> Entities { get; } = [];

    /// <summary>Tile coordinates that have been revealed out of fog-of-war. Keyed as <c>"x:y"</c>.</summary>
    public HashSet<string> RevealedTiles { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Command executed when the player requests a move to an adjacent tile on the region map.</summary>
    public ReactiveCommand<(int ToX, int ToY, string Direction), Unit> RequestMoveCommand { get; }

    /// <summary>Command that toggles the mini-map overlay.</summary>
    public ReactiveCommand<Unit, Unit> ToggleMiniMapCommand { get; }

    /// <summary>Initializes a new instance of <see cref="RegionTilemapViewModel"/>.</summary>
    /// <param name="onMove">
    /// Callback invoked when the player requests a move on the region map.
    /// Receives the <c>(toX, toY, direction)</c> tuple and must forward it to the server hub.
    /// </param>
    public RegionTilemapViewModel(Func<int, int, string, System.Threading.Tasks.Task> onMove)
    {
        RequestMoveCommand = ReactiveCommand.CreateFromTask<(int ToX, int ToY, string Direction)>(
            args => onMove(args.ToX, args.ToY, args.Direction));
        RequestMoveCommand.ThrownExceptions.Subscribe(ex =>
            Log.Warning(ex, "Region move command failed — server connection may be unavailable"));

        ToggleMiniMapCommand = ReactiveCommand.Create(() => { IsMiniMapOpen = !IsMiniMapOpen; });
    }

    // ── Entity helpers ────────────────────────────────────────────────────────

    /// <summary>Updates or inserts a player entity on the region map.</summary>
    public void UpsertEntity(Guid entityId, string entityType, string spriteKey, int tileX, int tileY, string direction)
    {
        var existing = Entities.FirstOrDefault(e => e.EntityId == entityId);
        if (existing is not null)
            Entities.Remove(existing);
        Entities.Add(new TileEntityState(entityId, entityType, spriteKey, tileX, tileY, direction));
    }

    /// <summary>Removes an entity from the region map (e.g. when a player enters a zone or disconnects).</summary>
    public void RemoveEntity(Guid entityId)
    {
        var entity = Entities.FirstOrDefault(e => e.EntityId == entityId);
        if (entity is not null)
            Entities.Remove(entity);
    }

    /// <summary>
    /// Centers the camera on the given tile coordinate using the current
    /// <see cref="ViewportWidthTiles"/> / <see cref="ViewportHeightTiles"/>.
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
        if (_regionMapData is null) return;

        var cx = tileX - viewportWidthTiles / 2;
        var cy = tileY - viewportHeightTiles / 2;

        CameraX = Math.Clamp(cx, 0, Math.Max(0, _regionMapData.Width  - viewportWidthTiles));
        CameraY = Math.Clamp(cy, 0, Math.Max(0, _regionMapData.Height - viewportHeightTiles));
    }

    /// <summary>Marks tiles within the given radius of <paramref name="centerX"/>, <paramref name="centerY"/> as revealed.</summary>
    public void RevealAround(int centerX, int centerY, int radius = 4)
    {
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy <= radius * radius)
                RevealedTiles.Add($"{centerX + dx}:{centerY + dy}");
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the tile at <paramref name="x"/>, <paramref name="y"/>
    /// is statically impassable according to the current region map's collision mask.
    /// Returns <see langword="false"/> when no map is loaded, letting the server decide.
    /// </summary>
    public bool IsBlocked(int x, int y)
    {
        if (_regionMapData is null) return false;
        if (x < 0 || y < 0 || x >= _regionMapData.Width || y >= _regionMapData.Height) return true;
        var idx = y * _regionMapData.Width + x;
        return idx < _regionMapData.CollisionMask.Length && _regionMapData.CollisionMask[idx];
    }
}
