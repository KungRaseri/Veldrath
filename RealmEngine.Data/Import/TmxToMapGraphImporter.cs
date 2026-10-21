using System.Security.Cryptography;
using System.Text;
using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Data.Import;

/// <summary>
/// Converts authored TMX region/zone maps plus <c>ZoneLocation</c> and connection rows
/// into DB-ready <see cref="MapGraph"/> instances.
/// </summary>
/// <remarks>
/// This is a one-time migration utility. It is intentionally free of database access so it
/// can be exercised with in-memory TMX fixtures. Region maps are derived from TMX
/// <c>zones</c>, <c>region_exits</c>, <c>spawns</c>, and <c>paths</c> object groups.
/// Zone maps are derived from TMX <c>labels</c>, <c>exits</c>, <c>spawns</c>, and <c>paths</c>
/// object groups, supplemented by <c>ZoneLocation</c> coordinates and
/// <c>ZoneLocationConnection</c> rows when TMX data is absent.
/// </remarks>
public static class TmxToMapGraphImporter
{
    private const double SnapRadius = 2.5;

    private const string EntryIcon = "login";
    private const string ZoneIcon = "map";
    private const string LocationIcon = "place";
    private const string ZoneExitIcon = "exit_to_app";
    private const string RegionExitIcon = "public";

    /// <summary>
    /// Imports region and zone map graphs from the supplied catalog and TMX maps.
    /// </summary>
    /// <param name="catalog">Catalog, location, and connection rows to convert.</param>
    /// <param name="regionMaps">TMX region maps keyed by region slug.</param>
    /// <param name="zoneMaps">Optional TMX zone maps keyed by zone slug.</param>
    /// <returns>The imported graphs plus a verification report.</returns>
    public static MapGraphImportResult Import(
        MapGraphImportCatalog catalog,
        IReadOnlyDictionary<string, TiledMap> regionMaps,
        IReadOnlyDictionary<string, TiledMap>? zoneMaps = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(regionMaps);

        var issues = new List<MapGraphImportIssue>();
        var regions = catalog.Regions.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        var zones = catalog.Zones.ToDictionary(z => z.Id, StringComparer.OrdinalIgnoreCase);

        var locationsByZone = catalog.Locations
            .GroupBy(l => l.ZoneId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(l => l.SortOrder).ThenBy(l => l.Slug, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var maps = new List<MapGraph>();

        foreach (var region in catalog.Regions.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
        {
            regionMaps.TryGetValue(region.Id, out var tmx);
            maps.Add(BuildRegionMap(region, tmx, regions, zones, catalog.RegionConnections, issues));
        }

        foreach (var zone in catalog.Zones.OrderBy(z => z.Id, StringComparer.OrdinalIgnoreCase))
        {
            TiledMap? tmx = null;
            zoneMaps?.TryGetValue(zone.Id, out tmx);

            var locations = locationsByZone.TryGetValue(zone.Id, out var found) ? found : [];
            maps.Add(BuildZoneMap(zone, tmx, locations, catalog.ZoneConnections, regions, zones, issues));
        }

        var report = Verify(maps, catalog, issues);
        return new MapGraphImportResult(maps, report);
    }

    // ── Region maps ───────────────────────────────────────────────────────────

    private static MapGraph BuildRegionMap(
        MapCatalogEntry region,
        TiledMap? tmx,
        IReadOnlyDictionary<string, MapCatalogEntry> regions,
        IReadOnlyDictionary<string, MapCatalogEntry> zones,
        IReadOnlyList<RegionConnectionSnapshot> regionConnections,
        ICollection<MapGraphImportIssue> issues)
    {
        var mapId = $"region:{region.Id}";
        var builder = new MapBuilder(mapId);

        if (tmx is not null)
        {
            foreach (var entry in tmx.GetZoneEntries()
                         .OrderBy(e => e.TileY)
                         .ThenBy(e => e.TileX)
                         .ThenBy(e => e.ZoneSlug, StringComparer.OrdinalIgnoreCase))
            {
                var nodeId = $"{mapId}:zone:{entry.ZoneSlug}";
                zones.TryGetValue(entry.ZoneSlug, out var catalogZone);

                if (catalogZone is null)
                {
                    issues.Add(new MapGraphImportIssue(
                        "zone_catalog_missing",
                        $"Region map '{mapId}' references zone '{entry.ZoneSlug}' which is not in the zone catalog.",
                        mapId,
                        nodeId));
                }

                var displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? catalogZone?.Name ?? TitleCaseSlug(entry.ZoneSlug)
                    : entry.DisplayName;

                builder.AddNode(
                    nodeId,
                    MapNodeType.Zone,
                    entry.ZoneSlug,
                    displayName,
                    catalogZone?.Description ?? $"Zone {displayName}.",
                    ZoneIcon,
                    entry.TileX,
                    entry.TileY,
                    isExitPoint: false);
            }

            foreach (var exit in tmx.GetRegionExits()
                         .OrderBy(e => e.TileY)
                         .ThenBy(e => e.TileX)
                         .ThenBy(e => e.TargetRegionId, StringComparer.OrdinalIgnoreCase))
            {
                var nodeId = $"{mapId}:exit:{exit.TargetRegionId}";
                regions.TryGetValue(exit.TargetRegionId, out var targetRegion);
                var displayName = targetRegion?.Name ?? TitleCaseSlug(exit.TargetRegionId);

                if (targetRegion is null)
                {
                    issues.Add(new MapGraphImportIssue(
                        "region_catalog_missing",
                        $"Region map '{mapId}' references region '{exit.TargetRegionId}' which is not in the region catalog.",
                        mapId,
                        nodeId));
                }

                builder.AddNode(
                    nodeId,
                    MapNodeType.RegionExit,
                    exit.TargetRegionId,
                    displayName,
                    targetRegion?.Description ?? $"Crossing to {displayName}.",
                    RegionExitIcon,
                    exit.TileX,
                    exit.TileY,
                    isExitPoint: true,
                    targetMapId: $"region:{exit.TargetRegionId}",
                    targetNodeId: $"region:{exit.TargetRegionId}:entry");
            }
        }
        else
        {
            issues.Add(new MapGraphImportIssue(
                "region_tmx_missing",
                $"No TMX region map was supplied for region '{region.Id}'; only catalog zones and connections were used.",
                mapId,
                Severity: MapGraphImportIssueSeverity.Warning));

            var index = 0;
            foreach (var zone in zones.Values
                         .Where(z => string.Equals(z.ParentRegionId, region.Id, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(z => z.Id, StringComparer.OrdinalIgnoreCase))
            {
                builder.AddNode(
                    $"{mapId}:zone:{zone.Id}",
                    MapNodeType.Zone,
                    zone.Id,
                    zone.Name,
                    zone.Description ?? $"Zone {zone.Name}.",
                    ZoneIcon,
                    index % 4,
                    index / 4,
                    isExitPoint: false);
                index++;
            }
        }

        SynthesizeRegionExits(region, builder, regions, regionConnections, issues);

        var preferred = ResolveRegionEntryPosition(tmx);
        builder.AddEntry(preferred.X, preferred.Y, "Arrival", $"Arrival point for {region.Name}.");

        var edges = new EdgeBuilder(mapId, issues);
        if (tmx is not null)
            AddPathEdges(edges, tmx.GetRegionPaths(), builder.Positions, builder.NodeTypes);

        ConnectOrphans(mapId, edges, builder.Positions, builder.NodeTypes, issues);

        return builder.Build(
            MapType.Region,
            region.Id,
            region.Name,
            region.Description ?? $"Region map for {region.Name}.",
            edges.Edges);
    }

    private static void SynthesizeRegionExits(
        MapCatalogEntry region,
        MapBuilder builder,
        IReadOnlyDictionary<string, MapCatalogEntry> regions,
        IReadOnlyList<RegionConnectionSnapshot> regionConnections,
        ICollection<MapGraphImportIssue> issues)
    {
        foreach (var connection in regionConnections
                     .Where(c => string.Equals(c.FromRegionId, region.Id, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(c => c.ToRegionId, StringComparer.OrdinalIgnoreCase))
        {
            var nodeId = $"{builder.MapId}:exit:{connection.ToRegionId}";
            if (builder.Contains(nodeId))
                continue;

            if (!regions.TryGetValue(connection.ToRegionId, out var targetRegion))
            {
                issues.Add(new MapGraphImportIssue(
                    "region_connection_target_missing",
                    $"Region connection '{region.Id}' -> '{connection.ToRegionId}' references an unknown region.",
                    builder.MapId));
                continue;
            }

            var maxX = builder.Positions.Count == 0 ? 0 : builder.Positions.Values.Max(p => p.X) + 2;
            var y = builder.Positions.Count == 0 ? 0 : builder.Positions.Values.Min(p => p.Y);

            builder.AddNode(
                nodeId,
                MapNodeType.RegionExit,
                connection.ToRegionId,
                targetRegion.Name,
                targetRegion.Description ?? $"Crossing to {targetRegion.Name}.",
                RegionExitIcon,
                maxX,
                y,
                isExitPoint: true,
                targetMapId: $"region:{connection.ToRegionId}",
                targetNodeId: $"region:{connection.ToRegionId}:entry");

            issues.Add(new MapGraphImportIssue(
                "region_exit_synthesized",
                $"Region exit to '{connection.ToRegionId}' was synthesized from a region connection.",
                builder.MapId,
                nodeId,
                MapGraphImportIssueSeverity.Warning));
        }
    }

    private static (int X, int Y) ResolveRegionEntryPosition(TiledMap? tmx)
    {
        if (tmx?.GetDefaultSpawn() is { } spawn)
            return (spawn.TileX, spawn.TileY);

        var point = tmx?.GetRegionPaths()
            .SelectMany(p => p.Points)
            .FirstOrDefault();

        return point is null
            ? (0, 0)
            : ((int)Math.Round(point.TileX), (int)Math.Round(point.TileY));
    }

    // ── Zone maps ─────────────────────────────────────────────────────────────

    private static MapGraph BuildZoneMap(
        MapCatalogEntry zone,
        TiledMap? tmx,
        IReadOnlyList<ZoneLocationSnapshot> locations,
        IReadOnlyList<ZoneConnectionSnapshot> zoneConnections,
        IReadOnlyDictionary<string, MapCatalogEntry> regions,
        IReadOnlyDictionary<string, MapCatalogEntry> zones,
        ICollection<MapGraphImportIssue> issues)
    {
        var mapId = $"zone:{zone.Id}";
        var builder = new MapBuilder(mapId);

        var labelPositions = (tmx?.GetZoneLabels() ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.ZoneSlug))
            .GroupBy(l => l.ZoneSlug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (g.First().TileX, g.First().TileY),
                StringComparer.OrdinalIgnoreCase);

        foreach (var location in locations)
        {
            var preferred = ResolveLocationPosition(location, labelPositions);

            builder.AddNode(
                $"{mapId}:location:{location.Slug}",
                MapNodeType.Location,
                location.Slug,
                location.DisplayName,
                BuildLocationDescription(zone, location),
                LocationIcon,
                preferred.X,
                preferred.Y,
                isExitPoint: false,
                isHidden: location.IsHidden,
                unlockType: location.UnlockType,
                unlockKey: location.UnlockKey,
                discoverThreshold: location.DiscoverThreshold);
        }

        var tmxExits = tmx?.GetZoneExits() ?? [];
        AddZoneExitNodes(mapId, builder, tmxExits, regions, zones, issues);

        var synthesizedExits = SynthesizeZoneExitsFromConnections(
            mapId,
            builder,
            locations,
            zoneConnections,
            regions,
            zones,
            issues);

        var preferredEntry = ResolveZoneEntryPosition(tmx, builder, locations);
        builder.AddEntry(preferredEntry.X, preferredEntry.Y, "Arrival", $"Arrival point for {zone.Name}.");

        var edges = new EdgeBuilder(mapId, issues);

        if (tmx is not null)
            AddPathEdges(edges, tmx.GetRegionPaths(), builder.Positions, builder.NodeTypes);

        var locationSlugs = new HashSet<string>(
            locations.Select(l => l.Slug),
            StringComparer.OrdinalIgnoreCase);

        AddWalkConnectionEdges(mapId, edges, builder, zoneConnections, locationSlugs, issues);
        AddSynthesizedExitEdges(edges, builder, synthesizedExits);

        ConnectOrphans(mapId, edges, builder.Positions, builder.NodeTypes, issues);

        return builder.Build(
            MapType.Zone,
            zone.Id,
            zone.Name,
            zone.Description ?? $"Zone map for {zone.Name}.",
            edges.Edges);
    }

    private static (int X, int Y) ResolveLocationPosition(
        ZoneLocationSnapshot location,
        IReadOnlyDictionary<string, (int X, int Y)> labelPositions)
    {
        if (location.TileX is not null && location.TileY is not null)
            return (location.TileX.Value, location.TileY.Value);

        return labelPositions.TryGetValue(location.Slug, out var label)
            ? label
            : (0, 0);
    }

    private static void AddZoneExitNodes(
        string mapId,
        MapBuilder builder,
        IReadOnlyList<ZoneExitDefinition> tmxExits,
        IReadOnlyDictionary<string, MapCatalogEntry> regions,
        IReadOnlyDictionary<string, MapCatalogEntry> zones,
        ICollection<MapGraphImportIssue> issues)
    {
        foreach (var exit in tmxExits
                     .OrderBy(e => e.TileY)
                     .ThenBy(e => e.TileX)
                     .ThenBy(e => e.TargetZoneId ?? e.TargetRegionId ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(exit.TargetZoneId))
            {
                var nodeId = $"{mapId}:exit:zone:{exit.TargetZoneId}";
                zones.TryGetValue(exit.TargetZoneId, out var targetZone);

                if (targetZone is null)
                {
                    issues.Add(new MapGraphImportIssue(
                        "zone_catalog_missing",
                        $"Zone map '{mapId}' references zone '{exit.TargetZoneId}' which is not in the zone catalog.",
                        mapId,
                        nodeId));
                }

                var displayName = string.IsNullOrWhiteSpace(exit.DisplayName)
                    ? targetZone?.Name ?? TitleCaseSlug(exit.TargetZoneId)
                    : exit.DisplayName;

                builder.AddNode(
                    nodeId,
                    MapNodeType.ZoneExit,
                    exit.TargetZoneId,
                    displayName,
                    targetZone?.Description ?? $"Passage to {targetZone?.Name ?? TitleCaseSlug(exit.TargetZoneId)}.",
                    ZoneExitIcon,
                    exit.TileX,
                    exit.TileY,
                    isExitPoint: true,
                    targetMapId: $"zone:{exit.TargetZoneId}",
                    targetNodeId: $"zone:{exit.TargetZoneId}:entry");
            }
            else if (!string.IsNullOrWhiteSpace(exit.TargetRegionId))
            {
                var nodeId = $"{mapId}:exit:region:{exit.TargetRegionId}";
                regions.TryGetValue(exit.TargetRegionId, out var targetRegion);

                if (targetRegion is null)
                {
                    issues.Add(new MapGraphImportIssue(
                        "region_catalog_missing",
                        $"Zone map '{mapId}' references region '{exit.TargetRegionId}' which is not in the region catalog.",
                        mapId,
                        nodeId));
                }

                var displayName = string.IsNullOrWhiteSpace(exit.DisplayName)
                    ? targetRegion?.Name ?? TitleCaseSlug(exit.TargetRegionId)
                    : exit.DisplayName;

                builder.AddNode(
                    nodeId,
                    MapNodeType.RegionExit,
                    exit.TargetRegionId,
                    displayName,
                    targetRegion?.Description ?? $"Return to {targetRegion?.Name ?? TitleCaseSlug(exit.TargetRegionId)}.",
                    RegionExitIcon,
                    exit.TileX,
                    exit.TileY,
                    isExitPoint: true,
                    targetMapId: $"region:{exit.TargetRegionId}",
                    targetNodeId: $"region:{exit.TargetRegionId}:entry");
            }
        }
    }

    private static IReadOnlyList<SynthesizedExit> SynthesizeZoneExitsFromConnections(
        string mapId,
        MapBuilder builder,
        IReadOnlyList<ZoneLocationSnapshot> locations,
        IReadOnlyList<ZoneConnectionSnapshot> zoneConnections,
        IReadOnlyDictionary<string, MapCatalogEntry> regions,
        IReadOnlyDictionary<string, MapCatalogEntry> zones,
        ICollection<MapGraphImportIssue> issues)
    {
        var locationSlugs = new HashSet<string>(
            locations.Select(l => l.Slug),
            StringComparer.OrdinalIgnoreCase);

        var synthesized = new List<SynthesizedExit>();

        foreach (var connection in zoneConnections
                     .Where(c => locationSlugs.Contains(c.FromLocationSlug))
                     .Where(c => !string.IsNullOrWhiteSpace(c.ToZoneId) || !string.IsNullOrWhiteSpace(c.ToRegionId))
                     .OrderBy(c => c.FromLocationSlug, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(c => c.ToZoneId ?? c.ToRegionId ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var sourceNodeId = $"{mapId}:location:{connection.FromLocationSlug}";
            if (!builder.Contains(sourceNodeId))
                continue;

            string nodeId;
            MapNodeType nodeType;
            string refId;
            string displayName;
            string description;
            string iconKey;
            string targetMapId;
            string targetNodeId;
            MapEdgeType edgeType;

            if (!string.IsNullOrWhiteSpace(connection.ToZoneId))
            {
                zones.TryGetValue(connection.ToZoneId, out var targetZone);
                nodeId = $"{mapId}:exit:zone:{connection.ToZoneId}";
                nodeType = MapNodeType.ZoneExit;
                refId = connection.ToZoneId;
                displayName = targetZone?.Name ?? TitleCaseSlug(connection.ToZoneId);
                description = targetZone?.Description ?? $"Passage to {displayName}.";
                iconKey = ZoneExitIcon;
                targetMapId = $"zone:{connection.ToZoneId}";
                targetNodeId = $"zone:{connection.ToZoneId}:entry";
                edgeType = MapEdgeType.ExitToZone;

                if (targetZone is null)
                {
                    issues.Add(new MapGraphImportIssue(
                        "zone_catalog_missing",
                        $"Connection '{connection.FromLocationSlug}' references zone '{connection.ToZoneId}' which is not in the zone catalog.",
                        mapId,
                        nodeId));
                }
            }
            else
            {
                var targetRegionId = connection.ToRegionId!;
                regions.TryGetValue(targetRegionId, out var targetRegion);
                nodeId = $"{mapId}:exit:region:{targetRegionId}";
                nodeType = MapNodeType.RegionExit;
                refId = targetRegionId;
                displayName = targetRegion?.Name ?? TitleCaseSlug(targetRegionId);
                description = targetRegion?.Description ?? $"Return to {displayName}.";
                iconKey = RegionExitIcon;
                targetMapId = $"region:{targetRegionId}";
                targetNodeId = $"region:{targetRegionId}:entry";
                edgeType = MapEdgeType.ExitToRegion;

                if (targetRegion is null)
                {
                    issues.Add(new MapGraphImportIssue(
                        "region_catalog_missing",
                        $"Connection '{connection.FromLocationSlug}' references region '{targetRegionId}' which is not in the region catalog.",
                        mapId,
                        nodeId));
                }
            }

            if (!builder.Contains(nodeId))
            {
                var sourcePosition = builder.Positions[sourceNodeId];
                var offset = FindFreeExitOffset(builder.Positions, sourcePosition);

                builder.AddNode(
                    nodeId,
                    nodeType,
                    refId,
                    displayName,
                    description,
                    iconKey,
                    sourcePosition.X + offset.X,
                    sourcePosition.Y + offset.Y,
                    isExitPoint: true,
                    targetMapId: targetMapId,
                    targetNodeId: targetNodeId);

                issues.Add(new MapGraphImportIssue(
                    "zone_exit_synthesized",
                    $"Exit node '{nodeId}' was synthesized from a location connection.",
                    mapId,
                    nodeId,
                    MapGraphImportIssueSeverity.Warning));
            }

            synthesized.Add(new SynthesizedExit(nodeId, sourceNodeId, connection, edgeType));
        }

        return synthesized;
    }

    private static (int X, int Y) ResolveZoneEntryPosition(
        TiledMap? tmx,
        MapBuilder builder,
        IReadOnlyList<ZoneLocationSnapshot> locations)
    {
        if (tmx?.GetDefaultSpawn() is { } spawn)
            return (spawn.TileX, spawn.TileY);

        var anchor = locations.FirstOrDefault(l =>
                         l.Slug.EndsWith("-square", StringComparison.OrdinalIgnoreCase))
                     ?? locations.FirstOrDefault();

        if (anchor is not null &&
            builder.Positions.TryGetValue($"{builder.MapId}:location:{anchor.Slug}", out var position))
        {
            return position;
        }

        return (0, 0);
    }

    private static void AddWalkConnectionEdges(
        string mapId,
        EdgeBuilder edges,
        MapBuilder builder,
        IReadOnlyList<ZoneConnectionSnapshot> zoneConnections,
        IReadOnlySet<string> locationSlugs,
        ICollection<MapGraphImportIssue> issues)
    {
        foreach (var connection in zoneConnections
                     .Where(c => locationSlugs.Contains(c.FromLocationSlug))
                     .Where(IsWalkConnection)
                     .OrderBy(c => c.FromLocationSlug, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(c => c.ToLocationSlug, StringComparer.OrdinalIgnoreCase))
        {
            var fromNodeId = $"{mapId}:location:{connection.FromLocationSlug}";
            var toNodeId = $"{mapId}:location:{connection.ToLocationSlug}";

            if (!builder.Contains(fromNodeId) || !builder.Contains(toNodeId))
            {
                issues.Add(new MapGraphImportIssue(
                    "zone_location_missing",
                    $"Walk connection '{connection.FromLocationSlug}' -> '{connection.ToLocationSlug}' references an unknown location.",
                    mapId));
                continue;
            }

            var direction = DirectionBetween(builder.Positions, fromNodeId, toNodeId);
            if (direction is null)
                continue;

            edges.Add(
                fromNodeId,
                toNodeId,
                direction.Value,
                MapEdgeType.Walk,
                string.Equals(connection.ConnectionType, "walk", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : connection.ConnectionType,
                connection.IsTraversable,
                isHidden: false);
        }
    }

    private static void AddSynthesizedExitEdges(
        EdgeBuilder edges,
        MapBuilder builder,
        IReadOnlyList<SynthesizedExit> synthesizedExits)
    {
        foreach (var exit in synthesizedExits)
        {
            if (!builder.Contains(exit.NodeId) || !builder.Contains(exit.SourceNodeId))
                continue;

            var direction = DirectionBetween(builder.Positions, exit.SourceNodeId, exit.NodeId);
            if (direction is null)
                continue;

            edges.Add(
                exit.SourceNodeId,
                exit.NodeId,
                direction.Value,
                exit.EdgeType,
                exit.Connection.ConnectionType,
                exit.Connection.IsTraversable,
                isHidden: false);
        }
    }

    private static bool IsWalkConnection(ZoneConnectionSnapshot connection) =>
        string.IsNullOrWhiteSpace(connection.ToZoneId) &&
        string.IsNullOrWhiteSpace(connection.ToRegionId) &&
        !string.IsNullOrWhiteSpace(connection.ToLocationSlug);

    private static string BuildLocationDescription(MapCatalogEntry zone, ZoneLocationSnapshot location)
    {
        var description = $"{location.DisplayName} in {zone.Name}.";
        var traits = new List<string>();

        if (location.IsHidden)
            traits.Add("Hidden until discovered.");
        if (!string.IsNullOrWhiteSpace(location.UnlockType))
            traits.Add($"Unlock: {location.UnlockType}.");
        if (!string.IsNullOrWhiteSpace(location.DominantLanguageSlug))
            traits.Add($"Language: {location.DominantLanguageSlug}.");

        return traits.Count == 0 ? description : $"{description} {string.Join(' ', traits)}";
    }

    // ── Edge derivation ───────────────────────────────────────────────────────

    private static void AddPathEdges(
        EdgeBuilder edges,
        IReadOnlyList<RegionPathDefinition> paths,
        IReadOnlyDictionary<string, (int X, int Y)> positions,
        IReadOnlyDictionary<string, MapNodeType> nodeTypes)
    {
        foreach (var path in paths)
        {
            var snapped = new List<string>();

            foreach (var point in path.Points)
            {
                var nearest = FindNearest(positions, point.TileX, point.TileY, SnapRadius);
                if (nearest is null)
                    continue;

                if (snapped.Count == 0 ||
                    !string.Equals(snapped[^1], nearest, StringComparison.OrdinalIgnoreCase))
                {
                    snapped.Add(nearest);
                }
            }

            for (var i = 0; i < snapped.Count - 1; i++)
            {
                var fromNodeId = snapped[i];
                var toNodeId = snapped[i + 1];
                var direction = DirectionBetween(positions, fromNodeId, toNodeId);
                if (direction is null)
                    continue;

                edges.AddBidirectional(
                    fromNodeId,
                    toNodeId,
                    direction.Value,
                    EdgeTypeFor(nodeTypes, fromNodeId, toNodeId),
                    path.Name,
                    isTraversable: true,
                    isHidden: false);
            }
        }
    }

    private static void ConnectOrphans(
        string mapId,
        EdgeBuilder edges,
        IReadOnlyDictionary<string, (int X, int Y)> positions,
        IReadOnlyDictionary<string, MapNodeType> nodeTypes,
        ICollection<MapGraphImportIssue> issues)
    {
        foreach (var nodeId in positions.Keys
                     .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                     .ToList())
        {
            if (edges.HasAnyConnection(nodeId))
                continue;

            var origin = positions[nodeId];
            var candidates = positions
                .Where(p => !string.Equals(p.Key, nodeId, StringComparison.OrdinalIgnoreCase))
                .Select(p => new
                {
                    Id = p.Key,
                    Distance = Math.Abs(p.Value.X - origin.X) + Math.Abs(p.Value.Y - origin.Y),
                })
                .OrderBy(c => c.Distance)
                .ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? connectedTo = null;

            foreach (var candidate in candidates)
            {
                var direction = DirectionBetween(positions, nodeId, candidate.Id);
                if (direction is null)
                    continue;

                if (!edges.AddBidirectional(
                        nodeId,
                        candidate.Id,
                        direction.Value,
                        EdgeTypeFor(nodeTypes, nodeId, candidate.Id),
                        label: null,
                        isTraversable: true,
                        isHidden: false))
                {
                    continue;
                }

                connectedTo = candidate.Id;
                break;
            }

            if (connectedTo is null)
            {
                issues.Add(new MapGraphImportIssue(
                    "node_orphan",
                    $"Node '{nodeId}' could not be connected to any eligible neighbour.",
                    mapId,
                    nodeId,
                    MapGraphImportIssueSeverity.Warning));
                continue;
            }

            issues.Add(new MapGraphImportIssue(
                "node_auto_connected",
                $"Node '{nodeId}' had no edges and was connected to '{connectedTo}'.",
                mapId,
                nodeId,
                MapGraphImportIssueSeverity.Warning));
        }
    }

    private static string? FindNearest(
        IReadOnlyDictionary<string, (int X, int Y)> positions,
        double x,
        double y,
        double radius)
    {
        var candidate = positions
            .Select(p => new
            {
                p.Key,
                Distance = Math.Sqrt(
                    Math.Pow(p.Value.X - x, 2) + Math.Pow(p.Value.Y - y, 2)),
            })
            .Where(c => c.Distance <= radius)
            .OrderBy(c => c.Distance)
            .ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return candidate?.Key;
    }

    private static MapDirection? DirectionBetween(
        IReadOnlyDictionary<string, (int X, int Y)> positions,
        string fromNodeId,
        string toNodeId)
    {
        if (!positions.TryGetValue(fromNodeId, out var from) ||
            !positions.TryGetValue(toNodeId, out var to))
        {
            return null;
        }

        return DirectionFromDelta(to.X - from.X, to.Y - from.Y);
    }

    private static readonly MapDirection[] ExitDirectionPreference =
    [
        MapDirection.North,
        MapDirection.East,
        MapDirection.South,
        MapDirection.West,
    ];

    private static (int X, int Y) FindFreeExitOffset(
        IReadOnlyDictionary<string, (int X, int Y)> positions,
        (int X, int Y) source)
    {
        foreach (var direction in ExitDirectionPreference)
        {
            var blocked = positions.Values.Any(position =>
            {
                if (position.X == source.X && position.Y == source.Y)
                    return false;

                return DirectionFromDelta(position.X - source.X, position.Y - source.Y) == direction;
            });

            if (blocked)
                continue;

            return direction switch
            {
                MapDirection.North => (0, -1),
                MapDirection.South => (0, 1),
                MapDirection.East => (1, 0),
                MapDirection.West => (-1, 0),
                _ => (1, 0),
            };
        }

        return (1, 0);
    }

    private static MapDirection? DirectionFromDelta(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
            return null;

        if (Math.Abs(deltaY) >= Math.Abs(deltaX))
            return deltaY < 0 ? MapDirection.North : MapDirection.South;

        return deltaX < 0 ? MapDirection.West : MapDirection.East;
    }

    private static MapDirection Opposite(MapDirection direction) => direction switch
    {
        MapDirection.North => MapDirection.South,
        MapDirection.South => MapDirection.North,
        MapDirection.East => MapDirection.West,
        MapDirection.West => MapDirection.East,
        MapDirection.Up => MapDirection.Down,
        MapDirection.Down => MapDirection.Up,
        MapDirection.In => MapDirection.Out,
        MapDirection.Out => MapDirection.In,
        _ => MapDirection.North,
    };

    private static MapEdgeType EdgeTypeFor(
        IReadOnlyDictionary<string, MapNodeType> nodeTypes,
        string fromNodeId,
        string toNodeId)
    {
        if (IsType(nodeTypes, fromNodeId, MapNodeType.ZoneExit) ||
            IsType(nodeTypes, toNodeId, MapNodeType.ZoneExit))
        {
            return MapEdgeType.ExitToZone;
        }

        if (IsType(nodeTypes, fromNodeId, MapNodeType.RegionExit) ||
            IsType(nodeTypes, toNodeId, MapNodeType.RegionExit))
        {
            return MapEdgeType.ExitToRegion;
        }

        return MapEdgeType.Walk;
    }

    private static bool IsType(
        IReadOnlyDictionary<string, MapNodeType> nodeTypes,
        string nodeId,
        MapNodeType expected) =>
        nodeTypes.TryGetValue(nodeId, out var actual) && actual == expected;

    // ── Verification ──────────────────────────────────────────────────────────

    private static MapGraphImportReport Verify(
        IReadOnlyList<MapGraph> maps,
        MapGraphImportCatalog catalog,
        List<MapGraphImportIssue> issues)
    {
        var knownRegions = new HashSet<string>(catalog.Regions.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var knownZones = new HashSet<string>(catalog.Zones.Select(z => z.Id), StringComparer.OrdinalIgnoreCase);
        var knownLocations = new HashSet<string>(catalog.Locations.Select(l => l.Slug), StringComparer.OrdinalIgnoreCase);
        var mapsById = maps.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        var mapReports = new List<MapGraphImportMapReport>();

        foreach (var map in maps.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase))
        {
            var validation = MapGraphValidator.Validate(map, knownRegions, knownZones, knownLocations);

            foreach (var node in map.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.TargetMapId))
                    continue;

                if (!mapsById.TryGetValue(node.TargetMapId, out var targetMap))
                {
                    issues.Add(new MapGraphImportIssue(
                        "target_map_missing",
                        $"Node '{node.Id}' targets map '{node.TargetMapId}' which was not imported.",
                        map.Id,
                        node.Id));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.TargetNodeId) ||
                    !targetMap.Nodes.Any(n =>
                        string.Equals(n.Id, node.TargetNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(new MapGraphImportIssue(
                        "target_node_missing",
                        $"Node '{node.Id}' targets node '{node.TargetNodeId}' which does not exist on map '{node.TargetMapId}'.",
                        map.Id,
                        node.Id));
                }
            }

            mapReports.Add(new MapGraphImportMapReport(
                map.Id,
                map.MapType,
                map.Nodes.Count,
                map.Edges.Count,
                validation.IsValid,
                validation.Issues));
        }

        var orderedIssues = issues
            .OrderBy(i => i.MapId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.NodeId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MapGraphImportReport(mapReports, orderedIssues);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string TitleCaseSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return "Unknown";

        return string.Join(' ', slug
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static Guid DeterministicEdgeId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record SynthesizedExit(
        string NodeId,
        string SourceNodeId,
        ZoneConnectionSnapshot Connection,
        MapEdgeType EdgeType);

    /// <summary>Builds one map's node set while reserving unique layout positions.</summary>
    private sealed class MapBuilder(string mapId)
    {
        private readonly Dictionary<(int X, int Y), string> _occupied = [];
        private readonly List<MapNodeEntry> _nodes = [];
        private readonly Dictionary<string, (int X, int Y)> _positions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapNodeType> _nodeTypes = new(StringComparer.OrdinalIgnoreCase);
        private int _nextSortOrder = 10;

        public string MapId { get; } = mapId;

        public string EntryId => $"{MapId}:entry";

        public IReadOnlyDictionary<string, (int X, int Y)> Positions => _positions;

        public IReadOnlyDictionary<string, MapNodeType> NodeTypes => _nodeTypes;

        public bool Contains(string nodeId) => _positions.ContainsKey(nodeId);

        public void AddEntry(int preferredX, int preferredY, string displayName, string description) =>
            AddNode(
                EntryId,
                MapNodeType.EntryPoint,
                refId: null,
                displayName,
                description,
                EntryIcon,
                preferredX,
                preferredY,
                isEntryPoint: true,
                isExitPoint: false,
                targetMapId: null,
                targetNodeId: null,
                sortOrder: 0);

        public void AddNode(
            string nodeId,
            MapNodeType nodeType,
            string? refId,
            string displayName,
            string description,
            string? iconKey,
            int preferredX,
            int preferredY,
            bool isExitPoint,
            string? targetMapId = null,
            string? targetNodeId = null,
            bool isHidden = false,
            string? unlockType = null,
            string? unlockKey = null,
            int? discoverThreshold = null,
            bool isEntryPoint = false,
            int sortOrder = -1)
        {
            var position = ReservePosition(nodeId, preferredX, preferredY);

            _positions[nodeId] = position;
            _nodeTypes[nodeId] = nodeType;
            _nodes.Add(new MapNodeEntry(
                nodeId,
                MapId,
                nodeType,
                refId,
                displayName,
                description,
                iconKey,
                position.X,
                position.Y,
                isEntryPoint,
                isExitPoint,
                targetMapId,
                targetNodeId,
                isHidden,
                unlockType,
                unlockKey,
                discoverThreshold,
                sortOrder >= 0 ? sortOrder : _nextSortOrder += 10));
        }

        public MapGraph Build(
            MapType mapType,
            string ownerId,
            string name,
            string description,
            IReadOnlyList<MapEdgeEntry> edges) =>
            new(
                MapId,
                mapType,
                ownerId,
                name,
                description,
                EntryId,
                _nodes
                    .OrderBy(n => n.SortOrder)
                    .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                edges
                    .OrderBy(e => e.FromNodeId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.Direction)
                    .ThenBy(e => e.ToNodeId, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        private (int X, int Y) ReservePosition(string nodeId, int preferredX, int preferredY)
        {
            if (_occupied.TryAdd((preferredX, preferredY), nodeId))
                return (preferredX, preferredY);

            for (var radius = 1; radius <= 1024; radius++)
            {
                for (var deltaX = -radius; deltaX <= radius; deltaX++)
                {
                    var deltaY = radius - Math.Abs(deltaX);

                    if (deltaY == 0 &&
                        _occupied.TryAdd((preferredX + deltaX, preferredY), nodeId))
                    {
                        return (preferredX + deltaX, preferredY);
                    }

                    if (deltaY > 0 &&
                        _occupied.TryAdd((preferredX + deltaX, preferredY + deltaY), nodeId))
                    {
                        return (preferredX + deltaX, preferredY + deltaY);
                    }

                    if (deltaY > 0 &&
                        _occupied.TryAdd((preferredX + deltaX, preferredY - deltaY), nodeId))
                    {
                        return (preferredX + deltaX, preferredY - deltaY);
                    }
                }
            }

            throw new InvalidOperationException($"Unable to reserve a layout position for node '{nodeId}'.");
        }
    }

    /// <summary>Collects map edges, enforcing one edge per cardinal direction per origin.</summary>
    private sealed class EdgeBuilder(string mapId, ICollection<MapGraphImportIssue> issues)
    {
        private readonly Dictionary<string, string> _directions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<MapEdgeEntry> _edges = [];

        public IReadOnlyList<MapEdgeEntry> Edges => _edges;

        public bool HasAnyConnection(string nodeId) =>
            _edges.Any(e =>
                string.Equals(e.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.ToNodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        public bool Add(
            string fromNodeId,
            string toNodeId,
            MapDirection direction,
            MapEdgeType edgeType,
            string? label,
            bool isTraversable,
            bool isHidden)
        {
            if (string.Equals(fromNodeId, toNodeId, StringComparison.OrdinalIgnoreCase))
                return false;

            var key = $"{fromNodeId}|{direction}";
            if (_directions.TryGetValue(key, out var existingTarget))
            {
                if (string.Equals(existingTarget, toNodeId, StringComparison.OrdinalIgnoreCase))
                    return true;

                issues.Add(new MapGraphImportIssue(
                    "edge_direction_conflict",
                    $"Node '{fromNodeId}' already has a {direction} edge to '{existingTarget}'; edge to '{toNodeId}' was skipped.",
                    mapId,
                    fromNodeId,
                    MapGraphImportIssueSeverity.Warning));
                return false;
            }

            _directions[key] = toNodeId;
            _edges.Add(new MapEdgeEntry(
                DeterministicEdgeId($"{mapId}|{fromNodeId}|{direction}|{toNodeId}"),
                mapId,
                fromNodeId,
                toNodeId,
                direction,
                edgeType,
                label,
                isTraversable,
                isHidden));

            return true;
        }

        public bool AddBidirectional(
            string fromNodeId,
            string toNodeId,
            MapDirection direction,
            MapEdgeType edgeType,
            string? label,
            bool isTraversable,
            bool isHidden)
        {
            var added = Add(
                fromNodeId,
                toNodeId,
                direction,
                edgeType,
                label,
                isTraversable,
                isHidden);

            Add(
                toNodeId,
                fromNodeId,
                Opposite(direction),
                edgeType,
                label,
                isTraversable,
                isHidden);

            return added;
        }
    }
}
