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
}

public class PlayerSessionRepository(ApplicationDbContext db) : IPlayerSessionRepository
{
    public Task<List<PlayerSession>> GetByZoneIdAsync(string zoneId) =>
        db.PlayerSessions.Where(s => s.ZoneId == zoneId).ToListAsync();

    public Task<List<PlayerSession>> GetByRegionIdAsync(string regionId) =>
        db.PlayerSessions.Where(s => s.RegionId == regionId).ToListAsync();

    public Task<List<PlayerSession>> GetOnRegionMapAsync(string regionId) =>
        db.PlayerSessions.Where(s => s.RegionId == regionId && s.ZoneId == null).ToListAsync();

    public Task<PlayerSession?> GetByConnectionIdAsync(string connectionId) =>
        db.PlayerSessions.FirstOrDefaultAsync(s => s.ConnectionId == connectionId);

    public Task<PlayerSession?> GetByCharacterIdAsync(Guid characterId) =>
        db.PlayerSessions.FirstOrDefaultAsync(s => s.CharacterId == characterId);

    public Task<PlayerSession?> GetByCharacterNameAsync(string characterName) =>
        db.PlayerSessions.FirstOrDefaultAsync(s => s.CharacterName == characterName);

    public async Task AddAsync(PlayerSession session)
    {
        db.PlayerSessions.Add(session);
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(PlayerSession session)
    {
        db.PlayerSessions.Remove(session);
        await db.SaveChangesAsync();
    }

    public async Task RemoveByConnectionIdAsync(string connectionId)
    {
        var session = await GetByConnectionIdAsync(connectionId);
        if (session is not null)
        {
            db.PlayerSessions.Remove(session);
            await db.SaveChangesAsync();
        }
    }

    public async Task UpdateLastMovedAtAsync(Guid characterId, DateTimeOffset time)
    {
        var session = await GetByCharacterIdAsync(characterId);
        if (session is null) return;
        session.LastMovedAt = time;
        await db.SaveChangesAsync();
    }

    public async Task UpdatePositionAsync(Guid characterId, int tileX, int tileY)
    {
        var session = await GetByCharacterIdAsync(characterId);
        if (session is null) return;
        session.TileX = tileX;
        session.TileY = tileY;
        await db.SaveChangesAsync();
    }

    public async Task SetZoneAsync(Guid characterId, string? zoneId)
    {
        var session = await GetByCharacterIdAsync(characterId);
        if (session is null) return;
        session.ZoneId = zoneId;
        await db.SaveChangesAsync();
    }

    public async Task SetRegionAsync(Guid characterId, string regionId)
    {
        var session = await GetByCharacterIdAsync(characterId);
        if (session is null) return;
        session.RegionId = regionId;
        session.ZoneId   = null;
        await db.SaveChangesAsync();
    }
}
