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
                map.Version,
                map.IsActive,
                map.ApprovedVersion,
                map.SubmittedAt,
                map.LastReviewNotes,
                map.LastReviewedAt))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Lists the references a node may point at, so the editor can offer pickers instead of free text.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Maps, zone owners, locations and region owners, each ordered by id.</returns>
    public async Task<MapTargetsDto> GetTargetsAsync(CancellationToken cancellationToken = default)
    {
        var maps = await db.Maps
            .AsNoTracking()
            .Select(map => new { map.Id, map.MapType, map.OwnerId, map.Name })
            .ToListAsync(cancellationToken);

        var zones = maps
            .Where(map => map.MapType is MapType.Zone or MapType.Interior)
            .Select(map => new MapTargetOptionDto(map.OwnerId, map.Name))
            .DistinctBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var regions = maps
            .Where(map => map.MapType == MapType.Region)
            .Select(map => new MapTargetOptionDto(map.OwnerId, map.Name))
            .DistinctBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var locations = await db.ZoneLocations
            .AsNoTracking()
            .OrderBy(location => location.Slug)
            .Select(location => new MapTargetOptionDto(location.Slug, location.DisplayName ?? location.Slug))
            .ToListAsync(cancellationToken);

        // Every node of every map: an exit's destination is a picker, and the whole set is small enough
        // (a few hundred rows) that a round trip per target map would be the slower design.
        var nodes = await db.MapNodes
            .AsNoTracking()
            .OrderBy(node => node.MapId)
            .ThenBy(node => node.SortOrder)
            .Select(node => new MapNodeOptionDto(node.Id, node.MapId, node.DisplayName, node.IsEntryPoint))
            .ToListAsync(cancellationToken);

        return new MapTargetsDto(
            maps.Select(map => new MapTargetOptionDto(map.Id, map.Name))
                .OrderBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            zones,
            locations,
            regions,
            nodes);
    }

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

        var issues = await ValidateWorldAsync(mapId, candidate, cancellationToken);
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
    /// Runs the same world validation a save runs, without persisting anything.
    /// </summary>
    /// <param name="mapId">Map identifier from the route.</param>
    /// <param name="candidate">The graph to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether a save would accept the graph, and every issue the author needs to see.</returns>
    public async Task<MapGraphValidationResponse> ValidateAsync(
        string mapId,
        MapGraphDto candidate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mapId))
        {
            return new MapGraphValidationResponse(
                false,
                [new MapGraphIssueDto("request_invalid", "A map id is required.", "Error", null, null)]);
        }

        if (!string.Equals(candidate.Id, mapId, StringComparison.OrdinalIgnoreCase))
        {
            return new MapGraphValidationResponse(
                false,
                [
                    new MapGraphIssueDto(
                        "request_invalid",
                        $"The graph id '{candidate.Id}' does not match the map being validated ('{mapId}').",
                        "Error",
                        mapId,
                        null),
                ]);
        }

        var issues = await ValidateWorldAsync(mapId, candidate, cancellationToken);
        var isValid = issues.All(issue => !string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        return new MapGraphValidationResponse(isValid, issues);
    }

    /// <summary>
    /// Validates the candidate as part of the whole world, so a save cannot strand a room or point an exit at a
    /// map that does not exist.
    /// </summary>
    internal async Task<IReadOnlyList<MapGraphIssueDto>> ValidateWorldAsync(
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

        // A candidate whose map is not stored yet still has to be validated in its own right: otherwise a
        // validate-before-create would report the graph as clean because nothing in the world matched its id.
        if (!maps.Any(map => string.Equals(map.Id, mapId, StringComparison.OrdinalIgnoreCase)))
            graphs.Add(ToSharedGraph(candidate));

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

    /// <summary>
    /// Projects a stored map onto the DTO the editor and the game read.
    /// </summary>
    /// <remarks>
    /// Authored coordinates are an ordering, not a distance: what matters is which cell a node occupies relative
    /// to its neighbours, and nothing in the game reads the absolute numbers. Hand-seeded graphs use coordinates
    /// like <c>x=34</c> beside <c>x=2</c>, which the editor rendered on a canvas thousands of pixels wide, so
    /// every read re-ranks each axis onto consecutive cells. A node with no authored position keeps
    /// <c>null</c> and stays in the unplaced lane. Saving the compacted graph persists the compact numbers, so
    /// the rule is self-healing and needs no data migration.
    /// </remarks>
    /// <param name="map">The stored map.</param>
    /// <returns>The graph as the API serves it.</returns>
    private static MapGraphDto ToDto(MapEntity map)
    {
        var columns = DenseRanks(map.Nodes.Select(node => node.LayoutX));
        var rows = DenseRanks(map.Nodes.Select(node => node.LayoutY));

        return new(
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
                    node.LayoutX is { } x ? columns[x] : null,
                    node.LayoutY is { } y ? rows[y] : null,
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
            map.Version,
            map.IsActive,
            map.ApprovedVersion,
            map.SubmittedAt,
            map.ApprovedAt,
            map.LastReviewNotes,
            map.LastReviewedAt);
    }

    /// <summary>Maps each distinct authored coordinate onto a consecutive index, smallest first.</summary>
    /// <param name="coordinates">The authored coordinates of one axis, `null` for an unplaced node.</param>
    /// <returns>The coordinate each value is displayed at.</returns>
    private static Dictionary<int, int> DenseRanks(IEnumerable<int?> coordinates) =>
        coordinates
            .Where(coordinate => coordinate is not null)
            .Select(coordinate => coordinate!.Value)
            .Distinct()
            .Order()
            .Select((coordinate, index) => (coordinate, index))
            .ToDictionary(entry => entry.coordinate, entry => entry.index);

    /// <summary>
    /// Creates an inactive draft with a single entry node.
    /// </summary>
    /// <remarks>
    /// Nothing is fabricated beyond the entry node: an exit would need a destination, and inventing one is how a
    /// map ends up pointing at content the author never chose. A dungeon draft is therefore not approvable until
    /// its author adds the way out, which is where <c>map_without_exit</c> belongs.
    /// </remarks>
    /// <param name="request">Kind, owner and name of the map to create.</param>
    /// <param name="createdByAccountId">Account creating the map.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created graph, or the reason it could not be created.</returns>
    public async Task<(MapGraphDto? Map, string? Error)> CreateDraftAsync(
        MapCreateRequest request,
        Guid createdByAccountId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MapType>(request.MapType, ignoreCase: true, out var mapType) ||
            mapType is MapType.World)
        {
            return (null, $"'{request.MapType}' is not a map kind that can be created.");
        }

        var ownerId = request.OwnerId?.Trim() ?? string.Empty;
        if (ownerId.Length == 0)
            return (null, "An owner slug is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "A name is required.");

        var mapId = mapType switch
        {
            MapType.Region => $"region:{ownerId}",
            MapType.Interior => $"interior:{ownerId}",
            _ => $"zone:{ownerId}",
        };

        if (await db.Maps.AnyAsync(map => map.Id == mapId, cancellationToken))
            return (null, $"Map '{mapId}' already exists.");

        if (await db.Maps.AnyAsync(
                map => map.MapType == mapType && map.OwnerId == ownerId,
                cancellationToken))
        {
            return (null, $"'{ownerId}' already owns a {mapType} map.");
        }

        var now = DateTimeOffset.UtcNow;
        var entryNodeId = $"{mapId}:entry";
        var map = new MapEntity
        {
            Id = mapId,
            MapType = mapType,
            OwnerId = ownerId,
            Name = request.Name.Trim(),
            Description = string.Empty,
            EntryNodeId = entryNodeId,
            IsActive = false,
            Version = 1,
            UpdatedAt = now,
            CreatedByAccountId = createdByAccountId,
            Nodes =
            [
                new MapNodeEntity
                {
                    Id = entryNodeId,
                    MapId = mapId,
                    NodeType = MapNodeType.EntryPoint,
                    DisplayName = "Arrival",
                    Description = string.Empty,
                    IsEntryPoint = true,
                    LayoutX = 0,
                    LayoutY = 0,
                    SortOrder = 0,
                },
            ],
        };

        db.Maps.Add(map);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created draft map {MapId}", mapId);
        return (ToDto(map), null);
    }

    /// <summary>Puts a draft forward for review, refusing a graph the world validator rejects.</summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="accountId">Account submitting the map.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the map is now queued, and the issues that blocked it when not.</returns>
    public async Task<MapLifecycleResponse> SubmitAsync(
        string mapId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return Missing(mapId);

        if (map.IsActive)
            return new MapLifecycleResponse(false, StatusConflict, $"Map '{mapId}' is already live.", []);

        var issues = await ValidateWorldAsync(mapId, ToDto(map), cancellationToken);
        if (HasErrors(issues))
        {
            return new MapLifecycleResponse(
                false,
                StatusInvalid,
                "The graph has errors; fix them before submitting.",
                issues);
        }

        var now = DateTimeOffset.UtcNow;
        map.SubmittedAt = now;
        map.SubmittedByAccountId = accountId;
        map.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return new MapLifecycleResponse(true, StatusOk, "Submitted for review.", issues);
    }

    /// <summary>
    /// Records a reviewer's decision. Approval is what makes a map live.
    /// </summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="reviewerAccountId">Account reviewing the map.</param>
    /// <param name="request">The decision and optional notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the decision was applied, and why not when it was refused.</returns>
    public async Task<MapReviewOutcome> ReviewAsync(
        string mapId,
        Guid reviewerAccountId,
        MapReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return new MapReviewOutcome(Missing(mapId), null, mapId);

        var author = map.SubmittedByAccountId ?? map.CreatedByAccountId;

        if (!request.Approved)
        {
            map.SubmittedAt = null;
            map.SubmittedByAccountId = null;
            map.LastReviewNotes = request.Notes;
            map.LastReviewedAt = DateTimeOffset.UtcNow;
            map.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Rejected map {MapId}: {Notes}", mapId, request.Notes);
            return new MapReviewOutcome(
                new MapLifecycleResponse(
                    true,
                    StatusOk,
                    request.Notes ?? "Rejected; the draft is editable again.",
                    []),
                author,
                map.Name);
        }

        if (map.SubmittedAt is null)
        {
            return new MapReviewOutcome(
                new MapLifecycleResponse(
                    false,
                    StatusConflict,
                    "The map has not been submitted for review.",
                    []),
                author,
                map.Name);
        }

        var issues = await ValidateWorldAsync(mapId, ToDto(map), cancellationToken);
        if (HasErrors(issues))
        {
            return new MapReviewOutcome(
                new MapLifecycleResponse(
                    false,
                    StatusInvalid,
                    "The graph has errors; it cannot be approved.",
                    issues),
                author,
                map.Name);
        }

        map.IsActive = true;
        map.ApprovedVersion = map.Version;
        map.ApprovedAt = DateTimeOffset.UtcNow;
        map.ApprovedByAccountId = reviewerAccountId;
        map.LastReviewNotes = request.Notes;
        map.LastReviewedAt = map.ApprovedAt;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Approved map {MapId} at version {Version}", mapId, map.Version);
        return new MapReviewOutcome(
            new MapLifecycleResponse(true, StatusOk, "Approved; the map is live.", issues),
            author,
            map.Name);
    }

    /// <summary>Retires a live map. Refused while any node still points at it.</summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the map is now inactive, and which exits blocked it when not.</returns>
    public async Task<MapLifecycleResponse> DeactivateAsync(
        string mapId,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return Missing(mapId);

        if (!map.IsActive)
            return new MapLifecycleResponse(true, StatusOk, "The map is already inactive.", []);

        var referring = await ReferringNodesAsync(mapId, cancellationToken);
        if (referring.Count > 0)
        {
            return new MapLifecycleResponse(
                false,
                StatusConflict,
                $"Other maps still exit here: {string.Join(", ", referring)}.",
                []);
        }

        map.IsActive = false;
        map.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deactivated map {MapId}", mapId);
        return new MapLifecycleResponse(true, StatusOk, "Deactivated; the map is no longer in the game.", []);
    }

    /// <summary>Deletes a draft. A live map is retired by deactivating instead.</summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the draft was removed, and why not when it was refused.</returns>
    public async Task<MapLifecycleResponse> DeleteDraftAsync(
        string mapId,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return Missing(mapId);

        if (map.IsActive)
        {
            return new MapLifecycleResponse(
                false,
                StatusConflict,
                "A live map is retired by deactivating, never deleted.",
                []);
        }

        var referring = await ReferringNodesAsync(mapId, cancellationToken);
        if (referring.Count > 0)
        {
            return new MapLifecycleResponse(
                false,
                StatusConflict,
                $"Other maps still exit here: {string.Join(", ", referring)}.",
                []);
        }

        db.Maps.Remove(map);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted draft map {MapId}", mapId);
        return new MapLifecycleResponse(true, StatusOk, "Draft deleted.", []);
    }

    private static bool HasErrors(IEnumerable<MapGraphIssueDto> issues) =>
        issues.Any(issue => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<string>> ReferringNodesAsync(
        string mapId,
        CancellationToken cancellationToken) =>
        await db.MapNodes
            .AsNoTracking()
            .Where(node => node.TargetMapId == mapId)
            .OrderBy(node => node.Id)
            .Select(node => node.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

    private static MapLifecycleResponse Missing(string mapId) =>
        new(false, StatusNotFound, $"Map '{mapId}' was not found.", []);

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
