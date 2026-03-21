using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for world location catalog data.</summary>
public class EfCoreWorldLocationRepository(ContentDbContext db, ILogger<EfCoreWorldLocationRepository> logger)
    : IWorldLocationRepository
{
    /// <inheritdoc />
    public async Task<List<WorldLocationEntry>> GetAllAsync()
    {
        var entities = await db.WorldLocations.AsNoTracking()
            .Where(w => w.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} world locations from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<WorldLocationEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.WorldLocations.AsNoTracking()
            .Where(w => w.IsActive && w.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<WorldLocationEntry>> GetByLocationTypeAsync(string locationType)
    {
        var lower = locationType.ToLowerInvariant();
        var entities = await db.WorldLocations.AsNoTracking()
            .Where(w => w.IsActive && w.LocationType.ToLower() == lower)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static WorldLocationEntry MapToModel(Entities.WorldLocation w) =>
        new(w.Slug, w.DisplayName ?? w.Slug, w.TypeKey, w.LocationType, w.RarityWeight,
            w.Stats.MinLevel, w.Stats.MaxLevel);
}
