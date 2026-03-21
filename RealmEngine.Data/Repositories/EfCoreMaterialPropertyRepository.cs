using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for material property catalog data.</summary>
public class EfCoreMaterialPropertyRepository(ContentDbContext db, ILogger<EfCoreMaterialPropertyRepository> logger)
    : IMaterialPropertyRepository
{
    /// <inheritdoc />
    public async Task<List<MaterialPropertyEntry>> GetAllAsync()
    {
        var entities = await db.MaterialProperties.AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} material properties from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<MaterialPropertyEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.MaterialProperties.AsNoTracking()
            .Where(m => m.IsActive && m.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<MaterialPropertyEntry>> GetByFamilyAsync(string family)
    {
        var lower = family.ToLowerInvariant();
        var entities = await db.MaterialProperties.AsNoTracking()
            .Where(m => m.IsActive && m.MaterialFamily.ToLower() == lower)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static MaterialPropertyEntry MapToModel(Entities.MaterialProperty m) =>
        new(m.Slug, m.DisplayName ?? m.Slug, m.TypeKey, m.MaterialFamily, m.CostScale, m.RarityWeight);
}
