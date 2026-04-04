using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using RealmUnbound.Contracts.Tilemap;

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

    /// <summary>Centers the camera on the given tile coordinate, clamped to map bounds.</summary>
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
}
