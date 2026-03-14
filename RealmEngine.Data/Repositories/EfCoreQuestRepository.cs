using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for quest catalog data.</summary>
public class EfCoreQuestRepository(ContentDbContext db, ILogger<EfCoreQuestRepository> logger)
    : IQuestRepository
{
    /// <inheritdoc />
    public async Task<List<Quest>> GetAllAsync()
    {
        var entities = await db.Quests.AsNoTracking()
            .Where(q => q.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} quests from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Quest?> GetBySlugAsync(string slug)
    {
        var entity = await db.Quests.AsNoTracking()
            .Where(q => q.IsActive && q.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Quest>> GetByTypeKeyAsync(string typeKey)
    {
        var entities = await db.Quests.AsNoTracking()
            .Where(q => q.IsActive && q.TypeKey == typeKey)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Quest MapToModel(Entities.Quest e) => new()
    {
        Id           = e.Slug,
        Slug         = e.Slug,
        Title        = e.DisplayName ?? e.Slug,
        DisplayName  = e.DisplayName ?? e.Slug,
        RarityWeight = e.RarityWeight,
        Type         = e.Traits.MainStory == true ? "main" : "side",
    };
}
