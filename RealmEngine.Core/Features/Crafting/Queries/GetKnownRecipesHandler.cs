using MediatR;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Queries;

/// <summary>
/// Handler for retrieving a character's known recipes.
/// </summary>
public class GetKnownRecipesHandler : IRequestHandler<GetKnownRecipesQuery, GetKnownRecipesResult>
{
    private readonly RecipeDataService _recipeCatalogLoader;
    private readonly CraftingService _craftingService;

    /// <summary>Initializes the handler.</summary>
    public GetKnownRecipesHandler(RecipeDataService recipeCatalogLoader, CraftingService craftingService)
    {
        _recipeCatalogLoader = recipeCatalogLoader;
        _craftingService = craftingService;
    }

    /// <summary>Handles the query for known recipes.</summary>
    public Task<GetKnownRecipesResult> Handle(GetKnownRecipesQuery request, CancellationToken cancellationToken)
    {
        // Get all recipes the character has learned
        var learnedRecipes = _recipeCatalogLoader.LoadAllRecipes()
            .Where(r => request.Character.LearnedRecipes.Contains(r.Slug))
            .ToList();

        // Add auto-unlock recipes (SkillLevel unlock method)
        var autoUnlockRecipes = _recipeCatalogLoader.LoadAllRecipes()
            .Where(r => r.UnlockMethod == RecipeUnlockMethod.SkillLevel)
            .Where(r => {
                return request.Character.Skills.TryGetValue(r.RequiredSkill!, out var characterSkill) && 
                       characterSkill.CurrentRank >= r.RequiredSkillLevel;
            })
            .ToList();

        // Combine and deduplicate
        var allKnownRecipes = learnedRecipes
            .Union(autoUnlockRecipes)
            .Distinct()
            .ToList();

        // Apply filters
        if (!string.IsNullOrEmpty(request.StationId))
        {
            allKnownRecipes = allKnownRecipes
                .Where(r => r.RequiredStation!.Equals(request.StationId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrEmpty(request.SkillName))
        {
            allKnownRecipes = allKnownRecipes
                .Where(r => r.RequiredSkill!.Equals(request.SkillName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Check crafting status for each recipe
        var recipeInfos = new List<RecipeInfo>();
        foreach (var recipe in allKnownRecipes)
        {
            var canCraft = _craftingService.CanCraftRecipe(request.Character, recipe, out _);
            var missingMaterials = new List<string>();

            // Check materials
            _craftingService.ValidateMaterials(request.Character, recipe, out var materialError);
            if (!string.IsNullOrEmpty(materialError))
            {
                // Parse missing materials from error message (simplified)
                foreach (var material in recipe.Materials)
                {
                    missingMaterials.Add($"{material.ItemReference} (need {material.Quantity})");
                }
            }

            // Check skill level
            var meetsSkillRequirement = request.Character.Skills.TryGetValue(recipe.RequiredSkill!, out var characterSkill) && 
                                         characterSkill.CurrentRank >= recipe.RequiredSkillLevel;

            var recipeInfo = new RecipeInfo
            {
                Recipe = recipe,
                CanCraft = canCraft,
                MissingMaterials = missingMaterials,
                MeetsSkillRequirement = meetsSkillRequirement
            };

            recipeInfos.Add(recipeInfo);
        }

        // Filter out uncraftable if requested
        if (!request.IncludeUncraftable)
        {
            recipeInfos = recipeInfos.Where(ri => ri.CanCraft).ToList();
        }

        return Task.FromResult(new GetKnownRecipesResult
        {
            Success = true,
            Message = $"Found {recipeInfos.Count} known recipes.",
            Recipes = recipeInfos
        });
    }
}
