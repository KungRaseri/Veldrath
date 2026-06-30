using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Veldrath.Client.Services;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.ViewModels;

/// <summary>
/// Represents a single clickable exit direction from the player's current tile.
/// </summary>
/// <param name="Direction">Cardinal direction label: N, S, E, W.</param>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="IsZoneExit">If true, stepping here triggers a zone transition.</param>
/// <param name="DestinationZone">Zone slug to transition to, when <paramref name="IsZoneExit"/> is true.</param>
public record LocationExitItem(
    string Direction,
    int ToX,
    int ToY,
    bool IsZoneExit,
    string? DestinationZone);

/// <summary>
/// Represents a visible entity at or near the player's current tile.
/// </summary>
/// <param name="EntityId">Unique entity identifier.</param>
/// <param name="Name">Display name or type label.</param>
/// <param name="EntityType">"player", "enemy", or "npc".</param>
/// <param name="IsSelf">Whether this is the local player's character.</param>
public record LocationEntityItem(
    Guid EntityId,
    string Name,
    string EntityType,
    bool IsSelf);

/// <summary>
/// View model for the zone location panel — the replacement for the 2D tilemap control.
/// Wraps a <see cref="TilemapViewModel"/> and derives reactive display data:
/// room description, available exits, visible entities, and contextual actions.
/// </summary>
public class ZoneLocationPanelViewModel : ViewModelBase
{
    private readonly TilemapViewModel _tilemap;
    private string _zoneName = string.Empty;
    private string _locationDescription = "Unknown location";
    private int _playerTileX;
    private int _playerTileY;

    /// <summary>Initializes a new instance of <see cref="ZoneLocationPanelViewModel"/>.</summary>
    /// <param name="tilemap">The underlying tilemap ViewModel to wrap.</param>
    public ZoneLocationPanelViewModel(TilemapViewModel tilemap)
    {
        _tilemap = tilemap;

        // Reflect tilemap changes onto our derived properties.
        tilemap.WhenAnyValue(x => x.TileMapData)
               .Subscribe(_ => RefreshFromTilemap());

        tilemap.WhenAnyValue(x => x.SelfEntityId)
               .Subscribe(_ => RefreshFromTilemap());

        tilemap.Entities.CollectionChanged += (_, _) => RefreshFromTilemap();

        MoveToExitCommand = ReactiveCommand.CreateFromTask<LocationExitItem>(DoMoveToExitAsync);
        InteractWithEntityCommand = ReactiveCommand.CreateFromTask<LocationEntityItem>(DoInteractWithEntityAsync);
    }

    // ── Observable state ──────────────────────────────────────────────────────

    /// <summary>Display name of the current zone.</summary>
    public string ZoneName
    {
        get => _zoneName;
        set => this.RaiseAndSetIfChanged(ref _zoneName, value);
    }

    /// <summary>Text description of the player's current location.</summary>
    public string LocationDescription
    {
        get => _locationDescription;
        set => this.RaiseAndSetIfChanged(ref _locationDescription, value);
    }

    /// <summary>Player's current tile X.</summary>
    public int PlayerTileX
    {
        get => _playerTileX;
        set => this.RaiseAndSetIfChanged(ref _playerTileX, value);
    }

    /// <summary>Player's current tile Y.</summary>
    public int PlayerTileY
    {
        get => _playerTileY;
        set => this.RaiseAndSetIfChanged(ref _playerTileY, value);
    }

    /// <summary>Clickable exits from the player's current position.</summary>
    public ObservableCollection<LocationExitItem> Exits { get; } = [];

    /// <summary>Entities visible at the player's current tile.</summary>
    public ObservableCollection<LocationEntityItem> Entities { get; } = [];

    /// <summary>Whether there are any entities at this location.</summary>
    public bool HasEntities => Entities.Count > 0;

    /// <summary>Whether there are any exits from this location.</summary>
    public bool HasExits => Exits.Count > 0;

    /// <summary>The underlying tilemap ViewModel (exposed for parent wiring).</summary>
    public TilemapViewModel Tilemap => _tilemap;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Command fired when the player clicks an exit to move.</summary>
    public ReactiveCommand<LocationExitItem, Unit> MoveToExitCommand { get; }

    /// <summary>Command fired when the player clicks an entity to interact.</summary>
    public ReactiveCommand<LocationEntityItem, Unit> InteractWithEntityCommand { get; }

    // ── Refresh logic ─────────────────────────────────────────────────────────

    /// <summary>Recalculates all derived state from the underlying tilemap.</summary>
    public void RefreshFromTilemap()
    {
        UpdatePlayerPosition();
        UpdateExits();
        UpdateEntities();
        UpdateDescription();
    }

    private void UpdatePlayerPosition()
    {
        if (_tilemap.SelfEntityId is null || _tilemap.TileMapData is null)
            return;

        var self = _tilemap.Entities.FirstOrDefault(e => e.EntityId == _tilemap.SelfEntityId.Value);
        if (self is not null)
        {
            PlayerTileX = self.TileX;
            PlayerTileY = self.TileY;
        }
    }

    private void UpdateExits()
    {
        Exits.Clear();

        var map = _tilemap.TileMapData;
        if (map is null) return;

        // Cardinal directions
        var (px, py) = (PlayerTileX, PlayerTileY);
        var directions = new[] { ("N", 0, -1), ("S", 0, 1), ("E", 1, 0), ("W", -1, 0) };

        foreach (var (dir, dx, dy) in directions)
        {
            var tx = px + dx;
            var ty = py + dy;

            // Check bounds
            if (tx < 0 || ty < 0 || tx >= map.Width || ty >= map.Height)
                continue;

            // Check collision mask
            var idx = ty * map.Width + tx;
            if (idx < map.CollisionMask.Length && map.CollisionMask[idx])
                continue;

            // Check if this is a zone-transition exit tile
            var zoneExit = map.ExitTiles.FirstOrDefault(e => e.TileX == tx && e.TileY == ty);
            var isZoneExit = zoneExit is not null;

            Exits.Add(new LocationExitItem(
                dir,
                tx,
                ty,
                isZoneExit,
                zoneExit?.ToZoneId));
        }

        this.RaisePropertyChanged(nameof(HasExits));
    }

    private void UpdateEntities()
    {
        Entities.Clear();

        if (_tilemap.TileMapData is null) return;

        var (px, py) = (PlayerTileX, PlayerTileY);

        foreach (var entity in _tilemap.Entities)
        {
            // Show entities at the same tile or adjacent tiles
            if (Math.Abs(entity.TileX - px) > 1 || Math.Abs(entity.TileY - py) > 1)
                continue;

            var isSelf = _tilemap.SelfEntityId == entity.EntityId;
            var name = entity.SpriteKey switch
            {
                _ when isSelf => "You",
                _ when entity.EntityType == "player" => $"Player {entity.EntityId.ToString()[..8]}",
                _ => entity.SpriteKey
            };

            Entities.Add(new LocationEntityItem(
                entity.EntityId,
                name,
                entity.EntityType,
                isSelf));
        }

        this.RaisePropertyChanged(nameof(HasEntities));
    }

    private void UpdateDescription()
    {
        var map = _tilemap.TileMapData;
        if (map is null)
        {
            LocationDescription = "No map data loaded.";
            return;
        }

        // Use the tile description service to generate a location description
        // from the base terrain and object layers at the player's current position.
        var baseLayer = map.Layers.FirstOrDefault(l => l.Name == "base" || l.ZIndex < 2);
        var objLayer  = map.Layers.FirstOrDefault(l => l.Name == "objects" || l.ZIndex >= 2);

        var baseData  = baseLayer?.Data;
        var objectData = objLayer?.Data;

        if (baseData is not null)
        {
            var desc = TileDescriptionService.GetLocationDescription(
                baseData, objectData, map.Width, PlayerTileX, PlayerTileY);
            LocationDescription = desc;
        }
        else
        {
            LocationDescription = $"Position ({PlayerTileX}, {PlayerTileY})";
        }
    }

    /// <summary>Called externally when the zone name is available (from GameViewModel).</summary>
    public void SetZoneName(string zoneName)
    {
        ZoneName = zoneName;
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    private Task DoMoveToExitAsync(LocationExitItem exit)
    {
        // Map direction to string expected by the hub
        var dir = exit.Direction switch
        {
            "N" => "N",
            "S" => "S",
            "E" => "E",
            "W" => "W",
            _ => "S"
        };

        _tilemap.RequestMoveCommand.Execute((exit.ToX, exit.ToY, dir)).Subscribe();
        return Task.CompletedTask;
    }

    private Task DoInteractWithEntityAsync(LocationEntityItem entity)
    {
        // TODO: Phase 5 — route to engage enemy, talk to NPC, etc.
        if (entity.EntityType == "enemy")
        {
            // Signal combat engagement via the game VM bridge
        }
        return Task.CompletedTask;
    }
}
