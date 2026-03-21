using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for actor instance catalog data.</summary>
public class EfCoreActorInstanceRepository(ContentDbContext db, ILogger<EfCoreActorInstanceRepository> logger)
    : IActorInstanceRepository
{
    /// <inheritdoc />
    public async Task<List<ActorInstanceEntry>> GetAllAsync()
    {
        var entities = await db.ActorInstances.AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} actor instances from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<ActorInstanceEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.ActorInstances.AsNoTracking()
            .Where(a => a.IsActive && a.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<ActorInstanceEntry>> GetByTypeKeyAsync(string typeKey)
    {
        var entities = await db.ActorInstances.AsNoTracking()
            .Where(a => a.IsActive && a.TypeKey == typeKey)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static ActorInstanceEntry MapToModel(Entities.ActorInstance a) =>
        new(a.Slug, a.DisplayName ?? a.Slug, a.TypeKey, a.ArchetypeId,
            a.LevelOverride, a.FactionOverride, a.RarityWeight);
}
