namespace Veldrath.Contracts.Map;

/// <summary>Lightweight map summary used by list views and authoring tools.</summary>
/// <param name="Id">Stable map identifier.</param>
/// <param name="MapType">Map kind: <c>World</c>, <c>Region</c>, or <c>Zone</c>.</param>
/// <param name="OwnerId">Region or zone slug that owns the map.</param>
/// <param name="Name">Human-readable map name.</param>
/// <param name="Description">Map description.</param>
/// <param name="EntryNodeId">Default arrival node, when configured.</param>
/// <param name="NodeCount">Number of nodes in the map.</param>
/// <param name="EdgeCount">Number of edges in the map.</param>
public record MapSummaryDto(
    string Id,
    string MapType,
    string OwnerId,
    string Name,
    string Description,
    string? EntryNodeId,
    int NodeCount,
    int EdgeCount,
    int Version = 1);

/// <summary>A complete region or zone map graph.</summary>
/// <param name="Id">Stable map identifier.</param>
/// <param name="MapType">Map kind: <c>World</c>, <c>Region</c>, or <c>Zone</c>.</param>
/// <param name="OwnerId">Region or zone slug that owns the map.</param>
/// <param name="Name">Human-readable map name.</param>
/// <param name="Description">Map description.</param>
/// <param name="EntryNodeId">Default arrival node, when configured.</param>
/// <param name="Nodes">Nodes belonging to the map.</param>
/// <param name="Edges">Intra-map edges belonging to the map.</param>
public record MapGraphDto(
    string Id,
    string MapType,
    string OwnerId,
    string Name,
    string Description,
    string? EntryNodeId,
    IReadOnlyList<MapNodeDto> Nodes,
    IReadOnlyList<MapEdgeDto> Edges,
    int Version = 1);

/// <summary>A node within a map graph.</summary>
/// <param name="Id">Stable node identifier.</param>
/// <param name="MapId">Map that owns the node.</param>
/// <param name="NodeType">Node kind, such as <c>EntryPoint</c>, <c>Zone</c>, <c>Location</c>, or <c>RegionExit</c>.</param>
/// <param name="RefId">Referenced zone, location, or region slug when applicable.</param>
/// <param name="DisplayName">Human-readable node label.</param>
/// <param name="Description">Detail text for the node.</param>
/// <param name="IconKey">Material icon key, when configured.</param>
/// <param name="LayoutX">Authored grid column, or <see langword="null"/> for automatic layout.</param>
/// <param name="LayoutY">Authored grid row, or <see langword="null"/> for automatic layout.</param>
/// <param name="IsEntryPoint">Whether this node is the map entry point.</param>
/// <param name="IsExitPoint">Whether this node represents a map transition.</param>
/// <param name="TargetMapId">Destination map for exit-like nodes.</param>
/// <param name="TargetNodeId">Destination node for exit-like nodes.</param>
/// <param name="IsHidden">Whether the node is hidden until discovered.</param>
/// <param name="UnlockType">Discovery mechanism, when hidden.</param>
/// <param name="UnlockKey">Quest, item, or achievement key required to unlock the node.</param>
/// <param name="DiscoverThreshold">Passive discovery level threshold, when applicable.</param>
/// <param name="SortOrder">Stable ordering hint for lists.</param>
public record MapNodeDto(
    string Id,
    string MapId,
    string NodeType,
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

/// <summary>A directed cardinal edge between two nodes on the same map.</summary>
/// <param name="Id">Edge identifier.</param>
/// <param name="MapId">Map that owns the edge.</param>
/// <param name="FromNodeId">Origin node identifier.</param>
/// <param name="ToNodeId">Destination node identifier.</param>
/// <param name="Direction">Cardinal direction: <c>North</c>, <c>South</c>, <c>East</c>, <c>West</c>, and vertical variants.</param>
/// <param name="EdgeType">Traversal type: <c>Walk</c>, <c>ExitToZone</c>, <c>ExitToRegion</c>, <c>Portal</c>, or <c>Door</c>.</param>
/// <param name="Label">Optional display label such as "East Road".</param>
/// <param name="IsTraversable">Whether the edge can currently be used.</param>
/// <param name="IsHidden">Whether the edge is hidden until discovered.</param>
public record MapEdgeDto(
    Guid Id,
    string MapId,
    string FromNodeId,
    string ToNodeId,
    string Direction,
    string EdgeType,
    string? Label,
    bool IsTraversable,
    bool IsHidden);

/// <summary>Payload for saving an authored map graph.</summary>
/// <param name="Map">The complete graph to persist. Its id must match the route.</param>
/// <param name="ExpectedVersion">
/// The version the author started from. A mismatch is a conflict rather than a silent overwrite, which is what
/// stops two editors from erasing each other's work.
/// </param>
public record MapGraphWriteRequest(MapGraphDto Map, int ExpectedVersion);

/// <summary>One validation issue reported by a map save.</summary>
/// <param name="Code">Stable machine-readable issue code, such as <c>node_unreachable</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Severity"><c>Error</c> or <c>Warning</c>.</param>
/// <param name="MapId">Map the issue belongs to, when applicable.</param>
/// <param name="NodeId">Node the issue belongs to, when applicable.</param>
public record MapGraphIssueDto(string Code, string Message, string Severity, string? MapId, string? NodeId);

/// <summary>Outcome of a map save attempt.</summary>
/// <param name="Success">Whether the graph was persisted.</param>
/// <param name="Status"><c>ok</c>, <c>conflict</c>, <c>invalid</c>, or <c>not_found</c>.</param>
/// <param name="Version">The stored version after the attempt: the new one on success, the current one otherwise.</param>
/// <param name="Issues">Validation issues, populated when the graph was refused.</param>
/// <param name="Message">Optional human-readable summary.</param>
public record MapGraphSaveResponse(
    bool Success,
    string Status,
    int Version,
    IReadOnlyList<MapGraphIssueDto> Issues,
    string? Message = null);

/// <summary>Hub request to travel to a map node.</summary>
/// <param name="MapId">Map containing the selected node.</param>
/// <param name="NodeId">Destination node identifier.</param>
public record TravelToNodeHubRequest(string MapId, string NodeId);

/// <summary>Hub request to enter a named location inside a zone.</summary>
/// <param name="ZoneId">Zone that owns the location.</param>
/// <param name="LocationSlug">Location to enter, by content slug.</param>
public record EnterZoneLocationHubRequest(string ZoneId, string LocationSlug);

