namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Durable store for per-character combat sessions.
/// </summary>
/// <remarks>
/// Replaces the in-process <c>CombatSessionStore</c>. A session is keyed by character id, which the database
/// enforces as the primary key of <c>CombatSessions</c>, so "one fight per character" is a constraint rather
/// than a convention. Sessions survive a restart, which is what makes the reconnect grace window in D4 possible.
/// </remarks>
public interface ICombatSessionStore
{
    /// <summary>Returns the character's active session, or <see langword="null"/> when they are not fighting.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ActiveCombatSession?> TryGetAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>Returns whether the character is currently in a fight.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsInCombatAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces the character's session.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="session">The session state to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(Guid characterId, ActiveCombatSession session, CancellationToken cancellationToken = default);

    /// <summary>Ends the character's session, if any.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends every session targeting the given enemy and returns the released character ids, so each of them can
    /// be told the fight is over.
    /// </summary>
    /// <param name="enemyId">Enemy instance id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Guid>> RemoveByEnemyIdAsync(Guid enemyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes sessions whose owner disconnected longer than <paramref name="grace"/> ago, and returns the affected
    /// character ids.
    /// </summary>
    /// <param name="grace">How long a disconnected session is allowed to survive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Guid>> PruneExpiredAsync(TimeSpan grace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the character's fight as suspended because they dropped, starting the reconnect grace window. The
    /// session itself is kept so the same fight can be resumed.
    /// </summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a session existed and was marked.</returns>
    Task<bool> MarkDisconnectedAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>Clears the suspending mark when the owner reconnects, resuming the same fight.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a session existed and was resumed.</returns>
    Task<bool> MarkConnectedAsync(Guid characterId, CancellationToken cancellationToken = default);
}
