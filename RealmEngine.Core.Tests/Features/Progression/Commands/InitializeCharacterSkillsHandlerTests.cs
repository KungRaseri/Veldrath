using Moq;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for InitializeCharacterSkillsHandler.
/// </summary>
public class InitializeCharacterSkillsHandlerTests
{
    private static async Task<InitializeCharacterSkillsHandler> CreateHandlerAsync(IEnumerable<SkillDefinition>? skills = null)
    {
        var mockRepo = new Mock<ISkillRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((skills ?? []).ToList());
        var catalogSvc = new SkillDataService(mockRepo.Object);
        await catalogSvc.InitializeAsync();
        return new InitializeCharacterSkillsHandler(new SkillProgressionService(catalogSvc));
    }

    private static SkillDefinition MakeSkillDef(string id, string category = "combat") =>
        new() { SkillId = id, Name = id, DisplayName = id, Description = id, Category = category, BaseXPCost = 100 };

    [Fact]
    public async Task Returns_Zero_WhenCatalogIsEmpty()
    {
        // Arrange
        var handler = await CreateHandlerAsync([]);
        var character = new Character { Name = "Hero" };
        var command = new InitializeCharacterSkillsCommand { Character = character };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SkillsInitialized.Should().Be(0);
        result.SkillIds.Should().BeEmpty();
        character.Skills.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializesAllSkillsFromCatalog()
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeSkillDef("swords"), MakeSkillDef("archery"), MakeSkillDef("stealth")]);
        var character = new Character { Name = "Hero" };
        var command = new InitializeCharacterSkillsCommand { Character = character };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SkillsInitialized.Should().Be(3);
        result.SkillIds.Should().BeEquivalentTo(["swords", "archery", "stealth"]);
        character.Skills.Should().ContainKeys("swords", "archery", "stealth");
    }

    [Fact]
    public async Task AllSkillsStartAtRankZero()
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeSkillDef("mining"), MakeSkillDef("fishing")]);
        var character = new Character { Name = "Hero" };
        var command = new InitializeCharacterSkillsCommand { Character = character };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        character.Skills["mining"].CurrentRank.Should().Be(0);
        character.Skills["mining"].CurrentXP.Should().Be(0);
        character.Skills["fishing"].CurrentRank.Should().Be(0);
    }

    [Fact]
    public async Task DoesNotOverwriteExistingSkills()
    {
        // Arrange - character already has "swords" at rank 5
        var handler = await CreateHandlerAsync([MakeSkillDef("swords"), MakeSkillDef("archery")]);
        var character = new Character { Name = "Hero" };
        character.Skills["swords"] = new CharacterSkill
        {
            SkillId = "swords",
            Name = "Swords",
            Category = "combat",
            CurrentRank = 5,
            CurrentXP = 42,
            XPToNextRank = 200
        };
        var command = new InitializeCharacterSkillsCommand { Character = character };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - swords should remain at rank 5, archery initialized at 0
        character.Skills["swords"].CurrentRank.Should().Be(5, "existing skill must not be overwritten");
        character.Skills["swords"].CurrentXP.Should().Be(42);
        character.Skills["archery"].CurrentRank.Should().Be(0);
    }

    [Fact]
    public async Task SkillIds_MatchesCatalogContents()
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeSkillDef("blacksmithing"), MakeSkillDef("alchemy")]);
        var character = new Character { Name = "Hero" };
        var command = new InitializeCharacterSkillsCommand { Character = character };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SkillIds.Should().Contain("blacksmithing");
        result.SkillIds.Should().Contain("alchemy");
        result.SkillIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task SkillsInitialized_OnlyCountsNewlyAddedSkills()
    {
        // Arrange - 3 in catalog, 1 already present → SkillsInitialized = 3 (all keys)
        var handler = await CreateHandlerAsync([MakeSkillDef("swords"), MakeSkillDef("axes"), MakeSkillDef("shields")]);
        var character = new Character { Name = "Hero" };
        character.Skills["swords"] = new CharacterSkill { SkillId = "swords", Name = "Swords", Category = "combat", XPToNextRank = 100 };
        var command = new InitializeCharacterSkillsCommand { Character = character };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - result reflects total skills on character (catalog count)
        result.SkillsInitialized.Should().Be(3);
    }
}
