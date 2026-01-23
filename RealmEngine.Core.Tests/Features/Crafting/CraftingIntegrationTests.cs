using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core.Features.Crafting.Commands;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Features.Crafting;

public class CraftingIntegrationTests
{
    private readonly IMediator _mediator;
    private readonly CraftingService _craftingService;
    private readonly RecipeCatalogLoader _recipeCatalogLoader;

    public CraftingIntegrationTests()
    {
        // Setup DI container
        var services = new ServiceCollection();
        
        // Use test data path
        var testDataPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json"
        );

        var dataCache = new GameDataCache(testDataPath);
        _recipeCatalogLoader = new RecipeCatalogLoader(dataCache);
        _craftingService = new CraftingService(_recipeCatalogLoader);

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CraftRecipeHandler).Assembly));
        services.AddSingleton(_craftingService);
        services.AddSingleton(_recipeCatalogLoader);

        var serviceProvider = services.BuildServiceProvider();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task EndToEnd_CraftIronIngot_Success()
    {
        // Arrange - Character with materials and skill
        var character = new Character
        {
            Name = "Master Smith",
            Level = 10,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill
                {
                    SkillId = "blacksmithing",
                    Name = "Blacksmithing",
                    CurrentRank = 5,
                    CurrentXP = 0
                }
            },
            Inventory = new List<Item>
            {
                new Item { Id = "iron-ore", Name = "Iron Ore" },
                new Item { Id = "iron-ore", Name = "Iron Ore" }
            }
        };

        // Get the recipe
        var recipe = _recipeCatalogLoader.GetRecipeById("recipe-iron-ingot");
        recipe.Should().NotBeNull("iron ingot recipe should exist");

        // Verify character can craft
        var canCraft = _craftingService.CanCraftRecipe(character, recipe!, out var reason);
        canCraft.Should().BeTrue($"should be able to craft: {reason}");

        // Act - Execute craft command
        var station = new CraftingStation { Id = "anvil", Name = "Anvil", Tier = 1 };
        var command = new CraftRecipeCommand
        {
            Character = character,
            Recipe = recipe!,
            Station = station
        };

        var result = await _mediator.Send(command);

        // Assert - Verify success
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(result.Message);
        result.CraftedItem.Should().NotBeNull();
        result.CraftedItem!.Name.Should().Contain("Iron Ingot");
        result.Quality.Should().BeInRange(0, 100);

        // Verify materials consumed
        character.Inventory.Count(i => i.Id == "iron-ore" || i.Name == "Iron Ore")
            .Should().Be(0, "all iron ore should be consumed");

        // Verify item added to inventory
        character.Inventory.Should().Contain(i => i.Name.Contains("Iron Ingot"));

        // Verify skill XP gained
        result.SkillXpGained.Should().BeGreaterThan(0);
        character.Skills["Blacksmithing"].CurrentXP.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EndToEnd_CraftHealthPotion_Success()
    {
        // Arrange - Character with alchemy skill and materials
        var character = new Character
        {
            Name = "Master Alchemist",
            Level = 15,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Alchemy"] = new CharacterSkill
                {
                    SkillId = "alchemy",
                    Name = "Alchemy",
                    CurrentRank = 12,
                    CurrentXP = 0
                }
            },
            Inventory = new List<Item>
            {
                // Add organic materials (wildcard match)
                new Item { Id = "herb", Name = "Herb" },
                new Item { Id = "herb", Name = "Herb" },
                new Item { Id = "herb", Name = "Herb" },
                new Item { Id = "herb", Name = "Herb" },
                new Item { Id = "herb", Name = "Herb" },
                // Add distilled water
                new Item { Id = "distilled-water", Name = "Distilled Water" }
            }
        };

        // Get recipe
        var recipe = _recipeCatalogLoader.GetRecipeById("recipe-health-potion");
        recipe.Should().NotBeNull("health potion recipe should exist");

        // Act - Craft potion
        var station = new CraftingStation { Id = "AlchemyTable", Name = "Alchemy Table", Tier = 1 };
        var command = new CraftRecipeCommand
        {
            Character = character,
            Recipe = recipe!,
            Station = station
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue(result.Message);
        result.CraftedItem.Should().NotBeNull();
        result.CraftedItem!.Name.Should().Contain("Health Potion");
        
        // Verify quality is within expected range (Uncommon to Rare)
        result.Quality.Should().BeInRange((int)ItemRarity.Uncommon, (int)ItemRarity.Rare);

        // Verify materials consumed
        character.Inventory.Count(i => i.Id == "herb").Should().Be(0);
        character.Inventory.Count(i => i.Id == "distilled-water").Should().Be(0);

        // Verify potion added
        character.Inventory.Should().Contain(i => i.Name.Contains("Health Potion"));
    }

    [Fact]
    public async Task EndToEnd_CraftWithoutMaterials_Fails()
    {
        // Arrange - Character with skill but NO materials
        var character = new Character
        {
            Name = "Broke Smith",
            Level = 10,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", CurrentRank = 10 }
            },
            Inventory = new List<Item>() // Empty inventory
        };

        var recipe = _recipeCatalogLoader.GetRecipeById("recipe-iron-ingot");

        // Act
        var station = new CraftingStation { Id = "anvil", Name = "Anvil", Tier = 1 };
        var command = new CraftRecipeCommand
        {
            Character = character,
            Recipe = recipe!,
            Station = station
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Missing");
        result.CraftedItem.Should().BeNull();
    }

    [Fact]
    public async Task EndToEnd_CraftWithLowSkill_Fails()
    {
        // Arrange - Character with materials but LOW skill
        var character = new Character
        {
            Name = "Novice Smith",
            Level = 1,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", CurrentRank = 0 } // Too low!
            },
            Inventory = new List<Item>
            {
                new Item { Id = "iron-ore", Name = "Iron Ore" },
                new Item { Id = "iron-ore", Name = "Iron Ore" }
            }
        };

        var recipe = _recipeCatalogLoader.GetRecipeById("recipe-iron-ingot");

        // Act
        var station = new CraftingStation { Id = "anvil", Name = "Anvil", Tier = 1 };
        var command = new CraftRecipeCommand
        {
            Character = character,
            Recipe = recipe!,
            Station = station
        };

        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("skill level");
        result.CraftedItem.Should().BeNull();
    }

    [Fact]
    public async Task EndToEnd_CraftTrainerRecipe_RequiresLearning()
    {
        // Arrange - High skill but recipe not learned
        var character = new Character
        {
            Name = "Legendary Smith",
            Level = 50,
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", CurrentRank = 50 }
            },
            Inventory = new List<Item>
            {
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "mithril-ingot", Name = "Mithril Ingot" },
                new Item { Id = "ruby", Name = "Ruby" },
                new Item { Id = "sapphire", Name = "Sapphire" }
            }
        };

        var recipe = _recipeCatalogLoader.GetRecipeById("legendary-sword-recipe");

        // Act - Try without learning
        var station = new CraftingStation { Id = "anvil", Name = "Anvil", Tier = 3 };
        var command = new CraftRecipeCommand
        {
            Character = character,
            Recipe = recipe!,
            Station = station
        };

        var resultBefore = await _mediator.Send(command);

        // Assert - Should fail
        resultBefore.Success.Should().BeFalse();
        resultBefore.Message.Should().Contain("not unlocked");

        // Now learn the recipe
        character.LearnedRecipes.Add("legendary-sword-recipe");
        
        var resultAfter = await _mediator.Send(command);

        // Should succeed now
        resultAfter.Success.Should().BeTrue();
        resultAfter.CraftedItem.Should().NotBeNull();
    }

    [Fact]
    public void GetAvailableRecipes_FiltersCorrectly()
    {
        // Arrange
        var character = new Character
        {
            Name = "Mid-level Crafter",
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", CurrentRank = 10 },
                ["Alchemy"] = new CharacterSkill { SkillId = "alchemy", CurrentRank = 15 }
            }
        };

        // Act - Get available blacksmithing recipes
        var blacksmithingRecipes = _craftingService.GetAvailableRecipes(character, "Anvil");

        // Assert
        blacksmithingRecipes.Should().NotBeEmpty();
        blacksmithingRecipes.Should().OnlyContain(r => 
            r.RequiredStation.Equals("Anvil", StringComparison.OrdinalIgnoreCase) &&
            r.RequiredSkillLevel <= 10
        );

        // Act - Get available alchemy recipes
        var alchemyRecipes = _craftingService.GetAvailableRecipes(character, "AlchemyTable");

        // Assert
        alchemyRecipes.Should().NotBeEmpty();
        alchemyRecipes.Should().OnlyContain(r => 
            r.RequiredStation.Equals("AlchemyTable", StringComparison.OrdinalIgnoreCase) &&
            r.RequiredSkillLevel <= 15
        );
    }
}
