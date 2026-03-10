using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

public interface IZoneRepository
{
    Task<List<Zone>> GetAllAsync();
    Task<Zone?> GetByIdAsync(string zoneId);
}

public interface IZoneSessionRepository
{
    Task<List<ZoneSession>> GetByZoneIdAsync(string zoneId);
    Task<ZoneSession?> GetByConnectionIdAsync(string connectionId);
    Task<ZoneSession?> GetByCharacterIdAsync(Guid characterId);
    Task AddAsync(ZoneSession session);
    Task RemoveAsync(ZoneSession session);
    Task RemoveByConnectionIdAsync(string connectionId);
}
