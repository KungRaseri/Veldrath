namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Durable store for the live enemies standing at zone locations.
/// </summary>
/// <remarks>
/// This interface replaces the in-process <c>ZoneLocationEnemyStore</c>: every read and write goes to Postgres,
/// so a restart or a rolling deploy no longer wipes the world. Implementations return plain
/// <see cref="SpawnedEnemy"/> instances for the combat math, but those instances are <em>views</em> — they are
/// materialised per call and must be persisted back through <see cref="SaveAsync"/> after a mutation.
/// </remarks>
public interface IEnemyInstanceStore
{
    /// <summary>
    /// Returns the living enemies at a location, spawning the roster first when the location has no rows yet.
    /// </summary>
    /// <param name="zoneGroup">SignalR zone group, e.g. <c>"zone:dark-forest_hardcore"</c>.</param>
    /// <param name="locationSlug">Zone-location slug.</param>
    /// <param name="spawnRoster">Invoked only when no roster exists for the location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The living enemies at the location.</returns>
    Task<IReadOnlyList<SpawnedEnemy>> GetAliveOrSpawnAsync(
        string zoneGroup,
        string locationSlug,
        Func<CancellationToken, Task<List<SpawnedEnemy>>> spawnRoster,
        CancellationToken cancellationToken = default);

    /// <summary>Returns one enemy instance by id, or <see langword="null"/> when it no longer exists.</summary>
    /// <param name="enemyId">Enemy instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SpawnedEnemy?> TryGetAsync(Guid enemyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the mutable state of a materialised enemy: current health, the participant set and every
    /// damage total. Called after the combat math has run and before the turn is broadcast.
    /// </summary>
    /// <param name="enemy">The materialised enemy whose state changed.</param>
    /// <param name="zoneGroup">SignalR zone group the enemy belongs to.</param>
    /// <param name="locationSlug">Zone-location slug the enemy belongs to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(
        SpawnedEnemy enemy,
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically flips a living enemy to dead and schedules its return. Exactly one caller can win, so rewards
    /// are distributed once even when several players land the killing blow at the same moment.
    /// </summary>
    /// <param name="enemyId">Enemy instance id.</param>
    /// <param name="respawnDelay">How long the enemy stays dead.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when this call performed the death transition.</returns>
    Task<bool> TryMarkDeadAsync(
        Guid enemyId,
        TimeSpan respawnDelay,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an enemy instance and its participants.</summary>
    /// <param name="enemyId">Enemy instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(Guid enemyId, CancellationToken cancellationToken = default);
}
