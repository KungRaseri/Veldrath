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

    private static async Task<ServerCharacter> SeedCharacterAsync(ApplicationDbContext db, int level = 3, string? attributes = null)
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
        IRecipeRepository recipes,
        IInventoryService inventory)
    {
        var characters = new CharacterRepository(db);
        return new(
            characters,
            recipes,
            inventory,
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
        var character = await SeedCharacterAsync(db);
        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.GetItemCountAsync(character.Name, IngotRef)).ReturnsAsync(5);
        inventory.Setup(i => i.RemoveItemAsync(character.Name, IngotRef, 2)).ReturnsAsync(true);
        inventory.Setup(i => i.AddItemAsync(character.Name, SwordRef, 1)).ReturnsAsync(true);

        var result = await Handler(db, Recipes(Longsword()).Object, inventory.Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RecipeSlug.Should().Be("iron-longsword");
        result.ItemRef.Should().Be(SwordRef);
        result.OutputQuantity.Should().Be(1);
        result.ConsumedMaterials.Should().Equal(new ConsumedMaterialDto(IngotRef, 2));
        result.SkillId.Should().Be("blacksmithing");
        result.SkillXp.Should().Be(15);
        result.SkillTotalXp.Should().Be(115);
        result.SkillRank.Should().Be(1);
        result.SkillRankedUp.Should().BeFalse("the character was already rank 1 before this craft");

        inventory.Verify(i => i.RemoveItemAsync(character.Name, IngotRef, 2), Times.Once);
        inventory.Verify(i => i.AddItemAsync(character.Name, SwordRef, 1), Times.Once);

        var updated = await db.Characters.FindAsync(character.Id);
        updated!.Attributes.Should().Contain("\"Skill_blacksmithing_XP\":115");
    }

    [Fact]
    public async Task Reports_the_rank_up_when_the_award_crosses_the_threshold()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(
            db,
            attributes: """{"CurrentHealth":20,"MaxHealth":30,"Skill_blacksmithing_Rank":0,"Skill_blacksmithing_XP":95}""");
        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.GetItemCountAsync(character.Name, IngotRef)).ReturnsAsync(5);
        inventory.Setup(i => i.RemoveItemAsync(character.Name, IngotRef, 2)).ReturnsAsync(true);
        inventory.Setup(i => i.AddItemAsync(character.Name, SwordRef, 1)).ReturnsAsync(true);

        var result = await Handler(db, Recipes(Longsword(requiredSkillLevel: 0)).Object, inventory.Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SkillXp.Should().Be(15);
        result.SkillTotalXp.Should().Be(110);
        result.SkillRank.Should().Be(1);
        result.SkillRankedUp.Should().BeTrue("95 + 15 crosses the first rank at 100 experience");
    }

    [Fact]
    public async Task Refuses_a_recipe_the_character_has_out_levelled_requirements_for()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, level: 1);
        var inventory = new Mock<IInventoryService>();

        var result = await Handler(db, Recipes(Longsword(requiredLevel: 5)).Object, inventory.Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("requires level 5");
        inventory.Verify(i => i.RemoveItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Refuses_a_recipe_above_the_character_skill_rank()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var inventory = new Mock<IInventoryService>();

        var result = await Handler(db, Recipes(Longsword(requiredSkillLevel: 4)).Object, inventory.Object).Handle(
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
        var character = await SeedCharacterAsync(db);
        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.GetItemCountAsync(character.Name, IngotRef)).ReturnsAsync(1);

        var result = await Handler(db, Recipes(Longsword()).Object, inventory.Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("1 more " + IngotRef);
        inventory.Verify(i => i.RemoveItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        inventory.Verify(i => i.AddItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Refunds_the_materials_when_the_output_cannot_be_added()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.GetItemCountAsync(character.Name, IngotRef)).ReturnsAsync(5);
        inventory.Setup(i => i.RemoveItemAsync(character.Name, IngotRef, 2)).ReturnsAsync(true);
        inventory.Setup(i => i.AddItemAsync(character.Name, SwordRef, 1)).ReturnsAsync(false);
        inventory.Setup(i => i.AddItemAsync(character.Name, IngotRef, 2)).ReturnsAsync(true);

        var result = await Handler(db, Recipes(Longsword()).Object, inventory.Object).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no room");
        // The two ingots come back, so a full bag cannot eat them.
        inventory.Verify(i => i.AddItemAsync(character.Name, IngotRef, 2), Times.Once);
    }

    [Fact]
    public async Task Fails_on_an_unknown_recipe()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var result = await Handler(db, Recipes(null).Object, Mock.Of<IInventoryService>()).Handle(
            new CraftItemHubCommand(character.Id, "no-such-recipe"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No recipe named 'no-such-recipe'");
    }

    [Fact]
    public async Task Fails_on_a_blank_recipe_slug()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);

        var result = await Handler(db, Recipes(null).Object, Mock.Of<IInventoryService>()).Handle(
            new CraftItemHubCommand(character.Id, "  "),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task Fails_when_the_character_is_gone()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await Handler(db, Recipes(Longsword()).Object, Mock.Of<IInventoryService>()).Handle(
            new CraftItemHubCommand(Guid.NewGuid(), "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Refuses_a_recipe_that_produces_nothing()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        var recipe = Longsword();
        recipe.OutputItemReference = "  ";

        var result = await Handler(db, Recipes(recipe).Object, Mock.Of<IInventoryService>()).Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("produces nothing");
    }

    [Fact]
    public async Task Dispatches_the_award_and_writes_the_character_once()
    {
        // The craft owns the attributes-blob write, so the award must arrive through the command and be folded into
        // that one save: a second load/save inside the award handler would overwrite the craft it accompanies.
        var character = new ServerCharacter
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = $"Smith_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = 3,
            Attributes = """{"Skill_blacksmithing_Rank":0,"Skill_blacksmithing_XP":95}""",
        };

        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        characters.Setup(r => r.UpdateAsync(character, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var mediator = Mediator(AwardHandler(characters.Object));

        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.GetItemCountAsync(character.Name, IngotRef)).ReturnsAsync(5);
        inventory.Setup(i => i.RemoveItemAsync(character.Name, IngotRef, 2)).ReturnsAsync(true);
        inventory.Setup(i => i.AddItemAsync(character.Name, SwordRef, 1)).ReturnsAsync(true);

        var handler = new CraftItemHubCommandHandler(
            characters.Object,
            Recipes(Longsword(requiredSkillLevel: 0)).Object,
            inventory.Object,
            mediator.Object,
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, "iron-longsword"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SkillId.Should().Be("blacksmithing");
        result.SkillXp.Should().Be(15);
        result.SkillTotalXp.Should().Be(110);
        result.SkillRank.Should().Be(1);
        result.SkillRankedUp.Should().BeTrue();

        mediator.Verify(
            m => m.Send(
                It.Is<AwardSkillXpHubCommand>(command =>
                    command.CharacterId == character.Id
                    && command.SkillId == "blacksmithing"
                    && command.Amount == 15),
                It.IsAny<CancellationToken>()),
            Times.Once);
        characters.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
        character.Attributes.Should().Contain("\"Skill_blacksmithing_XP\":110");
    }

    [Fact]
    public async Task Does_not_dispatch_an_award_when_the_recipe_trains_nothing()
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
        };

        var characters = new Mock<ICharacterRepository>();
        characters.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);

        var mediator = Mediator(AwardHandler(characters.Object));

        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.GetItemCountAsync(character.Name, IngotRef)).ReturnsAsync(5);
        inventory.Setup(i => i.RemoveItemAsync(character.Name, IngotRef, 2)).ReturnsAsync(true);
        inventory.Setup(i => i.AddItemAsync(character.Name, SwordRef, 1)).ReturnsAsync(true);

        var recipe = Longsword(requiredSkillLevel: 0, experience: 0);

        var handler = new CraftItemHubCommandHandler(
            characters.Object,
            Recipes(recipe).Object,
            inventory.Object,
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
        characters.Verify(r => r.UpdateAsync(It.IsAny<ServerCharacter>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
