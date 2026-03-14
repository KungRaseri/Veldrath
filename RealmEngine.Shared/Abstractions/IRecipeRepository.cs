using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading crafting recipe data.</summary>
public interface IRecipeRepository
{
    /// <summary>Returns all active recipes.</summary>
    Task<List<Recipe>> GetAllAsync();

    /// <summary>Returns a single recipe by slug.</summary>
    Task<Recipe?> GetBySlugAsync(string slug);

    /// <summary>Returns all recipes that require a given crafting skill (e.g. "blacksmithing").</summary>
    Task<List<Recipe>> GetByCraftingSkillAsync(string craftingSkill);
}
