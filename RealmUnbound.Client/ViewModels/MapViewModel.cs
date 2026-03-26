using System.Collections.ObjectModel;
using ReactiveUI;
using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.ViewModels;

/// <summary>
/// ViewModel for the full world-graph map screen.  Loads all regions and their zones in a single
/// pass and lays them out as a two-tier hierarchical graph (regions along the top, zones clustered
/// below each region) using <see cref="GraphLayout.ComputeHierarchical"/>.
/// </summary>
public class MapViewModel : ViewModelBase
{
    private readonly IZoneService _zoneService;
    private readonly string? _currentZoneId;
    private readonly string? _currentRegionId;
    private readonly Guid? _characterId;

    private string _title = "World Map";
    private MapNodeViewModel? _selectedNode;
    private bool _isLoading;

    /// <summary>Canvas width used by the layout algorithm and bound in XAML.</summary>
    public double CanvasWidth { get; } = 1120;

    /// <summary>Canvas height used by the layout algorithm and bound in XAML.</summary>
    public double CanvasHeight { get; } = 500;

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
        _characterId = characterId;

        Nodes = [];
        Edges = [];

        CloseCommand    = ReactiveCommand.Create(() => { /* handled externally by navigation */ });
        SelectNodeCommand = ReactiveCommand.Create<MapNodeViewModel>(node => SelectedNode = node);

        _ = LoadFullGraphAsync();
    }

    // ── Observable collections ──────────────────────────────────────────────

    /// <summary>Graph nodes currently visible on the canvas.</summary>
    public ObservableCollection<MapNodeViewModel> Nodes { get; }

    /// <summary>Graph edges currently visible on the canvas.</summary>
    public ObservableCollection<MapEdgeViewModel> Edges { get; }

    // ── State properties ────────────────────────────────────────────────────

    /// <summary>Header title shown in the map top-bar.</summary>
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

    /// <summary>Status-bar hint text.</summary>
    public string StatusText => "Click a node to view its details";

    /// <summary>Hint shown in the detail panel when no node is selected.</summary>
    public string NullSelectionHint => "Click a region or zone to view its details.";

    // ── Commands ────────────────────────────────────────────────────────────

    /// <summary>Close the map screen and return to the game view.</summary>
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CloseCommand { get; }

    /// <summary>Select a node without drilling into it.</summary>
    public ReactiveCommand<MapNodeViewModel, System.Reactive.Unit> SelectNodeCommand { get; }

    // ── Graph loader ─────────────────────────────────────────────────────────

    private async Task LoadFullGraphAsync()
    {
        IsLoading = true;
        try
        {
            var regions = await _zoneService.GetRegionsAsync();

            var regionNodeMap = regions.ToDictionary(
                r => r.Id,
                r => new MapNodeViewModel(r.Id, r.Name, "region")
                {
                    SubType    = r.Type,
                    MinLevel   = r.MinLevel,
                    IsCurrent  = r.Id == _currentRegionId,
                });

            SelectedNode = null;
            Nodes.Clear();
            Edges.Clear();
            foreach (var node in regionNodeMap.Values) Nodes.Add(node);

            // Region-region edges (deduplicated; connections are returned as directed pairs)
            var seenRegionEdges = new HashSet<(string, string)>();
            foreach (var region in regions)
            {
                var connected = await _zoneService.GetRegionConnectionsAsync(region.Id);
                foreach (var adj in connected)
                {
                    var a   = region.Id;
                    var b   = adj.Id;
                    var key = string.Compare(a, b, StringComparison.Ordinal) < 0 ? (a, b) : (b, a);
                    if (!seenRegionEdges.Add(key)) continue;
                    if (regionNodeMap.TryGetValue(a, out var fromNode) &&
                        regionNodeMap.TryGetValue(b, out var toNode))
                        Edges.Add(new MapEdgeViewModel(fromNode, toNode, "region_exit"));
                }
            }

            // Zone nodes — one per zone; zone_membership edges link each zone to its parent region
            foreach (var region in regions)
            {
                var zones = await _zoneService.GetZonesByRegionAsync(region.Id);
                foreach (var zone in zones)
                {
                    var zoneNode = new MapNodeViewModel(zone.Id, zone.Name, "zone")
                    {
                        SubType   = zone.Type,
                        MinLevel  = zone.MinLevel,
                        IsCurrent = zone.Id == _currentZoneId,
                    };
                    Nodes.Add(zoneNode);
                    Edges.Add(new MapEdgeViewModel(regionNodeMap[region.Id], zoneNode, "zone_membership"));
                }
            }

            GraphLayout.ComputeHierarchical(Nodes, Edges, CanvasWidth, CanvasHeight);
            Title = "World Map";
        }
        finally { IsLoading = false; }
    }
}
