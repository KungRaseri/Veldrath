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
    Task<List<PlayerSession>> GetByZoneIdAsync(string zoneId);

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
}
