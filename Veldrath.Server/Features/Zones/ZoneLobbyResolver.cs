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

    /// <summary>The zone has authored locations in scope, but none of them is flagged <c>IsSafeRespawn</c>.</summary>
    NoSafeLocation,

    /// <summary>The zone has no authored locations in scope at all.</summary>
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
    /// Gets the location a caller may fall back to when <see cref="Lobby"/> cannot be used. On the shared entry point
    /// this is the first location the character can see; on the scoped overload it is the first eligible location of
    /// that scope, and for a run it is the preferred slug only. <see langword="null"/> when nothing in scope qualifies.
    /// </summary>
    public ZoneLocationEntry? Fallback { get; init; }
}

/// <summary>
/// Resolves a zone's lobby for one character: the location flagged <c>IsSafeRespawn</c>, read through the
/// character's unlocked hidden locations.
/// </summary>
/// <remarks>
/// A shared anchor is non-instanced, and a run's own lobby is the same pair of flags plus <c>IsInstanced</c>. The
/// shared entry point therefore excludes instanced rows — an ordinary respawn or a reconnect that took one would
/// place a character inside somebody's run — while the scoped overload names which of the two it is resolving.
/// </remarks>
public interface IZoneLobbyResolver
{
    /// <summary>
    /// Resolves the shared lobby of <paramref name="zoneId"/> as it stands for <paramref name="characterId"/>.
    /// </summary>
    /// <param name="zoneId">Zone whose lobby to resolve.</param>
    /// <param name="characterId">Character whose hidden-location unlocks are honoured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The lobby, or the reason there is none to use.</returns>
    Task<ZoneLobbyResolution> ResolveAsync(
        string zoneId,
        Guid characterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the lobby of <paramref name="zoneId"/> for one scope: the shared zone when
    /// <paramref name="instanced"/> is <see langword="false"/>, or a run's own lobby when it is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The scoped answer filters by whether the location is instanced and by whether the zone map actually places it,
    /// before choosing between candidates, so a caller that is about to move a character can never be handed an
    /// anchor with no node to stand on. Candidates are ordered by slug rather than by catalog order: the choice
    /// between two sibling anchors must not depend on which row was authored first.
    /// </remarks>
    /// <param name="zoneId">Zone whose lobby to resolve.</param>
    /// <param name="characterId">Character whose hidden-location unlocks are honoured.</param>
    /// <param name="instanced">Whether the scope being resolved is a run rather than the shared zone.</param>
    /// <param name="placedLocationSlugs">
    /// Slugs the zone map places on location nodes, compared case-insensitively; a location the map does not place is
    /// not a destination.
    /// </param>
    /// <param name="preferredFallbackSlug">
    /// Room to fall back to when the scope has no usable lobby. Only a run uses it — falling back to the room the
    /// character died in is the one degradation that keeps them inside their run.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scoped lobby, or the reason there is none to use, plus the eligible fallback if any.</returns>
    Task<ZoneLobbyResolution> ResolveAsync(
        string zoneId,
        Guid characterId,
        bool instanced,
        IReadOnlySet<string> placedLocationSlugs,
        string? preferredFallbackSlug = null,
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
    public Task<ZoneLobbyResolution> ResolveAsync(
        string zoneId,
        Guid characterId,
        CancellationToken cancellationToken = default) =>
        ResolveCoreAsync(
            zoneId,
            characterId,
            instanced: false,
            placedLocationSlugs: null,
            preferredFallbackSlug: null,
            cancellationToken);

    /// <inheritdoc />
    public Task<ZoneLobbyResolution> ResolveAsync(
        string zoneId,
        Guid characterId,
        bool instanced,
        IReadOnlySet<string> placedLocationSlugs,
        string? preferredFallbackSlug = null,
        CancellationToken cancellationToken = default) =>
        ResolveCoreAsync(
            zoneId,
            characterId,
            instanced,
            placedLocationSlugs,
            preferredFallbackSlug,
            cancellationToken);

    /// <summary>Resolves one scope's lobby, with <see langword="null"/> placement meaning "the shared entry point".</summary>
    /// <param name="zoneId">Zone whose lobby to resolve.</param>
    /// <param name="characterId">Character whose unlocks are honoured.</param>
    /// <param name="instanced">Whether the scope is a run.</param>
    /// <param name="placedLocationSlugs">Slugs the zone map places, or <see langword="null"/> to skip the placement filter.</param>
    /// <param name="preferredFallbackSlug">Run-only fallback slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolution for that scope.</returns>
    private async Task<ZoneLobbyResolution> ResolveCoreAsync(
        string zoneId,
        Guid characterId,
        bool instanced,
        IReadOnlySet<string>? placedLocationSlugs,
        string? preferredFallbackSlug,
        CancellationToken cancellationToken)
    {
        var unlocked = await _unlocked.GetUnlockedSlugsAsync(characterId, cancellationToken);
        var visible = InScope(
            await _locations.GetByZoneIdAsync(zoneId, unlocked), instanced, placedLocationSlugs);

        var lobby = visible.FirstOrDefault(location => location.IsSafeRespawn);
        if (lobby is not null)
            return new ZoneLobbyResolution { Status = ZoneLobbyStatus.Resolved, Lobby = lobby };

        var fallback = PickFallback(visible, instanced, placedLocationSlugs is null, preferredFallbackSlug);

        // No lobby among the locations this character can see. Before calling the zone lobby-less, check whether the
        // zone authored one behind a lock: a hidden safe location is a real lobby that is closed to this character,
        // and that is a different, sayable outcome from a zone that never had one. The check keeps the same scope
        // filter, so a hidden run lobby is not reported to a caller resolving the shared zone.
        var locked = InScope(await _locations.GetHiddenByZoneIdAsync(zoneId), instanced, placedLocationSlugs)
            .FirstOrDefault(location => location.IsSafeRespawn);
        if (locked is not null)
        {
            return new ZoneLobbyResolution
            {
                Status = ZoneLobbyStatus.NotUnlocked,
                LockedLobby = locked,
                Fallback = fallback,
            };
        }

        return new ZoneLobbyResolution
        {
            Status = visible.Count == 0 ? ZoneLobbyStatus.NoLocations : ZoneLobbyStatus.NoSafeLocation,
            Fallback = fallback,
        };
    }

    /// <summary>
    /// Keeps the locations of one scope, and on the scoped path only those the zone map places on a node, ordered by
    /// slug so the choice between two sibling anchors does not depend on authoring order.
    /// </summary>
    /// <param name="locations">Locations to filter.</param>
    /// <param name="instanced">Whether the scope is a run.</param>
    /// <param name="placedLocationSlugs">Slugs the zone map places, or <see langword="null"/> to skip that filter.</param>
    /// <returns>The eligible locations of that scope.</returns>
    private static List<ZoneLocationEntry> InScope(
        IEnumerable<ZoneLocationEntry> locations,
        bool instanced,
        IReadOnlySet<string>? placedLocationSlugs)
    {
        var scoped = locations.Where(location => location.IsInstanced == instanced);

        if (placedLocationSlugs is not null)
            scoped = scoped.Where(location => placedLocationSlugs.Contains(location.Slug));

        var eligible = scoped.ToList();

        return placedLocationSlugs is null
            ? eligible
            : eligible.OrderBy(location => location.Slug, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Chooses the fallback the caller may use when the scope's lobby cannot be.
    /// </summary>
    /// <remarks>
    /// The shared entry point keeps its historical rule — the first visible location — because reconnect and ordinary
    /// respawn have always used it. The scoped path never borrows a run's room for the shared zone, and a run never
    /// borrows the shared zone's anchor: inside a run the only acceptable degradation is the room the character fell
    /// in, which is the slug the caller passed as <paramref name="preferredFallbackSlug"/>.
    /// </remarks>
    /// <param name="visible">Eligible visible locations, already in scope and ordered.</param>
    /// <param name="instanced">Whether the scope is a run.</param>
    /// <param name="sharedEntryPoint">Whether this is the placement-unfiltered shared resolution.</param>
    /// <param name="preferredFallbackSlug">Run-only fallback slug.</param>
    /// <returns>The fallback, or <see langword="null"/> when nothing in scope qualifies.</returns>
    private static ZoneLocationEntry? PickFallback(
        List<ZoneLocationEntry> visible,
        bool instanced,
        bool sharedEntryPoint,
        string? preferredFallbackSlug)
    {
        if (sharedEntryPoint || !instanced)
            return visible.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(preferredFallbackSlug))
            return null;

        return visible.FirstOrDefault(location =>
            string.Equals(location.Slug, preferredFallbackSlug, StringComparison.OrdinalIgnoreCase));
    }
}
