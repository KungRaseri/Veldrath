using System.Text.Json.Serialization;

namespace RealmEngine.Shared.Models;

/// <summary>Kind of map represented by a <see cref="MapGraph"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MapType
{
    /// <summary>World-level map. Reserved for a later slice.</summary>
    World,

    /// <summary>Region map whose nodes are zones and region exits.</summary>
    Region,

    /// <summary>Zone map whose nodes are locations and zone/region exits.</summary>
    Zone
}

/// <summary>Role a node plays within a map graph.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MapNodeType
{
    /// <summary>Default arrival point for the map.</summary>
    EntryPoint,

    /// <summary>A zone node on a region map.</summary>
    Zone,

    /// <summary>A named location node on a zone map.</summary>
    Location,

    /// <summary>An exit that leads to another zone.</summary>
    ZoneExit,

    /// <summary>An exit that leads to another region or back to the region map.</summary>
    RegionExit,

    /// <summary>A portal or other special transition.</summary>
    Portal,

    /// <summary>A stair or vertical transition.</summary>
    Stair,

    /// <summary>A flavour or navigation landmark with no transition.</summary>
    Landmark
}

/// <summary>Cardinal direction of a map edge or exit.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MapDirection
{
    /// <summary>North.</summary>
    North,

    /// <summary>South.</summary>
    South,

    /// <summary>East.</summary>
    East,

    /// <summary>West.</summary>
    West,

    /// <summary>Upwards, typically a stair or lift.</summary>
    Up,

    /// <summary>Downwards, typically a stair or shaft.</summary>
    Down,

    /// <summary>Inwards, typically an entrance.</summary>
    In,

    /// <summary>Outwards, typically an exit.</summary>
    Out
}

/// <summary>Type of traversal represented by a map edge.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MapEdgeType
{
    /// <summary>Normal walking travel within the map.</summary>
    Walk,

    /// <summary>An edge that leads to another zone.</summary>
    ExitToZone,

    /// <summary>An edge that leads to another region or the region map.</summary>
    ExitToRegion,

    /// <summary>A portal transition.</summary>
    Portal,

    /// <summary>A door transition.</summary>
    Door
}

/// <summary>A complete read model of a region or zone map graph.</summary>
/// <param name="Id">Stable map identifier.</param>
/// <param name="MapType">Whether this is a region or zone map.</param>
/// <param name="OwnerId">Region or zone slug that owns the map.</param>
/// <param name="Name">Human-readable map name.</param>
/// <param name="Description">Map description shown in the UI.</param>
/// <param name="EntryNodeId">Default arrival node, or <see langword="null"/> if unset.</param>
/// <param name="Nodes">All nodes belonging to the map.</param>
/// <param name="Edges">All intra-map edges belonging to the map.</param>
public sealed record MapGraph(
    string Id,
    MapType MapType,
    string OwnerId,
    string Name,
    string Description,
    string? EntryNodeId,
    IReadOnlyList<MapNodeEntry> Nodes,
    IReadOnlyList<MapEdgeEntry> Edges);

/// <summary>A single node in a map graph.</summary>
/// <param name="Id">Stable node identifier.</param>
/// <param name="MapId">Map that owns this node.</param>
/// <param name="NodeType">Role the node plays in the map.</param>
/// <param name="RefId">Referenced zone, location, or region slug when applicable.</param>
/// <param name="DisplayName">Human-readable node label.</param>
/// <param name="Description">Detail text for the node.</param>
/// <param name="IconKey">Material icon key, or <see langword="null"/>.</param>
/// <param name="LayoutX">Authored grid column, or <see langword="null"/> for auto-layout.</param>
/// <param name="LayoutY">Authored grid row, or <see langword="null"/> for auto-layout.</param>
/// <param name="IsEntryPoint">Whether this node is the map entry point.</param>
/// <param name="IsExitPoint">Whether this node represents a map transition.</param>
/// <param name="TargetMapId">Destination map for exit-like nodes, or <see langword="null"/>.</param>
/// <param name="TargetNodeId">Destination node for exit-like nodes, or <see langword="null"/>.</param>
/// <param name="IsHidden">Whether the node is hidden until discovered.</param>
/// <param name="UnlockType">Discovery mechanism, when hidden.</param>
/// <param name="UnlockKey">Quest, item, or achievement key required to unlock the node.</param>
/// <param name="DiscoverThreshold">Passive discovery level threshold, when applicable.</param>
/// <param name="SortOrder">Stable ordering hint for text lists.</param>
public sealed record MapNodeEntry(
    string Id,
    string MapId,
    MapNodeType NodeType,
    string? RefId,
    string DisplayName,
    string Description,
    string? IconKey,
    int? LayoutX,
    int? LayoutY,
    bool IsEntryPoint,
    bool IsExitPoint,
    string? TargetMapId,
    string? TargetNodeId,
    bool IsHidden,
    string? UnlockType,
    string? UnlockKey,
    int? DiscoverThreshold,
    int SortOrder);

/// <summary>A single intra-map edge in a map graph.</summary>
/// <param name="Id">Edge identifier.</param>
/// <param name="MapId">Map that owns this edge.</param>
/// <param name="FromNodeId">Origin node identifier.</param>
/// <param name="ToNodeId">Destination node identifier.</param>
/// <param name="Direction">Cardinal direction of the edge.</param>
/// <param name="EdgeType">Traversal type.</param>
/// <param name="Label">Optional display label such as "East Road".</param>
/// <param name="IsTraversable">Whether the edge can currently be used.</param>
/// <param name="IsHidden">Whether the edge is hidden until discovered.</param>
public sealed record MapEdgeEntry(
    Guid Id,
    string MapId,
    string FromNodeId,
    string ToNodeId,
    MapDirection Direction,
    MapEdgeType EdgeType,
    string? Label,
    bool IsTraversable,
    bool IsHidden);

/// <summary>Lightweight summary of a map for list views.</summary>
/// <param name="Id">Stable map identifier.</param>
/// <param name="MapType">Whether this is a region or zone map.</param>
/// <param name="OwnerId">Region or zone slug that owns the map.</param>
/// <param name="Name">Human-readable map name.</param>
/// <param name="Description">Map description.</param>
/// <param name="EntryNodeId">Default arrival node, or <see langword="null"/>.</param>
/// <param name="NodeCount">Number of nodes in the map.</param>
/// <param name="EdgeCount">Number of edges in the map.</param>
public sealed record MapSummary(
    string Id,
    MapType MapType,
    string OwnerId,
    string Name,
    string Description,
    string? EntryNodeId,
    int NodeCount,
    int EdgeCount);
