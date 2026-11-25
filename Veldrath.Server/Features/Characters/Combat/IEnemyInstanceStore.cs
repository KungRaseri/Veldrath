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
    /// <param name="zoneInstanceId">
    /// Instance run the roster belongs to, or <see langword="null"/> for the shared zone. The group already
    /// isolates runs, so this is recorded on new rows rather than used to filter; it is what the operator view
    /// queries and what cascades the roster away when the run is purged.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The living enemies at the location.</returns>
    Task<IReadOnlyList<SpawnedEnemy>> GetAliveOrSpawnAsync(
        string zoneGroup,
        string locationSlug,
        Func<CancellationToken, Task<List<SpawnedEnemy>>> spawnRoster,
        Guid? zoneInstanceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the living enemies at a location in a scope without spawning anything. Used to decide whether a
    /// room is cleared; a read that spawned a roster would answer "no" forever.
    /// </summary>
    /// <param name="zoneGroup">SignalR zone group, e.g. <c>"zone:the-halrow#i1:…"</c>.</param>
    /// <param name="locationSlug">Zone-location slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many enemies at that location are still alive.</returns>
    Task<int> CountAliveAsync(
        string zoneGroup,
        string locationSlug,
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
    /// Atomically flips a living enemy to dead, schedules its return using the configured respawn delay, and reports
    /// who the kill credits: the participants committed when the transition was applied.
    /// </summary>
    /// <remarks>
    /// Exactly one caller can win, so rewards are distributed once even when several players land the killing blow
    /// at the same moment. The participant set travels with the transition on purpose, and is read inside the same
    /// unit of work as the conditional update: a caller holding a <see cref="SpawnedEnemy"/> that was materialised
    /// while the enemy was alive cannot credit a set the kill never had, and a participant committed by a
    /// concurrent turn is credited even when the winner loaded the enemy before that turn.
    /// </remarks>
    /// <param name="enemyId">Enemy instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The character ids the kill credits, or <see langword="null"/> when another caller already performed the
    /// transition — and therefore already took the credit, the rewards and the room.
    /// </returns>
    Task<IReadOnlyList<Guid>?> TryMarkDeadAsync(Guid enemyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revives up to <paramref name="batchSize"/> dead enemies whose respawn time has passed, clearing their
    /// participants, and returns what came back so the caller can broadcast it.
    /// </summary>
    /// <remarks>
    /// On PostgreSQL the batch is claimed with <c>FOR UPDATE SKIP LOCKED</c> inside a transaction, so several
    /// instances can sweep concurrently without reviving the same enemy twice.
    /// </remarks>
    /// <param name="batchSize">Maximum number of enemies to revive in this sweep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The enemies that were brought back.</returns>
    Task<IReadOnlyList<EnemyRespawnedEvent>> SweepExpiredRespawnsAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an enemy instance and its participants.</summary>
    /// <param name="enemyId">Enemy instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(Guid enemyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every stored enemy whose zone group does not name a zone that owns its location, and returns how many
    /// rows went. This is debris cleanup, not read-side filtering: a row a client can never load still costs every
    /// read of its location and would otherwise stay in the table forever.
    /// </summary>
    /// <remarks>
    /// The caller supplies the zones per location slug because that mapping is content, and this store only sees the
    /// application database. A row whose slug is absent from the map is reported and left in place, never deleted: a
    /// slug the catalog no longer holds — or one whose location has been deactivated, since the map is built from the
    /// active locations — cannot be judged, and deleting on absence is how live state gets destroyed by a content
    /// rollback. The store logs a warning naming each distinct group/slug pair it left behind, so the rows it could
    /// not judge do not go unreported.
    /// </remarks>
    /// <param name="zonesByLocationSlug">Zones that own each location slug, keyed by slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many enemy rows were deleted.</returns>
    Task<int> RemoveForeignZoneGroupRowsAsync(
        IReadOnlyDictionary<string, IReadOnlySet<string>> zonesByLocationSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes back the pending respawn of every enemy that died at a location, which is what makes a
    /// <em>cleared</em> room stay quiet longer than a single kill does.
    /// </summary>
    /// <remarks>
    /// The extra time comes from the world options, not from the caller: respawn timing belongs to the store,
    /// and a caller passing a duration is how two places end up disagreeing about it.
    /// </remarks>
    /// <param name="zoneGroup">SignalR zone group the location belongs to.</param>
    /// <param name="locationSlug">Zone-location slug that was cleared.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many dead enemies were pushed back.</returns>
    Task<int> ExtendClearedRespawnsAsync(
        string zoneGroup,
        string locationSlug,
        CancellationToken cancellationToken = default);
}
