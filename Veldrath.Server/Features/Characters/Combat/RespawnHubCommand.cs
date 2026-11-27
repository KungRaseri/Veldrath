using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that handles a player choosing to respawn after death in normal mode.
/// Verifies the character is dead (HP at or below zero) and resets them to a safe state.
/// </summary>
/// <param name="CharacterId">The character requesting respawn.</param>
/// <param name="ZoneInstanceId">
/// The run the caller's connection is pointing at, or <see langword="null"/> when they died in the zone's shared
/// rooms. The hub supplies this from its own server-side state; a client never names a run, so the value can be
/// trusted to mean "the run this character is inside" and nothing else.
/// </param>
public record RespawnHubCommand(Guid CharacterId, Guid? ZoneInstanceId = null)
    : IRequest<RespawnHubResult>;

/// <summary>Result returned by <see cref="RespawnHubCommandHandler"/>.</summary>
public record RespawnHubResult
{
    /// <summary>Gets a value indicating whether the respawn succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's HP after respawning.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's mana after respawning.</summary>
    public int CurrentMana { get; init; }

    /// <summary>Gets the slug of the location the character woke up at, when the zone has one.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the display name of that location.</summary>
    public string? LocationDisplayName { get; init; }

    /// <summary>Gets whether the respawn point was a degraded fallback rather than an authored safe location.</summary>
    public bool UsedFallbackRespawn { get; init; }

    /// <summary>Gets the client-facing explanation when <see cref="UsedFallbackRespawn"/> is <see langword="true"/>.</summary>
    public string? Warning { get; init; }

    /// <summary>Gets when death sickness wears off, or <see langword="null"/> when none was applied.</summary>
    public DateTimeOffset? SicknessExpiresAt { get; init; }

    /// <summary>
    /// Gets the run the character kept, when the respawn stayed inside the still-open run they died in, or
    /// <see langword="null"/> when the wake put them back in the zone's shared scope.
    /// </summary>
    /// <remarks>
    /// The hub compares this with the run its connection was pointing at: equal means the group is already the right
    /// one, and <see langword="null"/> means the connection has to move to the shared scope. The command's read of the
    /// run is the decision point, so the hub never asks the run state a second question and gets a different answer.
    /// </remarks>
    public Guid? RetainedZoneInstanceId { get; init; }
}

/// <summary>
/// Handles <see cref="RespawnHubCommand"/> by verifying the character was killed (HP ≤ 0),
/// choosing where they wake, removing any lingering combat session, and restoring the character to minimum viable
/// health.
/// </summary>
/// <remarks>
/// The destination is decided before anything is written, so a death inside a run whose content offers no usable
/// respawn point fails without healing the character into a position the caller was not told about.
/// </remarks>
public class RespawnHubCommandHandler
    : IRequestHandler<RespawnHubCommand, RespawnHubResult>
{
    /// <summary>Said when no destination in either scope can be used, so the respawn writes nothing at all.</summary>
    private const string NoValidRespawnLocation = "No valid respawn location is available.";

    /// <summary>
    /// Said when a run still holds the character but authored no lobby they can use: they keep the run and stay on the
    /// room they fell in, which is the one degradation that cannot leave them outside their own run.
    /// </summary>
    private const string RunHasNoUsableLobby = "This run has no safe lobby you can use; you respawn where you fell.";

    private readonly ICharacterRepository _characterRepo;
    private readonly ICombatSessionStore _sessions;
    private readonly IZoneLobbyResolver _lobbies;
    private readonly IMapGraphRepository _maps;
    private readonly IZoneInstanceService _zoneInstances;
    private readonly IOptions<DeathPenaltyOptions> _penalty;
    private readonly ILogger<RespawnHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="RespawnHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="sessions">Store for per-character combat sessions.</param>
    /// <param name="lobbies">Zone lobby resolver, used to find the safe respawn point the character may use.</param>
    /// <param name="maps">Map graphs, used to name the node that represents the safe location.</param>
    /// <param name="zoneInstances">
    /// Run service, read once for the run the caller supplied so the handler can tell a still-open run from a gone one.
    /// </param>
    /// <param name="penalty">Configured death values, including the sickness window.</param>
    /// <param name="logger">Logger instance.</param>
    public RespawnHubCommandHandler(
        ICharacterRepository characterRepo,
        ICombatSessionStore sessions,
        IZoneLobbyResolver lobbies,
        IMapGraphRepository maps,
        IZoneInstanceService zoneInstances,
        IOptions<DeathPenaltyOptions> penalty,
        ILogger<RespawnHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _sessions = sessions;
        _lobbies = lobbies;
        _maps = maps;
        _zoneInstances = zoneInstances;
        _penalty = penalty;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the respawn outcome.</summary>
    /// <param name="request">The command containing the character's ID and the run they were inside, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="RespawnHubResult"/> describing the outcome.</returns>
    public async Task<RespawnHubResult> Handle(
        RespawnHubCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new RespawnHubResult { Success = false, ErrorMessage = "Character not found" };

        if (entity.DeletedAt.HasValue)
            return new RespawnHubResult { Success = false, ErrorMessage = "Hardcore character cannot respawn" };

        var attrs = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        attrs.TryGetValue("CurrentHealth", out int currentHp);

        if (currentHp > 0)
            return new RespawnHubResult { Success = false, ErrorMessage = "Character is not dead" };

        // Where they wake is settled before a single write: a run that offers no usable destination has to fail
        // rather than heal the character and then discover there is nowhere to put them.
        var disposition = await ResolveDispositionAsync(entity, request.ZoneInstanceId, cancellationToken);

        if (disposition.Failure is { } failure)
            return new RespawnHubResult { Success = false, ErrorMessage = failure };

        int maxHealth = attrs.TryGetValue("MaxHealth", out var mh) ? mh : entity.Level * 10;
        int maxMana = attrs.TryGetValue("MaxMana", out var mm) ? mm : entity.Level * 5;

        // Restore to 25% of max HP and full mana.
        int respawnHp = Math.Max(1, maxHealth / 4);
        int respawnMana = maxMana;

        attrs["CurrentHealth"] = respawnHp;
        attrs["CurrentMana"] = respawnMana;

        var options = _penalty.Value;
        DateTimeOffset? sicknessExpiresAt = null;
        if (options.DeathSicknessMinutes > 0 && options.DeathSicknessDamagePenaltyPercent > 0)
        {
            sicknessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(options.DeathSicknessMinutes);
            attrs[DeathPenalties.SicknessUntilKey] = DeathPenalties.ToSicknessStamp(sicknessExpiresAt.Value);
            attrs[DeathPenalties.SicknessPenaltyKey] = options.DeathSicknessDamagePenaltyPercent;
        }
        else
        {
            // A disabled sickness must not leave a stale window behind from an earlier death.
            attrs.Remove(DeathPenalties.SicknessUntilKey);
            attrs.Remove(DeathPenalties.SicknessPenaltyKey);
        }

        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        if (disposition.LocationSlug is { } locationSlug && !string.IsNullOrWhiteSpace(entity.CurrentZoneId))
        {
            await MoveToLocationAsync(
                entity,
                entity.CurrentZoneId!,
                locationSlug,
                disposition.Map,
                disposition.Node,
                cancellationToken);
        }

        await _sessions.RemoveAsync(request.CharacterId, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} respawned at {LocationSlug} with {Hp}/{MaxHp} HP (run {RunLabel})",
            request.CharacterId.ToString()[..8], disposition.LocationSlug ?? "(unchanged)",
            respawnHp, maxHealth,
            disposition.RetainedZoneInstanceId?.ToString()[..8] ?? "shared");

        return new RespawnHubResult
        {
            Success = true,
            CurrentHealth = respawnHp,
            CurrentMana = respawnMana,
            LocationSlug = disposition.LocationSlug,
            LocationDisplayName = disposition.LocationName,
            UsedFallbackRespawn = disposition.UsedFallback,
            Warning = disposition.Warning,
            SicknessExpiresAt = sicknessExpiresAt,
            RetainedZoneInstanceId = disposition.RetainedZoneInstanceId,
        };
    }

    /// <summary>
    /// Where a character wakes, and — when they keep their run — which run that is.
    /// </summary>
    /// <param name="LocationSlug">Location to move to, or <see langword="null"/> to stay on the saved position.</param>
    /// <param name="LocationName">Display name of that location.</param>
    /// <param name="UsedFallback">Whether the destination is a degradation rather than an authored safe location.</param>
    /// <param name="Warning">Player-facing explanation of a degradation, or <see langword="null"/> for none.</param>
    /// <param name="RetainedZoneInstanceId">Run the character keeps, or <see langword="null"/> to fall back to the shared scope.</param>
    /// <param name="Map">Zone map already read for a strict decision, so the move need not read it again.</param>
    /// <param name="Node">Location node already resolved for a strict decision.</param>
    /// <param name="Failure">Reason the respawn cannot proceed, or <see langword="null"/> when it can.</param>
    private readonly record struct RespawnDisposition(
        string? LocationSlug,
        string? LocationName,
        bool UsedFallback,
        string? Warning,
        Guid? RetainedZoneInstanceId,
        MapGraph? Map,
        MapNodeEntry? Node,
        string? Failure)
    {
        /// <summary>A failed disposition: nothing is written, and no scope or event changes.</summary>
        /// <param name="reason">Player-facing reason.</param>
        /// <returns>The disposition.</returns>
        public static RespawnDisposition Failed(string reason) =>
            new(null, null, false, null, null, null, null, reason);
    }

    /// <summary>
    /// Decides the destination and the run to keep, reading the supplied run exactly once.
    /// </summary>
    /// <remarks>
    /// An ordinary death — no run supplied — never touches the run service, so its behaviour is exactly what it was.
    /// A death inside a run branches on that one read: a still-open run in the character's own zone keeps the run and
    /// resolves the destination inside it; anything else is an unavailable run, which resolves only a mapped shared
    /// anchor and reports that no run was kept.
    /// </remarks>
    /// <param name="entity">Character that died.</param>
    /// <param name="suppliedInstanceId">Run the connection was pointing at, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The destination, the retained run and, on failure, why.</returns>
    private async Task<RespawnDisposition> ResolveDispositionAsync(
        Data.Entities.Character entity,
        Guid? suppliedInstanceId,
        CancellationToken cancellationToken)
    {
        if (suppliedInstanceId is not { } instanceId)
            return await ResolveSharedZoneAsync(entity, cancellationToken);

        if (string.IsNullOrWhiteSpace(entity.CurrentZoneId))
        {
            // A run-scoped death with no zone cannot name any destination, and healing in place would leave the caller
            // pointing at a run whose lobby was never chosen.
            return RespawnDisposition.Failed(NoValidRespawnLocation);
        }

        var zoneId = entity.CurrentZoneId!;
        var run = await _zoneInstances.GetAsync(instanceId, cancellationToken);
        var retainsRun = run is { State: Data.Entities.ZoneInstanceState.Open } &&
            string.Equals(run.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase);

        var zoneMap = await _maps.GetMapForZoneAsync(zoneId, cancellationToken);
        var placed = PlacedLocationSlugs(zoneMap);

        return retainsRun
            ? await ResolveInsideRunAsync(entity, zoneId, instanceId, zoneMap, placed, cancellationToken)
            : await ResolveOutOfAnUnavailableRunAsync(entity, zoneId, zoneMap, placed, cancellationToken);
    }

    /// <summary>
    /// Resolves the destination of an ordinary death, which keeps the zone's shared lobby rule and falls back the way
    /// it always has.
    /// </summary>
    /// <param name="entity">Character that died.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shared-scope destination.</returns>
    private async Task<RespawnDisposition> ResolveSharedZoneAsync(
        Data.Entities.Character entity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.CurrentZoneId))
            return new RespawnDisposition(null, null, false, null, null, null, null, null);

        var (slug, name, usedFallback, warning) = await ResolveRespawnLocationAsync(entity, cancellationToken);

        return new RespawnDisposition(slug, name, usedFallback, warning, null, null, null, null);
    }

    /// <summary>
    /// Resolves the wake of a character whose run is still open: the run's own lobby when the content offers one,
    /// otherwise the room they fell in, and never the shared zone's anchor.
    /// </summary>
    /// <param name="entity">Character that died.</param>
    /// <param name="zoneId">Zone the run belongs to.</param>
    /// <param name="instanceId">Run the character keeps.</param>
    /// <param name="zoneMap">Zone map the destination must be placed on.</param>
    /// <param name="placed">Slugs the map places on location nodes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The in-run destination, or a failure when the run offers none.</returns>
    private async Task<RespawnDisposition> ResolveInsideRunAsync(
        Data.Entities.Character entity,
        string zoneId,
        Guid instanceId,
        MapGraph? zoneMap,
        IReadOnlySet<string> placed,
        CancellationToken cancellationToken)
    {
        var resolution = await _lobbies.ResolveAsync(
            zoneId,
            entity.Id,
            instanced: true,
            placed,
            preferredFallbackSlug: entity.CurrentZoneLocationSlug,
            cancellationToken);

        if (resolution.Status == ZoneLobbyStatus.Resolved && resolution.Lobby is { } lobby)
        {
            if (FindLocationNode(zoneMap, lobby.Slug) is { } lobbyNode)
                return new RespawnDisposition(
                    lobby.Slug, lobby.DisplayName, false, null, instanceId, zoneMap, lobbyNode, null);

            // The resolver only returns placements, so this is content that changed between the two reads. Treat it
            // as an unusable lobby rather than writing a node that is not on the map.
            _logger.LogError(
                "Zone {ZoneId} has no map node for run lobby {LocationSlug}; character {CharacterIdPrefix} keeps the run {InstanceId} where they fell",
                zoneId, lobby.Slug, entity.Id.ToString()[..8], instanceId);
        }
        else if (resolution.Status == ZoneLobbyStatus.NotUnlocked && resolution.LockedLobby is { } locked)
        {
            _logger.LogError(
                "Zone {ZoneId} has a hidden run lobby {LocationSlug} that character {CharacterIdPrefix} has not unlocked; run {InstanceId} keeps them where they fell",
                zoneId, locked.Slug, entity.Id.ToString()[..8], instanceId);
        }

        // A run has no shared anchor to borrow: the only honest degradation is the room the character fell in, and
        // only while the saved map and node still name that same room.
        if (resolution.Fallback is { } fallen && IsSavedRoom(entity, zoneMap, fallen.Slug))
        {
            _logger.LogError(
                "Run {InstanceId} in zone {ZoneId} has no safe lobby character {CharacterIdPrefix} can use; they respawn on {LocationSlug} where they fell",
                instanceId, zoneId, entity.Id.ToString()[..8], fallen.Slug);

            return new RespawnDisposition(
                null, null, true, RunHasNoUsableLobby, instanceId, zoneMap, null, null);
        }

        _logger.LogError(
            "Run {InstanceId} in zone {ZoneId} has no respawn destination for character {CharacterIdPrefix} ({Status}); the respawn is refused",
            instanceId, zoneId, entity.Id.ToString()[..8], resolution.Status);

        return RespawnDisposition.Failed(NoValidRespawnLocation);
    }

    /// <summary>
    /// Resolves the wake of a character whose run is gone: only a mapped shared anchor will do, because the run they
    /// were in cannot take them.
    /// </summary>
    /// <param name="entity">Character that died.</param>
    /// <param name="zoneId">Zone the character stands in.</param>
    /// <param name="zoneMap">Zone map the destination must be placed on.</param>
    /// <param name="placed">Slugs the map places on location nodes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shared destination, or a failure when the zone has no mapped shared anchor.</returns>
    private async Task<RespawnDisposition> ResolveOutOfAnUnavailableRunAsync(
        Data.Entities.Character entity,
        string zoneId,
        MapGraph? zoneMap,
        IReadOnlySet<string> placed,
        CancellationToken cancellationToken)
    {
        var resolution = await _lobbies.ResolveAsync(
            zoneId, entity.Id, instanced: false, placed, preferredFallbackSlug: null, cancellationToken);

        (string Slug, string Name, bool UsedFallback, string? Warning)? destination = resolution.Status switch
        {
            ZoneLobbyStatus.Resolved when resolution.Lobby is { } lobby => (lobby.Slug, lobby.DisplayName, false, (string?)null),
            ZoneLobbyStatus.NotUnlocked when resolution.Fallback is { } fallback =>
                (fallback.Slug, fallback.DisplayName, true,
                    "This zone's safe haven is not yet open to you; you wake at its entrance instead."),
            ZoneLobbyStatus.NoSafeLocation when resolution.Fallback is { } entrance =>
                (entrance.Slug, entrance.DisplayName, true,
                    "This zone has no safe respawn point; you wake at its entrance instead."),
            _ => null,
        };

        if (resolution.Status == ZoneLobbyStatus.NotUnlocked && resolution.LockedLobby is { } hidden)
        {
            _logger.LogError(
                "Zone {ZoneId} has a hidden safe respawn location {LocationSlug} that character {CharacterIdPrefix} has not unlocked; waking from a closed run at {FallbackSlug}",
                zoneId, hidden.Slug, entity.Id.ToString()[..8], destination?.Slug ?? "(none)");
        }
        else if (resolution.Status == ZoneLobbyStatus.NoSafeLocation && destination is { } degraded)
        {
            _logger.LogError(
                "Zone {ZoneId} has no location flagged SafeRespawn; waking from a closed run at {LocationSlug}",
                zoneId, degraded.Slug);
        }

        if (destination is not { } chosen)
        {
            _logger.LogError(
                "Zone {ZoneId} has no mapped shared respawn destination for character {CharacterIdPrefix} ({Status}); the respawn is refused",
                zoneId, entity.Id.ToString()[..8], resolution.Status);

            return RespawnDisposition.Failed(NoValidRespawnLocation);
        }

        // The resolver already filtered by placement, so a node exists; reusing it keeps the strict path off the
        // slug-only fallback that the ordinary path still allows.
        var node = FindLocationNode(zoneMap, chosen.Slug);

        return new RespawnDisposition(
            chosen.Slug, chosen.Name, chosen.UsedFallback, chosen.Warning, null, zoneMap, node, null);
    }

    /// <summary>
    /// Reads the slugs the zone map places on location nodes, which is what makes an anchor a destination a client can
    /// actually resume onto.
    /// </summary>
    /// <param name="zoneMap">Zone map, or <see langword="null"/> when the zone has none.</param>
    /// <returns>The placed slugs, compared case-insensitively.</returns>
    private static IReadOnlySet<string> PlacedLocationSlugs(MapGraph? zoneMap) =>
        zoneMap is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : zoneMap.Nodes
                .Where(node => node.NodeType == MapNodeType.Location && !string.IsNullOrWhiteSpace(node.RefId))
                .Select(node => node.RefId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Finds the location node a slug occupies on the zone map.</summary>
    /// <param name="zoneMap">Zone map, or <see langword="null"/>.</param>
    /// <param name="locationSlug">Location slug to look for.</param>
    /// <returns>The node, or <see langword="null"/> when the map does not place that location.</returns>
    private static MapNodeEntry? FindLocationNode(MapGraph? zoneMap, string locationSlug) =>
        zoneMap?.Nodes.FirstOrDefault(node =>
            node.NodeType == MapNodeType.Location &&
            string.Equals(node.RefId, locationSlug, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether the character's saved map and node still describe <paramref name="locationSlug"/>, which is what makes
    /// "stay where you fell" a coherent position instead of a slug that points at another room.
    /// </summary>
    /// <param name="entity">Character that died.</param>
    /// <param name="zoneMap">Zone map the node must belong to.</param>
    /// <param name="locationSlug">Room the character would stay on.</param>
    /// <returns><see langword="true"/> when the saved position is that same room.</returns>
    private static bool IsSavedRoom(Data.Entities.Character entity, MapGraph? zoneMap, string locationSlug)
    {
        if (zoneMap is null ||
            !string.Equals(entity.CurrentZoneLocationSlug, locationSlug, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(entity.CurrentMapId, zoneMap.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FindLocationNode(zoneMap, locationSlug) is { } node &&
            string.Equals(entity.CurrentNodeId, node.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Moves the character onto the node that represents <paramref name="locationSlug"/> inside their zone.
    /// Writing the location slug alone is not enough: a client that logs in again resumes from the map node, so a
    /// respawn that moved only the slug put the player back at the zone's arrival node on the next reload.
    /// </summary>
    /// <param name="entity">The character that died.</param>
    /// <param name="zoneId">The zone the character died in; the map that names the location's node.</param>
    /// <param name="locationSlug">The zone location the character wakes up at.</param>
    /// <param name="knownMap">
    /// Map already read while deciding the destination, reused instead of reading it again; <see langword="null"/> on
    /// the ordinary path, which has not read one.
    /// </param>
    /// <param name="knownNode">Node already resolved while deciding the destination, if any.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task MoveToLocationAsync(
        Data.Entities.Character entity,
        string zoneId,
        string locationSlug,
        MapGraph? knownMap,
        MapNodeEntry? knownNode,
        CancellationToken cancellationToken)
    {
        var zoneMap = knownMap ?? await _maps.GetMapForZoneAsync(zoneId, cancellationToken);
        var locationNode = knownNode ?? FindLocationNode(zoneMap, locationSlug);

        if (zoneMap is null || locationNode is null)
        {
            // No map to name the node — a zone without a graph, or a location it never placed. The player still has
            // to move, so the slug is written on its own; the missing node is content debt and is reported as such.
            _logger.LogError(
                "Zone {ZoneId} has no map node for respawn location {LocationSlug}; character {CharacterIdPrefix} keeps their old node",
                zoneId, locationSlug, entity.Id.ToString()[..8]);
            await _characterRepo.UpdateCurrentZoneLocationAsync(entity.Id, locationSlug, cancellationToken);
            return;
        }

        await _characterRepo.UpdateMapPositionAsync(
            entity.Id,
            zoneMap.Id,
            locationNode.Id,
            zoneId,
            locationSlug,
            cancellationToken);
    }

    /// <summary>
    /// Finds where the character should wake up. The zone's authored safe location wins, read through the
    /// character's own unlocks so a hidden lobby they have opened is found; anything else is a degradation, so it
    /// is logged as an <c>Error</c> and said out loud to the player.
    /// </summary>
    /// <param name="entity">Character that died.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The location to move to (null to stay put), and how degraded that choice was.</returns>
    private async Task<(string? Slug, string? Name, bool UsedFallback, string? Warning)> ResolveRespawnLocationAsync(
        Data.Entities.Character entity,
        CancellationToken cancellationToken)
    {
        var lobby = await _lobbies.ResolveAsync(entity.CurrentZoneId!, entity.Id, cancellationToken);

        switch (lobby.Status)
        {
            case ZoneLobbyStatus.Resolved when lobby.Lobby is { } safe:
                return (safe.Slug, safe.DisplayName, false, null);

            case ZoneLobbyStatus.NotUnlocked when lobby.LockedLobby is { } locked:
                // The zone has a lobby, but it is hidden from this character. Say that rather than blaming the zone
                // for having none: the fallback is still the entrance, and the wording is the difference between
                // "broken content" and "not open to you yet".
                _logger.LogError(
                    "Zone {ZoneId} has a hidden safe respawn location {LocationSlug} that character {CharacterIdPrefix} has not unlocked; falling back to {FallbackSlug}",
                    entity.CurrentZoneId, locked.Slug, entity.Id.ToString()[..8],
                    lobby.Fallback?.Slug ?? "where they fell");
                return lobby.Fallback is { } fallback
                    ? (fallback.Slug, fallback.DisplayName, true,
                        "This zone's safe haven is not yet open to you; you wake at its entrance instead.")
                    : (null, null, true,
                        "This zone's safe haven is not yet open to you; you respawn where you fell.");

            case ZoneLobbyStatus.NoSafeLocation when lobby.Fallback is { } entrance:
                _logger.LogError(
                    "Zone {ZoneId} has no location flagged SafeRespawn; falling back to {LocationSlug}",
                    entity.CurrentZoneId, entrance.Slug);
                return (entrance.Slug, entrance.DisplayName, true,
                    "This zone has no safe respawn point; you wake at its entrance instead.");

            default:
                _logger.LogError(
                    "Zone {ZoneId} has no location a respawn can use for character {CharacterIdPrefix} ({Status}); they respawn where they fell",
                    entity.CurrentZoneId, entity.Id.ToString()[..8], lobby.Status);
                return (null, null, true, "This zone has no place to wake up; you respawn where you fell.");
        }
    }
}
