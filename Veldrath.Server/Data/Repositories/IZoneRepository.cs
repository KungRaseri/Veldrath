using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>Read-only repository for <see cref="Zone"/> catalog entries.</summary>
public interface IZoneRepository
{
    /// <summary>Returns all non-dev zones in the catalog.</summary>
    Task<List<Zone>> GetAllAsync();

    /// <summary>Returns the zone with the given <paramref name="zoneId"/>, or <see langword="null"/> if not found.</summary>
    Task<Zone?> GetByIdAsync(string zoneId);

    /// <summary>Returns all non-dev zones belonging to the given <paramref name="regionId"/>.</summary>
    Task<List<Zone>> GetByRegionIdAsync(string regionId);

    /// <summary>Returns all zones including dev-only sandbox zones. For internal/dev tooling use only.</summary>
    Task<List<Zone>> GetAllIncludingDevAsync();
}

/// <summary>Tracks active player sessions across the world (region maps and zones).</summary>
public interface IPlayerSessionRepository
{
    /// <summary>Returns all active sessions currently inside the specified zone.</summary>
    /// <remarks>
    /// This is the whole zone's population: it includes characters inside an instance run, which is what the
    /// public zone counts publish. Never use it for a roster the caller has to reach — a run is its own
    /// SignalR group, so use <see cref="GetByZoneInstanceAsync"/> inside a run and
    /// <see cref="GetOnSharedScopeAsync"/> in the shared zone.
    /// </remarks>
    Task<List<PlayerSession>> GetByZoneIdAsync(string zoneId);

    /// <summary>
    /// Returns the zone's shared-scope sessions — everyone standing in the zone who is not inside an instance
    /// run. Characters in a run are in a different SignalR group they can neither see nor chat with, so a
    /// shared-scope roster must not name them.
    /// </summary>
    /// <param name="zoneId">Zone whose shared scope to read.</param>
    Task<List<PlayerSession>> GetOnSharedScopeAsync(string zoneId);

    /// <summary>
    /// Returns the sessions pointing at one instance run — the run's own occupants, which excludes everyone
    /// else standing in the same zone inside a different run or in the shared scope.
    /// </summary>
    /// <param name="instanceId">Run id.</param>
    Task<List<PlayerSession>> GetByZoneInstanceAsync(Guid instanceId);

    /// <summary>Returns all active sessions within the specified region (zone or region map).</summary>
    Task<List<PlayerSession>> GetByRegionIdAsync(string regionId);

    /// <summary>Returns sessions in the specified region where the character is on the region map (not inside a zone).</summary>
    Task<List<PlayerSession>> GetOnRegionMapAsync(string regionId);

    /// <summary>Returns the session associated with <paramref name="connectionId"/>, or <see langword="null"/> if not found.</summary>
    Task<PlayerSession?> GetByConnectionIdAsync(string connectionId);

    /// <summary>Returns the active session for the given <paramref name="characterId"/>, or <see langword="null"/> if not found.</summary>
    Task<PlayerSession?> GetByCharacterIdAsync(Guid characterId);

    /// <summary>Returns the active session for the character with the given <paramref name="characterName"/>, or <see langword="null"/> if not found.</summary>
    Task<PlayerSession?> GetByCharacterNameAsync(string characterName);

    /// <summary>Persists a new player session.</summary>
    Task AddAsync(PlayerSession session);

    /// <summary>Removes the specified session.</summary>
    Task RemoveAsync(PlayerSession session);

    /// <summary>Removes the session associated with <paramref name="connectionId"/>, if one exists.</summary>
    Task RemoveByConnectionIdAsync(string connectionId);

    /// <summary>
    /// Updates the session's current map/node and optional zone/region context.
    /// Pass <see langword="null"/> for <paramref name="zoneId"/> when the character is on a region map.
    /// </summary>
    Task UpdateMapPositionAsync(
        Guid characterId,
        string mapId,
        string nodeId,
        string? zoneId,
        string? regionId);

    /// <summary>
    /// Records the instance run the character is inside, or clears it when they are in the shared zone.
    /// The run derives its occupants from this pointer, but the pointer does not outlive the connection: the
    /// session row leaves with it, so a reconnect re-derives the run from the still-open run instead of
    /// reading this value back.
    /// </summary>
    /// <param name="characterId">Character whose session to update.</param>
    /// <param name="instanceId">Run id, or <see langword="null"/> to leave the shared zone in place.</param>
    Task SetZoneInstanceAsync(Guid characterId, Guid? instanceId);
}
