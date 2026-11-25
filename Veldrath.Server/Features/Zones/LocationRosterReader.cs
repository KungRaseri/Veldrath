using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// Reads the live enemy roster standing at a zone location, spawning it on first contact.
/// </summary>
/// <remarks>
/// One reader for every way a character comes to stand somewhere: a walk
/// (<see cref="Map.TravelToNodeHubCommandHandler"/>) and a resume
/// (<see cref="Map.GetLocationRosterHubCommandHandler"/>) ask the same question of the same store. A second
/// read path would be a second spawn path — two arrivals at the same instant would each see an empty room and
/// insert their own cohort, which is how a location ends up with two rosters and only one of them rendered.
/// </remarks>
/// <remarks>
/// Durable rows are the species here, not the enemies in memory: <see cref="IEnemyInstanceStore"/> revives a
/// wiped world on read, so calling this again on a later arrival returns the cohort already standing there
/// instead of adding another one.
/// </remarks>
public sealed class LocationRosterReader
{
    private readonly IEnemyInstanceStore _enemies;
    private readonly IActorInstanceRepository _actorInstances;
    private readonly ActorPoolResolver _poolResolver;
    private readonly ILogger<LocationRosterReader> _logger;

    /// <summary>Initializes a new instance of <see cref="LocationRosterReader"/>.</summary>
    /// <param name="enemies">Store holding the live enemy instances at each scope and location.</param>
    /// <param name="actorInstances">Repository for the named actors content places at a location.</param>
    /// <param name="poolResolver">Resolver that draws the pool into a cohort when the location has none yet.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public LocationRosterReader(
        IEnemyInstanceStore enemies,
        IActorInstanceRepository actorInstances,
        ActorPoolResolver poolResolver,
        ILogger<LocationRosterReader> logger)
    {
        _enemies = enemies;
        _actorInstances = actorInstances;
        _poolResolver = poolResolver;
        _logger = logger;
    }

    /// <summary>
    /// Returns the living enemies at a location, spawning the cohort when the scope has none.
    /// </summary>
    /// <param name="zoneGroup">SignalR scope the roster belongs to — the shared zone, or a run's own group.</param>
    /// <param name="locationSlug">Zone-location slug the character is standing on.</param>
    /// <param name="location">The location row, or <see langword="null"/> when the slug is not in the catalog.</param>
    /// <param name="zoneInstanceId">Run the roster belongs to, or <see langword="null"/> in the shared zone.</param>
    /// <returns>
    /// The roster as the client renders it, and any content problem found while reading it. An authored pool
    /// that produced nothing is content debt: the player is told, the operator gets the resolver's log line.
    /// </returns>
    public async Task<(IReadOnlyList<SpawnedEnemySummary> Enemies, IReadOnlyList<string> Warnings)> ReadAsync(
        string zoneGroup,
        string locationSlug,
        ZoneLocationEntry? location,
        Guid? zoneInstanceId)
    {
        // A node whose RefId does not resolve is content debt, not a normal state: say so instead of pretending
        // the room is merely empty.
        if (location is null)
        {
            _logger.LogError(
                "Location '{LocationSlug}' is missing from the content catalog; no roster was read",
                locationSlug);

            return ([], [$"Content error: the location '{locationSlug}' is missing from the content catalog."]);
        }

        var pool = location.ActorPool ?? [];

        // A named actor placed here is part of the roster, not part of the pool: it is emitted once, whatever the
        // pool draws, because "the chief stands in the clearing" is content and not a probability.
        var placements = await _actorInstances.GetPlacedAsync(locationSlug, CancellationToken.None);

        // Reads the stored roster, spawning it on first arrival. Dead rows with an elapsed respawn time are
        // revived by the store, so this is also where a wiped world comes back. The token is deliberately not
        // forwarded: an arrival abandoned mid-read must not leave a half-written cohort behind, and the write is
        // the point of the call.
        var alive = await _enemies.GetAliveOrSpawnAsync(
            zoneGroup,
            locationSlug,
            _ => _poolResolver.SpawnRosterAsync(pool, placements),
            zoneInstanceId,
            CancellationToken.None);

        _logger.LogInformation(
            "{Count} enemies present at {ZoneGroup}/{LocationSlug}",
            alive.Count,
            zoneGroup,
            locationSlug);

        // Failsafe, not fallback: an authored pool that produced nothing, with nothing placed here either, means
        // broken content. A location that relies on its placements is not a broken pool.
        var warnings = new List<string>();
        if (alive.Count == 0 && pool.Count > 0 && placements.Count == 0)
        {
            warnings.Add(
                $"Content error: the spawn pool for '{location.Slug}' references no known actors. Please report this to a staff member.");
        }

        return (alive
            .Select(enemy => new SpawnedEnemySummary(
                enemy.Id,
                enemy.Name,
                enemy.Level,
                enemy.CurrentHealth,
                enemy.MaxHealth))
            .ToList(), warnings);
    }
}
