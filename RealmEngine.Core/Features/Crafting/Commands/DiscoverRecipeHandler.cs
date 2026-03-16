using MediatR;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Commands;

/// <summary>
/// Handler for recipe discovery through experimentation.
/// </summary>
public class DiscoverRecipeHandler : IRequestHandler<DiscoverRecipeCommand, DiscoverRecipeResult>
{
    private readonly RecipeDataService _recipeCatalogLoader;
    private readonly Random _random = new();

    /// <summary>Initializes the handler.</summary>
    public DiscoverRecipeHandler(RecipeDataService recipeCatalogLoader)
    {
        _recipeCatalogLoader = recipeCatalogLoader;
    }

    /// <summary>Handles the recipe discovery request.</summary>
    public Task<DiscoverRecipeResult> Handle(DiscoverRecipeCommand request, CancellationToken cancellationToken)
    {
        // Get character's skill level
        if (!request.Character.Skills.TryGetValue(request.SkillName, out var characterSkill))
        {
            return Task.FromResult(new DiscoverRecipeResult
            {
                Success = false,
                Message = $"You don't have the {request.SkillName} skill."
            });
        }

        // Get discoverable recipes for this skill that the character doesn't know
        var discoverableRecipes = _recipeCatalogLoader.LoadAllRecipes()
            .Where(r => r.UnlockMethod == RecipeUnlockMethod.Discovery)
            .Where(r => r.RequiredSkill == request.SkillName)
            .Where(r => !request.Character.LearnedRecipes.Contains(r.Slug))
            .ToList();

        // Filter by station if specified
        if (!string.IsNullOrEmpty(request.StationId))
        {
            discoverableRecipes = discoverableRecipes
                .Where(r => r.RequiredStation.Equals(request.StationId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter to recipes within skill range (current level ± 5)
        discoverableRecipes = discoverableRecipes
            .Where(r => r.RequiredSkillLevel >= characterSkill.CurrentRank - 5)
            .Where(r => r.RequiredSkillLevel <= characterSkill.CurrentRank + 5)
            .ToList();

        if (!discoverableRecipes.Any())
        {
            // Award small XP for trying
            var noRecipeXp = 1;
            characterSkill.CurrentXP += noRecipeXp;
            
            return Task.FromResult(new DiscoverRecipeResult
            {
                Success = false,
                Message = "There are no new recipes to discover at your current skill level.",
                SkillXpGained = noRecipeXp
            });
        }

        // Calculate discovery chance based on skill level
        // Base 5% chance, +0.5% per skill level above recipe requirement
        var baseChance = 0.05;
        var roll = _random.NextDouble();

        // Try to discover a recipe
        foreach (var recipe in discoverableRecipes.OrderBy(r => r.RequiredSkillLevel))
        {
            var skillDifference = characterSkill.CurrentRank - recipe.RequiredSkillLevel;
            var discoveryChance = baseChance + (skillDifference * 0.005);
            discoveryChance = Math.Clamp(discoveryChance, 0.01, 0.25); // 1% to 25% max

            if (roll < discoveryChance)
            {
                // Discovery successful!
                request.Character.LearnedRecipes.Add(recipe.Slug);

                // Award XP (higher for harder recipes)
                var xpGained = Math.Max(5, recipe.ExperienceGained / 2);
                characterSkill.CurrentXP += xpGained;

                return Task.FromResult(new DiscoverRecipeResult
                {
                    Success = true,
                    Message = $"Eureka! You discovered how to craft {recipe.Name}!",
                    DiscoveredRecipe = recipe,
                    SkillXpGained = xpGained
                });
            }
        }

        // Failed to discover, but gain small XP for experimentation
        var failXp = 2;
        characterSkill.CurrentXP += failXp;

        return Task.FromResult(new DiscoverRecipeResult
        {
            Success = false,
            Message = "Your experimentation didn't yield a new recipe, but you learned something from the attempt.",
            SkillXpGained = failXp
        });
    }
}
