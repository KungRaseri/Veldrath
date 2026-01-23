using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core.Features.Crafting.Commands;
using RealmEngine.Core.Features.Crafting.Queries;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Crafting;

/// <summary>
/// Integration tests for recipe learning and discovery systems.
/// </summary>
public class RecipeLearningIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly RecipeCatalogLoader _recipeCatalogLoader;

    public RecipeLearningIntegrationTests()
    {
        var services = new ServiceCollection();
        
        // Add MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CraftRecipeHandler>());
        
        // Add services with correct data path
        var testDataPath = Path.Combine("..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        var dataCache = new GameDataCache(testDataPath);
        services.AddSingleton(dataCache);
        services.AddSingleton<RecipeCatalogLoader>(sp => new RecipeCatalogLoader(dataCache));
        services.AddSingleton<CraftingService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        _recipeCatalogLoader = _serviceProvider.GetRequiredService<RecipeCatalogLoader>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    #region LearnRecipeCommand Tests

    [Fact]
    public async Task LearnRecipe_Success_AddsToLearnedRecipes()
    {
        // Arrange
        var character = new Character
        {
            Name = "Apprentice",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 20, CurrentXP = 0 }
            }
        };

        // Get a trainer recipe (legendary sword)
        var recipe = _recipeCatalogLoader.GetRecipeById("legendary-sword-recipe");
        recipe.Should().NotBeNull("legendary sword recipe should exist");

        // Act
        var command = new LearnRecipeCommand
        {
            Character = character,
            RecipeId = "legendary-sword-recipe",
            Source = "Trainer"
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("learned");
        result.RecipeName.Should().Be(recipe!.Name);
        character.LearnedRecipes.Should().Contain(recipe.Slug);
    }

    [Fact]
    public async Task LearnRecipe_AlreadyLearned_ReturnsError()
    {
        // Arrange
        var character = new Character
        {
            Name = "Apprentice",
            LearnedRecipes = new HashSet<string> { "legendary-sword-recipe" },
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 20, CurrentXP = 0 }
            }
        };

        // Act
        var command = new LearnRecipeCommand
        {
            Character = character,
            RecipeId = "legendary-sword-recipe"
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already know");
    }

    [Fact]
    public async Task LearnRecipe_SkillTooLow_ReturnsError()
    {
        // Arrange
        var character = new Character
        {
            Name = "Novice",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 1, CurrentXP = 0 }
            }
        };

        // Try to learn legendary recipe (requires high skill)
        // Act
        var command = new LearnRecipeCommand
        {
            Character = character,
            RecipeId = "legendary-sword-recipe"
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("too low");
    }

    [Fact]
    public async Task LearnRecipe_InvalidRecipeId_ReturnsError()
    {
        // Arrange
        var character = new Character
        {
            Name = "Apprentice",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>()
        };

        // Act
        var command = new LearnRecipeCommand
        {
            Character = character,
            RecipeId = "nonexistent-recipe"
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    #endregion

    #region GetKnownRecipesQuery Tests

    [Fact]
    public async Task GetKnownRecipes_IncludesLearnedRecipes()
    {
        // Arrange
        var character = new Character
        {
            Name = "Craftsman",
            LearnedRecipes = new HashSet<string> { "legendary-sword-recipe" },
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 50, CurrentXP = 0 }
            },
            Inventory = new List<Item>()
        };

        // Act
        var query = new GetKnownRecipesQuery
        {
            Character = character
        };

        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Recipes.Should().ContainSingle(r => r.Recipe.Slug == "legendary-sword-recipe");
    }

    [Fact]
    public async Task GetKnownRecipes_IncludesAutoUnlockRecipes()
    {
        // Arrange
        var character = new Character
        {
            Name = "Smith",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 10, CurrentXP = 0 }
            },
            Inventory = new List<Item>()
        };

        // Act
        var query = new GetKnownRecipesQuery
        {
            Character = character,
            SkillName = "Blacksmithing"
        };

        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Recipes.Should().NotBeEmpty("character should know some auto-unlock recipes");
        result.Recipes.Should().OnlyContain(r => 
            r.Recipe.UnlockMethod == RecipeUnlockMethod.SkillLevel || character.LearnedRecipes.Contains(r.Recipe.Slug));
    }

    [Fact]
    public async Task GetKnownRecipes_FiltersByStation()
    {
        // Arrange
        var character = new Character
        {
            Name = "Versatile Crafter",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 20, CurrentXP = 0 },
                ["Alchemy"] = new CharacterSkill { SkillId = "alchemy", Name = "Alchemy", CurrentRank = 20, CurrentXP = 0 }
            },
            Inventory = new List<Item>()
        };

        // Act - Get only Anvil recipes
        var query = new GetKnownRecipesQuery
        {
            Character = character,
            StationId = "Anvil"
        };

        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Recipes.Should().AllSatisfy(r => 
            r.Recipe.RequiredStation.Equals("Anvil", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetKnownRecipes_IdentifiesMissingMaterials()
    {
        // Arrange
        var character = new Character
        {
            Name = "Poor Smith",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 5, CurrentXP = 0 }
            },
            Inventory = new List<Item>() // Empty inventory
        };

        // Act
        var query = new GetKnownRecipesQuery
        {
            Character = character
        };

        var result = await _mediator.Send(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Recipes.Should().Contain(r => !r.CanCraft, "some recipes should be uncraftable without materials");
        result.Recipes.Where(r => !r.CanCraft).Should().AllSatisfy(r => 
            r.MissingMaterials.Should().NotBeEmpty());
    }

    #endregion

    #region DiscoverRecipeCommand Tests

    [Fact]
    public async Task DiscoverRecipe_WithValidSkill_ReturnsResult()
    {
        // Arrange
        var character = new Character
        {
            Name = "Experimenter",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 15, CurrentXP = 0 }
            }
        };

        // Act - Try multiple times since discovery is RNG-based
        DiscoverRecipeResult? successResult = null;
        for (int i = 0; i < 50; i++)
        {
            var command = new DiscoverRecipeCommand
            {
                Character = character,
                SkillName = "Blacksmithing",
                StationId = "Anvil"
            };

            var result = await _mediator.Send(command);
            
            if (result.Success)
            {
                successResult = result;
                break;
            }
        }

        // Assert - At least one attempt should eventually succeed (or we test the failure path)
        // With 50 attempts and 5%+ chance, we should see at least one success
        if (successResult != null)
        {
            successResult.Success.Should().BeTrue();
            successResult.DiscoveredRecipe.Should().NotBeNull();
            successResult.SkillXpGained.Should().BeGreaterThan(0);
            character.LearnedRecipes.Should().Contain(successResult.DiscoveredRecipe!.Slug);
        }
        else
        {
            // If we never succeeded, at least verify we got XP for trying
            character.Skills["Blacksmithing"].CurrentXP.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task DiscoverRecipe_WithoutSkill_ReturnsError()
    {
        // Arrange
        var character = new Character
        {
            Name = "Unskilled",
            LearnedRecipes = new HashSet<string>(),
            Skills = new Dictionary<string, CharacterSkill>()
        };

        // Act
        var command = new DiscoverRecipeCommand
        {
            Character = character,
            SkillName = "Blacksmithing"
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("don't have");
    }

    [Fact]
    public async Task DiscoverRecipe_AllRecipesKnown_ReturnsNoDiscoveries()
    {
        // Arrange - Character knows all discovery recipes
        var allRecipes = _recipeCatalogLoader.LoadAllRecipes()
            .Where(r => r.UnlockMethod == RecipeUnlockMethod.Discovery && r.RequiredSkill == "Blacksmithing")
            .Select(r => r.Slug)
            .ToHashSet();

        var character = new Character
        {
            Name = "Master",
            LearnedRecipes = allRecipes,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 100, CurrentXP = 0 }
            }
        };

        // Act
        var command = new DiscoverRecipeCommand
        {
            Character = character,
            SkillName = "Blacksmithing"
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no new recipes");
    }

    #endregion
}
