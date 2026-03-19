using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>Service for loading and caching recipes from the data repository.</summary>
public class RecipeDataService
{
    private readonly IRecipeRepository _repository;
    private readonly ILogger<RecipeDataService> _logger;
    private readonly Dictionary<string, List<Recipe>> _cache = new();
    private List<Recipe>? _allRecipesCache;

    /// <summary>Initializes a new instance of <see cref="RecipeDataService"/>.</summary>
    /// <param name="repository">Repository used to load recipe data.</param>
    /// <param name="logger">Logger instance.</param>
    public RecipeDataService(IRecipeRepository repository, ILogger<RecipeDataService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Loads and caches all recipes from the repository.</summary>
    /// <returns>The full list of recipes, or an empty list on failure.</returns>
    public List<Recipe> LoadAllRecipes()
    {
        if (_allRecipesCache != null)
            return _allRecipesCache;

        try
        {
            _allRecipesCache = _repository.GetAllAsync().GetAwaiter().GetResult();
            _logger.LogInformation("Loaded {Count} recipes from repository", _allRecipesCache.Count);
            return _allRecipesCache;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recipes from repository");
            return [];
        }
    }

    /// <summary>Loads and caches all recipes belonging to the specified category.</summary>
    /// <param name="category">The category slug to filter by.</param>
    /// <returns>Recipes in the given category.</returns>
    public List<Recipe> LoadRecipesByCategory(string category)
    {
        if (_cache.ContainsKey(category))
            return _cache[category];

        var categoryRecipes = LoadAllRecipes().Where(r => r.Category == category).ToList();
        _cache[category] = categoryRecipes;
        return categoryRecipes;
    }

    /// <summary>Finds a single recipe by its identifier or slug.</summary>
    /// <param name="recipeId">The recipe ID or slug to look up.</param>
    /// <returns>The matching recipe, or <see langword="null"/> if not found.</returns>
    public Recipe? GetRecipeById(string recipeId)
    {
        return LoadAllRecipes().FirstOrDefault(r => r.Id == recipeId || r.Slug == recipeId);
    }

    /// <summary>Returns all recipes whose required skill level does not exceed the given value.</summary>
    /// <param name="skillLevel">The player's current skill level.</param>
    /// <param name="category">Optional category filter; uses all recipes when <see langword="null"/>.</param>
    /// <returns>Recipes available at the given skill level.</returns>
    public List<Recipe> GetAvailableRecipes(int skillLevel, string? category = null)
    {
        var recipes = category != null
            ? LoadRecipesByCategory(category)
            : LoadAllRecipes();

        return recipes.Where(r => r.RequiredSkillLevel <= skillLevel).ToList();
    }

    /// <summary>Clears all cached recipe data, forcing a fresh load on the next request.</summary>
    public void ClearCache()
    {
        _cache.Clear();
        _allRecipesCache = null;
    }
}