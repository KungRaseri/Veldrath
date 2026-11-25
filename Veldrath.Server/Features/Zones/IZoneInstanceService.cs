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
    /// Returns the run a character would resume into at a location, without opening one.
    /// </summary>
    /// <remarks>
    /// A dropped connection takes its session row with it, so the pointer that row carried is gone by the time
    /// the character reconnects. The run itself survives — it stays open while anyone is still inside it and is
    /// keyed by the party (or by a solo character) — which is why the same lookup a late joiner uses can re-find
    /// it. A location that is not instanced has no run to resume, and a run that already emptied is deliberately
    /// not returned: it is waiting to be purged, so the character belongs in the shared zone instead of a group
    /// nothing else will join.
    /// </remarks>
    /// <param name="zoneId">Zone the character is resuming inside.</param>
    /// <param name="locationSlug">Location the character stood on, or <see langword="null"/> when they were not on one.</param>
    /// <param name="characterId">Character resuming.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The still-open run, or <see langword="null"/> when there is none to resume into.</returns>
    Task<ZoneInstance?> FindResumableAsync(
        string zoneId,
        string? locationSlug,
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a character is inside a run, clearing an earlier departure if they came back.
    /// </summary>
    /// <remarks>
    /// Only an <see cref="ZoneInstanceState.Open"/> run can be joined: an empty run has already spent its grace
    /// and is waiting for the sweep to delete it, so seating a character in it would put them in a group that is
    /// about to vanish.
    /// </remarks>
    /// <param name="instanceId">Run id.</param>
    /// <param name="characterId">Character inside it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="false"/> when the run no longer exists or is no longer open.</returns>
    Task<bool> JoinAsync(Guid instanceId, Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a character has left a run. The run itself stays open: the party may still be inside, and
    /// backtracking to the lobby must not lose the attempt.
    /// </summary>
    /// <remarks>
    /// The departure starts the run's emptiness grace, so a run that was held for hours is not swept on the
    /// first tick after its last member disconnects — a reconnecting player still gets the grace to resume.
    /// </remarks>
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

    /// <summary>Lists the rooms this run has already cleared.</summary>
    /// <param name="instanceId">Run id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleared room slugs, in the order they were cleared; empty when the run is unknown.</returns>
    Task<IReadOnlyList<string>> GetClearedLocationsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that every enemy in one room of a run has been defeated, and returns the credit that earned.
    /// </summary>
    /// <remarks>
    /// Clearing a room is a run fact; the first-clear is a character fact. Both are recorded here because the
    /// caller only knows "the last enemy died" and should not have to decide which is which. Clearing a room
    /// also grants the character the unlock of any hidden location gated on that room's slug — the authored
    /// <c>UnlockKey</c> is the link between "I beat the thing" and "the vault is now visible to me".
    /// </remarks>
    /// <param name="instanceId">Run id.</param>
    /// <param name="locationSlug">Room that was cleared.</param>
    /// <param name="characterId">Character whose kill cleared it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the clear earned, including the locations it unlocked.</returns>
    Task<LocationClearOutcome> RecordClearAsync(
        Guid instanceId,
        string locationSlug,
        Guid characterId,
        CancellationToken cancellationToken = default);
}

/// <summary>What clearing a room in a run earned.</summary>
/// <param name="Cleared">Whether the run now considers the room cleared.</param>
/// <param name="FirstClearOfRun">Whether this call is the one that cleared it.</param>
/// <param name="FirstClearForCharacter">Whether this is the first time this character has ever cleared it.</param>
/// <param name="UnlockedLocationSlugs">Hidden locations this clear made visible to the character.</param>
public record LocationClearOutcome(
    bool Cleared,
    bool FirstClearOfRun,
    bool FirstClearForCharacter,
    IReadOnlyList<string> UnlockedLocationSlugs);
