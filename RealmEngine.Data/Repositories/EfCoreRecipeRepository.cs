using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for crafting recipe catalog data.</summary>
public class EfCoreRecipeRepository(ContentDbContext db, ILogger<EfCoreRecipeRepository> logger)
    : IRecipeRepository
{
    /// <inheritdoc />
    public async Task<List<Recipe>> GetAllAsync()
    {
        var entities = await db.Recipes.AsNoTracking()
            .Where(r => r.IsActive)
            .Include(r => r.Ingredients)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} recipes from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Recipe?> GetBySlugAsync(string slug)
    {
        var entity = await db.Recipes.AsNoTracking()
            .Where(r => r.IsActive && r.Slug == slug)
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Recipe>> GetByCraftingSkillAsync(string craftingSkill)
    {
        var entities = await db.Recipes.AsNoTracking()
            .Where(r => r.IsActive && r.CraftingSkill == craftingSkill)
            .Include(r => r.Ingredients)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Recipe MapToModel(Entities.Recipe e) => new()
    {
        Id                  = e.Slug,
        Name                = e.DisplayName ?? e.Slug,
        Slug                = e.Slug,
        Category            = e.TypeKey,
        RequiredSkill       = e.CraftingSkill,
        RequiredSkillLevel  = e.CraftingLevel,
        OutputItemReference = $"@{e.OutputItemDomain}:{e.OutputItemSlug}",
        OutputQuantity      = e.OutputQuantity,
        Materials           = e.Ingredients.Select(i => new RecipeMaterial
        {
            ItemReference = $"@{i.ItemDomain}:{i.ItemSlug}",
            Quantity      = i.Quantity,
        }).ToList(),
    };
}
