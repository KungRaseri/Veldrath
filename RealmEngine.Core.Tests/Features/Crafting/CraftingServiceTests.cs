using FluentAssertions;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Services;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Crafting;

public class CraftingServiceTests
{
    private readonly CraftingService _craftingService;
    private readonly RecipeDataService _recipeCatalogLoader;

    public CraftingServiceTests()
    {
        _recipeCatalogLoader = new RecipeDataService(new InMemoryRecipeRepository());
        _craftingService = new CraftingService(_recipeCatalogLoader, NullLogger<CraftingService>.Instance);
    }

    #region CanCraftRecipe Tests

    [Fact]
    public void CanCraftRecipe_WithNullCharacter_ReturnsFalse()
    {
        // Arrange
        var recipe = CreateTestRecipe("test-recipe", "Blacksmithing", 1);

        // Act
        var result = _craftingService.CanCraftRecipe(null!, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Be("Character cannot be null");
    }

    [Fact]
    public void CanCraftRecipe_WithNullRecipe_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var result = _craftingService.CanCraftRecipe(character, null!, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Be("Recipe cannot be null");
    }

    [Fact]
    public void CanCraftRecipe_WhenSkillLevelTooLow_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 5);
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10);

        // Act
        var result = _craftingService.CanCraftRecipe(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Contain("Requires Blacksmithing skill level 10");
        reason.Should().Contain("current: 5");
    }

    [Fact]
    public void CanCraftRecipe_WhenRecipeNotUnlocked_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        var recipe = CreateTestRecipe("legendary-sword", "anvil", requiredLevel: 10, unlockMethod: RecipeUnlockMethod.Trainer);
        // Recipe not in character.LearnedRecipes

        // Act
        var result = _craftingService.CanCraftRecipe(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Contain("not unlocked");
    }

    [Fact]
    public void CanCraftRecipe_WhenMaterialsMissing_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 5 }
        ];
        // Character has empty inventory

        // Act
        var result = _craftingService.CanCraftRecipe(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Contain("Missing iron-ingot");
        reason.Should().Contain("need 5, have 0");
    }

    [Fact]
    public void CanCraftRecipe_WhenAllConditionsMet_ReturnsTrue()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" }
        ];

        var recipe = CreateTestRecipe("iron-dagger", "anvil", requiredLevel: 5);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 2 }
        ];

        // Act
        var result = _craftingService.CanCraftRecipe(character, recipe, out var reason);

        // Assert
        result.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void CanCraftRecipe_WithExactlyRequiredSkillLevel_ReturnsTrue()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" }
        ];

        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 1 }
        ];

        // Act
        var result = _craftingService.CanCraftRecipe(character, recipe, out var reason);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ValidateMaterials Tests

    [Fact]
    public void ValidateMaterials_WithNullInventory_ReturnsFalse()
    {
        // Arrange
        var character = new Character { Name = "Test", Inventory = null };
        var recipe = CreateTestRecipe("test-recipe", "anvil", 1);
        recipe.Materials = [new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 1 }];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Be("Character has no inventory");
    }

    [Fact]
    public void ValidateMaterials_WithEmptyInventory_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 3 }
        ];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Contain("Missing iron-ingot: need 3, have 0");
    }

    [Fact]
    public void ValidateMaterials_WithInsufficientQuantity_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" }
        ];

        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 5 }
        ];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Contain("need 5, have 2");
    }

    [Fact]
    public void ValidateMaterials_WithExactQuantity_ReturnsTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" }
        ];

        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 3 }
        ];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMaterials_WithExcessQuantity_ReturnsTrue()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" }
        ];

        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 3 }
        ];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateMaterials_WithMultipleMaterials_ValidatesAll()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "leather-grip", Name = "Leather Grip" }
        ];

        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 2 },
            new RecipeMaterial { ItemReference = "leather-grip", Quantity = 1 }
        ];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Fact]
    public void ValidateMaterials_WithMultipleMaterials_WhenOneMissing_ReturnsFalse()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Inventory =
        [
            new Item { Id = "iron-ingot", Name = "Iron Ingot" },
            new Item { Id = "iron-ingot", Name = "Iron Ingot" }
            // Missing leather-grip
        ];

        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.Materials =
        [
            new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 2 },
            new RecipeMaterial { ItemReference = "leather-grip", Quantity = 1 }
        ];

        // Act
        var result = _craftingService.ValidateMaterials(character, recipe, out var reason);

        // Assert
        result.Should().BeFalse();
        reason.Should().Contain("Missing leather-grip");
    }

    #endregion

    #region CalculateQuality Tests

    [Fact]
    public void CalculateQuality_WithNullCharacter_ReturnsMinQuality()
    {
        // Arrange
        var recipe = CreateTestRecipe("iron-sword", "anvil", 1);
        recipe.MinQuality = ItemRarity.Common;
        recipe.MaxQuality = ItemRarity.Rare;

        // Act
        var quality = _craftingService.CalculateQuality(null!, recipe);

        // Assert
        quality.Should().Be((int)ItemRarity.Common);
    }

    [Fact]
    public void CalculateQuality_WithNullRecipe_ReturnsCommon()
    {
        // Arrange
        var character = CreateTestCharacter();

        // Act
        var quality = _craftingService.CalculateQuality(character, null!);

        // Assert
        quality.Should().Be((int)ItemRarity.Common);
    }

    [Fact]
    public void CalculateQuality_AtMinimumSkillLevel_ReturnsQualityInRange()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10);
        recipe.MinQuality = ItemRarity.Common;
        recipe.MaxQuality = ItemRarity.Uncommon;

        // Act
        var quality = _craftingService.CalculateQuality(character, recipe);

        // Assert
        quality.Should().BeInRange((int)ItemRarity.Common, (int)ItemRarity.Uncommon);
    }

    [Fact]
    public void CalculateQuality_WithHigherSkill_ImprovedQualityChance()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 50);
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10);
        recipe.MinQuality = ItemRarity.Common;
        recipe.MaxQuality = ItemRarity.Epic;

        // Act - Run multiple times to test quality distribution
        var qualities = Enumerable.Range(0, 20)
            .Select(_ => _craftingService.CalculateQuality(character, recipe))
            .ToList();

        // Assert
        qualities.Should().AllSatisfy(q => q.Should().BeInRange((int)ItemRarity.Common, (int)ItemRarity.Epic));
        qualities.Average().Should().BeGreaterThan((int)ItemRarity.Common); // Should trend higher
    }

    [Fact]
    public void CalculateQuality_NeverExceedsMaxQuality()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 100); // Very high skill
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 1);
        recipe.MinQuality = ItemRarity.Common;
        recipe.MaxQuality = ItemRarity.Uncommon;

        // Act - Run multiple times
        var qualities = Enumerable.Range(0, 50)
            .Select(_ => _craftingService.CalculateQuality(character, recipe))
            .ToList();

        // Assert
        qualities.Should().AllSatisfy(q => q.Should().BeLessThanOrEqualTo((int)ItemRarity.Uncommon));
    }

    [Fact]
    public void CalculateQuality_NeverBelowMinQuality()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10);
        recipe.MinQuality = ItemRarity.Rare;
        recipe.MaxQuality = ItemRarity.Epic;

        // Act - Run multiple times
        var qualities = Enumerable.Range(0, 50)
            .Select(_ => _craftingService.CalculateQuality(character, recipe))
            .ToList();

        // Assert
        qualities.Should().AllSatisfy(q => q.Should().BeGreaterThanOrEqualTo((int)ItemRarity.Rare));
    }

    #endregion

    #region GetAvailableRecipes Tests

    [Fact]
    public void GetAvailableRecipes_WithNullCharacter_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _craftingService.GetAvailableRecipes(null!));
    }

    [Fact]
    public void GetAvailableRecipes_WithNoSkills_ReturnsEmptyList()
    {
        // Arrange
        var character = new Character { Name = "Newbie", Skills = new Dictionary<string, CharacterSkill>() };

        // Act
        var recipes = _craftingService.GetAvailableRecipes(character);

        // Assert
        recipes.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableRecipes_FiltersBySkillLevel()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 5, alchemyLevel: 10);

        // Act
        var recipes = _craftingService.GetAvailableRecipes(character);

        // Assert - Should only include recipes at or below skill levels
        recipes.Should().OnlyContain(r =>
            (r.RequiredStation.Equals("Anvil", StringComparison.OrdinalIgnoreCase) && r.RequiredSkillLevel <= 5) ||
            (r.RequiredStation.Equals("AlchemyTable", StringComparison.OrdinalIgnoreCase) && r.RequiredSkillLevel <= 10)
        );
    }

    [Fact]
    public void GetAvailableRecipes_FiltersByStation()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 20, alchemyLevel: 20);

        // Act
        var recipes = _craftingService.GetAvailableRecipes(character, stationId: "anvil");

        // Assert (case-insensitive comparison since service does case-insensitive filtering)
        recipes.Should().OnlyContain(r => r.RequiredStation.Equals("anvil", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region IsRecipeUnlocked Tests (via CanCraftRecipe)

    [Fact]
    public void IsRecipeUnlocked_SkillLevel_UnlockedAtRequiredLevel()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 10);
        character.Inventory = [new Item { Id = "iron-ingot", Name = "Iron Ingot" }];
        
        var recipe = CreateTestRecipe("iron-sword", "anvil", requiredLevel: 10, unlockMethod: RecipeUnlockMethod.SkillLevel);
        recipe.Materials = [new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 1 }];

        // Act
        var result = _craftingService.CanCraftRecipe(character, recipe, out _);

        // Assert
        result.Should().BeTrue("recipe should be unlocked by skill level");
    }

    [Fact]
    public void IsRecipeUnlocked_Trainer_RequiresLearning()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 50);
        character.Inventory = [new Item { Id = "iron-ingot", Name = "Iron Ingot" }];
        
        var recipe = CreateTestRecipe("master-sword", "anvil", requiredLevel: 10, unlockMethod: RecipeUnlockMethod.Trainer);
        recipe.Materials = [new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 1 }];

        // Act - Without learning
        var resultNotLearned = _craftingService.CanCraftRecipe(character, recipe, out var reasonNotLearned);

        // Add to learned recipes
        character.LearnedRecipes = ["master-sword"];
        var resultLearned = _craftingService.CanCraftRecipe(character, recipe, out _);

        // Assert
        resultNotLearned.Should().BeFalse();
        reasonNotLearned.Should().Contain("not unlocked");
        resultLearned.Should().BeTrue();
    }

    [Fact]
    public void IsRecipeUnlocked_QuestReward_RequiresQuestCompletion()
    {
        // Arrange
        var character = CreateTestCharacter(blacksmithingLevel: 30);
        character.Inventory = [new Item { Id = "iron-ingot", Name = "Iron Ingot" }];
        
        var recipe = CreateTestRecipe("hero-sword", "anvil", requiredLevel: 20, unlockMethod: RecipeUnlockMethod.QuestReward);
        recipe.Materials = [new RecipeMaterial { ItemReference = "iron-ingot", Quantity = 1 }];

        // Act - Before quest
        var resultBefore = _craftingService.CanCraftRecipe(character, recipe, out _);

        // Complete quest (add to learned recipes)
        character.LearnedRecipes = ["hero-sword"];
        var resultAfter = _craftingService.CanCraftRecipe(character, recipe, out _);

        // Assert
        resultBefore.Should().BeFalse();
        resultAfter.Should().BeTrue();
    }

    [Fact]
    public void IsRecipeUnlocked_Discovery_RequiresDiscovery()
    {
        // Arrange
        var character = CreateTestCharacter(alchemyLevel: 25);
        character.Inventory = [new Item { Id = "herb", Name = "Herb" }];
        
        var recipe = CreateTestRecipe("mysterious-potion", "alchemytable", requiredLevel: 15, unlockMethod: RecipeUnlockMethod.Discovery);
        recipe.Materials = [new RecipeMaterial { ItemReference = "herb", Quantity = 1 }];

        // Act - Before discovery
        var resultBefore = _craftingService.CanCraftRecipe(character, recipe, out _);

        // Discover recipe
        character.LearnedRecipes = ["mysterious-potion"];
        var resultAfter = _craftingService.CanCraftRecipe(character, recipe, out _);

        // Assert
        resultBefore.Should().BeFalse();
        resultAfter.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static Character CreateTestCharacter(
        int blacksmithingLevel = 0,
        int alchemyLevel = 0,
        int enchantingLevel = 0)
    {
        var character = new Character
        {
            Name = "Test Character",
            Level = 10,
            Skills = new Dictionary<string, CharacterSkill>(),
            Inventory = [],
            LearnedRecipes = []
        };

        if (blacksmithingLevel > 0)
        {
            character.Skills["Blacksmithing"] = new CharacterSkill
            {
                SkillId = "blacksmithing",
                Name = "Blacksmithing",
                CurrentRank = blacksmithingLevel
            };
        }

        if (alchemyLevel > 0)
        {
            character.Skills["Alchemy"] = new CharacterSkill
            {
                SkillId = "alchemy",
                Name = "Alchemy",
                CurrentRank = alchemyLevel
            };
        }

        if (enchantingLevel > 0)
        {
            character.Skills["Enchanting"] = new CharacterSkill
            {
                SkillId = "enchanting",
                Name = "Enchanting",
                CurrentRank = enchantingLevel
            };
        }

        return character;
    }

    private static Recipe CreateTestRecipe(
        string id,
        string station,
        int requiredLevel,
        RecipeUnlockMethod unlockMethod = RecipeUnlockMethod.SkillLevel)
    {
        return new Recipe
        {
            Id = id,
            Name = id.Replace("-", " ").ToUpper(),
            RequiredStation = station,
            RequiredSkillLevel = requiredLevel,
            UnlockMethod = unlockMethod,
            CraftingTime = 5,
            Materials = [],
            OutputItemReference = "test-output",
            MinQuality = ItemRarity.Common,
            MaxQuality = ItemRarity.Rare
        };
    }

    #endregion
}
