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

    /// <summary>The caller may not write this map: it belongs to another account.</summary>
    private const string StatusForbidden = "forbidden";

    /// <summary>
    /// Lists the maps the caller may see, with its version, size and whether they may write it.
    /// </summary>
    /// <remarks>
    /// The portal is public to signed-in accounts, so the list is scoped rather than gated: everyone sees the
    /// maps that are live, and a player also sees the drafts they created. An editor or a reviewer sees every
    /// draft, because curating and deciding is their job.
    /// </remarks>
    /// <param name="caller">Who is asking, and how far their authority reaches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The visible map summaries, ordered by id.</returns>
    public async Task<IReadOnlyList<MapSummaryDto>> ListAsync(
        MapCaller caller,
        CancellationToken cancellationToken = default)
    {
        var accountId = caller.AccountId;
        var seesEveryDraft = caller.SeesEveryDraft;
        var canManageAny = caller.CanManageAny;

        var summaries = await db.Maps
            .AsNoTracking()
            // The same rule as `MapCaller.CanSee`, spelled out because EF cannot translate an instance method
            // over the queryable. `GetTargetsAsync` and `GetAsync` call the helper; this one cannot, so the two
            // must be kept identical by hand.
            .Where(map => seesEveryDraft || map.IsActive || map.CreatedByAccountId == accountId)
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
                map.LastReviewedAt,
                canManageAny || map.CreatedByAccountId == accountId))
            .ToListAsync(cancellationToken);

        // The review queue asks one question per row — "would approval accept this?" — and a decision re-runs
        // the world rules, so answering it here is the same pass a reviewer would trigger. One world load
        // answers every row: asking per row cost two round trips and a full catalog load each, so the queue
        // got slower with every draft waiting for a decision.
        var awaiting = summaries
            .Where(map => map.SubmittedAt is not null && !map.IsActive)
            .Select(map => map.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (awaiting.Count == 0) return summaries;

        var world = await LoadWorldAsync(cancellationToken);

        return summaries
            .Select(map => awaiting.Contains(map.Id) ? map with { Review = VerdictFor(world, map) } : map)
            .ToList();
    }

    /// <summary>
    /// Lists the references a node may point at, so the editor can offer pickers instead of free text.
    /// </summary>
    /// <param name="caller">Who is asking, so a stranger's draft is not in the response at all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Maps, zone owners, locations and region owners, each ordered by id.</returns>
    public async Task<MapTargetsDto> GetTargetsAsync(
        MapCaller caller,
        CancellationToken cancellationToken = default)
    {
        var allMaps = await db.Maps
            .AsNoTracking()
            .Select(map => new { map.Id, map.MapType, map.OwnerId, map.Name, map.IsActive, map.CreatedByAccountId })
            .ToListAsync(cancellationToken);

        // A draft is visible to its author and to reviewers, exactly as `GetAsync` decides it, so the picker
        // cannot hand out an id — or a label — that the detail read would hide.
        var maps = allMaps
            .Where(map => caller.CanSee(map.IsActive, map.CreatedByAccountId))
            .ToList();

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

        // Every node of a visible map: an exit's destination is a picker, and the whole set is small enough
        // (a few hundred rows) that a round trip per target map would be the slower design. A node inherits
        // its map's visibility, so a draft's nodes must not leak through the picker either.
        var visibleMapIds = maps
            .Select(map => map.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodes = await db.MapNodes
            .AsNoTracking()
            .Where(node => visibleMapIds.Contains(node.MapId))
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

    /// <summary>Reads one map graph in full, if the caller may see it.</summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="caller">Who is asking, so a stranger's draft reads as missing rather than visible.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The graph including its version, or <see langword="null"/> when it does not exist — or is a draft the
    /// caller did not write.
    /// </returns>
    public async Task<MapGraphDto?> GetAsync(
        string mapId,
        MapCaller caller,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: false, cancellationToken);
        if (map is null) return null;

        // A draft belongs to its author until a reviewer approves it. Reading someone else's unfinished map
        // is not part of the public portal, and a 404 says less than a 403.
        if (!caller.CanSee(map)) return null;

        return ToDto(map) with { CanEdit = caller.MayEdit(map.CreatedByAccountId) };
    }

    /// <summary>
    /// Validates and persists a map graph.
    /// </summary>
    /// <param name="mapId">Map identifier from the route.</param>
    /// <param name="candidate">The graph to store.</param>
    /// <param name="expectedVersion">The version the caller started from.</param>
    /// <param name="caller">Who is asking, so a map belonging to someone else is refused.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome, with validation issues when the graph was refused.</returns>
    public async Task<MapGraphSaveResponse> SaveAsync(
        string mapId,
        MapGraphDto candidate,
        int expectedVersion,
        MapCaller caller,
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

        if (!caller.MayEdit(map.CreatedByAccountId))
        {
            logger.LogInformation(
                "Refused a save of {MapId}: account {AccountId} is not its author and cannot manage content",
                mapId,
                caller.AccountId);

            return ForbiddenSave();
        }

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

        // Gameplay reads the stored graph of every active map, so a save over a live map would put a graph no
        // reviewer has seen in front of players the moment it commits. Author saves stay refused until map
        // revisions can publish an approved copy — retirement is not a way out, because most live maps are
        // referenced by other maps' nodes and DeactivateAsync refuses them. The client shows this sentence
        // verbatim, so it names the freeze rather than sending the author down a path that cannot work yet.
        if (map.IsActive && !caller.SeesEveryDraft)
        {
            logger.LogInformation(
                "Refused a save of {MapId}: it is live and account {AccountId} cannot take it out of play",
                mapId,
                caller.AccountId);

            return new MapGraphSaveResponse(
                false,
                StatusConflict,
                map.Version,
                [],
                LiveMapSaveRefusal(mapId));
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
    /// <param name="mapId">Map identifier the candidate claims to be.</param>
    /// <param name="candidate">The graph to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The issues that belong to this map, plus an exit pointing at a map that does not exist.</returns>
    internal async Task<IReadOnlyList<MapGraphIssueDto>> ValidateWorldAsync(
        string mapId,
        MapGraphDto candidate,
        CancellationToken cancellationToken = default) =>
        ValidateWorld(await LoadWorldAsync(cancellationToken), mapId, candidate);

    /// <summary>The catalog the world rules read, loaded once so a pass over many maps loads it once.</summary>
    /// <param name="Maps">Every authored map, with its nodes and edges.</param>
    /// <param name="Locations">Every zone location, which is how a node's reference is resolved.</param>
    private sealed record MapWorld(
        IReadOnlyList<MapEntity> Maps,
        IReadOnlyList<RealmEngine.Data.Entities.ZoneLocation> Locations);

    /// <summary>Loads the whole catalog the world rules need.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The maps, each with its nodes and edges, and every zone location.</returns>
    private async Task<MapWorld> LoadWorldAsync(CancellationToken cancellationToken)
    {
        var maps = await db.Maps
            .AsNoTracking()
            .Include(map => map.Nodes)
            .Include(map => map.Edges)
            .ToListAsync(cancellationToken);

        var locations = await db.ZoneLocations.AsNoTracking().ToListAsync(cancellationToken);

        return new MapWorld(maps, locations);
    }

    /// <summary>Validates the candidate as part of the world snapshot it is given.</summary>
    /// <param name="world">The catalog snapshot to validate against.</param>
    /// <param name="mapId">Map identifier the candidate claims to be.</param>
    /// <param name="candidate">The graph to validate.</param>
    /// <returns>The issues that belong to this map, plus an exit pointing at a map that does not exist.</returns>
    private static IReadOnlyList<MapGraphIssueDto> ValidateWorld(
        MapWorld world,
        string mapId,
        MapGraphDto candidate)
    {
        var maps = world.Maps;

        var graphs = maps
            .Select(map => string.Equals(map.Id, mapId, StringComparison.OrdinalIgnoreCase)
                ? ToSharedGraph(candidate)
                : ToSharedGraph(map))
            .ToList();

        // A candidate whose map is not stored yet still has to be validated in its own right: otherwise a
        // validate-before-create would report the graph as clean because nothing in the world matched its id.
        if (!maps.Any(map => string.Equals(map.Id, mapId, StringComparison.OrdinalIgnoreCase)))
            graphs.Add(ToSharedGraph(candidate));

        // The snapshot holds every location, which keeps the JSON-owned traits out of the SQL translation: the
        // dungeon set is a pure function of the catalog, derived exactly like the seeded-world guard in
        // SeededContentValidationTests (a dungeon room plus a lobby).
        var locations = world.Locations;
        var locationSlugs = locations.Select(location => location.Slug).ToList();

        var dungeonZoneSlugs = locations
            .Where(location => location.Traits.IsDungeon == true)
            .Select(location => location.ZoneId)
            .Where(zoneId => locations.Any(other =>
                other.ZoneId == zoneId && other.Traits.SafeRespawn == true))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Which zone each location belongs to, so a zone map is checked against its own locations rather than
        // the whole catalog. TryAdd rather than ToDictionary: a duplicated slug is a catalog problem the audit
        // reports, not a reason for a save to throw.
        var locationZones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations)
            locationZones.TryAdd(location.Slug, location.ZoneId);

        var liveMapIds = maps
            .Where(map => map.IsActive)
            .Select(map => map.Id)
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
            dungeonZoneSlugs: dungeonZoneSlugs,
            locationZones: locationZones,
            liveMapIds: liveMapIds);

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

        // An interior hangs off a location that already exists; a region or a zone map is what *defines* its
        // owner, so a new slug is exactly right there.
        if (mapType is MapType.Interior &&
            !await db.ZoneLocations.AnyAsync(location => location.Slug == ownerId, cancellationToken))
        {
            return (
                null,
                $"No location with slug '{ownerId}' exists. Create the location first, then its interior.");
        }

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
        // The creator is the author, so the editor can open it with the write controls already on.
        return (ToDto(map) with { CanEdit = true }, null);
    }

    /// <summary>Puts a draft forward for review, refusing a graph the world validator rejects.</summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="caller">The author submitting, or an editor submitting on their behalf.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the map is now queued, and the issues that blocked it when not.</returns>
    public async Task<MapLifecycleResponse> SubmitAsync(
        string mapId,
        MapCaller caller,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return Missing(mapId);

        if (!caller.MayEdit(map.CreatedByAccountId)) return Forbidden(mapId);

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
        map.SubmittedByAccountId = caller.AccountId;
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

        // A warning is what lets an author save a half-made map — that is how a zone gets built at all — so it
        // is not a reason to refuse a save. Approving is the claim that the map is ready, and a map with no
        // exit strands every character that arrives on it.
        if (BlocksPublication(issues))
        {
            return new MapReviewOutcome(
                new MapLifecycleResponse(
                    false,
                    StatusInvalid,
                    "The map has no exit, so a character could only leave it by dying.",
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
    /// <param name="caller">Who is asking, so only the author or an editor may delete it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the draft was removed, and why not when it was refused.</returns>
    public async Task<MapLifecycleResponse> DeleteDraftAsync(
        string mapId,
        MapCaller caller,
        CancellationToken cancellationToken = default)
    {
        var map = await LoadAsync(mapId, tracking: true, cancellationToken);
        if (map is null)
            return Missing(mapId);

        if (!caller.MayEdit(map.CreatedByAccountId)) return Forbidden(mapId);

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

    /// <summary>
    /// Whether the issues block putting a map live even though they do not block a save. Approval is the gate:
    /// a draft may be half-made, a live map may not.
    /// </summary>
    private static bool BlocksPublication(IEnumerable<MapGraphIssueDto> issues) =>
        issues.Any(issue => issue.Code == "map_without_exit");

    /// <summary>
    /// The verdict the review queue shows for a map awaiting a decision.
    /// </summary>
    /// <remarks>
    /// Computed with <see cref="BlocksPublication"/> and the same error rule the decision applies, so the queue
    /// cannot promise an approval the decision would then refuse.
    /// </remarks>
    /// <param name="world">The catalog snapshot loaded for this list.</param>
    /// <param name="summary">The submitted map, as the list already reported it.</param>
    /// <returns>The verdict, or <see langword="null"/> when the map is no longer in the snapshot.</returns>
    private MapReviewVerdictDto? VerdictFor(MapWorld world, MapSummaryDto summary)
    {
        var stored = world.Maps
            .FirstOrDefault(map => string.Equals(map.Id, summary.Id, StringComparison.OrdinalIgnoreCase));

        if (stored is null) return null;

        var issues = ValidateWorld(world, summary.Id, ToDto(stored));
        var errors = issues.Count(issue =>
            string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));

        var blocked = errors > 0 || BlocksPublication(issues);

        return new MapReviewVerdictDto(blocked ? "blocked" : "ready", errors, issues.Count - errors);
    }

    /// <summary>
    /// The account to tell about a lifecycle change, and the name to title the notification with.
    /// </summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The submitter or creator and the map's name, or <see langword="null"/> when no such map exists. Seeded
    /// content has neither author, which the returned <see cref="MapAuthor.AccountId"/> reports as null.
    /// </returns>
    public async Task<MapAuthor?> AuthorOfAsync(string mapId, CancellationToken cancellationToken = default)
    {
        var map = await db.Maps
            .AsNoTracking()
            .Where(candidate => candidate.Id == mapId)
            .Select(candidate => new
            {
                candidate.SubmittedByAccountId,
                candidate.CreatedByAccountId,
                candidate.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return map is null
            ? null
            : new MapAuthor(map.SubmittedByAccountId ?? map.CreatedByAccountId, map.Name);
    }

    private static MapLifecycleResponse Forbidden(string mapId) =>
        new(
            false,
            StatusForbidden,
            $"Map '{mapId}' belongs to another account, and this account cannot manage content.",
            []);

    private static MapGraphSaveResponse ForbiddenSave() =>
        new(
            false,
            StatusForbidden,
            0,
            [],
            "This map belongs to another account, and this account cannot manage content.");

    /// <summary>
    /// The refusal an author sees when they try to save over a live map.
    /// </summary>
    /// <remarks>
    /// The client renders this sentence verbatim, so it names the freeze rather than a workaround. Retirement
    /// is not an authoring workflow: most live maps are referenced by other maps' nodes, and
    /// <see cref="DeactivateAsync"/> refuses those, so the old "deactivate, edit, submit, approve" instructions
    /// sent the author down a path that cannot work until map revisions exist.
    /// </remarks>
    /// <param name="mapId">Map identifier being saved.</param>
    /// <returns>The refusal message.</returns>
    private static string LiveMapSaveRefusal(string mapId) =>
        $"Map '{mapId}' is live. Authors cannot save changes to live maps until map revisions are supported.";

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
