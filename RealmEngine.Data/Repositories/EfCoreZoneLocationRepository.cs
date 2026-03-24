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
    public async Task<List<ZoneLocationEntry>> GetByLocationTypeAsync(string locationType)
    {
        var lower = locationType.ToLowerInvariant();
        var entities = await db.ZoneLocations.AsNoTracking()
            .Where(w => w.IsActive && w.LocationType.ToLower() == lower)
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
    public async Task<List<ZoneLocationConnectionEntry>> GetConnectionsFromAsync(string locationSlug)
    {
        var entities = await db.ZoneLocationConnections.AsNoTracking()
            .Where(c => c.FromLocationSlug == locationSlug)
            .ToListAsync();

        return entities.Select(c => new ZoneLocationConnectionEntry(
            c.FromLocationSlug, c.ToLocationSlug, c.ToZoneId, c.ConnectionType, c.IsTraversable))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<ZoneLocationConnectionEntry>> GetAllConnectionsForZoneAsync(string zoneId)
    {
        var slugs = await db.ZoneLocations.AsNoTracking()
            .Where(l => l.IsActive && l.ZoneId == zoneId)
            .Select(l => l.Slug)
            .ToListAsync();

        var entities = await db.ZoneLocationConnections.AsNoTracking()
            .Where(c => slugs.Contains(c.FromLocationSlug))
            .ToListAsync();

        return entities.Select(c => new ZoneLocationConnectionEntry(
            c.FromLocationSlug, c.ToLocationSlug, c.ToZoneId, c.ConnectionType, c.IsTraversable))
            .ToList();
    }

    private static ZoneLocationEntry MapToModel(Entities.ZoneLocation w) =>
        new(w.Slug, w.DisplayName ?? w.Slug, w.TypeKey, w.ZoneId, w.LocationType, w.RarityWeight,
            w.Stats.MinLevel, w.Stats.MaxLevel,
            w.Traits.IsHidden ?? false, w.Traits.UnlockType, w.Traits.UnlockKey, w.Traits.DiscoverThreshold);
}
