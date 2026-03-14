using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryRecipeRepository : IRecipeRepository
{
    /// <inheritdoc />
    public Task<List<Recipe>> GetAllAsync() => Task.FromResult(new List<Recipe>());

    /// <inheritdoc />
    public Task<Recipe?> GetBySlugAsync(string slug) => Task.FromResult<Recipe?>(null);

    /// <inheritdoc />
    public Task<List<Recipe>> GetByCraftingSkillAsync(string craftingSkill) => Task.FromResult(new List<Recipe>());
}
