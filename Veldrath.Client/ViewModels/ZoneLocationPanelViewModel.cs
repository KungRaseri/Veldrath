using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using Veldrath.Client.Services;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Client.ViewModels;

/// <summary>Types of contextual actions available for an entity.</summary>
public enum EntityActionType
{
    /// <summary>Talk to an NPC.</summary>
    Talk,

    /// <summary>Inspect an entity to learn more about it.</summary>
    Inspect,

    /// <summary>Engage an enemy in combat.</summary>
    Fight,

    /// <summary>Send a private whisper to another player.</summary>
    Whisper,
}

/// <summary>Represents a single contextual action that can be performed on an entity.</summary>
/// <param name="ActionType">The type of action.</param>
/// <param name="DisplayLabel">Human-readable label for the action button.</param>
/// <param name="Command">The reactive command that executes the action, passing the parent entity as parameter.</param>
public record EntityAction(
    EntityActionType ActionType,
    string DisplayLabel,
    ReactiveCommand<LocationEntityItem, Unit> Command);

/// <summary>
/// Represents a single clickable exit direction from the player's current tile.
/// </summary>
/// <param name="Direction">Cardinal direction label: N, S, E, W.</param>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="IsZoneExit">If true, stepping here triggers a zone transition.</param>
/// <param name="DisplayLabel">Human-readable label for the exit button.</param>
/// <param name="DestinationZone">Zone slug to transition to, when <paramref name="IsZoneExit"/> is true.</param>
public record LocationExitItem(
    string Direction,
    int ToX,
    int ToY,
    bool IsZoneExit,
    string DisplayLabel,
    string? DestinationZone);

/// <summary>
/// Represents a visible entity at or near the player's current tile,
/// with contextual actions derived from its type.
/// </summary>
/// <param name="EntityId">Unique entity identifier.</param>
/// <param name="Name">Display name or type label.</param>
/// <param name="EntityType">"player", "enemy", or "npc".</param>
/// <param name="IsSelf">Whether this is the local player's character.</param>
/// <param name="Actions">Available contextual actions for this entity.</param>
public record LocationEntityItem(
    Guid EntityId,
    string Name,
    string EntityType,
    bool IsSelf,
    IReadOnlyList<EntityAction> Actions);

/// <summary>
/// View model for the zone location panel — the replacement for the 2D tilemap control.
/// Wraps a <see cref="TilemapViewModel"/> and derives reactive display data:
/// room description, available exits, visible entities, POI locations, and contextual actions.
/// </summary>
public class ZoneLocationPanelViewModel : ViewModelBase
{
    private readonly TilemapViewModel _tilemap;
    private readonly Func<Guid, Task> _onTalkToEntity;
    private readonly Func<Guid, Task> _onInspectEntity;
    private readonly Func<Guid, Task> _onFightEntity;
    private string _zoneName = string.Empty;
    private string _locationDescription = "Unknown location";
    private int _playerTileX;
    private int _playerTileY;
    private bool _isLoading;

    /// <summary>Initializes a new instance of <see cref="ZoneLocationPanelViewModel"/>.</summary>
    /// <param name="tilemap">The underlying tilemap ViewModel to wrap.</param>
    /// <param name="onTalkToEntity">Callback invoked when the player talks to an NPC entity.</param>
    /// <param name="onInspectEntity">Callback invoked when the player inspects any entity.</param>
    /// <param name="onFightEntity">Callback invoked when the player fights an enemy entity.</param>
    public ZoneLocationPanelViewModel(
        TilemapViewModel tilemap,
        Func<Guid, Task>? onTalkToEntity = null,
        Func<Guid, Task>? onInspectEntity = null,
        Func<Guid, Task>? onFightEntity = null)
    {
        _tilemap = tilemap;
        _onTalkToEntity = onTalkToEntity ?? (_ => Task.CompletedTask);
        _onInspectEntity = onInspectEntity ?? (_ => Task.CompletedTask);
        _onFightEntity = onFightEntity ?? (_ => Task.CompletedTask);

        // Reflect tilemap changes onto our derived properties.
        tilemap.WhenAnyValue(x => x.TileMapData)
               .Subscribe(_ => RefreshFromTilemap());

        tilemap.WhenAnyValue(x => x.SelfEntityId)
               .Subscribe(_ => RefreshFromTilemap());

        tilemap.Entities.CollectionChanged += (_, _) => RefreshFromTilemap();

        MoveToExitCommand = ReactiveCommand.CreateFromTask<LocationExitItem>(DoMoveToExitAsync);
        TalkToEntityCommand = ReactiveCommand.CreateFromTask<LocationEntityItem>(DoTalkToEntityAsync);
        InspectEntityCommand = ReactiveCommand.CreateFromTask<LocationEntityItem>(DoInspectEntityAsync);
        FightEntityCommand = ReactiveCommand.CreateFromTask<LocationEntityItem>(DoFightEntityAsync);
        NavigateToLocationCommand = ReactiveCommand.CreateFromTask<ZoneLocationItemViewModel>(DoNavigateToLocationAsync);
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

    /// <summary>Zone locations / POIs within the current zone.</summary>
    public ObservableCollection<ZoneLocationItemViewModel> ZoneLocations { get; } = [];

    /// <summary>Whether there are any entities at this location.</summary>
    public bool HasEntities => Entities.Count > 0;

    /// <summary>Whether there are any exits from this location.</summary>
    public bool HasExits => Exits.Count > 0;

    /// <summary>Whether there are any POI locations in this zone.</summary>
    public bool HasZoneLocations => ZoneLocations.Count > 0;

    /// <summary>Whether the panel has a known zone to display (zone name is non-empty).</summary>
    public bool HasZone => !string.IsNullOrEmpty(_zoneName);

    /// <summary>True when there's no map data and no player position yet (diagnostic for initial state).</summary>
    public bool HasNoData => !HasExits && !HasEntities && !HasZoneLocations;

    /// <summary>Whether the zone is currently in the process of loading its initial data.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>Descriptive text shown when zone or map data is unavailable.</summary>
    public string LoadingStatus
    {
        get
        {
            if (!string.IsNullOrEmpty(ErrorMessage)) return $"Error: {ErrorMessage}";
            if (string.IsNullOrEmpty(_zoneName)) return "No Zone — Enter a zone to begin";
            if (_tilemap.TileMapData is null) return "Entering zone, loading map data...";
            if (_tilemap.SelfEntityId is null) return "Waiting for character identity...";
            if (_tilemap.Entities.Count == 0) return "Waiting for entity positions...";
            return "Data loaded.";
        }
    }

    /// <summary>The underlying tilemap ViewModel (exposed for parent wiring).</summary>
    public TilemapViewModel Tilemap => _tilemap;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Command fired when the player clicks an exit to move.</summary>
    public ReactiveCommand<LocationExitItem, Unit> MoveToExitCommand { get; }

    /// <summary>Command fired when the player clicks the "Talk" action on an NPC entity.</summary>
    public ReactiveCommand<LocationEntityItem, Unit> TalkToEntityCommand { get; }

    /// <summary>Command fired when the player clicks the "Inspect" action on any entity.</summary>
    public ReactiveCommand<LocationEntityItem, Unit> InspectEntityCommand { get; }

    /// <summary>Command fired when the player clicks the "Fight" action on an enemy entity.</summary>
    public ReactiveCommand<LocationEntityItem, Unit> FightEntityCommand { get; }

    /// <summary>Command fired when the player clicks a POI to navigate there.</summary>
    public ReactiveCommand<ZoneLocationItemViewModel, Unit> NavigateToLocationCommand { get; }

    // ── Refresh logic ─────────────────────────────────────────────────────────

    /// <summary>Recalculates all derived state from the underlying tilemap.</summary>
    public void RefreshFromTilemap()
    {
        UpdatePlayerPosition();
        UpdateExits();
        UpdateEntities();
        UpdateDescription();
        IsLoading = _tilemap.TileMapData is null || _tilemap.SelfEntityId is null;
        this.RaisePropertyChanged(nameof(HasZone));
        this.RaisePropertyChanged(nameof(HasNoData));
        this.RaisePropertyChanged(nameof(LoadingStatus));
        this.RaisePropertyChanged(nameof(ErrorMessage));
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

            // Build display label: "Leave to ZoneName" for zone transitions, direction for normal moves
            var label = isZoneExit
                ? $"Leave to {zoneExit!.ToZoneId}"
                : dir;

            Exits.Add(new LocationExitItem(
                dir,
                tx,
                ty,
                isZoneExit,
                label,
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

            var locationItem = new LocationEntityItem(
                entity.EntityId,
                name,
                entity.EntityType,
                isSelf,
                []);

            // Resolve and attach contextual actions based on entity type
            var actions = ResolveActionsForEntity(locationItem);
            Entities.Add(locationItem with { Actions = actions });
        }

        this.RaisePropertyChanged(nameof(HasEntities));
    }

    /// <summary>
    /// Determines the set of contextual actions available for an entity based on its type.
    /// </summary>
    /// <param name="entity">The entity to resolve actions for.</param>
    /// <returns>A read-only list of available actions.</returns>
    private IReadOnlyList<EntityAction> ResolveActionsForEntity(LocationEntityItem entity)
    {
        if (entity.IsSelf)
            return [];

        var actions = new List<EntityAction>();

        switch (entity.EntityType)
        {
            case "npc":
                actions.Add(new EntityAction(EntityActionType.Talk, "Talk", TalkToEntityCommand));
                actions.Add(new EntityAction(EntityActionType.Inspect, "Inspect", InspectEntityCommand));
                break;

            case "enemy":
                actions.Add(new EntityAction(EntityActionType.Fight, "Fight", FightEntityCommand));
                actions.Add(new EntityAction(EntityActionType.Inspect, "Inspect", InspectEntityCommand));
                break;

            case "player":
                actions.Add(new EntityAction(EntityActionType.Inspect, "Inspect", InspectEntityCommand));
                // Whisper is optional — could be added later via a separate command
                break;
        }

        return actions;
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
        ErrorMessage = string.Empty;
        IsLoading = false;
        this.RaisePropertyChanged(nameof(HasZone));
        this.RaisePropertyChanged(nameof(LoadingStatus));
    }

    /// <summary>
    /// Replaces all POI/zone-location entries with the given collection.
    /// Called externally when <c>GameViewModel</c> loads zone locations.
    /// </summary>
    public void SetZoneLocations(IEnumerable<ZoneLocationItemViewModel> locations)
    {
        ZoneLocations.Clear();
        foreach (var loc in locations)
            ZoneLocations.Add(loc);
        this.RaisePropertyChanged(nameof(HasZoneLocations));
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

    /// <summary>Handles the Talk action: initiates dialogue with an NPC entity.</summary>
    /// <param name="entity">The NPC entity to talk to.</param>
    private async Task DoTalkToEntityAsync(LocationEntityItem entity)
    {
        if (entity.EntityType != "npc") return;
        await _onTalkToEntity(entity.EntityId);
    }

    /// <summary>Handles the Inspect action: examines an entity for descriptive information.</summary>
    /// <param name="entity">The entity to inspect.</param>
    private async Task DoInspectEntityAsync(LocationEntityItem entity)
    {
        await _onInspectEntity(entity.EntityId);
    }

    /// <summary>Handles the Fight action: engages an enemy entity in combat.</summary>
    /// <param name="entity">The enemy entity to fight.</param>
    private async Task DoFightEntityAsync(LocationEntityItem entity)
    {
        if (entity.EntityType != "enemy") return;
        await _onFightEntity(entity.EntityId);
    }

    private Task DoNavigateToLocationAsync(ZoneLocationItemViewModel location)
    {
        // Forward to the location's own navigate command if available
        location.NavigateCommand?.Execute(Unit.Default).Subscribe();
        return Task.CompletedTask;
    }
}
