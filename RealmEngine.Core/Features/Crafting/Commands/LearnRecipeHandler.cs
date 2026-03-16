using MediatR;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Commands;

/// <summary>
/// Handler for learning new crafting recipes.
/// </summary>
public class LearnRecipeHandler : IRequestHandler<LearnRecipeCommand, LearnRecipeResult>
{
    private readonly RecipeDataService _recipeCatalogLoader;

    /// <summary>Initializes the handler.</summary>
    public LearnRecipeHandler(RecipeDataService recipeCatalogLoader)
    {
        _recipeCatalogLoader = recipeCatalogLoader;
    }

    /// <summary>Handles the recipe learning request.</summary>
    public Task<LearnRecipeResult> Handle(LearnRecipeCommand request, CancellationToken cancellationToken)
    {
        // Validate recipe exists
        var recipe = _recipeCatalogLoader.GetRecipeById(request.RecipeId);
        if (recipe == null)
        {
            return Task.FromResult(new LearnRecipeResult
            {
                Success = false,
                Message = $"Recipe '{request.RecipeId}' not found."
            });
        }

        // Check if already learned
        if (request.Character.LearnedRecipes.Contains(recipe.Slug))
        {
            return Task.FromResult(new LearnRecipeResult
            {
                Success = false,
                Message = $"You already know how to craft {recipe.Name}.",
                RecipeName = recipe.Name
            });
        }

        // Check skill requirement (can't learn recipes too far above current skill)
        // Trainers can teach any recipe, but discovery/experimentation has limits
        if (request.Source != "Trainer" &&
            request.Character.Skills.TryGetValue(recipe.RequiredSkill!, out var characterSkill) && 
            recipe.RequiredSkillLevel > characterSkill.CurrentRank + 10)
        {
            return Task.FromResult(new LearnRecipeResult
            {
                Success = false,
                Message = $"Your {recipe.RequiredSkill} skill ({characterSkill!.CurrentRank}) is too low to learn this recipe (requires {recipe.RequiredSkillLevel}).",
                RecipeName = recipe.Name
            });
        }

        // Learn the recipe
        request.Character.LearnedRecipes.Add(recipe.Slug);

        return Task.FromResult(new LearnRecipeResult
        {
            Success = true,
            Message = $"You learned how to craft {recipe.Name}!",
            RecipeName = recipe.Name
        });
    }
}
