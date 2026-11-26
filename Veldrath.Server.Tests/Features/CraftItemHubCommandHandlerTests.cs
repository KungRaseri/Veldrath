using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Tests.Infrastructure;
using PlayerAccount = Veldrath.Server.Data.Entities.PlayerAccount;
using ServerCharacter = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="CraftItemHubCommandHandler"/>: the phase that replaced a placeholder which
/// charged a flat fifty gold and produced nothing.
/// </summary>
/// <remarks>
/// The materials and the output live in the character's own <c>InventoryBlob</c> — the bag the client
/// reads. These tests seed that blob the way harvesting does (the bare slug) rather than mocking an
/// inventory service, which is what let the craft path read an empty table unnoticed.
/// </remarks>
public class CraftItemHubCommandHandlerTests : IDisposable
{
    private const string IngotRef = "@items/materials/ingots:iron-ingot";
    private const string SwordRef = "@items/weapons/swords:iron-longsword";

    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static Recipe Longsword(int requiredLevel = 1, int requiredSkillLevel = 1, int experience = 15) => new()
    {
        Id = "iron-longsword",
        Name = "Iron longsword",
        Slug = "iron-longsword",
        Description = "A plain blade.",
        Category = "weapons",
        RequiredLevel = requiredLevel,
        RequiredSkill = "blacksmithing",
        RequiredSkillLevel = requiredSkillLevel,
        RequiredStation = "blacksmith_forge",
        OutputItemReference = SwordRef,
        OutputQuantity = 1,
        ExperienceGained = experience,
        Materials = [new RecipeMaterial { ItemReference = IngotRef, Quantity = 2 }],
    };

    /// <summary>Builds an <c>InventoryBlob</c> the way harvesting, chests and combat loot write one.</summary>
    private static string Bag(params (string ItemRef, int Quantity)[] entries)
    {
        var bag = CharacterBag.Read(null);
        foreach (var (itemRef, quantity) in entries)
            CharacterBag.Add(bag, itemRef, quantity);
        return CharacterBag.Serialize(bag);
    }

    private static string BagOf(int ingots) => Bag(("iron-ingot", ingots));

    private static async Task<ServerCharacter> SeedCharacterAsync(
        ApplicationDbContext db,
        int level = 3,
        string? attributes = null,
        string inventoryBlob = "[]")
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new ServerCharacter
        {
            AccountId = account.Id,
            Name = $"Smith_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Attributes = attributes ?? """{"CurrentHealth":20,"MaxHealth":30,"Skill_blacksmithing_Rank":1,"Skill_blacksmithing_XP":100}""",
            InventoryBlob = inventoryBlob,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static Mock<IRecipeRepository> Recipes(Recipe? recipe)
    {
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync(recipe);
        return recipes;
    }

    private CraftItemHubCommandHandler Handler(
        ApplicationDbContext db,
        IRecipeRepository recipes) =>
        Handler(new CharacterRepository(db), recipes);

    private static CraftItemHubCommandHandler Handler(
        ICharacterRepository characters,
        IRecipeRepository recipes)
    {
        return new(
            characters,
            recipes,
            Mediator(AwardHandler(characters)).Object,
            NullLogger<CraftItemHubCommandHandler>.Instance);
    }

    private static AwardSkillXpHubCommandHandler AwardHandler(ICharacterRepository characters) =>
        new(characters, NullLogger<AwardSkillXpHubCommandHandler>.Instance);

    /// <summary>
    /// Routes the craft path's award command to the real award handler, so these tests exercise the actual
    /// rule (key convention, rank size) instead of a stubbed answer.
    /// </summary>
    private static Mock<IMediator> Mediator(AwardSkillXpHubCommandHandler awards)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<AwardSkillXpHubResult> request, CancellationToken cancellationToken) =>
                awards.Handle((AwardSkillXpHubCommand)request, cancellationToken));
        return mediator;
    }

    [Fact]
    public async Task Consumes_the_materials_adds_the_output_and_awards_skill_experience()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: BagOf(5));

        var result = await Handler(db, Recipes(Longsword()).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.RecipeSlug.Should().Be("iron-longsword");
        result.ItemRef.Should().Be(SwordRef);
        result.OutputQuantity.Should().Be(1);
        result.ConsumedMaterials.Should().Equal(new ConsumedMaterialDto(IngotRef, 2));
        result.SkillId.Should().Be("blacksmithing");
        result.SkillXp.Should().Be(15);
        result.SkillTotalXp.Should().Be(115);
        result.SkillRank.Should().Be(1);
        result.SkillRankedUp.Should().BeFalse("the character was already rank 1 before this craft");

        var stored = await db.Characters.FindAsync(character.Id);
        var bag = CharacterBag.Read(stored!.InventoryBlob);
        CharacterBag.Count(bag, IngotRef).Should().Be(3, "two of the five ingots were consumed");
        CharacterBag.Count(bag, "iron-longsword").Should().Be(1, "the output is in the bag the client reads");
        stored.Attributes.Should().Contain("\"Skill_blacksmithing_XP\":115");
        stored.LastPlayedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Reads_a_material_held_under_its_full_reference_as_well_as_its_slug()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: Bag((IngotRef, 3)));

        var result = await Handler(db, Recipes(Longsword()).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        var bag = CharacterBag.Read((await db.Characters.FindAsync(character.Id))!.InventoryBlob);
        CharacterBag.Count(bag, "iron-ingot").Should().Be(1);
    }

    [Fact]
    public async Task Stacks_the_output_onto_a_stack_the_bag_already_carries()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db,
            inventoryBlob: Bag((IngotRef, 2), (SwordRef, 1)));

        var result = await Handler(db, Recipes(Longsword()).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        var bag = CharacterBag.Read((await db.Characters.FindAsync(character.Id))!.InventoryBlob);
        bag.Should().ContainSingle("the output stacked onto the existing sword and the ingots were consumed");
        CharacterBag.Count(bag, "iron-longsword").Should().Be(2);
        bag[0]["ItemRef"].GetString().Should().Be(SwordRef, "the entry keeps the form it was written in");
    }

    [Fact]
    public async Task Reports_the_rank_up_when_the_award_crosses_the_threshold()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db,
            attributes: """{"CurrentHealth":20,"MaxHealth":30,"Skill_blacksmithing_Rank":0,"Skill_blacksmithing_XP":95}""",
            inventoryBlob: BagOf(5));

        var result = await Handler(db, Recipes(Longsword(requiredSkillLevel: 0)).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.SkillXp.Should().Be(15);
        result.SkillTotalXp.Should().Be(110);
        result.SkillRank.Should().Be(1);
        result.SkillRankedUp.Should().BeTrue("95 + 15 crosses the first rank at 100 experience");
    }

    [Fact]
    public async Task Refuses_a_recipe_the_character_has_out_levelled_requirements_for()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, level: 1, inventoryBlob: BagOf(5));

        var result = await Handler(db, Recipes(Longsword(requiredLevel: 5)).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("requires level 5");
        var stored = await db.Characters.FindAsync(character.Id);
        stored!.InventoryBlob.Should().Be(character.InventoryBlob, "a refused craft touches nothing");
    }

    [Fact]
    public async Task Refuses_a_recipe_above_the_character_skill_rank()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: BagOf(5));

        var result = await Handler(db, Recipes(Longsword(requiredSkillLevel: 4)).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("blacksmithing rank 4");
        result.ErrorMessage.Should().Contain("you are rank 1");
    }

    [Fact]
    public async Task Names_the_shortfall_without_consuming_anything()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: BagOf(1));

        var result = await Handler(db, Recipes(Longsword()).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("1 more " + IngotRef);

        var stored = await db.Characters.FindAsync(character.Id);
        var bag = CharacterBag.Read(stored!.InventoryBlob);
        CharacterBag.Count(bag, "iron-ingot").Should().Be(1, "the shortfall costs the player nothing");
        CharacterBag.Count(bag, "iron-longsword").Should().Be(0);
    }

    [Fact]
    public async Task Fails_on_an_unknown_recipe()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: BagOf(5));

        var result = await Handler(db, Recipes(null).Object).Handle(
            new CraftItemHubCommand(character.Id, "no-such-recipe"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No recipe named 'no-such-recipe'");
    }

    [Fact]
    public async Task Fails_on_a_blank_recipe_slug()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: BagOf(5));

        var result = await Handler(db, Recipes(null).Object).Handle(
            new CraftItemHubCommand(character.Id, "  "),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task Fails_when_the_character_is_gone()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await Handler(db, Recipes(Longsword()).Object).Handle(
            new CraftItemHubCommand(Guid.NewGuid(), "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Refuses_a_recipe_that_produces_nothing()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, inventoryBlob: BagOf(5));
        var recipe = Longsword();
        recipe.OutputItemReference = "  ";

        var result = await Handler(db, Recipes(recipe).Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("produces nothing");
    }

    [Fact]
    public async Task Dispatches_the_award_and_writes_the_character_once()
    {
        // The craft owns the character-row write, so the award must arrive through the command and be folded into
        // that one save: a second load/save inside the award handler would overwrite the bag it accompanies.
        var character = new ServerCharacter
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = $"Smith_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = 3,
            Attributes = """{"Skill_blacksmithing_Rank":0,"Skill_blacksmithing_XP":95}""",
            InventoryBlob = BagOf(5),
        };

        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        characters.Setup(r => r.UpdateAsync(character, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = Handler(characters.Object, Recipes(Longsword(requiredSkillLevel: 0)).Object);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.SkillId.Should().Be("blacksmithing");
        result.SkillXp.Should().Be(15);
        result.SkillTotalXp.Should().Be(110);
        result.SkillRank.Should().Be(1);
        result.SkillRankedUp.Should().BeTrue();

        characters.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
        character.Attributes.Should().Contain("\"Skill_blacksmithing_XP\":110");
        CharacterBag.Count(CharacterBag.Read(character.InventoryBlob), "iron-longsword").Should().Be(1);
    }

    [Fact]
    public async Task Writes_the_bag_without_dispatching_an_award_when_the_recipe_trains_nothing()
    {
        var character = new ServerCharacter
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = $"Smith_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = 3,
            Attributes = "{}",
            InventoryBlob = BagOf(5),
        };

        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        characters.Setup(r => r.UpdateAsync(character, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var mediator = Mediator(AwardHandler(characters.Object));
        var recipe = Longsword(requiredSkillLevel: 0, experience: 0);
        var handler = new CraftItemHubCommandHandler(
            characters.Object,
            Recipes(recipe).Object,
            mediator.Object,
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue("the item is still crafted");
        result.SkillId.Should().BeNull();
        result.SkillXp.Should().Be(0);
        mediator.Verify(
            m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // The bag changed even though no award was made, so the character row is still written exactly once.
        characters.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
        CharacterBag.Count(CharacterBag.Read(character.InventoryBlob), "iron-longsword").Should().Be(1);
    }
}
