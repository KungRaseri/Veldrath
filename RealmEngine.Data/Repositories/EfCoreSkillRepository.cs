using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for skill catalog data.</summary>
public class EfCoreSkillRepository(ContentDbContext db, ILogger<EfCoreSkillRepository> logger)
    : ISkillRepository
{
    /// <inheritdoc />
    public async Task<List<SkillDefinition>> GetAllAsync()
    {
        var entities = await db.Skills.AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} skills from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<SkillDefinition?> GetBySlugAsync(string slug)
    {
        var entity = await db.Skills.AsNoTracking()
            .Where(s => s.IsActive && s.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<SkillDefinition>> GetByCategoryAsync(string category)
    {
        var entities = await db.Skills.AsNoTracking()
            .Where(s => s.IsActive && s.TypeKey == category)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static SkillDefinition MapToModel(Entities.Skill e) => new()
    {
        SkillId          = e.Slug,
        Name             = e.Slug,
        DisplayName      = e.DisplayName ?? e.Slug,
        Description      = string.Empty,
        Category         = e.TypeKey,
        BaseXPCost       = e.Stats.XpPerRank ?? 100,
        CostMultiplier   = 0.5,
        MaxRank          = e.MaxRank,
        GoverningAttribute = e.Traits.Combat == true      ? "strength"     :
                             e.Traits.Crafting == true    ? "intelligence" :
                             e.Traits.Social == true      ? "charisma"     :
                             e.Traits.Exploration == true ? "dexterity"    :
                             "none",
    };
}
