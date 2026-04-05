using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using RealmUnbound.Contracts.Tilemap;
using Serilog;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Lightweight snapshot of any entity (character or enemy) positioned on the tile grid.</summary>
/// <param name="EntityId">Unique identifier for this entity instance.</param>
/// <param name="EntityType">Type discriminator: <c>"player"</c> or <c>"enemy"</c>.</param>
/// <param name="SpriteKey">Tileset tile index or named sprite key used to look up the sprite bitmap.</param>
/// <param name="TileX">Current tile column.</param>
/// <param name="TileY">Current tile row.</param>
/// <param name="Direction">Cardinal facing direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, <c>"right"</c>.</param>
public record TileEntityState(Guid EntityId, string EntityType, string SpriteKey, int TileX, int TileY, string Direction);

/// <summary>
/// View model for the real-time tilemap. Holds the current map data, visible entity positions,
/// camera origin, fog reveal state, and the command used to request movement from the server.
/// </summary>
public class TilemapViewModel : ViewModelBase
{
    private TileMapDto? _tileMapData;
    private int _cameraX;
    private int _cameraY;
    private bool _isMiniMapOpen;
    private Guid? _selfEntityId;

    // ── Map data ─────────────────────────────────────────────────────────────

    /// <summary>The current zone's tilemap definition, received from the server via <c>ZoneTileMap</c>.</summary>
    public TileMapDto? TileMapData
    {
        get => _tileMapData;
        set
        {
            this.RaiseAndSetIfChanged(ref _tileMapData, value);
            this.RaisePropertyChanged(nameof(HasMap));
        }
    }

    /// <summary>Whether a tilemap has been loaded for the current zone.</summary>
    public bool HasMap => _tileMapData is not null;

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
    /// Number of tile columns visible in the viewport. Updated by <c>TilemapControl</c> on resize.
    /// Defaults to 26 (1248 px ÷ 48 px/tile) until the control reports its actual bounds.
    /// </summary>
    public int ViewportWidthTiles  { get; set; } = 26;

    /// <summary>
    /// Number of tile rows visible in the viewport. Updated by <c>TilemapControl</c> on resize.
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

    /// <summary>All live entity positions on the map (players + enemies). Mutated on the UI thread.</summary>
    public ObservableCollection<TileEntityState> Entities { get; } = [];

    /// <summary>Tile coordinates that have been revealed out of fog-of-war. Keyed as <c>"x:y"</c>.</summary>
    public HashSet<string> RevealedTiles { get; } = [];

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Command executed when the player requests a move to an adjacent tile.</summary>
    public ReactiveCommand<(int ToX, int ToY, string Direction), Unit> RequestMoveCommand { get; }

    /// <summary>Command that toggles the mini-map overlay.</summary>
    public ReactiveCommand<Unit, Unit> ToggleMiniMapCommand { get; }

    /// <summary>Initializes a new instance of <see cref="TilemapViewModel"/>.</summary>
    /// <param name="onMove">
    /// Callback invoked when the player requests a move.
    /// Receives the <c>(toX, toY, direction)</c> tuple and must forward it to the server hub.
    /// </param>
    public TilemapViewModel(Func<int, int, string, System.Threading.Tasks.Task> onMove)
    {
        RequestMoveCommand = ReactiveCommand.CreateFromTask<(int ToX, int ToY, string Direction)>(
            args => onMove(args.ToX, args.ToY, args.Direction));
        RequestMoveCommand.ThrownExceptions.Subscribe(ex =>
            Log.Warning(ex, "Move command failed — server connection may be unavailable"));

        ToggleMiniMapCommand = ReactiveCommand.Create(() => { IsMiniMapOpen = !IsMiniMapOpen; });
    }

    // ── Entity helpers ────────────────────────────────────────────────────────

    /// <summary>Updates or inserts a character entity on the map.</summary>
    public void UpsertEntity(Guid entityId, string entityType, string spriteKey, int tileX, int tileY, string direction)
    {
        var existing = Entities.FirstOrDefault(e => e.EntityId == entityId);
        if (existing is not null)
            Entities.Remove(existing);
        Entities.Add(new TileEntityState(entityId, entityType, spriteKey, tileX, tileY, direction));
    }

    /// <summary>Removes an entity from the map (e.g. when a player leaves the zone or an enemy dies).</summary>
    public void RemoveEntity(Guid entityId)
    {
        var entity = Entities.FirstOrDefault(e => e.EntityId == entityId);
        if (entity is not null)
            Entities.Remove(entity);
    }

    /// <summary>
    /// Centers the camera on the given tile coordinate using the current
    /// <see cref="ViewportWidthTiles"/> / <see cref="ViewportHeightTiles"/>.
    /// The camera is clamped so it never scrolls past the map boundary (dead-zone at edges).
    /// </summary>
    public void CenterCameraOn(int tileX, int tileY)
        => CenterCameraOn(tileX, tileY, ViewportWidthTiles, ViewportHeightTiles);

    /// <summary>
    /// Centers the camera on the given tile coordinate with explicit viewport dimensions.
    /// The camera is clamped so it never scrolls past the map boundary (dead-zone at edges).
    /// </summary>
    public void CenterCameraOn(int tileX, int tileY, int viewportWidthTiles, int viewportHeightTiles)
    {
        if (_tileMapData is null) return;

        var cx = tileX - viewportWidthTiles / 2;
        var cy = tileY - viewportHeightTiles / 2;

        CameraX = Math.Clamp(cx, 0, Math.Max(0, _tileMapData.Width  - viewportWidthTiles));
        CameraY = Math.Clamp(cy, 0, Math.Max(0, _tileMapData.Height - viewportHeightTiles));
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
    /// is statically impassable according to the current map's collision mask.
    /// Returns <see langword="false"/> when no map is loaded, letting the server decide.
    /// </summary>
    public bool IsBlocked(int x, int y)
    {
        if (_tileMapData is null) return false;
        if (x < 0 || y < 0 || x >= _tileMapData.Width || y >= _tileMapData.Height) return true;
        var idx = y * _tileMapData.Width + x;
        return idx < _tileMapData.CollisionMask.Length && _tileMapData.CollisionMask[idx];
    }
}
