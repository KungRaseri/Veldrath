using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Read-only repository for <see cref="Zone"/> catalog entries.</summary>
public interface IZoneRepository
{
    /// <summary>Returns all zones in the catalog.</summary>
    Task<List<Zone>> GetAllAsync();

    /// <summary>Returns the zone with the given <paramref name="zoneId"/>, or <see langword="null"/> if not found.</summary>
    Task<Zone?> GetByIdAsync(string zoneId);
}

/// <summary>Tracks active player connections within a zone.</summary>
public interface IZoneSessionRepository
{
    /// <summary>Returns all active sessions in the specified zone.</summary>
    Task<List<ZoneSession>> GetByZoneIdAsync(string zoneId);

    /// <summary>Returns the session associated with <paramref name="connectionId"/>, or <see langword="null"/> if not found.</summary>
    Task<ZoneSession?> GetByConnectionIdAsync(string connectionId);

    /// <summary>Returns the active session for the given <paramref name="characterId"/>, or <see langword="null"/> if not found.</summary>
    Task<ZoneSession?> GetByCharacterIdAsync(Guid characterId);

    /// <summary>Persists a new zone session.</summary>
    Task AddAsync(ZoneSession session);

    /// <summary>Removes the specified session.</summary>
    Task RemoveAsync(ZoneSession session);

    /// <summary>Removes the session associated with <paramref name="connectionId"/>, if one exists.</summary>
    Task RemoveByConnectionIdAsync(string connectionId);
}
