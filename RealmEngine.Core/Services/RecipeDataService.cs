using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

public class RecipeDataService
{
    private readonly IRecipeRepository _repository;
    private readonly ILogger<RecipeDataService> _logger;
    private readonly Dictionary<string, List<Recipe>> _cache = new();
    private List<Recipe>? _allRecipesCache;

    public RecipeDataService(IRecipeRepository repository, ILogger<RecipeDataService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

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

    public List<Recipe> LoadRecipesByCategory(string category)
    {
        if (_cache.ContainsKey(category))
            return _cache[category];

        var categoryRecipes = LoadAllRecipes().Where(r => r.Category == category).ToList();
        _cache[category] = categoryRecipes;
        return categoryRecipes;
    }

    public Recipe? GetRecipeById(string recipeId)
    {
        return LoadAllRecipes().FirstOrDefault(r => r.Id == recipeId || r.Slug == recipeId);
    }

    public List<Recipe> GetAvailableRecipes(int skillLevel, string? category = null)
    {
        var recipes = category != null
            ? LoadRecipesByCategory(category)
            : LoadAllRecipes();

        return recipes.Where(r => r.RequiredSkillLevel <= skillLevel).ToList();
    }

    public void ClearCache()
    {
        _cache.Clear();
        _allRecipesCache = null;
    }
}