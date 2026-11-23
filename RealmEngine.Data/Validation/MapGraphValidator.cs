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
/// Validates region, zone and interior map graphs before they are seeded or served.
/// The validator enforces explicit entry/exit nodes, intra-map cardinal edges,
/// optional cross-context slug references, and a way back out of every zone.
/// </summary>
public static class MapGraphValidator
{
    /// <summary>Unlock types that gate a node on content the player must have earned.</summary>
    private static readonly HashSet<string> KeyBasedUnlockTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "quest",
        "item",
        "achievement",
    };

    /// <summary>Validates a single map graph.</summary>
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

        if (map.MapType == MapType.Interior &&
            knownLocationSlugs is not null &&
            !knownLocationSlugs.Contains(map.OwnerId))
        {
            AddError(
                issues,
                "interior_owner_unknown",
                $"Interior map '{map.Id}' is owned by unknown location '{map.OwnerId}'.",
                map.Id);
        }

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

            // A skill-check unlock is a roll against the player's own skill and carries its gate in
            // DiscoverThreshold; only the content-gated types need a key that names a quest/item/achievement.
            if (!string.IsNullOrWhiteSpace(node.UnlockType) &&
                KeyBasedUnlockTypes.Contains(node.UnlockType) &&
                string.IsNullOrWhiteSpace(node.UnlockKey))
            {
                AddError(
                    issues,
                    "unlock_key_missing",
                    $"Node '{node.Id}' is gated by '{node.UnlockType}' but names no UnlockKey.",
                    map.Id,
                    node.Id);
            }

            ValidateCrossContextReference(node, map.Id, knownRegionSlugs, knownZoneSlugs, knownLocationSlugs, issues);
        }

        ValidateEntryNode(map, nodes, issues);
        ValidateEdges(map, nodes, issues);
        ValidateReachability(map, nodes, issues);
        ValidateExitPresence(map, nodes, issues);

        return new MapGraphValidationResult(issues);
    }

    /// <summary>
    /// Validates a whole world: every map on its own, plus the rules that need more than one map —
    /// destination maps and destination nodes must exist, a zone map must actually place the locations that
    /// belong to its zone, and a live map must be reachable from somewhere.
    /// </summary>
    /// <param name="maps">Every map in the world.</param>
    /// <param name="knownRegionSlugs">Optional set of valid region slugs for cross-context checks.</param>
    /// <param name="knownZoneSlugs">Optional set of valid zone slugs for cross-context checks.</param>
    /// <param name="knownLocationSlugs">Optional set of valid location slugs for cross-context checks.</param>
    /// <param name="dungeonZoneSlugs">
    /// Zone slugs that are dungeons. A dungeon's stranded room or missing exit is an error rather than a warning,
    /// because a player inside one cannot walk out of it.
    /// </param>
    /// <param name="locationZones">
    /// Optional map from location slug to the zone it belongs to. Supplying it enables the two zone-coverage
    /// rules below; without it the validator cannot know which locations belong to which zone.
    /// </param>
    /// <param name="liveMapIds">
    /// Optional set of maps that are in the game. Only those are reported as unreachable: a draft nobody links
    /// to yet is the normal state of a map being written.
    /// </param>
    /// <returns>A validation result containing errors and warnings for the whole world.</returns>
    public static MapGraphValidationResult ValidateWorld(
        IReadOnlyList<MapGraph> maps,
        IReadOnlySet<string>? knownRegionSlugs = null,
        IReadOnlySet<string>? knownZoneSlugs = null,
        IReadOnlySet<string>? knownLocationSlugs = null,
        IReadOnlySet<string>? dungeonZoneSlugs = null,
        IReadOnlyDictionary<string, string>? locationZones = null,
        IReadOnlySet<string>? liveMapIds = null)
    {
        var issues = new List<MapGraphValidationIssue>();
        var knownMapIds = maps.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Built with TryAdd rather than ToDictionary: this is public API, and a caller that hands us the
        // same map twice should get a reported issue, not an exception from the validator itself.
        var nodeIdsByMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in maps)
        {
            nodeIdsByMap.TryAdd(
                map.Id,
                map.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        foreach (var map in maps)
        {
            var isDungeonZone = map.MapType == MapType.Zone &&
                dungeonZoneSlugs is not null &&
                dungeonZoneSlugs.Contains(map.OwnerId);

            foreach (var issue in Validate(map, knownRegionSlugs, knownZoneSlugs, knownLocationSlugs).Issues)
            {
                var isDungeonBlocker = issue.Code is "node_unreachable" or "map_without_exit";
                issues.Add(isDungeonZone && isDungeonBlocker && issue.Severity == MapGraphValidationSeverity.Warning
                    ? issue with { Severity = MapGraphValidationSeverity.Error }
                    : issue);
            }

            // A zone map is the picture of its zone: a location node must reference a location of *this* zone,
            // and every location of the zone should be on the map. The first is an error because the node
            // would place a location where it does not exist; the second is a warning because a map can
            // legitimately be authored before every location is drawn.
            if (map.MapType == MapType.Zone && locationZones is not null)
            {
                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var node in map.Nodes)
                {
                    if (node.NodeType != MapNodeType.Location || string.IsNullOrWhiteSpace(node.RefId))
                        continue;

                    if (locationZones.TryGetValue(node.RefId, out var locationZone))
                    {
                        referenced.Add(node.RefId);

                        if (!string.Equals(locationZone, map.OwnerId, StringComparison.OrdinalIgnoreCase))
                        {
                            AddError(
                                issues,
                                "node_reference_zone_mismatch",
                                $"Node '{node.Id}' references location '{node.RefId}', which belongs to zone " +
                                $"'{locationZone}' rather than '{map.OwnerId}'.",
                                map.Id,
                                node.Id);
                        }
                    }
                }

                foreach (var location in locationZones
                    .Where(pair => string.Equals(pair.Value, map.OwnerId, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key)
                    .OrderBy(slug => slug, StringComparer.OrdinalIgnoreCase))
                {
                    if (referenced.Contains(location))
                        continue;

                    AddWarning(
                        issues,
                        "location_missing_node",
                        $"Location '{location}' belongs to this zone but has no node on the map.",
                        map.Id,
                        null);
                }
            }

            foreach (var node in map.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.TargetMapId))
                    continue;

                if (!knownMapIds.Contains(node.TargetMapId))
                {
                    AddError(
                        issues,
                        "target_map_unknown",
                        $"Node '{node.Id}' exits to unknown map '{node.TargetMapId}'.",
                        map.Id,
                        node.Id);
                    continue;
                }

                // A blank TargetNodeId is already an error from exit_target_missing; this rule is about a
                // name that looks plausible and points at nothing, which is what a typo in the editor is.
                if (string.IsNullOrWhiteSpace(node.TargetNodeId) ||
                    !nodeIdsByMap.TryGetValue(node.TargetMapId, out var targetNodes) ||
                    targetNodes.Contains(node.TargetNodeId))
                {
                    continue;
                }

                AddError(
                    issues,
                    "target_node_unknown",
                    $"Node '{node.Id}' exits to node '{node.TargetNodeId}', which does not exist on map '{node.TargetMapId}'.",
                    map.Id,
                    node.Id);
            }
        }

        // Reachability across maps: a live zone or interior that nothing exits to cannot be entered by a
        // character, however complete its own graph is. Regions are exempt until there is a world map to
        // hold them, and drafts are exempt because they are still being written.
        if (liveMapIds is not null)
        {
            // A Zone node on a region map is a board marker, not a door. The seeder links it to the zone map so
            // that travelling to the node enters the zone, but the board is not a through-route: travel into a
            // zone only happens from a node the character is standing next to, and stepping onto any Zone node
            // enters that zone rather than passing over it. Counting the marker as an exit therefore hid every
            // zone that has no actual door on another map — measured on a fresh stack at 571823af, 18 of 20
            // zone maps were "reachable" on the strength of their region node alone and the Drowning Pits could
            // not be entered on foot at all. Every other node that carries a target is a door a character can
            // use from where it stands: a zone exit, portal, stair, or a location that opens into an interior.
            var targetedMapIds = maps
                .SelectMany(map => map.Nodes
                    .Where(node => !IsRegionBoardMarker(map, node))
                    .Where(node => !string.IsNullOrWhiteSpace(node.TargetMapId))
                    .Select(node => node.TargetMapId!))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var map in maps)
            {
                if (map.MapType is not (MapType.Zone or MapType.Interior))
                    continue;
                if (!liveMapIds.Contains(map.Id) || targetedMapIds.Contains(map.Id))
                    continue;

                AddWarning(
                    issues,
                    "map_unreachable",
                    $"Map '{map.Id}' is live but no other map exits to it, so nobody can reach it.",
                    map.Id);
            }
        }

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

    private static void ValidateExitPresence(
        MapGraph map,
        IReadOnlyDictionary<string, MapNodeEntry> nodes,
        ICollection<MapGraphValidationIssue> issues)
    {
        // Regions are the top of the walkable tree in this world model and interior maps are reached
        // from their owning location, so only a zone is required to offer its own way out.
        if (map.MapType != MapType.Zone)
            return;

        var hasExit = nodes.Values.Any(n => n.NodeType is MapNodeType.ZoneExit or MapNodeType.RegionExit);
        if (hasExit)
            return;

        AddWarning(
            issues,
            "map_without_exit",
            $"Zone map '{map.Id}' has no ZoneExit or RegionExit node, so a character can only leave it by dying.",
            map.Id);
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

    /// <summary>
    /// Reports whether a node is a zone marker on a region board rather than a door.
    /// </summary>
    /// <remarks>
    /// <c>MapGraphSeeder.LinkRegionZonesToZoneMaps</c> points every region <c>Zone</c> node at that zone's own
    /// map so travelling to the node enters the zone. That target says where the node leads, not that the zone
    /// can be entered from elsewhere: the character still has to stand next to the node on the region board,
    /// and the only node a character arriving in a region can stand next to is a neighbour of the entry.
    /// </remarks>
    /// <param name="map">Map that owns the node.</param>
    /// <param name="node">Node to classify.</param>
    /// <returns><see langword="true"/> when the node is a region board's zone marker.</returns>
    private static bool IsRegionBoardMarker(MapGraph map, MapNodeEntry node) =>
        map.MapType == MapType.Region && node.NodeType == MapNodeType.Zone;

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
