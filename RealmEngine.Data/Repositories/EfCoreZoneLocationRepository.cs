using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for zone location catalog data.</summary>
public class EfCoreZoneLocationRepository(ContentDbContext db, ILogger<EfCoreZoneLocationRepository> logger)
    : IZoneLocationRepository
{
    /// <inheritdoc />
    public async Task<List<ZoneLocationEntry>> GetAllAsync()
    {
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} zone locations from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<ZoneLocationEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<ZoneLocationEntry>> GetByTypeKeyAsync(string typeKey)
    {
        var lower = typeKey.ToLowerInvariant();
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.TypeKey.ToLower() == lower)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId)
    {
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.ZoneId == zoneId && w.Traits.IsHidden != true)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId, IEnumerable<string> unlockedSlugs)
    {
        var unlocked = unlockedSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.ZoneId == zoneId
                        && (w.Traits.IsHidden != true || unlocked.Contains(w.Slug)))
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ZoneLocationEntry>> GetHiddenByZoneIdAsync(string zoneId)
    {
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.ZoneId == zoneId && w.Traits.IsHidden == true)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ZoneLocationEntry>> GetAllByZoneIdAsync(string zoneId)
    {
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.ZoneId == zoneId)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static ZoneLocationEntry MapToModel(Entities.ZoneLocation w) =>
        new(w.Slug, w.DisplayName ?? w.Slug, w.TypeKey, w.ZoneId, w.RarityWeight,
            w.Stats.MinLevel, w.Stats.MaxLevel,
            w.Traits.IsHidden ?? false, w.Traits.UnlockType, w.Traits.UnlockKey, w.Traits.DiscoverThreshold,
            w.ActorPool.Select(e => new ActorPoolEntry(e.ArchetypeSlug, e.Weight)).ToList());
}
