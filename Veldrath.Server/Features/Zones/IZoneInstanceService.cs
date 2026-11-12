using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// Owns the lifecycle of <see cref="ZoneInstance"/> runs: finding the one a character belongs in, creating
/// it when there is none, tracking participation, and clearing runs nobody is inside any more.
/// </summary>
public interface IZoneInstanceService
{
    /// <summary>
    /// Returns the open run the character belongs in for a zone, creating it when there is none.
    /// </summary>
    /// <remarks>
    /// A character in a party is keyed by the party; a character with no party gets a run of their own, so a
    /// solo delve is a real run and a retry after it empties is a new id. Two members entering at the same
    /// instant both end up in the same run: the partial unique index decides, and the loser adopts the winner.
    /// </remarks>
    /// <param name="zoneId">Zone being entered.</param>
    /// <param name="characterId">Character entering it.</param>
    /// <param name="difficultyMode">Optional difficulty suffix, carried for wilderness scopes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The open run, never <see langword="null"/>.</returns>
    Task<ZoneInstance> FindOrCreateAsync(
        string zoneId,
        Guid characterId,
        string? difficultyMode = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a run by id, or <see langword="null"/> when it has been purged.</summary>
    /// <param name="instanceId">Run id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ZoneInstance?> GetAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a character is inside a run, clearing an earlier departure if they came back.
    /// </summary>
    /// <param name="instanceId">Run id.</param>
    /// <param name="characterId">Character inside it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="false"/> when the run no longer exists.</returns>
    Task<bool> JoinAsync(Guid instanceId, Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a character has left a run. The run itself stays open: the party may still be inside, and
    /// backtracking to the lobby must not lose the attempt.
    /// </summary>
    /// <param name="instanceId">Run id.</param>
    /// <param name="characterId">Character leaving it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LeaveAsync(Guid instanceId, Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks unoccupied runs as empty once the grace window has passed, and deletes runs that were already
    /// empty before the window. Rides the existing respawn sweep rather than adding a timer.
    /// </summary>
    /// <param name="grace">How long a run survives with nobody inside it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many runs were deleted.</returns>
    Task<int> SweepAsync(TimeSpan grace, CancellationToken cancellationToken = default);
}
