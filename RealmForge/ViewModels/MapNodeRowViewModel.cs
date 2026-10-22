using ReactiveUI;
using RealmEngine.Shared.Models;

namespace RealmForge.ViewModels;

/// <summary>
/// Editable row model for a single map node in the RealmForge map-graph editor.
/// </summary>
public sealed class MapNodeRowViewModel : ReactiveObject
{
    private string _id = string.Empty;
    private MapNodeType _nodeType = MapNodeType.Location;
    private string? _refId;
    private string _displayName = string.Empty;
    private string _description = string.Empty;
    private string? _iconKey;
    private int? _layoutX;
    private int? _layoutY;
    private bool _isEntryPoint;
    private bool _isExitPoint;
    private string? _targetMapId;
    private string? _targetNodeId;
    private bool _isHidden;
    private string? _unlockType;
    private string? _unlockKey;
    private int? _discoverThreshold;
    private int _sortOrder;

    /// <summary>Creates an empty row for a new node.</summary>
    public MapNodeRowViewModel()
    {
    }

    /// <summary>Creates a row populated from an existing map node entry.</summary>
    /// <param name="entry">The node entry to copy.</param>
    public MapNodeRowViewModel(MapNodeEntry entry)
    {
        Id = entry.Id;
        NodeType = entry.NodeType;
        RefId = entry.RefId;
        DisplayName = entry.DisplayName;
        Description = entry.Description;
        IconKey = entry.IconKey;
        LayoutX = entry.LayoutX;
        LayoutY = entry.LayoutY;
        IsEntryPoint = entry.IsEntryPoint;
        IsExitPoint = entry.IsExitPoint;
        TargetMapId = entry.TargetMapId;
        TargetNodeId = entry.TargetNodeId;
        IsHidden = entry.IsHidden;
        UnlockType = entry.UnlockType;
        UnlockKey = entry.UnlockKey;
        DiscoverThreshold = entry.DiscoverThreshold;
        SortOrder = entry.SortOrder;
    }

    /// <summary>Stable node identifier.</summary>
    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    /// <summary>Role the node plays in the map.</summary>
    public MapNodeType NodeType
    {
        get => _nodeType;
        set => this.RaiseAndSetIfChanged(ref _nodeType, value);
    }

    /// <summary>Referenced zone, location, or region slug when applicable.</summary>
    public string? RefId
    {
        get => _refId;
        set => this.RaiseAndSetIfChanged(ref _refId, value);
    }

    /// <summary>Human-readable node label.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    /// <summary>Detail text for the node.</summary>
    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    /// <summary>Material icon key, or <see langword="null"/>.</summary>
    public string? IconKey
    {
        get => _iconKey;
        set => this.RaiseAndSetIfChanged(ref _iconKey, value);
    }

    /// <summary>Authored grid column, or <see langword="null"/> for auto-layout.</summary>
    public int? LayoutX
    {
        get => _layoutX;
        set => this.RaiseAndSetIfChanged(ref _layoutX, value);
    }

    /// <summary>Authored grid row, or <see langword="null"/> for auto-layout.</summary>
    public int? LayoutY
    {
        get => _layoutY;
        set => this.RaiseAndSetIfChanged(ref _layoutY, value);
    }

    /// <summary>Whether this node is the map entry point.</summary>
    public bool IsEntryPoint
    {
        get => _isEntryPoint;
        set => this.RaiseAndSetIfChanged(ref _isEntryPoint, value);
    }

    /// <summary>Whether this node represents a map transition.</summary>
    public bool IsExitPoint
    {
        get => _isExitPoint;
        set => this.RaiseAndSetIfChanged(ref _isExitPoint, value);
    }

    /// <summary>Destination map for exit-like nodes.</summary>
    public string? TargetMapId
    {
        get => _targetMapId;
        set => this.RaiseAndSetIfChanged(ref _targetMapId, value);
    }

    /// <summary>Destination node for exit-like nodes.</summary>
    public string? TargetNodeId
    {
        get => _targetNodeId;
        set => this.RaiseAndSetIfChanged(ref _targetNodeId, value);
    }

    /// <summary>Whether the node is hidden until discovered.</summary>
    public bool IsHidden
    {
        get => _isHidden;
        set => this.RaiseAndSetIfChanged(ref _isHidden, value);
    }

    /// <summary>Discovery mechanism, when hidden.</summary>
    public string? UnlockType
    {
        get => _unlockType;
        set => this.RaiseAndSetIfChanged(ref _unlockType, value);
    }

    /// <summary>Quest, item, or achievement key required to unlock the node.</summary>
    public string? UnlockKey
    {
        get => _unlockKey;
        set => this.RaiseAndSetIfChanged(ref _unlockKey, value);
    }

    /// <summary>Passive discovery level threshold, when applicable.</summary>
    public int? DiscoverThreshold
    {
        get => _discoverThreshold;
        set => this.RaiseAndSetIfChanged(ref _discoverThreshold, value);
    }

    /// <summary>Stable ordering hint for text lists.</summary>
    public int SortOrder
    {
        get => _sortOrder;
        set => this.RaiseAndSetIfChanged(ref _sortOrder, value);
    }

    /// <summary>All node types available for selection in the editor.</summary>
    public IReadOnlyList<MapNodeType> NodeTypes { get; } = Enum.GetValues<MapNodeType>();

    /// <summary>Short label used by node pickers and lists.</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName)
        ? Id
        : $"{Id} · {DisplayName}";

    /// <summary>Projects this row back into an authoring entry.</summary>
    /// <param name="mapId">Identifier of the owning map.</param>
    /// <returns>The projected node entry.</returns>
    public MapNodeEntry ToEntry(string mapId) => new(
        Id,
        mapId,
        NodeType,
        RefId,
        DisplayName,
        Description,
        IconKey,
        LayoutX,
        LayoutY,
        IsEntryPoint,
        IsExitPoint,
        TargetMapId,
        TargetNodeId,
        IsHidden,
        UnlockType,
        UnlockKey,
        DiscoverThreshold,
        SortOrder);
}
