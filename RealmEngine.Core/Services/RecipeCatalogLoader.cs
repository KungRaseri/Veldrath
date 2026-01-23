using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;
using Serilog;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for loading crafting recipe catalogs from JSON files.
/// Reads from recipes/catalog.json with unified recipe structure.
/// </summary>
public class RecipeCatalogLoader
{
    private readonly GameDataCache _dataCache;
    private readonly Dictionary<string, List<Recipe>> _cache = new();
    private List<Recipe>? _allRecipesCache;

    /// <summary>
    /// Initializes a new instance of the RecipeCatalogLoader class.
    /// </summary>
    /// <param name="dataCache">The game data cache service.</param>
    public RecipeCatalogLoader(GameDataCache dataCache)
    {
        _dataCache = dataCache;
    }

    /// <summary>
    /// Load all recipes from the unified catalog.
    /// </summary>
    /// <returns>List of all recipes across all categories.</returns>
    public List<Recipe> LoadAllRecipes()
    {
        if (_allRecipesCache != null)
        {
            return _allRecipesCache;
        }

        var recipesPath = "recipes/catalog.json";
        var recipesFile = _dataCache.GetFile(recipesPath);
        
        if (recipesFile == null)
        {
            Log.Warning("Recipe catalog not found: {RecipesPath}", recipesPath);
            return new List<Recipe>();
        }

        try
        {
            var catalog = recipesFile.JsonData;
            var allRecipes = new List<Recipe>();

            var recipeTypes = catalog["recipe_types"] as JObject;
            if (recipeTypes == null)
            {
                Log.Warning("Missing 'recipe_types' in recipe catalog");
                return new List<Recipe>();
            }

            // Iterate through each recipe type (weapons, consumables, etc.)
            foreach (var typeProperty in recipeTypes.Properties())
            {
                var categoryName = typeProperty.Name;
                var typeData = typeProperty.Value as JObject;
                var items = typeData?["items"] as JArray;

                if (items == null) continue;

                foreach (var recipeToken in items)
                {
                    var recipe = ParseRecipe(recipeToken, categoryName);
                    if (recipe != null)
                    {
                        allRecipes.Add(recipe);
                    }
                }
            }

            _allRecipesCache = allRecipes;
            Log.Information("Loaded {Count} recipes from catalog", allRecipes.Count);
            return allRecipes;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading recipe catalog");
            return new List<Recipe>();
        }
    }

    /// <summary>
    /// Load recipes from a specific category.
    /// </summary>
    /// <param name="category">The recipe category (e.g., "blacksmithing_weapons", "alchemy_potions").</param>
    /// <returns>List of recipes in the specified category.</returns>
    public List<Recipe> LoadRecipesByCategory(string category)
    {
        if (_cache.ContainsKey(category))
        {
            return _cache[category];
        }

        var allRecipes = LoadAllRecipes();
        var categoryRecipes = allRecipes.Where(r => r.Category == category).ToList();
        
        _cache[category] = categoryRecipes;
        return categoryRecipes;
    }

    /// <summary>
    /// Get a specific recipe by its ID.
    /// </summary>
    /// <param name="recipeId">The unique recipe ID.</param>
    /// <returns>The recipe if found, otherwise null.</returns>
    public Recipe? GetRecipeById(string recipeId)
    {
        var allRecipes = LoadAllRecipes();
        return allRecipes.FirstOrDefault(r => r.Id == recipeId || r.Slug == recipeId);
    }

    /// <summary>
    /// Get all recipes that a character can craft based on skill level.
    /// </summary>
    /// <param name="skillLevel">The character's crafting skill level.</param>
    /// <param name="category">Optional category filter.</param>
    /// <returns>List of craftable recipes.</returns>
    public List<Recipe> GetAvailableRecipes(int skillLevel, string? category = null)
    {
        var recipes = category != null 
            ? LoadRecipesByCategory(category) 
            : LoadAllRecipes();

        return recipes.Where(r => r.RequiredSkillLevel <= skillLevel).ToList();
    }

    /// <summary>
    /// Parse a recipe from JSON token.
    /// </summary>
    private Recipe? ParseRecipe(JToken recipeToken, string category)
    {
        try
        {
            var slug = recipeToken["slug"]?.ToString();
            var name = recipeToken["name"]?.ToString();
            
            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(name))
            {
                Log.Warning("Recipe missing slug or name in category {Category}", category);
                return null;
            }

            var recipe = new Recipe
            {
                Id = slug,
                Name = name,
                Description = recipeToken["description"]?.ToString() ?? string.Empty,
                Category = category,
                Slug = recipeToken["slug"]?.ToString() ?? string.Empty,
                RequiredSkill = recipeToken["requiredSkill"]?.ToString(),
                RequiredSkillLevel = recipeToken["requiredSkillLevel"]?.Value<int>() ?? 0,
                RequiredStation = recipeToken["requiredStation"]?.ToString() ?? string.Empty,
                RequiredStationTier = recipeToken["requiredStationTier"]?.Value<int>() ?? 1,
                CraftingTime = recipeToken["craftingTime"]?.Value<int>() ?? 0,
                ExperienceGained = recipeToken["experienceGained"]?.Value<int>() ?? 0,
                RarityWeight = recipeToken["rarityWeight"]?.Value<int>() ?? 100,
                UnlockMethod = ParseUnlockMethod(recipeToken["unlockMethod"]?.ToString())
            };

            // Parse produced item
            var producedItem = recipeToken["producedItem"] as JObject;
            if (producedItem != null)
            {
                recipe.OutputItemReference = producedItem["itemReference"]?.ToString() ?? string.Empty;
                recipe.OutputQuantity = producedItem["quantity"]?.Value<int>() ?? 1;
                recipe.MinQuality = ParseRarity(producedItem["minQuality"]?.ToString());
                recipe.MaxQuality = ParseRarity(producedItem["maxQuality"]?.ToString());
            }

            // Parse components (materials)
            var components = recipeToken["components"] as JArray;
            if (components != null)
            {
                foreach (var component in components)
                {
                    var material = new RecipeMaterial
                    {
                        ItemReference = component["itemReference"]?.ToString() ?? string.Empty,
                        Quantity = component["quantity"]?.Value<int>() ?? 1
                    };
                    recipe.Materials.Add(material);
                }
            }

            // Parse optional catalyst (stored as JSON string for now)
            var catalyst = recipeToken["optionalCatalyst"] as JObject;
            if (catalyst != null)
            {
                var catalystInfo = new
                {
                    itemReference = catalyst["itemReference"]?.ToString() ?? string.Empty,
                    effect = catalyst["effect"]?.ToString() ?? string.Empty
                };
                recipe.UnlockRequirement = $"Catalyst: {catalystInfo.itemReference} - {catalystInfo.effect}";
            }

            return recipe;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing recipe in category {Category}", category);
            return null;
        }
    }

    /// <summary>
    /// Parse unlock method from string.
    /// </summary>
    private RecipeUnlockMethod ParseUnlockMethod(string? unlockMethod)
    {
        if (string.IsNullOrEmpty(unlockMethod))
            return RecipeUnlockMethod.Default;

        return unlockMethod switch
        {
            "SkillLevel" => RecipeUnlockMethod.SkillLevel,
            "Trainer" => RecipeUnlockMethod.Trainer,
            "Discovery" => RecipeUnlockMethod.Discovery,
            "QuestReward" => RecipeUnlockMethod.QuestReward,
            "Achievement" => RecipeUnlockMethod.Achievement,
            _ => RecipeUnlockMethod.Default
        };
    }

    /// <summary>
    /// Parse rarity from string.
    /// </summary>
    private ItemRarity ParseRarity(string? rarity)
    {
        if (string.IsNullOrEmpty(rarity))
            return ItemRarity.Common;

        return rarity switch
        {
            "Common" => ItemRarity.Common,
            "Uncommon" => ItemRarity.Uncommon,
            "Rare" => ItemRarity.Rare,
            "Epic" => ItemRarity.Epic,
            "Legendary" => ItemRarity.Legendary,
            _ => ItemRarity.Common
        };
    }

    /// <summary>
    /// Clear the internal cache.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _allRecipesCache = null;
    }
}
