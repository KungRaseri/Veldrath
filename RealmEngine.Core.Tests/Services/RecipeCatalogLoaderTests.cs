using FluentAssertions;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Unit tests for RecipeCatalogLoader service.
/// Tests recipe loading, parsing, filtering, and caching.
/// </summary>
[Trait("Category", "Service")]
public class RecipeCatalogLoaderTests
{
    private readonly GameDataCache _dataCache;
    private readonly RecipeCatalogLoader _loader;

    public RecipeCatalogLoaderTests()
    {
        // Use actual game data path - recipes catalog exists at RealmEngine.Data/Data/Json/recipes/catalog.json
        var testDataPath = Path.Combine("..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataCache = new GameDataCache(testDataPath);
        _loader = new RecipeCatalogLoader(_dataCache);
    }

    #region LoadAllRecipes Tests

    [Fact]
    public void LoadAllRecipes_Should_Load_All_Recipes()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        recipes.Should().NotBeNull();
        recipes.Should().NotBeEmpty();
        // Note: Actual count depends on recipes/catalog.json content
    }

    [Fact]
    public void LoadAllRecipes_Should_Cache_Results()
    {
        // Act
        var firstLoad = _loader.LoadAllRecipes();
        var secondLoad = _loader.LoadAllRecipes();

        // Assert - Should return same instance (cached)
        firstLoad.Should().BeSameAs(secondLoad);
    }

    [Fact]
    public void LoadAllRecipes_Should_Parse_Recipe_Names()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert - Check for known recipes
        recipes.Should().Contain(r => r.Name == "Iron Ingot");
        recipes.Should().Contain(r => r.Name == "Steel Ingot");
        recipes.Should().Contain(r => r.Name == "Health Potion");
        recipes.Should().Contain(r => r.Name == "Battleaxe");
        recipes.Should().Contain(r => r.Name == "Orb of Flames");
    }

    [Fact]
    public void LoadAllRecipes_Should_Parse_Recipe_Slugs()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var ironIngot = recipes.FirstOrDefault(r => r.Name == "Iron Ingot");
        ironIngot.Should().NotBeNull();
        ironIngot!.Slug.Should().Be("recipe-iron-ingot");
    }

    [Fact]
    public void LoadAllRecipes_Should_Parse_Required_Skills()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var blacksmithingRecipes = recipes.Where(r => r.RequiredSkill == "Blacksmithing");
        blacksmithingRecipes.Should().NotBeEmpty();

        var alchemyRecipes = recipes.Where(r => r.RequiredSkill == "Alchemy");
        alchemyRecipes.Should().NotBeEmpty();

        var enchantingRecipes = recipes.Where(r => r.RequiredSkill == "Enchanting");
        enchantingRecipes.Should().NotBeEmpty();

        var jewelcraftingRecipes = recipes.Where(r => r.RequiredSkill == "Jewelcrafting");
        jewelcraftingRecipes.Should().NotBeEmpty();
    }

    [Fact]
    public void LoadAllRecipes_Should_Parse_Components()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var ironIngot = recipes.First(r => r.Name == "Iron Ingot");
        ironIngot.Materials.Should().HaveCount(1);
        ironIngot.Materials[0].ItemReference.Should().Be("@items/materials/ore:iron-ore");
        ironIngot.Materials[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void LoadAllRecipes_Should_Parse_Produced_Items()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var ironIngot = recipes.First(r => r.Name == "Iron Ingot");
        ironIngot.OutputItemReference.Should().Be("@items/materials/ingot:iron-ingot");
        ironIngot.OutputQuantity.Should().Be(1);
        ironIngot.MinQuality.Should().Be(ItemRarity.Common);
        ironIngot.MaxQuality.Should().Be(ItemRarity.Common);
    }

    [Fact]
    public void LoadAllRecipes_Should_Handle_Multi_Component_Recipes()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var steelIngot = recipes.First(r => r.Name == "Steel Ingot");
        steelIngot.Materials.Should().HaveCount(2);
        steelIngot.Materials.Should().Contain(c => c.ItemReference == "@items/materials/ingot:iron-ingot");
        steelIngot.Materials.Should().Contain(c => c.ItemReference == "@items/materials/reagent:coal");
    }

    #endregion

    #region LoadRecipesByCategory Tests

    [Fact]
    public void LoadRecipesByCategory_Should_Return_Blacksmithing_Refining_Recipes()
    {
        // Act
        var recipes = _loader.LoadRecipesByCategory("blacksmithing_refining");

        // Assert
        recipes.Should().HaveCount(4);
        recipes.Should().Contain(r => r.Name == "Iron Ingot");
        recipes.Should().Contain(r => r.Name == "Steel Ingot");
        recipes.Should().Contain(r => r.Name == "Bronze Ingot");
        recipes.Should().Contain(r => r.Name == "Mithril Ingot");
    }

    [Fact]
    public void LoadRecipesByCategory_Should_Return_Alchemy_Potions_Recipes()
    {
        // Act
        var recipes = _loader.LoadRecipesByCategory("alchemy_potions");

        // Assert
        recipes.Should().HaveCount(3);
        recipes.Should().Contain(r => r.Name == "Health Potion");
        recipes.Should().Contain(r => r.Name == "Mana Potion");
        recipes.Should().Contain(r => r.Name == "Stamina Potion");
    }

    [Fact]
    public void LoadRecipesByCategory_Should_Cache_Category_Results()
    {
        // Act
        var firstLoad = _loader.LoadRecipesByCategory("blacksmithing_weapons");
        var secondLoad = _loader.LoadRecipesByCategory("blacksmithing_weapons");

        // Assert
        firstLoad.Should().BeSameAs(secondLoad);
    }

    [Fact]
    public void LoadRecipesByCategory_Should_Return_Empty_For_Invalid_Category()
    {
        // Act
        var recipes = _loader.LoadRecipesByCategory("nonexistent_category");

        // Assert
        recipes.Should().BeEmpty();
    }

    #endregion

    #region GetRecipeById Tests

    [Fact]
    public void GetRecipeById_Should_Find_Recipe_By_Slug()
    {
        // Act
        var recipe = _loader.GetRecipeById("recipe-iron-ingot");

        // Assert
        recipe.Should().NotBeNull();
        recipe!.Name.Should().Be("Iron Ingot");
    }

    [Fact]
    public void GetRecipeById_Should_Return_Null_For_Invalid_Id()
    {
        // Act
        var recipe = _loader.GetRecipeById("nonexistent-recipe");

        // Assert
        recipe.Should().BeNull();
    }

    [Fact]
    public void GetRecipeById_Should_Find_Recipes_Across_Categories()
    {
        // Act
        var ironIngot = _loader.GetRecipeById("recipe-iron-ingot");
        var healthPotion = _loader.GetRecipeById("recipe-health-potion");
        var steelIngot = _loader.GetRecipeById("recipe-steel-ingot");

        // Assert
        ironIngot.Should().NotBeNull();
        ironIngot!.Category.Should().Be("blacksmithing_refining");

        healthPotion.Should().NotBeNull();
        healthPotion!.Category.Should().Be("alchemy_potions");

        steelIngot.Should().NotBeNull();
        steelIngot!.Category.Should().Be("blacksmithing_refining");
    }

    #endregion

    #region GetAvailableRecipes Tests

    [Fact]
    public void GetAvailableRecipes_Should_Filter_By_Skill_Level()
    {
        // Act - Test filtering at different skill levels
        var level1Recipes = _loader.GetAvailableRecipes(1);
        var level10Recipes = _loader.GetAvailableRecipes(10);
        var level50Recipes = _loader.GetAvailableRecipes(50);

        // Assert
        level1Recipes.Should().NotBeEmpty();
        level10Recipes.Count.Should().BeGreaterThan(level1Recipes.Count);
        level50Recipes.Count.Should().BeGreaterThanOrEqualTo(level10Recipes.Count);
        
        // All recipes should respect the skill level filter
        level1Recipes.Should().OnlyContain(r => r.RequiredSkillLevel <= 1);
        level10Recipes.Should().OnlyContain(r => r.RequiredSkillLevel <= 10);
        level50Recipes.Should().OnlyContain(r => r.RequiredSkillLevel <= 50);
    }

    [Fact]
    public void GetAvailableRecipes_Should_Filter_By_Category_And_Level()
    {
        // Act
        var recipes = _loader.GetAvailableRecipes(15, "blacksmithing_refining");

        // Assert
        recipes.Should().HaveCount(3); // Iron, Steel, Bronze (not Mithril level 45)
        recipes.Should().Contain(r => r.Name == "Iron Ingot");
        recipes.Should().Contain(r => r.Name == "Steel Ingot");
        recipes.Should().Contain(r => r.Name == "Bronze Ingot");
        recipes.Should().NotContain(r => r.Name == "Mithril Ingot");
    }

    [Fact]
    public void GetAvailableRecipes_Should_Return_Empty_For_Level_Zero()
    {
        // Act
        var recipes = _loader.GetAvailableRecipes(0);

        // Assert
        recipes.Should().BeEmpty();
    }

    #endregion

    #region Recipe Structure Tests

    [Fact]
    public void Recipes_Should_Have_Valid_Crafting_Times()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        recipes.Should().OnlyContain(r => r.CraftingTime > 0);
    }

    [Fact]
    public void Recipes_Should_Have_Valid_Experience_Values()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        recipes.Should().OnlyContain(r => r.ExperienceGained > 0);
    }

    [Fact]
    public void Recipes_Should_Have_Valid_Station_Requirements()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var stations = new[] { "Anvil", "AlchemyTable", "EnchantingTable", "JewelryBench", "Workbench", "TanningRack", "Loom" };
        recipes.Should().OnlyContain(r => stations.Contains(r.RequiredStation));
    }

    [Fact]
    public void Recipes_Should_Have_Valid_Station_Tiers()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        recipes.Should().OnlyContain(r => r.RequiredStationTier >= 1 && r.RequiredStationTier <= 3);
    }

    [Fact]
    public void Recipes_Should_Have_Progression_Tiers()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert - Basic recipes should have tier 1, advanced recipes tier 2-3
        var tier1Recipes = recipes.Where(r => r.RequiredStationTier == 1).ToList();
        var tier2Recipes = recipes.Where(r => r.RequiredStationTier == 2).ToList();
        var tier3Recipes = recipes.Where(r => r.RequiredStationTier == 3).ToList();

        tier1Recipes.Should().NotBeEmpty();
        tier2Recipes.Should().NotBeEmpty();
        tier3Recipes.Should().NotBeEmpty();
    }

    #endregion

    #region Optional Catalyst Tests

    [Fact]
    public void Recipes_With_Catalysts_Should_Parse_Correctly()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert - Check a recipe with optional catalyst (Battleaxe has catalyst)
        var battleaxe = recipes.FirstOrDefault(r => r.Name == "Battleaxe");
        if (battleaxe != null && !string.IsNullOrEmpty(battleaxe.UnlockRequirement))
        {
            battleaxe.UnlockRequirement.Should().Contain("Catalyst:");
            battleaxe.UnlockRequirement.Should().Contain("@items/consumables/elixirs:quenching-oil");
            battleaxe.UnlockRequirement.Should().Contain("+1 enchantment slot");
        }
    }

    [Fact]
    public void Recipes_Without_Catalysts_Should_Not_Have_Catalyst_Data()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert - Iron Ingot has no catalyst
        var ironIngot = recipes.First(r => r.Name == "Iron Ingot");
        if (!string.IsNullOrEmpty(ironIngot.UnlockRequirement))
        {
            ironIngot.UnlockRequirement.Should().NotContain("Catalyst:");
        }
    }

    #endregion

    #region Cross-Discipline Tests

    [Fact]
    public void Should_Have_Blacksmithing_Recipes()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var blacksmithingRecipes = recipes.Where(r => r.RequiredSkill == "Blacksmithing").ToList();
        blacksmithingRecipes.Should().HaveCount(7); // 4 refining + 2 weapons + 1 armor
    }

    [Fact]
    public void Should_Have_Alchemy_Recipes()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var alchemyRecipes = recipes.Where(r => r.RequiredSkill == "Alchemy").ToList();
        alchemyRecipes.Should().NotBeEmpty();
        alchemyRecipes.Should().HaveCountGreaterThanOrEqualTo(5); // At least 5 Alchemy recipes
    }

    [Fact]
    public void Should_Have_Enchanting_Recipes()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var enchantingRecipes = recipes.Where(r => r.RequiredSkill == "Enchanting").ToList();
        enchantingRecipes.Should().HaveCount(4); // 2 scrolls + 2 runes
    }

    [Fact]
    public void Should_Have_Jewelcrafting_Recipes()
    {
        // Act
        var recipes = _loader.LoadAllRecipes();

        // Assert
        var jewelcraftingRecipes = recipes.Where(r => r.RequiredSkill == "Jewelcrafting").ToList();
        jewelcraftingRecipes.Should().HaveCount(3); // 2 orbs + 1 polished gem
    }

    #endregion

    #region Cache Management Tests

    [Fact]
    public void ClearCache_Should_Clear_All_Recipe_Cache()
    {
        // Arrange - Load recipes to populate cache
        var firstLoad = _loader.LoadAllRecipes();

        // Act
        _loader.ClearCache();
        var secondLoad = _loader.LoadAllRecipes();

        // Assert - Should be different instances after cache clear
        firstLoad.Should().NotBeSameAs(secondLoad);
    }

    [Fact]
    public void ClearCache_Should_Clear_Category_Cache()
    {
        // Arrange
        var firstLoad = _loader.LoadRecipesByCategory("blacksmithing_weapons");

        // Act
        _loader.ClearCache();
        var secondLoad = _loader.LoadRecipesByCategory("blacksmithing_weapons");

        // Assert - Different instances after cache clear
        firstLoad.Should().NotBeSameAs(secondLoad);
    }

    #endregion
}
