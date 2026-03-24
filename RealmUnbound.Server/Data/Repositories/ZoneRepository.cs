using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

public class ZoneRepository(ApplicationDbContext db) : IZoneRepository
{
    /// <inheritdoc/>
    public Task<List<Zone>> GetAllAsync() =>
        db.Zones.OrderBy(z => z.IsStarter ? 0 : 1).ThenBy(z => z.Name).ToListAsync();

    /// <inheritdoc/>
    public Task<Zone?> GetByIdAsync(string zoneId) =>
        db.Zones.FirstOrDefaultAsync(z => z.Id == zoneId);

    /// <inheritdoc/>
    public Task<List<Zone>> GetByRegionIdAsync(string regionId) =>
        db.Zones.Where(z => z.RegionId == regionId).OrderBy(z => z.MinLevel).ThenBy(z => z.Name).ToListAsync();

    /// <inheritdoc/>
    public Task<List<ZoneConnection>> GetConnectionsAsync(string zoneId) =>
        db.ZoneConnections.Where(c => c.FromZoneId == zoneId).ToListAsync();
}

public class ZoneSessionRepository(ApplicationDbContext db) : IZoneSessionRepository
{
    public Task<List<ZoneSession>> GetByZoneIdAsync(string zoneId) =>
        db.ZoneSessions.Where(s => s.ZoneId == zoneId).ToListAsync();

    public Task<ZoneSession?> GetByConnectionIdAsync(string connectionId) =>
        db.ZoneSessions.FirstOrDefaultAsync(s => s.ConnectionId == connectionId);

    public Task<ZoneSession?> GetByCharacterIdAsync(Guid characterId) =>
        db.ZoneSessions.FirstOrDefaultAsync(s => s.CharacterId == characterId);

    public async Task AddAsync(ZoneSession session)
    {
        db.ZoneSessions.Add(session);
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(ZoneSession session)
    {
        db.ZoneSessions.Remove(session);
        await db.SaveChangesAsync();
    }

    public async Task RemoveByConnectionIdAsync(string connectionId)
    {
        var session = await GetByConnectionIdAsync(connectionId);
        if (session is not null)
        {
            db.ZoneSessions.Remove(session);
            await db.SaveChangesAsync();
        }
    }
}
