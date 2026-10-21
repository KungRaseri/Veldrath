using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Entities;

/// <summary>
/// A region or zone map definition. Each map owns a set of nodes and intra-map edges.
/// Exits between maps are represented by exit nodes carrying a target map and node.
/// </summary>
public class Map
{
    /// <summary>Stable map identifier, for example <c>region:thornveil</c> or <c>zone:crestfall</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Whether this map represents a region, a zone, or a future world map.</summary>
    public MapType MapType { get; set; } = MapType.Zone;

    /// <summary>Region or zone slug that owns this map.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Human-readable map name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description shown in the map header.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Default arrival node identifier, or <see langword="null"/> when unset.</summary>
    public string? EntryNodeId { get; set; }

    /// <summary>Whether the map is active content.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Content version, incremented by authoring tools.</summary>
    public int Version { get; set; } = 1;

    /// <summary>UTC timestamp of the last content update.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Nodes belonging to this map.</summary>
    public ICollection<MapNode> Nodes { get; set; } = [];

    /// <summary>Intra-map edges belonging to this map.</summary>
    public ICollection<MapEdge> Edges { get; set; } = [];
}

/// <summary>
/// A node within a region or zone map. Exit-like nodes carry the destination map and node
/// so that map transitions are explicit data rather than special-case logic.
/// </summary>
public class MapNode
{
    /// <summary>Stable node identifier, for example <c>crestfall:square</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Map that owns this node.</summary>
    public string MapId { get; set; } = string.Empty;

    /// <summary>Role the node plays in the map.</summary>
    public MapNodeType NodeType { get; set; } = MapNodeType.Location;

    /// <summary>Referenced zone, location, or region slug, when applicable.</summary>
    public string? RefId { get; set; }

    /// <summary>Human-readable node label.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Detail text shown in the node panel.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Material icon key, or <see langword="null"/>.</summary>
    public string? IconKey { get; set; }

    /// <summary>Authored grid column, or <see langword="null"/> for automatic layout.</summary>
    public int? LayoutX { get; set; }

    /// <summary>Authored grid row, or <see langword="null"/> for automatic layout.</summary>
    public int? LayoutY { get; set; }

    /// <summary>Whether this node is the map's entry point.</summary>
    public bool IsEntryPoint { get; set; }

    /// <summary>Whether this node represents a map transition.</summary>
    public bool IsExitPoint { get; set; }

    /// <summary>Destination map identifier for exit-like nodes.</summary>
    public string? TargetMapId { get; set; }

    /// <summary>Destination node identifier for exit-like nodes.</summary>
    public string? TargetNodeId { get; set; }

    /// <summary>Whether the node is hidden until discovered.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Discovery mechanism, when hidden.</summary>
    public string? UnlockType { get; set; }

    /// <summary>Quest, item, or achievement key required to unlock the node.</summary>
    public string? UnlockKey { get; set; }

    /// <summary>Passive discovery level threshold, when applicable.</summary>
    public int? DiscoverThreshold { get; set; }

    /// <summary>Stable ordering hint for text lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Parent map.</summary>
    public Map Map { get; set; } = null!;

    /// <summary>Edges starting at this node.</summary>
    public ICollection<MapEdge> OutgoingEdges { get; set; } = [];

    /// <summary>Edges ending at this node.</summary>
    public ICollection<MapEdge> IncomingEdges { get; set; } = [];
}

/// <summary>
/// A directed, cardinal edge between two nodes on the same map.
/// Cross-map transitions belong to exit nodes, not to edges.
/// </summary>
public class MapEdge
{
    /// <summary>Edge identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Map that owns this edge.</summary>
    public string MapId { get; set; } = string.Empty;

    /// <summary>Origin node identifier.</summary>
    public string FromNodeId { get; set; } = string.Empty;

    /// <summary>Destination node identifier.</summary>
    public string ToNodeId { get; set; } = string.Empty;

    /// <summary>Cardinal direction from the origin node.</summary>
    public MapDirection Direction { get; set; } = MapDirection.North;

    /// <summary>Traversal type of the edge.</summary>
    public MapEdgeType EdgeType { get; set; } = MapEdgeType.Walk;

    /// <summary>Optional display label such as <c>East Road</c>.</summary>
    public string? Label { get; set; }

    /// <summary>Whether the edge can currently be used.</summary>
    public bool IsTraversable { get; set; } = true;

    /// <summary>Whether the edge is hidden until discovered.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Parent map.</summary>
    public Map Map { get; set; } = null!;

    /// <summary>Origin node.</summary>
    public MapNode FromNode { get; set; } = null!;

    /// <summary>Destination node.</summary>
    public MapNode ToNode { get; set; } = null!;
}
