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

    /// <summary>
    /// Whether the map is in the game. Only a reviewer's approval sets this: the game read path filters on it,
    /// so a draft is invisible to players and an edit cannot flip it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Content version, incremented by authoring tools.</summary>
    public int Version { get; set; } = 1;

    /// <summary>UTC timestamp of the last content update.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Account that created the map, or <see langword="null"/> for seeded content.</summary>
    public Guid? CreatedByAccountId { get; set; }

    /// <summary>When the map was put forward for review, or <see langword="null"/> while it is an unsubmitted draft.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Account that submitted the map for review.</summary>
    public Guid? SubmittedByAccountId { get; set; }

    /// <summary>When a reviewer last approved the map.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Account that approved the map.</summary>
    public Guid? ApprovedByAccountId { get; set; }

    /// <summary>
    /// The map version a reviewer signed off. Zero means never approved; a value below <see cref="Version"/> means
    /// the map is live but has changed since it was reviewed.
    /// </summary>
    public int ApprovedVersion { get; set; }

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
