using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="ICharacterUnlockedConnectionRepository"/>.</summary>
public class CharacterUnlockedConnectionRepository(ApplicationDbContext db) : ICharacterUnlockedConnectionRepository
{
    /// <inheritdoc />
    public async Task<HashSet<int>> GetUnlockedIdsAsync(Guid characterId, CancellationToken ct = default)
    {
        var ids = await db.CharacterUnlockedConnections
            .Where(u => u.CharacterId == characterId)
            .Select(u => u.ConnectionId)
            .ToListAsync(ct);

        return [.. ids];
    }

    /// <inheritdoc />
    public Task<bool> IsUnlockedAsync(Guid characterId, int connectionId, CancellationToken ct = default) =>
        db.CharacterUnlockedConnections
            .AnyAsync(u => u.CharacterId == characterId && u.ConnectionId == connectionId, ct);

    /// <inheritdoc />
    public async Task AddUnlockAsync(Guid characterId, int connectionId, string unlockSource, CancellationToken ct = default)
    {
        var alreadyExists = await db.CharacterUnlockedConnections
            .AnyAsync(u => u.CharacterId == characterId && u.ConnectionId == connectionId, ct);

        if (alreadyExists) return;

        db.CharacterUnlockedConnections.Add(new CharacterUnlockedConnection
        {
            CharacterId  = characterId,
            ConnectionId = connectionId,
            UnlockSource = unlockSource,
            UnlockedAt   = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
