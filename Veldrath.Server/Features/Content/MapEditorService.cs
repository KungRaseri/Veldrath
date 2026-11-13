using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Map;
using Veldrath.Server.Data.Entities;
using MapEdgeEntity = RealmEngine.Data.Entities.MapEdge;
using MapEntity = RealmEngine.Data.Entities.Map;
using MapNodeEntity = RealmEngine.Data.Entities.MapNode;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The write path behind the Foundry map editor: list the authored graphs, read one in full, and save one
/// with the same validator the seed has to pass.
/// </summary>
/// <remarks>
/// <para>
/// Saving validates the <em>world</em>, not just the edited map, because the failures that matter are
/// cross-map ones — an exit pointing at a map that does not exist, or a dungeon room nothing can reach. The
/// candidate graph replaces the stored one before validation, so a save cannot be accepted on the strength of
/// the graph it is overwriting.
/// </para>
/// <para>
/// Every save is optimistic: the caller sends the version it started from, and a mismatch is a conflict rather
/// than a silent overwrite. Two authors editing the same floor is the normal case in a content team.
/// </para>
/// </remarks>
public class MapEditorService(ContentDbContext db, ILogger<MapEditorService> logger)
{
    private const string StatusOk = "ok";
    private const string StatusConflict = "conflict";
    private const string StatusInvalid = "invalid";
    private const string StatusNotFound = "not_found";

    /// <summary>Lists every authored map with its version and size.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The map summaries, ordered by id.</returns>
    public async Task<IReadOnlyList<MapSummaryDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Maps
            .AsNoTracking()
            .OrderBy(map => map.Id)
            .Select(map => new MapSummaryDto(
                map.Id,
                map.MapType.ToString(),
                map.OwnerId,
                map.Name,
                map.Description,
                map.EntryNodeId,
                map.Nodes.Count,
                map.Edges.Count,
                map.Version))
            .ToListAsync(cancellationToken);

    /// <summary>Reads one map graph in full.</summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The graph including its version, or <see langword="null"/> when it does not exist.</returns>
    public async Task<MapGraphDto?> GetAsync(string mapId, CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: false, cancellationToken);
        return map is null ? null : ToDto(map);
    }

    /// <summary>
    /// Validates and persists a map graph.
    /// </summary>
    /// <param name="mapId">Map identifier from the route.</param>
    /// <param name="candidate">The graph to store.</param>
    /// <param name="expectedVersion">The version the caller started from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome, with validation issues when the graph was refused.</returns>
    public async Task<MapGraphSaveResponse> SaveAsync(
        string mapId,
        MapGraphDto candidate,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(candidate.Id, mapId, StringComparison.OrdinalIgnoreCase))
        {
            return Refuse(
                0,
                StatusInvalid,
                [$"The graph id '{candidate.Id}' does not match the map being saved ('{mapId}')."]);
        }

        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return new MapGraphSaveResponse(false, StatusNotFound, 0, [], $"Map '{mapId}' was not found.");

        if (map.Version != expectedVersion)
        {
            logger.LogInformation(
                "Refused a save of {MapId}: expected version {ExpectedVersion}, stored {StoredVersion}",
                mapId,
                expectedVersion,
                map.Version);

            return new MapGraphSaveResponse(
                false,
                StatusConflict,
                map.Version,
                [],
                $"Map '{mapId}' is at version {map.Version}; reload before saving.");
        }

        var issues = await ValidateAsync(mapId, candidate, cancellationToken);
        var errors = issues.Where(issue => issue.Severity == "Error").ToList();

        if (errors.Count > 0)
        {
            logger.LogInformation(
                "Refused a save of {MapId} with {ErrorCount} validation error(s)",
                mapId,
                errors.Count);

            return new MapGraphSaveResponse(false, StatusInvalid, map.Version, issues, "The graph has errors.");
        }

        Apply(map, candidate);
        map.Version++;
        map.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Saved map {MapId} at version {Version}", mapId, map.Version);

        return new MapGraphSaveResponse(true, StatusOk, map.Version, issues);
    }

    /// <summary>
    /// Validates the candidate as part of the whole world, so a save cannot strand a room or point an exit at a
    /// map that does not exist.
    /// </summary>
    internal async Task<IReadOnlyList<MapGraphIssueDto>> ValidateAsync(
        string mapId,
        MapGraphDto candidate,
        CancellationToken cancellationToken = default)
    {
        var maps = await db.Maps
            .AsNoTracking()
            .Include(map => map.Nodes)
            .Include(map => map.Edges)
            .ToListAsync(cancellationToken);

        var graphs = maps
            .Select(map => string.Equals(map.Id, mapId, StringComparison.OrdinalIgnoreCase)
                ? ToSharedGraph(candidate)
                : ToSharedGraph(map))
            .ToList();

        // Content is small enough to load whole, and doing so keeps the JSON-owned traits out of the SQL
        // translation: the dungeon set is a pure function of the catalog, derived exactly like the seeded-world
        // guard in SeededContentValidationTests (a dungeon room plus a lobby).
        var locations = await db.ZoneLocations.AsNoTracking().ToListAsync(cancellationToken);
        var locationSlugs = locations.Select(location => location.Slug).ToList();

        var dungeonZoneSlugs = locations
            .Where(location => location.Traits.IsDungeon == true)
            .Select(location => location.ZoneId)
            .Where(zoneId => locations.Any(other =>
                other.ZoneId == zoneId && other.Traits.SafeRespawn == true))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = MapGraphValidator.ValidateWorld(
            graphs,
            knownRegionSlugs: graphs
                .Where(graph => graph.MapType == MapType.Region)
                .Select(graph => graph.OwnerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            knownZoneSlugs: graphs
                .Where(graph => graph.MapType == MapType.Zone || graph.MapType == MapType.Interior)
                .Select(graph => graph.OwnerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            knownLocationSlugs: locationSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase),
            dungeonZoneSlugs: dungeonZoneSlugs);

        return result.Issues
            .Where(issue =>
                issue.MapId is null ||
                string.Equals(issue.MapId, mapId, StringComparison.OrdinalIgnoreCase) ||
                issue.Code is "target_map_unknown")
            .Select(issue => new MapGraphIssueDto(
                issue.Code,
                issue.Message,
                issue.Severity.ToString(),
                issue.MapId,
                issue.NodeId))
            .ToList();
    }

    private async Task<MapEntity?> LoadAsync(string mapId, bool tracking, CancellationToken cancellationToken)
    {
        var query = db.Maps
            .Include(map => map.Nodes)
            .Include(map => map.Edges)
            .AsQueryable();

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(map => map.Id == mapId, cancellationToken);
    }

    private void Apply(MapEntity map, MapGraphDto candidate)
    {
        map.MapType = Enum.TryParse<MapType>(candidate.MapType, ignoreCase: true, out var mapType)
            ? mapType
            : map.MapType;
        map.OwnerId = candidate.OwnerId;
        map.Name = candidate.Name;
        map.Description = candidate.Description;
        map.EntryNodeId = candidate.EntryNodeId;

        ReconcileNodes(map, candidate.Nodes);
        ReconcileEdges(map, candidate.Edges);
    }

    private static void ReconcileNodes(MapEntity map, IReadOnlyList<MapNodeDto> nodes)
    {
        var incoming = nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var stale in map.Nodes.Where(node => !incoming.ContainsKey(node.Id)).ToList())
            map.Nodes.Remove(stale);

        foreach (var dto in nodes)
        {
            var node = map.Nodes.FirstOrDefault(existing =>
                string.Equals(existing.Id, dto.Id, StringComparison.OrdinalIgnoreCase));

            if (node is null)
            {
                node = new MapNodeEntity { Id = dto.Id, MapId = map.Id };
                map.Nodes.Add(node);
            }

            node.MapId = map.Id;
            node.NodeType = Enum.TryParse<MapNodeType>(dto.NodeType, ignoreCase: true, out var nodeType)
                ? nodeType
                : node.NodeType;
            node.RefId = dto.RefId;
            node.DisplayName = dto.DisplayName;
            node.Description = dto.Description;
            node.IconKey = dto.IconKey;
            node.LayoutX = dto.LayoutX;
            node.LayoutY = dto.LayoutY;
            node.IsEntryPoint = dto.IsEntryPoint;
            node.IsExitPoint = dto.IsExitPoint;
            node.TargetMapId = dto.TargetMapId;
            node.TargetNodeId = dto.TargetNodeId;
            node.IsHidden = dto.IsHidden;
            node.UnlockType = dto.UnlockType;
            node.UnlockKey = dto.UnlockKey;
            node.DiscoverThreshold = dto.DiscoverThreshold;
            node.SortOrder = dto.SortOrder;
        }
    }

    private static void ReconcileEdges(MapEntity map, IReadOnlyList<MapEdgeDto> edges)
    {
        var incoming = edges.ToDictionary(edge => edge.Id);

        foreach (var stale in map.Edges.Where(edge => !incoming.ContainsKey(edge.Id)).ToList())
            map.Edges.Remove(stale);

        foreach (var dto in edges)
        {
            var edge = map.Edges.FirstOrDefault(existing => existing.Id == dto.Id);

            if (edge is null)
            {
                edge = new MapEdgeEntity { Id = dto.Id, MapId = map.Id };
                map.Edges.Add(edge);
            }

            edge.MapId = map.Id;
            edge.FromNodeId = dto.FromNodeId;
            edge.ToNodeId = dto.ToNodeId;
            edge.Direction = Enum.TryParse<MapDirection>(dto.Direction, ignoreCase: true, out var direction)
                ? direction
                : edge.Direction;
            edge.EdgeType = Enum.TryParse<MapEdgeType>(dto.EdgeType, ignoreCase: true, out var edgeType)
                ? edgeType
                : edge.EdgeType;
            edge.Label = dto.Label;
            edge.IsTraversable = dto.IsTraversable;
            edge.IsHidden = dto.IsHidden;
        }
    }

    private static MapGraphDto ToDto(MapEntity map) =>
        new(
            map.Id,
            map.MapType.ToString(),
            map.OwnerId,
            map.Name,
            map.Description,
            map.EntryNodeId,
            map.Nodes
                .OrderBy(node => node.SortOrder)
                .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
                .Select(node => new MapNodeDto(
                    node.Id,
                    node.MapId,
                    node.NodeType.ToString(),
                    node.RefId,
                    node.DisplayName,
                    node.Description,
                    node.IconKey,
                    node.LayoutX,
                    node.LayoutY,
                    node.IsEntryPoint,
                    node.IsExitPoint,
                    node.TargetMapId,
                    node.TargetNodeId,
                    node.IsHidden,
                    node.UnlockType,
                    node.UnlockKey,
                    node.DiscoverThreshold,
                    node.SortOrder))
                .ToList(),
            map.Edges
                .OrderBy(edge => edge.FromNodeId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.Direction)
                .Select(edge => new MapEdgeDto(
                    edge.Id,
                    edge.MapId,
                    edge.FromNodeId,
                    edge.ToNodeId,
                    edge.Direction.ToString(),
                    edge.EdgeType.ToString(),
                    edge.Label,
                    edge.IsTraversable,
                    edge.IsHidden))
                .ToList(),
            map.Version);

    private static MapGraph ToSharedGraph(MapEntity map) =>
        ToSharedGraph(ToDto(map));

    private static MapGraph ToSharedGraph(MapGraphDto dto) =>
        new(
            dto.Id,
            Enum.TryParse<MapType>(dto.MapType, ignoreCase: true, out var mapType) ? mapType : MapType.Zone,
            dto.OwnerId,
            dto.Name,
            dto.Description,
            dto.EntryNodeId,
            dto.Nodes
                .Select(node => new MapNodeEntry(
                    node.Id,
                    node.MapId,
                    Enum.TryParse<MapNodeType>(node.NodeType, ignoreCase: true, out var nodeType)
                        ? nodeType
                        : MapNodeType.Location,
                    node.RefId,
                    node.DisplayName,
                    node.Description,
                    node.IconKey,
                    node.LayoutX,
                    node.LayoutY,
                    node.IsEntryPoint,
                    node.IsExitPoint,
                    node.TargetMapId,
                    node.TargetNodeId,
                    node.IsHidden,
                    node.UnlockType,
                    node.UnlockKey,
                    node.DiscoverThreshold,
                    node.SortOrder))
                .ToList(),
            dto.Edges
                .Select(edge => new MapEdgeEntry(
                    edge.Id,
                    edge.MapId,
                    edge.FromNodeId,
                    edge.ToNodeId,
                    Enum.TryParse<MapDirection>(edge.Direction, ignoreCase: true, out var direction)
                        ? direction
                        : MapDirection.North,
                    Enum.TryParse<MapEdgeType>(edge.EdgeType, ignoreCase: true, out var edgeType)
                        ? edgeType
                        : MapEdgeType.Walk,
                    edge.Label,
                    edge.IsTraversable,
                    edge.IsHidden))
                .ToList());

    private static MapGraphSaveResponse Refuse(int version, string status, IReadOnlyList<string> messages) =>
        new(
            false,
            status,
            version,
            messages.Select(message => new MapGraphIssueDto("request_invalid", message, "Error", null, null)).ToList(),
            messages.FirstOrDefault());
}
