using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Validation;

/// <summary>Severity of a map graph validation issue.</summary>
public enum MapGraphValidationSeverity
{
    /// <summary>The graph is usable, but the issue should be reviewed.</summary>
    Warning,

    /// <summary>The graph is not usable until the issue is fixed.</summary>
    Error
}

/// <summary>A single issue found while validating a map graph.</summary>
/// <param name="Code">Stable machine-readable issue code.</param>
/// <param name="Message">Human-readable issue description.</param>
/// <param name="Severity">Whether the issue blocks use of the graph.</param>
/// <param name="MapId">Map involved in the issue, when applicable.</param>
/// <param name="NodeId">Node involved in the issue, when applicable.</param>
public sealed record MapGraphValidationIssue(
    string Code,
    string Message,
    MapGraphValidationSeverity Severity = MapGraphValidationSeverity.Error,
    string? MapId = null,
    string? NodeId = null);

/// <summary>Result of validating a map graph.</summary>
/// <param name="Issues">All issues found during validation.</param>
public sealed record MapGraphValidationResult(IReadOnlyList<MapGraphValidationIssue> Issues)
{
    /// <summary>Gets a value indicating whether the graph has no error-level issues.</summary>
    public bool IsValid => Issues.All(i => i.Severity != MapGraphValidationSeverity.Error);
}

/// <summary>
/// Validates region and zone map graphs before they are seeded or served.
/// The validator enforces explicit entry/exit nodes, intra-map cardinal edges,
/// and optional cross-context slug references.
/// </summary>
public static class MapGraphValidator
{
    /// <summary>Validates a map graph.</summary>
    /// <param name="map">The graph to validate.</param>
    /// <param name="knownRegionSlugs">Optional set of valid region slugs for cross-context checks.</param>
    /// <param name="knownZoneSlugs">Optional set of valid zone slugs for cross-context checks.</param>
    /// <param name="knownLocationSlugs">Optional set of valid location slugs for cross-context checks.</param>
    /// <returns>A validation result containing errors and warnings.</returns>
    public static MapGraphValidationResult Validate(
        MapGraph map,
        IReadOnlySet<string>? knownRegionSlugs = null,
        IReadOnlySet<string>? knownZoneSlugs = null,
        IReadOnlySet<string>? knownLocationSlugs = null)
    {
        var issues = new List<MapGraphValidationIssue>();

        if (string.IsNullOrWhiteSpace(map.Id))
            AddError(issues, "map_id_missing", "Map id is required.");
        if (string.IsNullOrWhiteSpace(map.OwnerId))
            AddError(issues, "map_owner_missing", "Map owner id is required.", map.Id);
        if (string.IsNullOrWhiteSpace(map.Name))
            AddError(issues, "map_name_missing", "Map name is required.", map.Id);

        var nodes = new Dictionary<string, MapNodeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in map.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                AddError(issues, "node_id_missing", "Node id is required.", map.Id);
                continue;
            }

            if (!nodes.TryAdd(node.Id, node))
            {
                AddError(issues, "node_duplicate", $"Node '{node.Id}' is duplicated.", map.Id, node.Id);
                continue;
            }

            if (!string.Equals(node.MapId, map.Id, StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    issues,
                    "node_map_mismatch",
                    $"Node '{node.Id}' belongs to map '{node.MapId}' but is listed on '{map.Id}'.",
                    map.Id,
                    node.Id);
            }

            if (string.IsNullOrWhiteSpace(node.DisplayName))
                AddError(issues, "node_name_missing", $"Node '{node.Id}' has no display name.", map.Id, node.Id);

            var isExitLike = node.NodeType is
                MapNodeType.ZoneExit or
                MapNodeType.RegionExit or
                MapNodeType.Portal or
                MapNodeType.Stair;

            if (isExitLike)
            {
                if (string.IsNullOrWhiteSpace(node.TargetMapId) || string.IsNullOrWhiteSpace(node.TargetNodeId))
                {
                    AddError(
                        issues,
                        "exit_target_missing",
                        $"Exit node '{node.Id}' must define TargetMapId and TargetNodeId.",
                        map.Id,
                        node.Id);
                }

                if (!node.IsExitPoint)
                {
                    AddWarning(
                        issues,
                        "exit_flag_missing",
                        $"Exit node '{node.Id}' is not marked IsExitPoint.",
                        map.Id,
                        node.Id);
                }
            }
            else if (node.IsExitPoint)
            {
                AddWarning(
                    issues,
                    "exit_flag_unexpected",
                    $"Node '{node.Id}' is marked IsExitPoint but is not an exit node type.",
                    map.Id,
                    node.Id);
            }

            if (node.NodeType == MapNodeType.EntryPoint && !node.IsEntryPoint)
            {
                AddError(
                    issues,
                    "entry_flag_missing",
                    $"Entry node '{node.Id}' is not marked IsEntryPoint.",
                    map.Id,
                    node.Id);
            }

            ValidateCrossContextReference(node, map.Id, knownRegionSlugs, knownZoneSlugs, knownLocationSlugs, issues);
        }

        ValidateEntryNode(map, nodes, issues);
        ValidateEdges(map, nodes, issues);
        ValidateReachability(map, nodes, issues);

        return new MapGraphValidationResult(issues);
    }

    private static void ValidateEntryNode(
        MapGraph map,
        IReadOnlyDictionary<string, MapNodeEntry> nodes,
        ICollection<MapGraphValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(map.EntryNodeId))
        {
            AddError(issues, "entry_missing", "Map must define EntryNodeId.", map.Id);
            return;
        }

        if (!nodes.TryGetValue(map.EntryNodeId, out var entryNode))
        {
            AddError(
                issues,
                "entry_not_found",
                $"Entry node '{map.EntryNodeId}' does not exist on the map.",
                map.Id,
                map.EntryNodeId);
            return;
        }

        if (!entryNode.IsEntryPoint)
        {
            AddError(
                issues,
                "entry_flag_mismatch",
                $"Entry node '{entryNode.Id}' is not marked IsEntryPoint.",
                map.Id,
                entryNode.Id);
        }

        if (entryNode.NodeType != MapNodeType.EntryPoint)
        {
            AddWarning(
                issues,
                "entry_type_mismatch",
                $"Entry node '{entryNode.Id}' has node type '{entryNode.NodeType}' instead of EntryPoint.",
                map.Id,
                entryNode.Id);
        }
    }

    private static void ValidateEdges(
        MapGraph map,
        IReadOnlyDictionary<string, MapNodeEntry> nodes,
        ICollection<MapGraphValidationIssue> issues)
    {
        var edgeIds = new HashSet<Guid>();
        var outgoingDirections = new HashSet<(string FromNodeId, MapDirection Direction)>();

        foreach (var edge in map.Edges)
        {
            if (!edgeIds.Add(edge.Id))
            {
                AddError(issues, "edge_duplicate", $"Edge '{edge.Id}' is duplicated.", map.Id);
                continue;
            }

            if (!string.Equals(edge.MapId, map.Id, StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    issues,
                    "edge_map_mismatch",
                    $"Edge '{edge.Id}' belongs to map '{edge.MapId}' but is listed on '{map.Id}'.",
                    map.Id);
            }

            if (!nodes.ContainsKey(edge.FromNodeId))
            {
                AddError(
                    issues,
                    "edge_origin_missing",
                    $"Edge '{edge.Id}' references unknown origin node '{edge.FromNodeId}'.",
                    map.Id,
                    edge.FromNodeId);
            }

            if (!nodes.ContainsKey(edge.ToNodeId))
            {
                AddError(
                    issues,
                    "edge_target_missing",
                    $"Edge '{edge.Id}' references unknown destination node '{edge.ToNodeId}'.",
                    map.Id,
                    edge.ToNodeId);
            }

            if (string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.OrdinalIgnoreCase))
            {
                AddError(issues, "edge_self_reference", $"Edge '{edge.Id}' connects a node to itself.", map.Id, edge.FromNodeId);
            }

            if (!outgoingDirections.Add((edge.FromNodeId, edge.Direction)))
            {
                AddError(
                    issues,
                    "edge_direction_duplicate",
                    $"Node '{edge.FromNodeId}' has more than one edge in direction '{edge.Direction}'.",
                    map.Id,
                    edge.FromNodeId);
            }
        }
    }

    private static void ValidateReachability(
        MapGraph map,
        IReadOnlyDictionary<string, MapNodeEntry> nodes,
        ICollection<MapGraphValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(map.EntryNodeId) ||
            !nodes.ContainsKey(map.EntryNodeId))
        {
            return;
        }

        var adjacency = map.Edges
            .GroupBy(e => e.FromNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ToNodeId).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { map.EntryNodeId };
        var queue = new Queue<string>();
        queue.Enqueue(map.EntryNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var targets))
                continue;

            foreach (var target in targets)
            {
                if (visited.Add(target))
                    queue.Enqueue(target);
            }
        }

        foreach (var node in nodes.Values)
        {
            if (!visited.Contains(node.Id))
            {
                AddWarning(
                    issues,
                    "node_unreachable",
                    $"Node '{node.Id}' is not reachable from the entry node.",
                    map.Id,
                    node.Id);
            }
        }
    }

    private static void ValidateCrossContextReference(
        MapNodeEntry node,
        string mapId,
        IReadOnlySet<string>? knownRegionSlugs,
        IReadOnlySet<string>? knownZoneSlugs,
        IReadOnlySet<string>? knownLocationSlugs,
        ICollection<MapGraphValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(node.RefId))
            return;

        var isValid = node.NodeType switch
        {
            MapNodeType.Zone => knownZoneSlugs is null || knownZoneSlugs.Contains(node.RefId),
            MapNodeType.ZoneExit => knownZoneSlugs is null || knownZoneSlugs.Contains(node.RefId),
            MapNodeType.Location => knownLocationSlugs is null || knownLocationSlugs.Contains(node.RefId),
            MapNodeType.RegionExit => knownRegionSlugs is null || knownRegionSlugs.Contains(node.RefId),
            _ => true,
        };

        if (!isValid)
        {
            AddError(
                issues,
                "node_reference_unknown",
                $"Node '{node.Id}' references unknown {node.NodeType} slug '{node.RefId}'.",
                mapId,
                node.Id);
        }
    }

    private static void AddError(
        ICollection<MapGraphValidationIssue> issues,
        string code,
        string message,
        string? mapId = null,
        string? nodeId = null) =>
        issues.Add(new MapGraphValidationIssue(code, message, MapGraphValidationSeverity.Error, mapId, nodeId));

    private static void AddWarning(
        ICollection<MapGraphValidationIssue> issues,
        string code,
        string message,
        string? mapId = null,
        string? nodeId = null) =>
        issues.Add(new MapGraphValidationIssue(code, message, MapGraphValidationSeverity.Warning, mapId, nodeId));
}
