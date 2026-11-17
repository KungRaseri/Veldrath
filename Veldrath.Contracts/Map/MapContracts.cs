namespace Veldrath.Contracts.Map;

/// <summary>Whether approving a submitted map would succeed, as the review queue reports it.</summary>
/// <param name="Status">
/// <c>ready</c> when the decision would accept the map, <c>blocked</c> when it would refuse it.
/// </param>
/// <param name="Errors">Validation errors, which refuse an approval exactly as they refuse a save.</param>
/// <param name="Warnings">Warnings, which a submitted draft may carry and which a reviewer still has to see.</param>
public record MapReviewVerdictDto(string Status, int Errors, int Warnings);

/// <summary>Lightweight map summary used by list views and authoring tools.</summary>
/// <param name="Id">Stable map identifier.</param>
/// <param name="MapType">Map kind: <c>World</c>, <c>Region</c>, or <c>Zone</c>.</param>
/// <param name="OwnerId">Region or zone slug that owns the map.</param>
/// <param name="Name">Human-readable map name.</param>
/// <param name="Description">Map description.</param>
/// <param name="EntryNodeId">Default arrival node, when configured.</param>
/// <param name="NodeCount">Number of nodes in the map.</param>
/// <param name="EdgeCount">Number of edges in the map.</param>
/// <param name="Review">
/// Whether approving the map would succeed. Filled for maps awaiting a decision, which is what the review
/// queue asks about each row; <see langword="null"/> on every other map, and when the verdict could not be
/// worked out.
/// </param>
public record MapSummaryDto(
    string Id,
    string MapType,
    string OwnerId,
    string Name,
    string Description,
    string? EntryNodeId,
    int NodeCount,
    int EdgeCount,
    int Version = 1,
    bool IsActive = true,
    int ApprovedVersion = 0,
    DateTimeOffset? SubmittedAt = null,
    string? LastReviewNotes = null,
    DateTimeOffset? LastReviewedAt = null,
    bool CanEdit = false,
    MapReviewVerdictDto? Review = null)
{
    /// <summary>Whether the map is live but has been edited since a reviewer approved it.</summary>
    public bool ChangedSinceApproval => IsActive && ApprovedVersion < Version;
}

/// <summary>Request to create an empty draft map.</summary>
/// <param name="MapType">Map kind: <c>Region</c>, <c>Zone</c>, or <c>Interior</c>.</param>
/// <param name="OwnerId">Region, zone or location slug that will own the map.</param>
/// <param name="Name">Human-readable map name.</param>
public record MapCreateRequest(string MapType, string OwnerId, string Name);

/// <summary>A reviewer's decision on a submitted map.</summary>
/// <param name="Approved">Whether the map should enter the game.</param>
/// <param name="Notes">Optional review notes, recorded on the rejection path.</param>
public record MapReviewRequest(bool Approved, string? Notes = null);

/// <summary>Outcome of a lifecycle action — submit, review, deactivate or delete.</summary>
/// <param name="Success">Whether the action changed anything.</param>
/// <param name="Status"><c>ok</c>, <c>invalid</c>, <c>not_found</c> or <c>conflict</c>.</param>
/// <param name="Message">Human-readable explanation, including the reason a refusal happened.</param>
/// <param name="Issues">Validation issues, populated when a graph is not ready for the action.</param>
public record MapLifecycleResponse(
    bool Success,
    string Status,
    string? Message,
    IReadOnlyList<MapGraphIssueDto> Issues);

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
    int Version = 1,
    bool IsActive = true,
    int ApprovedVersion = 0,
    DateTimeOffset? SubmittedAt = null,
    DateTimeOffset? ApprovedAt = null,
    string? LastReviewNotes = null,
    DateTimeOffset? LastReviewedAt = null,
    bool CanEdit = false)
{
    /// <summary>
    /// Whether the map has been edited since a reviewer approved it. Derived rather than stored, so it cannot
    /// disagree with the two numbers it comes from.
    /// </summary>
    public bool ChangedSinceApproval => IsActive && ApprovedVersion < Version;
}

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

/// <summary>One option in a reference picker: a slug or identifier, and the label to show.</summary>
/// <param name="Id">The value stored on the node — a slug, a map id, or an owner slug.</param>
/// <param name="Label">Human-readable name for the option.</param>
public record MapTargetOptionDto(string Id, string Label);

/// <summary>One node of an authored map, for the destination picker on an exit.</summary>
/// <param name="Id">The node identifier.</param>
/// <param name="MapId">The map that owns the node.</param>
/// <param name="Label">Human-readable node label.</param>
/// <param name="IsEntryPoint">Whether the node is its map's entry point, so a picker can default to it.</param>
public record MapNodeOptionDto(string Id, string MapId, string Label, bool IsEntryPoint);

/// <summary>
/// The references a node may point at.
/// </summary>
/// <param name="Maps">Every authored map, for exit destinations.</param>
/// <param name="Zones">Owners of the zone and interior maps, for a node whose type is <c>Zone</c>.</param>
/// <param name="Locations">Every zone location, for a room node's <c>RefId</c>.</param>
/// <param name="Regions">Owners of the region maps, for a region exit's <c>RefId</c>.</param>
/// <param name="Nodes">Every node of every map, so an exit's destination node is chosen rather than typed.</param>
/// <remarks>
/// Served so the editor can render pickers instead of free text: a mistyped reference is the defect the
/// world validator has to catch, and a picker cannot produce one.
/// </remarks>
public record MapTargetsDto(
    IReadOnlyList<MapTargetOptionDto> Maps,
    IReadOnlyList<MapTargetOptionDto> Zones,
    IReadOnlyList<MapTargetOptionDto> Locations,
    IReadOnlyList<MapTargetOptionDto> Regions,
    IReadOnlyList<MapNodeOptionDto> Nodes);

/// <summary>Payload for validating an authored map graph without saving it.</summary>
/// <param name="Map">The candidate graph. Its id must match the route.</param>
public record MapGraphValidationRequest(MapGraphDto Map);

/// <summary>Outcome of a validate-only pass over the whole world.</summary>
/// <param name="IsValid">Whether a save would accept the graph: no issue has <c>Error</c> severity.</param>
/// <param name="Issues">
/// Every issue attributed to this map, plus any exit that points at a map that does not exist. A
/// pre-existing defect in a sibling map is not listed, so an edit here is never blocked by someone else's.
/// </param>
public record MapGraphValidationResponse(bool IsValid, IReadOnlyList<MapGraphIssueDto> Issues);

/// <summary>Hub request to travel to a map node.</summary>
/// <param name="MapId">Map containing the selected node.</param>
/// <param name="NodeId">Destination node identifier.</param>
public record TravelToNodeHubRequest(string MapId, string NodeId);

/// <summary>Hub request to enter a named location inside a zone.</summary>
/// <param name="ZoneId">Zone that owns the location.</param>
/// <param name="LocationSlug">Location to enter, by content slug.</param>
public record EnterZoneLocationHubRequest(string ZoneId, string LocationSlug);

