using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Character = Veldrath.Server.Data.Entities.Character;
using Recipe = RealmEngine.Shared.Models.Recipe;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the attribute-blob readers the character hub handlers share: a corrupt <c>Attributes</c> JSON blob must
/// be treated as empty (with a warning) instead of failing the command, so a bad row can never brick a character.
/// </summary>
public class HubHandlerCorruptAttributesTests
{
    private const string Corrupt = "{not json";

    private static (Mock<ICharacterRepository> Repo, Character Character) Arrange(int level = 1)
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Attributes = Corrupt,
        };

        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return (repo, character);
    }

    [Fact]
    public async Task AwardSkillXp_StartsFromZero_WhenTheAttributesBlobIsCorrupt()
    {
        var (repo, character) = Arrange();
        var handler = new AwardSkillXpHubCommandHandler(
            repo.Object, NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new AwardSkillXpHubCommand { CharacterId = character.Id, SkillId = "herbalism", Amount = 150 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalXp.Should().Be(150);
        result.CurrentRank.Should().Be(1);
        result.RankedUp.Should().BeTrue();
        character.Attributes.Should().Contain("Skill_herbalism_XP");
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestAtLocation_RejectsANegativeCost()
    {
        var (repo, character) = Arrange();
        var handler = new RestAtLocationHubCommandHandler(
            repo.Object, NullLogger<RestAtLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new RestAtLocationHubCommand { CharacterId = character.Id, LocationId = "inn", CostInGold = -1 },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Rest cost cannot be negative.");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RestAtLocation_RestsForFree_WhenTheAttributesBlobIsCorrupt()
    {
        var (repo, character) = Arrange(level: 3);
        var handler = new RestAtLocationHubCommandHandler(
            repo.Object, NullLogger<RestAtLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new RestAtLocationHubCommand { CharacterId = character.Id, LocationId = "inn", CostInGold = 0 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().Be(30);
        result.MaxHealth.Should().Be(30);
        result.CurrentMana.Should().Be(15);
        result.GoldRemaining.Should().Be(0);
    }

    [Fact]
    public async Task AddGold_StartsFromZero_WhenTheAttributesBlobIsCorrupt()
    {
        var (repo, character) = Arrange();
        var handler = new AddGoldHubCommandHandler(
            repo.Object, NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new AddGoldHubCommand { CharacterId = character.Id, Amount = 25, Source = "Loot" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.GoldAdded.Should().Be(25);
        result.NewGoldTotal.Should().Be(25);
    }

    [Fact]
    public async Task CraftItem_ToleratesACorruptAttributesBlob_AndFailsOnAMissingRecipe()
    {
        // E5 replaced the flat-gold stub: crafting now reads the recipe catalog, so a corrupt blob
        // must be survivable rather than the reason for the failure.
        var (repo, character) = Arrange();
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Recipe?)null);

        var handler = new CraftItemHubCommandHandler(
            repo.Object,
            recipes.Object,
            Mock.Of<IMediator>(),
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, "iron-sword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No recipe");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UseItem_ReportsAMissingBagEntry_WhenTheInventoryBlobIsCorrupt()
    {
        var (repo, character) = Arrange();
        character.InventoryBlob = Corrupt;
        var items = new Mock<RealmEngine.Shared.Abstractions.IItemRepository>();
        var handler = new UseItemHubCommandHandler(
            repo.Object, items.Object, NullLogger<UseItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UseItemHubCommand { CharacterId = character.Id, ItemRef = "health_potion" },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("in your bag");
        items.Verify(r => r.GetBySlugAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetInventory_TreatsACorruptAttributesBlobAsEmpty()
    {
        var (repo, character) = Arrange();
        character.InventoryBlob = "[]";
        character.EquipmentBlob = "{}";
        var items = new Mock<RealmEngine.Shared.Abstractions.IItemRepository>();
        var handler = new GetInventoryHubCommandHandler(
            repo.Object, items.Object, NullLogger<GetInventoryHubCommandHandler>.Instance);

        var result = await handler.Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
        result.Equipped.Should().BeEmpty();
    }

    [Fact]
    public async Task AddGold_StampsTheLastPlayedTime()
    {
        var (repo, character) = Arrange();
        character.Attributes = """{"Gold":10}""";
        character.LastPlayedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var before = character.LastPlayedAt;
        var handler = new AddGoldHubCommandHandler(
            repo.Object, NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new AddGoldHubCommand { CharacterId = character.Id, Amount = -5 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewGoldTotal.Should().Be(5);
        character.LastPlayedAt.Should().BeAfter(before);
    }
}
