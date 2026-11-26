using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// Whether a zone's lobby — the location flagged <c>IsSafeRespawn</c> — can be used for a character, and
/// when it cannot, why. Every placement that means "put them back in the zone" reads the same answer,
/// so the reconnect and the respawn cannot disagree about where a zone's lobby is.
/// </summary>
public enum ZoneLobbyStatus
{
    /// <summary>The zone's safe location resolved and the character may be placed there.</summary>
    Resolved,

    /// <summary>
    /// The zone has a safe location, but it is hidden and this character has not unlocked it — the case the
    /// hidden-filtering zone lookup used to miss silently.
    /// </summary>
    NotUnlocked,

    /// <summary>The zone has authored locations, but none of them is flagged <c>IsSafeRespawn</c>.</summary>
    NoSafeLocation,

    /// <summary>The zone has no authored locations at all.</summary>
    NoLocations,
}

/// <summary>What <see cref="ZoneLobbyResolver"/> found for one character in one zone.</summary>
public record ZoneLobbyResolution
{
    /// <summary>Gets whether the lobby resolved, and when it did not, why.</summary>
    public ZoneLobbyStatus Status { get; init; }

    /// <summary>Gets the location the character may wake at, when <see cref="Status"/> is <see cref="ZoneLobbyStatus.Resolved"/>.</summary>
    public ZoneLocationEntry? Lobby { get; init; }

    /// <summary>
    /// Gets the safe location the character has not unlocked, when <see cref="Status"/> is
    /// <see cref="ZoneLobbyStatus.NotUnlocked"/>. A hidden location is not missing — it is closed to this character,
    /// and a caller that keeps a fallback position can say so instead of looking like the zone has no safe place.
    /// </summary>
    public ZoneLocationEntry? LockedLobby { get; init; }

    /// <summary>
    /// Gets the first location the character can see, which is the entrance fallback when the lobby cannot be used.
    /// <see langword="null"/> when the zone shows this character no locations at all.
    /// </summary>
    public ZoneLocationEntry? Fallback { get; init; }
}

/// <summary>
/// Resolves a zone's lobby for one character: the location flagged <c>IsSafeRespawn</c>, read through the
/// character's unlocked hidden locations.
/// </summary>
public interface IZoneLobbyResolver
{
    /// <summary>
    /// Resolves the lobby of <paramref name="zoneId"/> as it stands for <paramref name="characterId"/>.
    /// </summary>
    /// <param name="zoneId">Zone whose lobby to resolve.</param>
    /// <param name="characterId">Character whose hidden-location unlocks are honoured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lobby, or the reason there is none to use.</returns>
    Task<ZoneLobbyResolution> ResolveAsync(
        string zoneId,
        Guid characterId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IZoneLobbyResolver"/>. The zone lookup filters hidden locations unless the character has
/// unlocked them, so asking it with the character's unlocks is what makes a hidden lobby reachable; a lobby that is
/// still hidden afterwards is reported as <see cref="ZoneLobbyStatus.NotUnlocked"/> rather than as an absent one.
/// </summary>
public class ZoneLobbyResolver : IZoneLobbyResolver
{
    private readonly IZoneLocationRepository _locations;
    private readonly ICharacterUnlockedLocationRepository _unlocked;

    /// <summary>Initializes a new instance of <see cref="ZoneLobbyResolver"/>.</summary>
    /// <param name="locations">Zone location catalog.</param>
    /// <param name="unlocked">Per-character hidden-location unlocks.</param>
    public ZoneLobbyResolver(
        IZoneLocationRepository locations,
        ICharacterUnlockedLocationRepository unlocked)
    {
        _locations = locations;
        _unlocked = unlocked;
    }

    /// <inheritdoc />
    public async Task<ZoneLobbyResolution> ResolveAsync(
        string zoneId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var unlocked = await _unlocked.GetUnlockedSlugsAsync(characterId, cancellationToken);
        var visible = await _locations.GetByZoneIdAsync(zoneId, unlocked);

        var lobby = visible.FirstOrDefault(location => location.IsSafeRespawn);
        if (lobby is not null)
            return new ZoneLobbyResolution { Status = ZoneLobbyStatus.Resolved, Lobby = lobby };

        // No lobby among the locations this character can see. Before calling the zone lobby-less, check whether the
        // zone authored one behind a lock: a hidden safe location is a real lobby that is closed to this character,
        // and that is a different, sayable outcome from a zone that never had one.
        var locked = (await _locations.GetHiddenByZoneIdAsync(zoneId))
            .FirstOrDefault(location => location.IsSafeRespawn);
        if (locked is not null)
        {
            return new ZoneLobbyResolution
            {
                Status = ZoneLobbyStatus.NotUnlocked,
                LockedLobby = locked,
                Fallback = visible.FirstOrDefault(),
            };
        }

        return new ZoneLobbyResolution
        {
            Status = visible.Count == 0 ? ZoneLobbyStatus.NoLocations : ZoneLobbyStatus.NoSafeLocation,
            Fallback = visible.FirstOrDefault(),
        };
    }
}
