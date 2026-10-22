using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;
using RealmForge.Services;
using Serilog;

namespace RealmForge.ViewModels;

/// <summary>
/// View model for the RealmForge map-graph editor: browse maps, edit nodes and edges,
/// validate graphs, and import/export JSON authoring documents for bulk authoring.
/// </summary>
public sealed class MapGraphEditorViewModel : ReactiveObject
{
    private readonly MapGraphEditorService _service;
    private bool _suppressDirty;
    private bool _suppressSelectionReload;
    private string _mapId = string.Empty;
    private MapType _selectedMapType = MapType.Zone;
    private string _ownerId = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string? _entryNodeId;
    private bool _isNew = true;
    private bool _isBusy;
    private bool _isDirty;
    private string? _statusMessage;
    private string _validationSummary = "Not validated.";
    private MapSummary? _selectedMap;
    private MapNodeRowViewModel? _selectedNode;
    private MapEdgeRowViewModel? _selectedEdge;

    /// <summary>Creates the editor view model and begins loading maps in the background.</summary>
    /// <param name="service">Service used to load and persist map graphs.</param>
    public MapGraphEditorViewModel(MapGraphEditorService service)
    {
        _service = service;

        RefreshCommand = ReactiveCommand.CreateFromTask(LoadMapsAsync);
        NewMapCommand = ReactiveCommand.Create(StartNewMap);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteSelectedAsync);
        ValidateCommand = ReactiveCommand.Create(ValidateCurrent);
        AddNodeCommand = ReactiveCommand.Create(AddNode);
        RemoveNodeCommand = ReactiveCommand.Create(RemoveSelectedNode);
        AddEdgeCommand = ReactiveCommand.Create(AddEdge);
        RemoveEdgeCommand = ReactiveCommand.Create(RemoveSelectedEdge);

        this.WhenAnyValue(
                x => x.MapId,
                x => x.SelectedMapType,
                x => x.OwnerId,
                x => x.Name,
                x => x.Description,
                x => x.EntryNodeId)
            .Skip(1)
            .Subscribe(_ => MarkDirty());
    }

    /// <summary>All maps available in the content database.</summary>
    public ObservableCollection<MapSummary> Maps { get; } = [];

    /// <summary>Editable node rows for the selected map.</summary>
    public ObservableCollection<MapNodeRowViewModel> Nodes { get; } = [];

    /// <summary>Editable edge rows for the selected map.</summary>
    public ObservableCollection<MapEdgeRowViewModel> Edges { get; } = [];

    /// <summary>Validation issues produced by the most recent validation run.</summary>
    public ObservableCollection<string> ValidationIssues { get; } = [];

    /// <summary>Map kinds available for selection in the editor.</summary>
    public IReadOnlyList<MapType> MapTypes { get; } = Enum.GetValues<MapType>();

    /// <summary>Gets or sets the currently selected map summary; selecting loads its graph.</summary>
    public MapSummary? SelectedMap
    {
        get => _selectedMap;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMap, value);
            if (value is not null && !_suppressSelectionReload)
                _ = LoadGraphAsync(value.Id);
        }
    }

    /// <summary>Stable map identifier. Editable only while creating a new map.</summary>
    public string MapId
    {
        get => _mapId;
        set
        {
            this.RaiseAndSetIfChanged(ref _mapId, value);
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    /// <summary>Whether the editor is currently creating a map that does not exist yet.</summary>
    public bool IsNew
    {
        get => _isNew;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isNew, value);
            this.RaisePropertyChanged(nameof(Title));
        }
    }

    /// <summary>Map kind (world, region, or zone).</summary>
    public MapType SelectedMapType
    {
        get => _selectedMapType;
        set => this.RaiseAndSetIfChanged(ref _selectedMapType, value);
    }

    /// <summary>Region or zone slug that owns the map.</summary>
    public string OwnerId
    {
        get => _ownerId;
        set => this.RaiseAndSetIfChanged(ref _ownerId, value);
    }

    /// <summary>Human-readable map name.</summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    /// <summary>Map description shown in the game UI.</summary>
    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    /// <summary>Default arrival node, or <see langword="null"/> when unset.</summary>
    public string? EntryNodeId
    {
        get => _entryNodeId;
        set => this.RaiseAndSetIfChanged(ref _entryNodeId, value);
    }

    /// <summary>Currently selected node row.</summary>
    public MapNodeRowViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            this.RaisePropertyChanged(nameof(HasDetailSelection));
        }
    }

    /// <summary>Currently selected edge row.</summary>
    public MapEdgeRowViewModel? SelectedEdge
    {
        get => _selectedEdge;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedEdge, value);
            this.RaisePropertyChanged(nameof(HasDetailSelection));
        }
    }

    /// <summary>Whether a node or edge is selected for editing.</summary>
    public bool HasDetailSelection => SelectedNode is not null || SelectedEdge is not null;

    /// <summary>Whether an operation is currently running.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    /// <summary>Whether the editor has unsaved changes.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    /// <summary>Human-readable status text for the last operation.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    /// <summary>Summary of the most recent validation run.</summary>
    public string ValidationSummary
    {
        get => _validationSummary;
        private set => this.RaiseAndSetIfChanged(ref _validationSummary, value);
    }

    /// <summary>Header title for the editor.</summary>
    public string Title => IsNew ? "New Map Graph" : $"Map · {MapId}";

    /// <summary>Node count display text.</summary>
    public string NodeCountText => $"{Nodes.Count} node(s)";

    /// <summary>Edge count display text.</summary>
    public string EdgeCountText => $"{Edges.Count} edge(s)";

    /// <summary>Reloads the map list from the database.</summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    /// <summary>Starts a new, unsaved map graph.</summary>
    public ReactiveCommand<Unit, Unit> NewMapCommand { get; }

    /// <summary>Persists the current map graph.</summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>Deletes the selected map graph.</summary>
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    /// <summary>Validates the current map graph and lists issues.</summary>
    public ReactiveCommand<Unit, Unit> ValidateCommand { get; }

    /// <summary>Adds a new node row.</summary>
    public ReactiveCommand<Unit, Unit> AddNodeCommand { get; }

    /// <summary>Removes the selected node row and any edges referencing it.</summary>
    public ReactiveCommand<Unit, Unit> RemoveNodeCommand { get; }

    /// <summary>Adds a new edge row.</summary>
    public ReactiveCommand<Unit, Unit> AddEdgeCommand { get; }

    /// <summary>Removes the selected edge row.</summary>
    public ReactiveCommand<Unit, Unit> RemoveEdgeCommand { get; }

    /// <summary>Loads the map list and selects the previously selected map when possible.</summary>
    /// <returns>A task that completes when the list has been refreshed.</returns>
    public async Task LoadMapsAsync()
    {
        IsBusy = true;
        try
        {
            var graphs = await _service.LoadAllAsync();

            _suppressSelectionReload = true;
            try
            {
                Maps.Clear();
                foreach (var graph in graphs)
                    Maps.Add(ToSummary(graph));
            }
            finally
            {
                _suppressSelectionReload = false;
            }

            if (Maps.Count == 0)
            {
                StatusMessage = _service.LastError
                    ?? "No maps found. Import a JSON document or create a new map.";
                return;
            }

            var keepId = SelectedMap?.Id ?? MapId;
            var match = Maps.FirstOrDefault(m => string.Equals(m.Id, keepId, StringComparison.Ordinal))
                ?? Maps[0];

            _suppressSelectionReload = true;
            SelectedMap = match;
            _suppressSelectionReload = false;

            StatusMessage = $"Loaded {Maps.Count} map(s).";
            await LoadGraphAsync(match.Id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Loads a map graph into the editor.</summary>
    /// <param name="mapId">Stable map identifier.</param>
    /// <returns><see langword="true"/> when the graph was loaded.</returns>
    public async Task<bool> LoadGraphAsync(string mapId)
    {
        IsBusy = true;
        try
        {
            var graph = await _service.LoadAsync(mapId);
            if (graph is null)
            {
                StatusMessage = _service.LastError ?? $"Map '{mapId}' was not found.";
                return false;
            }

            ApplyGraph(graph, isNew: false);
            StatusMessage = $"Loaded '{graph.Name}'.";
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Starts editing a brand-new map graph.</summary>
    public void StartNewMap()
    {
        _suppressSelectionReload = true;
        SelectedMap = null;
        _suppressSelectionReload = false;

        ApplyGraph(
            new MapGraph(string.Empty, MapType.Zone, string.Empty, "New Map", string.Empty, null, [], []),
            isNew: true);
        StatusMessage = "New map ready. Set an id, add nodes, then save.";
    }

    /// <summary>Projects the current editor state into an authoring graph.</summary>
    /// <returns>The projected map graph.</returns>
    public MapGraph BuildGraph()
    {
        var mapId = MapId.Trim();
        return new MapGraph(
            mapId,
            SelectedMapType,
            OwnerId.Trim(),
            Name.Trim(),
            Description,
            string.IsNullOrWhiteSpace(EntryNodeId) ? null : EntryNodeId.Trim(),
            [.. Nodes.Select(n => n.ToEntry(mapId))],
            [.. Edges.Select(e => e.ToEntry(mapId))]);
    }

    /// <summary>Validates the current editor state and populates <see cref="ValidationIssues"/>.</summary>
    public void ValidateCurrent() => ApplyValidation(MapGraphEditorService.Validate(BuildGraph()));

    /// <summary>Adds a new node row with a generated identifier.</summary>
    public void AddNode()
    {
        var index = Nodes.Count + 1;
        while (Nodes.Any(n => string.Equals(n.Id, $"node-{index}", StringComparison.OrdinalIgnoreCase)))
            index++;

        var node = new MapNodeRowViewModel
        {
            Id = $"node-{index}",
            NodeType = MapNodeType.Location,
            DisplayName = $"Node {index}",
            SortOrder = Nodes.Count,
        };

        node.PropertyChanged += OnRowPropertyChanged;
        Nodes.Add(node);
        SelectedNode = node;
        MarkDirty();
        this.RaisePropertyChanged(nameof(NodeCountText));
    }

    /// <summary>Removes the selected node row and every edge that references it.</summary>
    public void RemoveSelectedNode()
    {
        if (SelectedNode is null)
            return;

        var id = SelectedNode.Id;
        SelectedNode.PropertyChanged -= OnRowPropertyChanged;
        Nodes.Remove(SelectedNode);

        foreach (var edge in Edges.Where(e =>
                     string.Equals(e.FromNodeId, id, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.ToNodeId, id, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            edge.PropertyChanged -= OnRowPropertyChanged;
            Edges.Remove(edge);
        }

        SelectedNode = Nodes.FirstOrDefault();
        MarkDirty();
        this.RaisePropertyChanged(nameof(NodeCountText));
        this.RaisePropertyChanged(nameof(EdgeCountText));
    }

    /// <summary>Adds a new edge row connecting the first two available nodes.</summary>
    public void AddEdge()
    {
        var from = SelectedNode?.Id ?? Nodes.FirstOrDefault()?.Id ?? string.Empty;
        var to = Nodes.FirstOrDefault(n => !string.Equals(n.Id, from, StringComparison.Ordinal))?.Id
            ?? string.Empty;

        var edge = new MapEdgeRowViewModel
        {
            FromNodeId = from,
            ToNodeId = to,
            Direction = MapDirection.North,
            EdgeType = MapEdgeType.Walk,
            IsTraversable = true,
        };

        edge.PropertyChanged += OnRowPropertyChanged;
        Edges.Add(edge);
        SelectedEdge = edge;
        MarkDirty();
        this.RaisePropertyChanged(nameof(EdgeCountText));
    }

    /// <summary>Removes the selected edge row.</summary>
    public void RemoveSelectedEdge()
    {
        if (SelectedEdge is null)
            return;

        SelectedEdge.PropertyChanged -= OnRowPropertyChanged;
        Edges.Remove(SelectedEdge);
        SelectedEdge = Edges.FirstOrDefault();
        MarkDirty();
        this.RaisePropertyChanged(nameof(EdgeCountText));
    }

    /// <summary>Persists the current graph to the content database.</summary>
    /// <returns>A task that completes when the save attempt finishes.</returns>
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(MapId))
        {
            StatusMessage = "Map id is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var graph = BuildGraph();
            var validation = MapGraphEditorService.Validate(graph);
            ApplyValidation(validation);

            if (!await _service.SaveAsync(graph))
            {
                StatusMessage = _service.LastError ?? "Save failed.";
                return;
            }

            IsDirty = false;
            var errorCount = validation.Issues.Count(i => i.Severity == MapGraphValidationSeverity.Error);
            StatusMessage = errorCount == 0
                ? "Saved."
                : $"Saved with {errorCount} validation error(s) — review the issues list.";

            IsNew = false;
            SelectedMap = null;
            await LoadMapsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save map graph");
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Deletes the selected map graph.</summary>
    /// <returns>A task that completes when the delete attempt finishes.</returns>
    public async Task DeleteSelectedAsync()
    {
        var mapId = SelectedMap?.Id ?? MapId;
        if (string.IsNullOrWhiteSpace(mapId))
        {
            StatusMessage = "Select a saved map to delete.";
            return;
        }

        IsBusy = true;
        try
        {
            if (!await _service.DeleteAsync(mapId))
            {
                StatusMessage = _service.LastError ?? "Delete failed.";
                return;
            }

            StatusMessage = $"Deleted '{mapId}'.";
            SelectedMap = null;
            StartNewMap();
            await LoadMapsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Exports every map graph as an indented JSON document.</summary>
    /// <returns>The JSON document, or an empty string when loading failed.</returns>
    public Task<string> BuildExportJsonAsync() => _service.ExportJsonAsync();

    /// <summary>Writes every map graph to a JSON file.</summary>
    /// <param name="path">Destination file path.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public async Task<bool> ExportToFileAsync(string path)
    {
        IsBusy = true;
        try
        {
            var success = await _service.ExportToFileAsync(path);
            StatusMessage = success
                ? $"Exported maps to '{path}'."
                : _service.LastError ?? "Export failed.";
            return success;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Imports map graphs from a JSON file and reloads the editor.</summary>
    /// <param name="path">Source file path.</param>
    /// <param name="overwriteExisting">Whether existing maps with the same id are replaced.</param>
    /// <returns>The number of maps written.</returns>
    public async Task<int> ImportFromFileAsync(string path, bool overwriteExisting = true)
    {
        IsBusy = true;
        try
        {
            var written = await _service.ImportFromFileAsync(path, overwriteExisting);
            StatusMessage = written > 0
                ? $"Imported {written} map(s) from '{path}'."
                : _service.LastError ?? "Import found no maps to write.";
            await LoadMapsAsync();
            return written;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Imports map graphs from a JSON document and reloads the editor.</summary>
    /// <param name="json">The JSON authoring document.</param>
    /// <param name="overwriteExisting">Whether existing maps with the same id are replaced.</param>
    /// <returns>The number of maps written.</returns>
    public async Task<int> ImportJsonAsync(string json, bool overwriteExisting = true)
    {
        IsBusy = true;
        try
        {
            var written = await _service.ImportJsonAsync(json, overwriteExisting);
            StatusMessage = written > 0
                ? $"Imported {written} map(s)."
                : _service.LastError ?? "Import found no maps to write.";
            await LoadMapsAsync();
            return written;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static MapSummary ToSummary(MapGraph graph) => new(
        graph.Id,
        graph.MapType,
        graph.OwnerId,
        graph.Name,
        graph.Description,
        graph.EntryNodeId,
        graph.Nodes.Count,
        graph.Edges.Count);

    private void ApplyGraph(MapGraph graph, bool isNew)
    {
        _suppressDirty = true;
        try
        {
            MapId = graph.Id;
            IsNew = isNew;
            SelectedMapType = graph.MapType;
            OwnerId = graph.OwnerId;
            Name = graph.Name;
            Description = graph.Description;
            EntryNodeId = graph.EntryNodeId;

            ReplaceNodes(graph.Nodes.Select(n => new MapNodeRowViewModel(n)));
            ReplaceEdges(graph.Edges.Select(e => new MapEdgeRowViewModel(e)));

            ValidationIssues.Clear();
            ValidationSummary = "Not validated.";
            IsDirty = false;
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private void ReplaceNodes(IEnumerable<MapNodeRowViewModel> nodes)
    {
        foreach (var node in Nodes)
            node.PropertyChanged -= OnRowPropertyChanged;

        Nodes.Clear();
        foreach (var node in nodes)
        {
            node.PropertyChanged += OnRowPropertyChanged;
            Nodes.Add(node);
        }

        SelectedNode = Nodes.FirstOrDefault();
        this.RaisePropertyChanged(nameof(NodeCountText));
    }

    private void ReplaceEdges(IEnumerable<MapEdgeRowViewModel> edges)
    {
        foreach (var edge in Edges)
            edge.PropertyChanged -= OnRowPropertyChanged;

        Edges.Clear();
        foreach (var edge in edges)
        {
            edge.PropertyChanged += OnRowPropertyChanged;
            Edges.Add(edge);
        }

        SelectedEdge = Edges.FirstOrDefault();
        this.RaisePropertyChanged(nameof(EdgeCountText));
    }

    private void ApplyValidation(MapGraphValidationResult result)
    {
        ValidationIssues.Clear();
        foreach (var issue in result.Issues)
            ValidationIssues.Add($"{issue.Severity}: {issue.Message} ({(issue.NodeId ?? issue.MapId ?? "map")})");

        var errors = result.Issues.Count(i => i.Severity == MapGraphValidationSeverity.Error);
        var warnings = result.Issues.Count(i => i.Severity == MapGraphValidationSeverity.Warning);
        ValidationSummary = result.Issues.Count == 0
            ? "Valid — no issues found."
            : $"{errors} error(s), {warnings} warning(s).";
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void MarkDirty()
    {
        if (_suppressDirty)
            return;

        IsDirty = true;
    }
}
