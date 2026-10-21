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
    int EdgeCount);

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
    IReadOnlyList<MapEdgeDto> Edges);

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

/// <summary>Hub request to travel to a map node.</summary>
/// <param name="MapId">Map containing the selected node.</param>
/// <param name="NodeId">Destination node identifier.</param>
public record TravelToNodeHubRequest(string MapId, string NodeId);

/// <summary>Result of travelling to a map node.</summary>
/// <param name="Success">Whether the travel succeeded.</param>
/// <param name="ErrorMessage">Failure reason when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="MapId">Map the character is on after the action.</param>
/// <param name="NodeId">Node the character occupies after the action.</param>
/// <param name="MapChanged">Whether the action moved the character to another map.</param>
/// <param name="Message">Human-readable outcome message.</param>
public record TravelToNodeResultDto(
    bool Success,
    string? ErrorMessage,
    string? MapId,
    string? NodeId,
    bool MapChanged,
    string? Message);
