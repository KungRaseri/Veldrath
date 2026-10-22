using ReactiveUI;
using RealmEngine.Shared.Models;

namespace RealmForge.ViewModels;

/// <summary>
/// Editable row model for a single intra-map edge in the RealmForge map-graph editor.
/// </summary>
public sealed class MapEdgeRowViewModel : ReactiveObject
{
    private Guid _id = Guid.NewGuid();
    private string _fromNodeId = string.Empty;
    private string _toNodeId = string.Empty;
    private MapDirection _direction = MapDirection.North;
    private MapEdgeType _edgeType = MapEdgeType.Walk;
    private string? _label;
    private bool _isTraversable = true;
    private bool _isHidden;

    /// <summary>Creates an empty row for a new edge.</summary>
    public MapEdgeRowViewModel()
    {
    }

    /// <summary>Creates a row populated from an existing map edge entry.</summary>
    /// <param name="entry">The edge entry to copy.</param>
    public MapEdgeRowViewModel(MapEdgeEntry entry)
    {
        Id = entry.Id;
        FromNodeId = entry.FromNodeId;
        ToNodeId = entry.ToNodeId;
        Direction = entry.Direction;
        EdgeType = entry.EdgeType;
        Label = entry.Label;
        IsTraversable = entry.IsTraversable;
        IsHidden = entry.IsHidden;
    }

    /// <summary>Edge identifier.</summary>
    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    /// <summary>Origin node identifier.</summary>
    public string FromNodeId
    {
        get => _fromNodeId;
        set => this.RaiseAndSetIfChanged(ref _fromNodeId, value);
    }

    /// <summary>Destination node identifier.</summary>
    public string ToNodeId
    {
        get => _toNodeId;
        set => this.RaiseAndSetIfChanged(ref _toNodeId, value);
    }

    /// <summary>Cardinal direction of the edge.</summary>
    public MapDirection Direction
    {
        get => _direction;
        set => this.RaiseAndSetIfChanged(ref _direction, value);
    }

    /// <summary>Traversal type of the edge.</summary>
    public MapEdgeType EdgeType
    {
        get => _edgeType;
        set => this.RaiseAndSetIfChanged(ref _edgeType, value);
    }

    /// <summary>Optional display label such as "East Road".</summary>
    public string? Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    /// <summary>Whether the edge can currently be used.</summary>
    public bool IsTraversable
    {
        get => _isTraversable;
        set => this.RaiseAndSetIfChanged(ref _isTraversable, value);
    }

    /// <summary>Whether the edge is hidden until discovered.</summary>
    public bool IsHidden
    {
        get => _isHidden;
        set => this.RaiseAndSetIfChanged(ref _isHidden, value);
    }

    /// <summary>All cardinal directions available for selection in the editor.</summary>
    public IReadOnlyList<MapDirection> Directions { get; } = Enum.GetValues<MapDirection>();

    /// <summary>All edge types available for selection in the editor.</summary>
    public IReadOnlyList<MapEdgeType> EdgeTypes { get; } = Enum.GetValues<MapEdgeType>();

    /// <summary>Short label used by edge pickers and lists.</summary>
    public string DisplayLabel =>
        $"{FromNodeId} → {ToNodeId} ({Direction})";

    /// <summary>Projects this row back into an authoring entry.</summary>
    /// <param name="mapId">Identifier of the owning map.</param>
    /// <returns>The projected edge entry.</returns>
    public MapEdgeEntry ToEntry(string mapId) => new(
        Id,
        mapId,
        FromNodeId,
        ToNodeId,
        Direction,
        EdgeType,
        Label,
        IsTraversable,
        IsHidden);
}
