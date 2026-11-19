using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Queries;
using CharacterEntity = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="GetCraftableRecipesQueryHandler"/>: the three gates a recipe has to pass
/// (level, skill rank, materials), the order the panel shows them in, and the two reads that are
/// allowed to fail without taking the page down.
/// </summary>
/// <remarks>
/// The handler had no tests at all before the .NET coverage gate started failing on
/// <c>Veldrath.Server</c> branches: it is the craft panel's whole data source, and every reason a
/// player sees comes out of the private <c>UnavailableReason</c> ordering asserted here.
/// </remarks>
public class GetCraftableRecipesQueryTests
{
    private static readonly Guid CharacterId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<ICharacterRepository> _characters = new();
    private readonly Mock<IRecipeRepository> _recipes = new();
    private readonly Mock<IInventoryService> _inventory = new();

    private GetCraftableRecipesQueryHandler Handler() =>
        new(
            _characters.Object,
            _recipes.Object,
            _inventory.Object,
            NullLogger<GetCraftableRecipesQueryHandler>.Instance);

    private static CharacterEntity Character(int level = 1, string attributes = "{}", string name = "Crafty") =>
        new()
        {
            AccountId = Guid.NewGuid(),
            Name = name,
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Attributes = attributes,
        };

    private static Recipe Recipe(
        string slug,
        int requiredLevel = 1,
        string? requiredSkill = null,
        int requiredSkillLevel = 0,
        string requiredStation = "",
        int outputQuantity = 1,
        string? outputRef = null,
        params RecipeMaterial[] materials) =>
        new()
        {
            Id = slug,
            Name = slug,
            Slug = slug,
            RequiredLevel = requiredLevel,
            RequiredSkill = requiredSkill,
            RequiredSkillLevel = requiredSkillLevel,
            RequiredStation = requiredStation,
            OutputItemReference = outputRef ?? $"@items/{slug}",
            OutputQuantity = outputQuantity,
            Materials = [.. materials],
        };

    private static RecipeMaterial Material(string itemRef, int quantity) =>
        new() { ItemReference = itemRef, Quantity = quantity };

    private void Given(CharacterEntity? character, params Recipe[] recipes)
    {
        _characters
            .Setup(characters => characters.GetByIdAsync(CharacterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        _recipes.Setup(catalog => catalog.GetAllAsync()).ReturnsAsync([.. recipes]);
        _inventory
            .Setup(inventory => inventory.GetItemCountAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0);
    }

    private Task<IReadOnlyList<CraftableRecipeDto>> QueryAsync() =>
        Handler().Handle(new GetCraftableRecipesQuery(CharacterId), CancellationToken.None);

    [Fact]
    public async Task Returns_nothing_when_the_character_is_gone()
    {
        Given(null, Recipe("iron-dagger"));

        (await QueryAsync()).Should().BeEmpty();
        _recipes.Verify(catalog => catalog.GetAllAsync(), Times.Never, "there is nothing to price up");
    }

    [Fact]
    public async Task Returns_nothing_when_the_recipe_catalog_fails()
    {
        _characters
            .Setup(characters => characters.GetByIdAsync(CharacterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Character());
        _recipes.Setup(catalog => catalog.GetAllAsync()).ThrowsAsync(new InvalidOperationException("catalog down"));

        // The panel has to stay empty rather than crash: a missing catalog is an outage, not a rejection.
        (await QueryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Skips_a_recipe_that_names_no_output_item()
    {
        Given(Character(), Recipe("ghost-recipe", outputRef: "  "), Recipe("iron-dagger"));

        // A recipe with no output cannot be made or shown; it is dropped rather than priced up.
        (await QueryAsync()).Select(recipe => recipe.Slug).Should().Equal("iron-dagger");
    }

    [Fact]
    public async Task Prices_every_material_and_ignores_blank_or_zero_ones()
    {
        Given(
            Character(name: "Smith"),
            Recipe(
                "iron-dagger",
                materials:
                [
                    Material("@items/iron-ingot", 2),
                    Material("  ", 5),
                    Material("@items/leather-strip", 0),
                ]));

        _inventory
            .Setup(inventory => inventory.GetItemCountAsync("Smith", "@items/iron-ingot"))
            .ReturnsAsync(3);

        var recipe = (await QueryAsync()).Should().ContainSingle().Subject;

        recipe.Materials.Should().ContainSingle();
        recipe.Materials[0].Should().BeEquivalentTo(
            new CraftMaterialDto { ItemRef = "@items/iron-ingot", Required = 2, Owned = 3 });
        recipe.Available.Should().BeTrue();
    }

    [Fact]
    public async Task Refuses_a_recipe_above_the_character_level()
    {
        Given(Character(level: 2), Recipe("plate-helm", requiredLevel: 5, materials: [Material("@items/iron-ingot", 1)]));

        var recipe = (await QueryAsync()).Should().ContainSingle().Subject;

        recipe.Available.Should().BeFalse();
        recipe.UnavailableReason.Should().Be("Requires level 5; you are level 2.");
    }

    [Fact]
    public async Task Refuses_a_recipe_whose_skill_rank_is_too_low()
    {
        Given(
            Character(level: 9, attributes: """{"Strength":14,"Skill_Blacksmithing_Rank":1}"""),
            Recipe("plate-helm", requiredSkill: "blacksmithing", requiredSkillLevel: 3));

        var recipe = (await QueryAsync()).Should().ContainSingle().Subject;

        recipe.SkillRank.Should().Be(1);
        recipe.UnavailableReason.Should().Be("Requires blacksmithing rank 3; you are rank 1.");
    }

    [Fact]
    public async Task Reports_a_missing_skill_rank_as_zero()
    {
        Given(
            Character(level: 9, attributes: """{"Skill_Alchemy_Rank":2}"""),
            Recipe("healing-draught", requiredSkill: "alchemy", requiredSkillLevel: 2),
            Recipe("poison-vial", requiredSkill: "toxicology", requiredSkillLevel: 1));

        var recipes = await QueryAsync();

        recipes.Single(recipe => recipe.Slug == "healing-draught").Available.Should().BeTrue();
        recipes.Single(recipe => recipe.Slug == "poison-vial").SkillRank.Should().Be(0);
        recipes.Single(recipe => recipe.Slug == "poison-vial").UnavailableReason.Should()
            .Be("Requires toxicology rank 1; you are rank 0.");
    }

    [Fact]
    public async Task Treats_unreadable_attributes_as_no_skills_rather_than_failing()
    {
        Given(
            Character(level: 9, attributes: "{not json"),
            Recipe("plate-helm", requiredSkill: "blacksmithing", requiredSkillLevel: 1));

        var recipe = (await QueryAsync()).Should().ContainSingle().Subject;

        recipe.SkillRank.Should().Be(0);
        recipe.Available.Should().BeFalse();
    }

    [Fact]
    public async Task Lists_every_shortfall_in_one_reason()
    {
        Given(
            Character(),
            Recipe(
                "iron-dagger",
                materials: [Material("@items/iron-ingot", 4), Material("@items/leather-strip", 2)]));

        _inventory
            .Setup(inventory => inventory.GetItemCountAsync(It.IsAny<string>(), "@items/iron-ingot"))
            .ReturnsAsync(1);

        var recipe = (await QueryAsync()).Should().ContainSingle().Subject;

        recipe.UnavailableReason.Should().Be("Short of 3 more @items/iron-ingot, 2 more @items/leather-strip.");
    }

    [Fact]
    public async Task Normalises_the_informational_fields()
    {
        Given(
            Character(),
            Recipe("iron-dagger", requiredStation: "  ", outputQuantity: 0),
            Recipe("iron-sword", requiredStation: "forge", outputQuantity: 3));

        var recipes = await QueryAsync();

        var dagger = recipes.Single(recipe => recipe.Slug == "iron-dagger");
        dagger.RequiredStation.Should().BeNull("the schema reports a blank station as absent");
        dagger.OutputQuantity.Should().Be(1, "a recipe that names no quantity still makes one");

        recipes.Single(recipe => recipe.Slug == "iron-sword").RequiredStation.Should().Be("forge");
        recipes.Single(recipe => recipe.Slug == "iron-sword").OutputQuantity.Should().Be(3);
    }

    [Fact]
    public async Task Shows_available_recipes_first_then_by_level_then_by_name()
    {
        Given(
            Character(level: 2),
            Recipe("zulu-blade", requiredLevel: 2),
            Recipe("hard-helm", requiredLevel: 9),
            Recipe("alpha-charm", requiredLevel: 2),
            Recipe("basic-hook", requiredLevel: 1));

        // Recipe slug and name are the same here, so the ordering is readable in the assertion.
        var order = (await QueryAsync()).Select(recipe => recipe.Slug).ToList();

        order.Should().Equal("basic-hook", "alpha-charm", "zulu-blade", "hard-helm");
    }
}
