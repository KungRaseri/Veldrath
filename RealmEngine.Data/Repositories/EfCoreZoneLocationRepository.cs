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
            .Where(w => w.IsActive && w.ZoneId == zoneId)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static ZoneLocationEntry MapToModel(Entities.ZoneLocation w) =>
        new(w.Slug, w.DisplayName ?? w.Slug, w.TypeKey, w.ZoneId, w.LocationType, w.RarityWeight,
            w.Stats.MinLevel, w.Stats.MaxLevel);
}
