using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="ICharacterUnlockedLocationRepository"/>.</summary>
public class CharacterUnlockedLocationRepository(ApplicationDbContext db) : ICharacterUnlockedLocationRepository
{
    /// <inheritdoc />
    public async Task<HashSet<string>> GetUnlockedSlugsAsync(Guid characterId, CancellationToken ct = default)
    {
        var slugs = await db.CharacterUnlockedLocations
            .Where(u => u.CharacterId == characterId)
            .Select(u => u.LocationSlug)
            .ToListAsync(ct);

        return [.. slugs];
    }

    /// <inheritdoc />
    public Task<bool> IsUnlockedAsync(Guid characterId, string locationSlug, CancellationToken ct = default) =>
        db.CharacterUnlockedLocations
            .AnyAsync(u => u.CharacterId == characterId && u.LocationSlug == locationSlug, ct);

    /// <inheritdoc />
    public async Task AddUnlockAsync(Guid characterId, string locationSlug, string unlockSource, CancellationToken ct = default)
    {
        var alreadyExists = await db.CharacterUnlockedLocations
            .AnyAsync(u => u.CharacterId == characterId && u.LocationSlug == locationSlug, ct);

        if (alreadyExists) return;

        db.CharacterUnlockedLocations.Add(new CharacterUnlockedLocation
        {
            CharacterId  = characterId,
            LocationSlug = locationSlug,
            UnlockSource = unlockSource,
            UnlockedAt   = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
