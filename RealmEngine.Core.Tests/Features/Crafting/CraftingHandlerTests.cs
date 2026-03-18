using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Crafting.Commands;
using RealmEngine.Core.Features.Crafting.Queries;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Crafting;

[Trait("Category", "Feature")]
public class CraftRecipeHandlerTests
{
  private static Recipe MakeRecipe(
      string id,
      string station = "anvil",
      int requiredLevel = 1,
      RecipeUnlockMethod unlockMethod = RecipeUnlockMethod.SkillLevel) => new()
      {
        Id = id,
        Name = id.Replace("-", " "),
        Slug = id,
        RequiredStation = station,
        RequiredSkillLevel = requiredLevel,
        RequiredSkill = "Blacksmithing",
        UnlockMethod = unlockMethod,
        OutputItemReference = "iron-sword",
        MinQuality = ItemRarity.Common,
        MaxQuality = ItemRarity.Common,
        Materials = [],
        CraftingTime = 5
      };

  private static Character MakeCharacter(int smithingLevel = 10) => new()
  {
    Name = "Smith",
    Level = 10,
    Skills = new()
    {
      ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = smithingLevel }
    },
    Inventory = [],
    LearnedRecipes = ["iron-sword"]
  };

  private static CraftingStation MakeStation(string id) => new()
  {
    Id = id,
    Name = id,
    Slug = id
  };

  private static RecipeDataService MakeRecipeService(List<Recipe>? recipes = null)
  {
    var repo = new Mock<IRecipeRepository>();
    repo.Setup(r => r.GetAllAsync()).ReturnsAsync(recipes ?? []);
    return new RecipeDataService(repo.Object, NullLogger<RecipeDataService>.Instance);
  }

  private static CraftingService MakeCraftingService(RecipeDataService? rds = null) =>
      new(rds ?? MakeRecipeService(), NullLogger<CraftingService>.Instance);

  [Fact]
  public async Task Handle_ReturnsFailure_WhenCanCraftRecipeFails()
  {
    var recipe = MakeRecipe("iron-sword", requiredLevel: 50);
    var character = MakeCharacter(smithingLevel: 1);
    var station = MakeStation("anvil");

    var craftingService = MakeCraftingService();
    var handler = new CraftRecipeHandler(craftingService, NullLogger<CraftRecipeHandler>.Instance);

    var result = await handler.Handle(
        new CraftRecipeCommand { Character = character, Recipe = recipe, Station = station }, default);

    result.Success.Should().BeFalse();
    result.Message.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Handle_ReturnsFailure_WhenStationDoesNotMatchRecipe()
  {
    // Recipe requires "anvil" station, but player is at "workbench"
    var recipe = MakeRecipe("iron-sword", station: "anvil");
    var character = MakeCharacter(smithingLevel: 10);
    // Station id/name/slug must NOT match the recipe's required station
    var wrongStation = new CraftingStation { Id = "workbench", Name = "Workbench", Slug = "workbench" };

    var craftingService = MakeCraftingService();
    var handler = new CraftRecipeHandler(craftingService, NullLogger<CraftRecipeHandler>.Instance);

    var result = await handler.Handle(
        new CraftRecipeCommand { Character = character, Recipe = recipe, Station = wrongStation }, default);

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("anvil");
  }

  [Fact]
  public async Task Handle_ReturnsSuccess_WhenAllConditionsMet()
  {
    var recipe = MakeRecipe("iron-sword", station: "anvil", requiredLevel: 1);
    var character = MakeCharacter(smithingLevel: 10);
    var station = MakeStation("anvil");
    // No materials needed (empty Materials list)

    var craftingService = MakeCraftingService();
    var handler = new CraftRecipeHandler(craftingService, NullLogger<CraftRecipeHandler>.Instance);

    var result = await handler.Handle(
        new CraftRecipeCommand { Character = character, Recipe = recipe, Station = station }, default);

    result.Success.Should().BeTrue();
    result.CraftedItem.Should().NotBeNull();
  }

  [Fact]
  public async Task Handle_AddsItemToInventory_OnSuccess()
  {
    var recipe = MakeRecipe("iron-sword", station: "anvil", requiredLevel: 1);
    var character = MakeCharacter(smithingLevel: 10);
    var station = MakeStation("anvil");
    var initialCount = character.Inventory.Count;

    var craftingService = MakeCraftingService();
    var handler = new CraftRecipeHandler(craftingService, NullLogger<CraftRecipeHandler>.Instance);

    await handler.Handle(
        new CraftRecipeCommand { Character = character, Recipe = recipe, Station = station }, default);

    character.Inventory.Should().HaveCount(initialCount + 1);
  }
}

[Trait("Category", "Feature")]
public class LearnRecipeHandlerTests
{
  private static RecipeDataService MakeService(IEnumerable<Recipe>? recipes = null)
  {
    var repo = new Mock<IRecipeRepository>();
    repo.Setup(r => r.GetAllAsync()).ReturnsAsync(recipes?.ToList() ?? []);
    return new RecipeDataService(repo.Object, NullLogger<RecipeDataService>.Instance);
  }

  private static Recipe MakeRecipe(string id, string skill = "Blacksmithing", int level = 1) => new()
  {
    Id = id,
    Name = id,
    Slug = id,
    RequiredStation = "anvil",
    RequiredSkillLevel = level,
    RequiredSkill = skill,
    UnlockMethod = RecipeUnlockMethod.Trainer,
    OutputItemReference = "item",
    MinQuality = ItemRarity.Common,
    MaxQuality = ItemRarity.Common,
    Materials = [],
    CraftingTime = 1
  };

  [Fact]
  public async Task Handle_ReturnsFailure_WhenRecipeNotFound()
  {
    var handler = new LearnRecipeHandler(MakeService());
    var character = new Character { Name = "Hero", Inventory = [], LearnedRecipes = [], Skills = [], Level = 1 };

    var result = await handler.Handle(
        new LearnRecipeCommand { Character = character, RecipeId = "unknown-recipe" }, default);

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("unknown-recipe");
  }

  [Fact]
  public async Task Handle_ReturnsFailure_WhenAlreadyKnown()
  {
    var recipe = MakeRecipe("iron-sword");
    var handler = new LearnRecipeHandler(MakeService([recipe]));
    var character = new Character { Name = "Hero", Inventory = [], LearnedRecipes = ["iron-sword"], Skills = [], Level = 1 };

    var result = await handler.Handle(
        new LearnRecipeCommand { Character = character, RecipeId = "iron-sword" }, default);

    result.Success.Should().BeFalse();
    result.Message.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Handle_Succeeds_WhenRecipeExistsAndNotKnown()
  {
    var recipe = MakeRecipe("fire-potion", "Alchemy", level: 1);
    var handler = new LearnRecipeHandler(MakeService([recipe]));
    var character = new Character
    {
      Name = "Hero",
      Inventory = [],
      LearnedRecipes = [],
      Skills = new() { ["Alchemy"] = new CharacterSkill { SkillId = "alchemy", Name = "Alchemy", CurrentRank = 10 } },
      Level = 1
    };

    var result = await handler.Handle(
        new LearnRecipeCommand { Character = character, RecipeId = "fire-potion", Source = "Trainer" }, default);

    result.Success.Should().BeTrue();
    result.RecipeName.Should().Be(recipe.Name);
    character.LearnedRecipes.Should().Contain("fire-potion");
  }

  [Fact]
  public async Task Handle_AddsRecipeToLearnedRecipes_OnSuccess()
  {
    var recipe = MakeRecipe("basic-bread", "Cooking", level: 1);
    var handler = new LearnRecipeHandler(MakeService([recipe]));
    var character = new Character { Name = "Baker", Inventory = [], LearnedRecipes = [], Skills = [], Level = 1 };

    await handler.Handle(new LearnRecipeCommand { Character = character, RecipeId = "basic-bread", Source = "Trainer" }, default);

    character.LearnedRecipes.Should().Contain("basic-bread");
  }
}

[Trait("Category", "Feature")]
public class DiscoverRecipeHandlerTests
{
  private static RecipeDataService MakeService(IEnumerable<Recipe>? recipes = null)
  {
    var repo = new Mock<IRecipeRepository>();
    repo.Setup(r => r.GetAllAsync()).ReturnsAsync(recipes?.ToList() ?? []);
    return new RecipeDataService(repo.Object, NullLogger<RecipeDataService>.Instance);
  }

  private static Recipe MakeDiscoverable(string id, string skill = "Alchemy", int level = 5) => new()
  {
    Id = id,
    Name = id,
    Slug = id,
    RequiredStation = "alchemytable",
    RequiredSkillLevel = level,
    RequiredSkill = skill,
    UnlockMethod = RecipeUnlockMethod.Discovery,
    ExperienceGained = 10,
    OutputItemReference = "potion",
    MinQuality = ItemRarity.Common,
    MaxQuality = ItemRarity.Common,
    Materials = [],
    CraftingTime = 1
  };

  [Fact]
  public async Task Handle_ReturnsFailure_WhenCharacterLacksSkill()
  {
    var handler = new DiscoverRecipeHandler(MakeService());
    var character = new Character { Name = "Hero", Inventory = [], LearnedRecipes = [], Skills = [], Level = 1 };

    var result = await handler.Handle(
        new DiscoverRecipeCommand { Character = character, SkillName = "Alchemy" }, default);

    result.Success.Should().BeFalse();
    result.Message.Should().Contain("Alchemy");
  }

  [Fact]
  public async Task Handle_ReturnsFailure_WhenNoDiscoverableRecipesExist()
  {
    var handler = new DiscoverRecipeHandler(MakeService()); // empty repo
    var character = new Character
    {
      Name = "Hero",
      Inventory = [],
      LearnedRecipes = [],
      Skills = new() { ["Alchemy"] = new CharacterSkill { SkillId = "alchemy", Name = "Alchemy", CurrentRank = 10 } },
      Level = 1
    };

    var result = await handler.Handle(
        new DiscoverRecipeCommand { Character = character, SkillName = "Alchemy" }, default);

    result.Success.Should().BeFalse();
    result.Message.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Handle_AwardsXp_EvenOnFailure()
  {
    var handler = new DiscoverRecipeHandler(MakeService());
    var skill = new CharacterSkill { SkillId = "alchemy", Name = "Alchemy", CurrentRank = 10 };
    var character = new Character
    {
      Name = "Hero",
      Inventory = [],
      LearnedRecipes = [],
      Skills = new() { ["Alchemy"] = skill },
      Level = 1
    };
    var xpBefore = skill.CurrentXP;

    await handler.Handle(new DiscoverRecipeCommand { Character = character, SkillName = "Alchemy" }, default);

    skill.CurrentXP.Should().BeGreaterThan(xpBefore);
  }
}

[Trait("Category", "Feature")]
public class GetKnownRecipesHandlerTests
{
  private static Recipe MakeRecipe(
      string id, string skill = "Blacksmithing", int level = 1,
      RecipeUnlockMethod unlock = RecipeUnlockMethod.SkillLevel) => new()
      {
        Id = id,
        Name = id,
        Slug = id,
        RequiredStation = "anvil",
        RequiredSkillLevel = level,
        RequiredSkill = skill,
        UnlockMethod = unlock,
        ExperienceGained = 5,
        OutputItemReference = "item",
        MinQuality = ItemRarity.Common,
        MaxQuality = ItemRarity.Common,
        Materials = [],
        CraftingTime = 1
      };

  private static (GetKnownRecipesHandler, RecipeDataService) MakeHandler(List<Recipe>? recipes = null)
  {
    var repo = new Mock<IRecipeRepository>();
    repo.Setup(r => r.GetAllAsync()).ReturnsAsync(recipes ?? []);
    var rds = new RecipeDataService(repo.Object, NullLogger<RecipeDataService>.Instance);
    var cs = new CraftingService(rds, NullLogger<CraftingService>.Instance);
    return (new GetKnownRecipesHandler(rds, cs), rds);
  }

  [Fact]
  public async Task Handle_ReturnsEmpty_WhenCharacterKnowsNoRecipes()
  {
    var (handler, _) = MakeHandler([MakeRecipe("sword", unlock: RecipeUnlockMethod.Trainer)]);
    var character = new Character { Name = "Hero", Inventory = [], LearnedRecipes = [], Skills = [], Level = 1 };

    var result = await handler.Handle(new GetKnownRecipesQuery { Character = character }, default);

    result.Recipes.Should().BeEmpty();
  }

  [Fact]
  public async Task Handle_ReturnsLearnedRecipes_WhenCharacterHasLearned()
  {
    var recipe = MakeRecipe("iron-sword", unlock: RecipeUnlockMethod.Trainer);
    var (handler, _) = MakeHandler([recipe]);
    var character = new Character
    {
      Name = "Hero",
      Inventory = [],
      LearnedRecipes = ["iron-sword"],
      Skills = [],
      Level = 1
    };

    var result = await handler.Handle(new GetKnownRecipesQuery { Character = character }, default);

    result.Recipes.Should().HaveCount(1);
    result.Recipes[0].Recipe.Slug.Should().Be("iron-sword");
  }

  [Fact]
  public async Task Handle_AutoUnlocksRecipes_WhenSkillLevelSufficient()
  {
    var recipe = MakeRecipe("silver-sword", level: 5, unlock: RecipeUnlockMethod.SkillLevel);
    var (handler, _) = MakeHandler([recipe]);
    var character = new Character
    {
      Name = "Smith",
      Inventory = [],
      LearnedRecipes = [],
      Skills = new() { ["Blacksmithing"] = new CharacterSkill { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = 10 } },
      Level = 1
    };

    var result = await handler.Handle(new GetKnownRecipesQuery { Character = character }, default);

    result.Recipes.Should().HaveCount(1);
  }

  [Fact]
  public async Task Handle_FiltersByStationId_WhenProvided()
  {
    var swordRecipe = MakeRecipe("sword", "Blacksmithing");
    swordRecipe.RequiredStation = "anvil";
    var potionRecipe = MakeRecipe("potion", "Alchemy");
    potionRecipe.RequiredStation = "alchemytable";

    var (handler, _) = MakeHandler([swordRecipe, potionRecipe]);
    var character = new Character
    {
      Name = "Hero",
      Inventory = [],
      LearnedRecipes = ["sword", "potion"],
      Skills = [],
      Level = 1
    };

    var result = await handler.Handle(
        new GetKnownRecipesQuery { Character = character, StationId = "anvil" }, default);

    result.Recipes.Should().HaveCount(1);
    result.Recipes[0].Recipe.Slug.Should().Be("sword");
  }
}
