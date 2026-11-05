using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Crafting.Commands;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Crafting.Commands;

/// <summary>
/// Covers the <see cref="CraftRecipeHandler"/> branches the base suite leaves out: the failure
/// catch-all, material consumption (specific references plus wildcards), the station → item-type and
/// station → skill mappings, and the crafting skill's XP level-up loop.
/// </summary>
[Trait("Category", "Feature")]
public class CraftRecipeHandlerExtendedTests
{
    private static CraftRecipeHandler CreateHandler()
    {
        var recipeRepository = new Mock<IRecipeRepository>();
        recipeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var craftingService = new CraftingService(
            new RecipeDataService(recipeRepository.Object, NullLogger<RecipeDataService>.Instance),
            NullLogger<CraftingService>.Instance);
        return new CraftRecipeHandler(craftingService, NullLogger<CraftRecipeHandler>.Instance);
    }

    private static Recipe MakeRecipe(
        string id = "test-recipe",
        string station = "anvil",
        int experienceGained = 10,
        List<RecipeMaterial>? materials = null) => new()
        {
            Id = id,
            Name = id.Replace("-", " "),
            Slug = id,
            RequiredStation = station,
            RequiredSkillLevel = 0,
            UnlockMethod = RecipeUnlockMethod.Default,
            OutputItemReference = id,
            MinQuality = ItemRarity.Common,
            MaxQuality = ItemRarity.Common,
            Materials = materials ?? [],
            ExperienceGained = experienceGained
        };

    private static Character MakeCharacter(int craftingRank = 0, List<Item>? inventory = null) => new()
    {
        Name = "Smith",
        Level = 10,
        Skills = new()
        {
            ["Blacksmithing"] = new CharacterSkill
            {
                SkillId = "Blacksmithing",
                Name = "Blacksmithing",
                CurrentRank = craftingRank,
                CurrentXP = 0
            }
        },
        Inventory = inventory ?? []
    };

    private static CraftingStation MakeStation(string id) => new() { Id = id, Name = id, Slug = id };

    private static Item MakeMaterial(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Type = ItemType.Material
    };

    [Fact]
    public async Task Handle_WhenValidationThrows_ReturnsFailureInsteadOfPropagating()
    {
        var recipe = MakeRecipe();
        recipe.RequiredStation = null!; // the skill lookup dereferences the station string

        var result = await CreateHandler().Handle(
            new CraftRecipeCommand { Character = MakeCharacter(), Recipe = recipe, Station = MakeStation("anvil") },
            default);

        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("Failed to craft item:");
        result.CraftedItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ConsumesSpecificAndWildcardMaterials()
    {
        var inventory = new List<Item>
        {
            MakeMaterial("iron-ore", "Iron Ore"),
            MakeMaterial("iron-ore", "Iron Ore"),
            MakeMaterial("gold-ore", "Gold Ore")
        };
        var recipe = MakeRecipe(materials:
        [
            new RecipeMaterial { ItemReference = "@items/materials/ores:iron-ore", Quantity = 2 },
            new RecipeMaterial { ItemReference = "@items/materials/*:*", Quantity = 1 }
        ]);
        var character = MakeCharacter(inventory: inventory);

        var result = await CreateHandler().Handle(
            new CraftRecipeCommand { Character = character, Recipe = recipe, Station = MakeStation("anvil") },
            default);

        result.Success.Should().BeTrue();
        result.MaterialsConsumed.Should().HaveCount(3);
        result.MaterialsConsumed.Count(m => m == "1x Iron Ore").Should().Be(2);
        result.MaterialsConsumed.Should().Contain("1x Gold Ore", "the wildcard consumes the remaining material");
        character.Inventory.Should().ContainSingle("only the crafted item remains")
            .Which.Should().BeSameAs(result.CraftedItem);
    }

    [Theory]
    [InlineData("anvil", ItemType.Weapon, "Blacksmithing")]
    [InlineData("blacksmith_forge", ItemType.Weapon, "Blacksmithing")]
    [InlineData("alchemy_table", ItemType.Consumable, "Alchemy")]
    [InlineData("enchanting_altar", ItemType.EnchantmentScroll, "Enchanting")]
    [InlineData("cooking_fire", ItemType.Consumable, "Cooking")]
    [InlineData("workbench", ItemType.Material, "Carpentry")]
    [InlineData("loom", ItemType.Material, "Tailoring")]
    [InlineData("tanning_rack", ItemType.Material, "Leatherworking")]
    [InlineData("jewelry_bench", ItemType.Material, "Jewelcrafting")]
    [InlineData("strange_device", ItemType.Material, "Crafting")]
    public async Task Handle_MapsStationToItemTypeAndCraftingSkill(string station, ItemType expectedType, string expectedSkill)
    {
        var character = MakeCharacter();

        var result = await CreateHandler().Handle(
            new CraftRecipeCommand
            {
                Character = character,
                Recipe = MakeRecipe(station: station),
                Station = MakeStation(station)
            },
            default);

        result.Success.Should().BeTrue();
        result.CraftedItem!.Type.Should().Be(expectedType);
        character.Skills.Should().ContainKey(expectedSkill, "crafting awards XP to the station's skill");
    }

    [Fact]
    public async Task Handle_AwardsSkillXp_AndRanksUpOnHundredXpSteps()
    {
        var character = MakeCharacter(craftingRank: 10);
        var recipe = MakeRecipe(experienceGained: 250);

        var result = await CreateHandler().Handle(
            new CraftRecipeCommand { Character = character, Recipe = recipe, Station = MakeStation("anvil") },
            default);

        result.Success.Should().BeTrue();
        result.SkillXpGained.Should().Be(250);
        var skill = character.Skills["Blacksmithing"];
        skill.CurrentRank.Should().Be(12, "250 XP is two full 100-XP ranks");
        skill.CurrentXP.Should().Be(50);
    }

    [Fact]
    public async Task Handle_AddsCraftedItemToInventory_AndReportsQuality()
    {
        var character = MakeCharacter();

        var result = await CreateHandler().Handle(
            new CraftRecipeCommand
            {
                Character = character,
                Recipe = MakeRecipe(id: "iron-sword"),
                Station = MakeStation("anvil")
            },
            default);

        result.Success.Should().BeTrue();
        character.Inventory.Should().ContainSingle().Which.Should().BeSameAs(result.CraftedItem);
        result.Message.Should().Contain("Successfully crafted iron sword");
        result.CraftedItem!.Description.Should().Contain("quality");
        result.CraftedItem.Traits.Should().ContainKey("Strength", "weapons gain quality-scaled traits");
        result.Quality.Should().Be(0, "common min and max quality clamp the rolled quality");
    }

    [Fact]
    public async Task Handle_WhenMaterialsAreMissing_ReturnsFailureWithoutConsumingItems()
    {
        var character = MakeCharacter(inventory: [MakeMaterial("gold-ore", "Gold Ore")]);
        var recipe = MakeRecipe(materials:
        [
            new RecipeMaterial { ItemReference = "@items/materials/ores:iron-ore", Quantity = 2 }
        ]);

        var result = await CreateHandler().Handle(
            new CraftRecipeCommand { Character = character, Recipe = recipe, Station = MakeStation("anvil") },
            default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Missing");
        character.Inventory.Should().ContainSingle("failed validation must not consume anything");
    }
}
