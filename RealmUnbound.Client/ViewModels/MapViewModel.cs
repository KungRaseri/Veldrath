using System.Collections.ObjectModel;
using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>The zoom level currently displayed by the map.</summary>
public enum MapLevel
{
    /// <summary>World view — one node per Region.</summary>
    World,

    /// <summary>Region view — one node per Zone in the region.</summary>
    Region,

    /// <summary>Zone view — one node per ZoneLocation in the zone.</summary>
    Zone,
}

/// <summary>
/// ViewModel for the full traversal-graph map screen.  Supports three drill-down levels:
/// World → Region → Zone (locations).  Positions are computed by
/// <see cref="GraphLayout"/> (Fruchterman–Reingold spring layout).
/// </summary>
public class MapViewModel : ViewModelBase
{
    private readonly IZoneService _zoneService;
    private readonly string? _currentZoneId;
    private readonly string? _currentRegionId;
    private readonly string? _currentZoneLocationSlug;
    private readonly Guid? _characterId;

    // Drill-down breadcrumb
    private string? _focusedRegionId;
    private string? _focusedRegionName;
    private string? _focusedZoneName;

    private MapLevel _mapLevel = MapLevel.World;
    private string _title = "Map";
    private MapNodeViewModel? _selectedNode;
    private bool _isLoading;

    /// <summary>Canvas width used by the layout algorithm and bound in XAML.</summary>
    public double CanvasWidth { get; } = 860;

    /// <summary>Canvas height used by the layout algorithm and bound in XAML.</summary>
    public double CanvasHeight { get; } = 560;

    /// <summary>Initializes a new instance of <see cref="MapViewModel"/>.</summary>
    /// <param name="zoneService">HTTP service for zone/region/world data.</param>
    /// <param name="currentZoneId">The zone the character is currently in.</param>
    /// <param name="currentRegionId">The region the character is currently in.</param>
    /// <param name="currentZoneLocationSlug">The specific ZoneLocation the character is standing at, if any.</param>
    /// <param name="characterId">Character ID for hidden-location filtering.</param>
    public MapViewModel(
        IZoneService zoneService,
        string? currentZoneId = null,
        string? currentRegionId = null,
        string? currentZoneLocationSlug = null,
        Guid? characterId = null)
    {
        _zoneService = zoneService;
        _currentZoneId = currentZoneId;
        _currentRegionId = currentRegionId;
        _currentZoneLocationSlug = currentZoneLocationSlug;
        _characterId = characterId;

        Nodes = [];
        Edges = [];

        DrillIntoCommand = ReactiveCommand.CreateFromTask<MapNodeViewModel>(DoDrillIntoAsync);
        DrillOutCommand = ReactiveCommand.CreateFromTask(DoDrillOutAsync);
        GoToWorldCommand = ReactiveCommand.CreateFromTask(LoadWorldLevelAsync);
        GoToRegionCommand = ReactiveCommand.CreateFromTask(DoGoToRegionAsync);
        CloseCommand = ReactiveCommand.Create(() => { /* handled externally by navigation */ });
        SelectNodeCommand = ReactiveCommand.Create<MapNodeViewModel>(node => SelectedNode = node);

        _ = LoadWorldLevelAsync();
    }

    // ── Observable collections ──────────────────────────────────────────────

    /// <summary>Graph nodes currently visible on the canvas.</summary>
    public ObservableCollection<MapNodeViewModel> Nodes { get; }

    /// <summary>Graph edges currently visible on the canvas.</summary>
    public ObservableCollection<MapEdgeViewModel> Edges { get; }

    // ── State properties ────────────────────────────────────────────────────

    /// <summary>Currently active zoom level.</summary>
    public MapLevel MapLevel
    {
        get => _mapLevel;
        private set
        {
            this.RaiseAndSetIfChanged(ref _mapLevel, value);
            this.RaisePropertyChanged(nameof(IsWorldLevel));
            this.RaisePropertyChanged(nameof(IsRegionLevel));
            this.RaisePropertyChanged(nameof(IsZoneLevel));
            this.RaisePropertyChanged(nameof(CanDrillOut));
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(NullSelectionHint));
            this.RaisePropertyChanged(nameof(DrillInLabel));
        }
    }

    /// <summary>Header title for the currently displayed map level.</summary>
    public string Title
    {
        get => _title;
        private set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    /// <summary>The node the user last clicked.</summary>
    public MapNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (_selectedNode is not null) _selectedNode.IsSelected = false;
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            if (_selectedNode is not null) _selectedNode.IsSelected = true;
        }
    }

    /// <summary>Whether a data fetch is in progress.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>Whether the current level is World.</summary>
    public bool IsWorldLevel => MapLevel == MapLevel.World;

    /// <summary>Whether the current level is Region.</summary>
    public bool IsRegionLevel => MapLevel == MapLevel.Region;

    /// <summary>Whether the current level is Zone (locations).</summary>
    public bool IsZoneLevel => MapLevel == MapLevel.Zone;

    /// <summary>Whether the user can navigate back to the previous level.</summary>
    public bool CanDrillOut => MapLevel != MapLevel.World;

    /// <summary>Region name currently in focus (set when drilling into a region; null at world level).</summary>
    public string? FocusedRegionName
    {
        get => _focusedRegionName;
        private set => this.RaiseAndSetIfChanged(ref _focusedRegionName, value);
    }

    /// <summary>Zone name currently in focus (set when drilling into a zone; null above zone level).</summary>
    public string? FocusedZoneName
    {
        get => _focusedZoneName;
        private set => this.RaiseAndSetIfChanged(ref _focusedZoneName, value);
    }

    /// <summary>Contextual status-bar hint text for the current map level.</summary>
    public string StatusText => MapLevel == MapLevel.Zone
        ? "Single-click to select a location"
        : "Single-click to select  •  Double-click to zoom in";

    /// <summary>Hint shown in the detail panel when no node is selected; adapts to current level.</summary>
    public string NullSelectionHint => MapLevel == MapLevel.Zone
        ? "Click a location to view its details."
        : "Click a node to select it. Double-click to zoom in.";

    /// <summary>Label for the drill-in button in the detail panel; varies by current level.</summary>
    public string DrillInLabel => MapLevel == MapLevel.World ? "View Region →" : "View Locations →";

    // ── Commands ────────────────────────────────────────────────────────────

    /// <summary>Drill in to the selected node's detail level.</summary>
    public ReactiveCommand<MapNodeViewModel, System.Reactive.Unit> DrillIntoCommand { get; }

    /// <summary>Drill back out to the previous map level.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DrillOutCommand { get; }

    /// <summary>Jump directly to the World map level from any level.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> GoToWorldCommand { get; }

    /// <summary>Jump directly to the currently focused region level.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> GoToRegionCommand { get; }

    /// <summary>Close the map screen and return to the game view.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CloseCommand { get; }

    /// <summary>Select a node without drilling into it.</summary>
    public ReactiveCommand<MapNodeViewModel, System.Reactive.Unit> SelectNodeCommand { get; }

    // ── Level loaders ───────────────────────────────────────────────────────

    private async Task LoadWorldLevelAsync()
    {
        IsLoading = true;
        try
        {
            var regions = await _zoneService.GetRegionsAsync();
            var nodeMap = regions.ToDictionary(
                r => r.Id,
                r => new MapNodeViewModel(r.Id, r.Name, "region")
                {
                    SubType = r.Type,
                    MinLevel = r.MinLevel,
                    IsCurrent = r.Id == _currentRegionId,
                });

            SelectedNode = null;
            Nodes.Clear();
            Edges.Clear();
            foreach (var node in nodeMap.Values) Nodes.Add(node);

            // Build edges from region connections — fetch adj list for every region
            var seen = new HashSet<(string, string)>();
            foreach (var region in regions)
            {
                var connected = await _zoneService.GetRegionConnectionsAsync(region.Id);
                foreach (var adj in connected)
                {
                    var key = (region.Id, adj.Id);
                    var rkey = (adj.Id, region.Id);
                    if (seen.Contains(key) || seen.Contains(rkey)) continue;
                    seen.Add(key);
                    if (nodeMap.TryGetValue(region.Id, out var fromNode) &&
                        nodeMap.TryGetValue(adj.Id, out var toNode))
                        Edges.Add(new MapEdgeViewModel(fromNode, toNode, "region_exit"));
                }
            }

            RunLayout();
            MapLevel = MapLevel.World;
            Title = "World Map";
            FocusedRegionName = null;
            FocusedZoneName = null;
        }
        finally { IsLoading = false; }
    }

    private async Task LoadRegionLevelAsync(string regionId)
    {
        IsLoading = true;
        try
        {
            var zones = await _zoneService.GetZonesByRegionAsync(regionId);
            var nodeMap = zones.ToDictionary(
                z => z.Id,
                z => new MapNodeViewModel(z.Id, z.Name, "zone")
                {
                    SubType = z.Type,
                    MinLevel = z.MinLevel,
                    IsCurrent = z.Id == _currentZoneId,
                });

            SelectedNode = null;
            Nodes.Clear();
            Edges.Clear();
            foreach (var node in nodeMap.Values) Nodes.Add(node);

            // Build edges from zone connections for each zone
            var seen = new HashSet<(string, string)>();
            foreach (var zone in zones)
            {
                var connections = await _zoneService.GetZoneConnectionsAsync(zone.Id);
                foreach (var conn in connections)
                {
                    var key = (conn.FromZoneId, conn.ToZoneId);
                    var rkey = (conn.ToZoneId, conn.FromZoneId);
                    if (seen.Contains(key) || seen.Contains(rkey)) continue;
                    seen.Add(key);
                    if (nodeMap.TryGetValue(conn.FromZoneId, out var fromNode) &&
                        nodeMap.TryGetValue(conn.ToZoneId, out var toNode))
                        Edges.Add(new MapEdgeViewModel(fromNode, toNode, "zone_exit"));
                }
            }

            RunLayout();
            MapLevel = MapLevel.Region;

            var region = await _zoneService.GetRegionAsync(regionId);
            var regionName = region?.Name ?? "Region";
            Title = regionName;
            FocusedRegionName = regionName;
            FocusedZoneName = null;
        }
        finally { IsLoading = false; }
    }

    private async Task LoadZoneLevelAsync(string zoneId)
    {
        IsLoading = true;
        try
        {
            var locations = await _zoneService.GetZoneLocationsAsync(zoneId, _characterId);
            var connections = await _zoneService.GetZoneLocationConnectionsAsync(zoneId);

            var nodeMap = locations.ToDictionary(
                l => l.Slug,
                l => new MapNodeViewModel(l.Slug, l.DisplayName, "location")
                {
                    SubType = l.LocationType,
                    MinLevel = l.MinLevel,
                    IsCurrent = l.Slug == _currentZoneLocationSlug,
                    IsHidden = l.IsHidden,
                });

            SelectedNode = null;
            Nodes.Clear();
            Edges.Clear();
            foreach (var node in nodeMap.Values) Nodes.Add(node);

            foreach (var conn in connections.Where(c => c.IsTraversable))
            {
                if (nodeMap.TryGetValue(conn.FromLocationSlug, out var fromNode) &&
                    conn.ToLocationSlug is not null &&
                    nodeMap.TryGetValue(conn.ToLocationSlug, out var toNode))
                    Edges.Add(new MapEdgeViewModel(fromNode, toNode, conn.ConnectionType, conn.IsTraversable));
            }

            RunLayout();
            MapLevel = MapLevel.Zone;

            var zone = await _zoneService.GetZoneAsync(zoneId);
            var zoneName = zone?.Name ?? "Zone";
            Title = zone is not null ? $"{zone.Name} — Locations" : "Zone Map";
            FocusedZoneName = zoneName;
        }
        finally { IsLoading = false; }
    }

    private async Task DoDrillIntoAsync(MapNodeViewModel node)
    {
        SelectedNode = node;
        switch (MapLevel)
        {
            case MapLevel.World:
                _focusedRegionId = node.Id;
                await LoadRegionLevelAsync(node.Id);
                break;
            case MapLevel.Region:
                await LoadZoneLevelAsync(node.Id);
                break;
        }
    }

    private async Task DoDrillOutAsync()
    {
        switch (MapLevel)
        {
            case MapLevel.Zone:
                if (_focusedRegionId is not null)
                    await LoadRegionLevelAsync(_focusedRegionId);
                else
                    await LoadWorldLevelAsync();
                break;
            case MapLevel.Region:
                await LoadWorldLevelAsync();
                break;
        }
    }

    private Task DoGoToRegionAsync() =>
        _focusedRegionId is not null ? LoadRegionLevelAsync(_focusedRegionId) : Task.CompletedTask;

    private void RunLayout() =>
        GraphLayout.Compute(Nodes, Edges, CanvasWidth, CanvasHeight);
}
