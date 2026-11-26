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
    /// <see cref="GetOnSharedZoneAsync"/> in the shared zone.
    /// </remarks>
    Task<List<PlayerSession>> GetByZoneIdAsync(string zoneId);

    /// <summary>
    /// Returns the sessions sharing the caller's own scope of a zone: everyone standing in the zone who is not
    /// inside an instance run, and who is on the caller's side of the zone's difficulty split.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scope has two dimensions and this read applies both, because a roster must only name connections the
    /// caller can actually reach. A character in a run is in a different SignalR group, so <paramref name="zoneId"/>
    /// plus a cleared run pointer is the first dimension.
    /// </para>
    /// <para>
    /// The second dimension is difficulty. A wilderness zone shards its shared scope by difficulty — the group is
    /// <c>zone:{zoneId}_{difficultyMode}</c> — so a normal-difficulty character and a hardcore one in the same zone
    /// are in different groups and can neither see nor chat with each other. Every other zone type has one shared
    /// scope for all difficulties, and passes <see langword="null"/>. The pair is the same pair that composes the
    /// scope's group name (<see cref="Veldrath.Server.Features.Zones.ZoneGroupName.ForZone"/>); passing a different pair here than
    /// the one the connection joined names players the caller cannot reach.
    /// </para>
    /// </remarks>
    /// <param name="zoneId">Zone whose shared scope to read.</param>
    /// <param name="difficultyMode">
    /// The zone's difficulty suffix, or <see langword="null"/> when the zone's shared scope is not split by
    /// difficulty.
    /// </param>
    Task<List<PlayerSession>> GetOnSharedZoneAsync(string zoneId, string? difficultyMode);

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
