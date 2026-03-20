using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for playable species, reading from <see cref="ContentDbContext"/>.
/// </summary>
public class EfCoreSpeciesRepository(ContentDbContext db, ILogger<EfCoreSpeciesRepository> logger)
    : ISpeciesRepository
{
    /// <inheritdoc />
    public async Task<List<Species>> GetAllSpeciesAsync()
    {
        var entities = await db.Species
            .AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} species from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Species?> GetSpeciesBySlugAsync(string slug)
    {
        var entity = await db.Species
            .AsNoTracking()
            .Where(s => s.IsActive && s.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Species>> GetSpeciesByTypeAsync(string typeKey)
    {
        var entities = await db.Species
            .AsNoTracking()
            .Where(s => s.IsActive && s.TypeKey == typeKey.ToLowerInvariant())
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Species MapToModel(Entities.Species entity) => new()
    {
        Slug         = entity.Slug,
        DisplayName  = entity.DisplayName ?? entity.Slug,
        TypeKey      = entity.TypeKey,
        RarityWeight = entity.RarityWeight,
    };
}
