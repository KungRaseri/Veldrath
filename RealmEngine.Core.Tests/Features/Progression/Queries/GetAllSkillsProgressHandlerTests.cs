using Moq;
using RealmEngine.Core.Features.Progression.Queries;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetAllSkillsProgressHandler.
/// </summary>
public class GetAllSkillsProgressHandlerTests
{
    private static async Task<GetAllSkillsProgressHandler> CreateHandlerAsync(IEnumerable<SkillDefinition>? skills = null)
    {
        var mockRepo = new Mock<ISkillRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((skills ?? []).ToList());
        var catalogSvc = new SkillDataService(mockRepo.Object);
        await catalogSvc.InitializeAsync();
        return new GetAllSkillsProgressHandler(new SkillProgressionService(catalogSvc));
    }

    private static SkillDefinition MakeSkillDef(string id, string category = "combat", int baseXP = 100) =>
        new() { SkillId = id, Name = id, DisplayName = id, Description = id, Category = category, BaseXPCost = baseXP };

    [Fact]
    public async Task Returns_EmptyList_WhenCatalogIsEmpty()
    {
        // Arrange
        var handler = await CreateHandlerAsync([]);
        var character = new Character { Name = "Hero" };
        var query = new GetAllSkillsProgressQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Skills.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_UntrainedEntry_WhenCharacterHasNoSkillData()
    {
        // Arrange - catalog has swords, character has no trained skills
        var handler = await CreateHandlerAsync([MakeSkillDef("swords")]);
        var character = new Character { Name = "Hero" };
        var query = new GetAllSkillsProgressQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Skills.Should().HaveCount(1);
        var swords = result.Skills[0];
        swords.SkillId.Should().Be("swords");
        swords.CurrentRank.Should().Be(0);
        swords.CurrentXP.Should().Be(0);
    }

    [Fact]
    public async Task Returns_TrainedEntry_WhenCharacterHasSkillData()
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeSkillDef("archery")]);
        var character = new Character { Name = "Aria" };
        character.Skills["archery"] = new CharacterSkill
        {
            SkillId = "archery",
            Name = "Archery",
            Category = "combat",
            CurrentRank = 7,
            CurrentXP = 55,
            XPToNextRank = 200
        };
        var query = new GetAllSkillsProgressQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Skills.Should().HaveCount(1);
        var archery = result.Skills[0];
        archery.CurrentRank.Should().Be(7);
        archery.CurrentXP.Should().Be(55);
        archery.ProgressPercent.Should().BeApproximately(27.5, 1.0, "55/200 = 27.5%");
    }

    [Fact]
    public async Task IncludesAllCatalogSkills_WhenCharacterHasSubset()
    {
        // Arrange - 3 skills in catalog, character has trained only one
        var handler = await CreateHandlerAsync([MakeSkillDef("swords"), MakeSkillDef("archery"), MakeSkillDef("stealth")]);
        var character = new Character { Name = "Hero" };
        character.Skills["swords"] = new CharacterSkill
        {
            SkillId = "swords",
            Name = "Swords",
            Category = "combat",
            CurrentRank = 10,
            CurrentXP = 0,
            XPToNextRank = 300
        };
        var query = new GetAllSkillsProgressQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - all 3 catalog skills should appear
        result.Skills.Should().HaveCount(3);
        result.Skills.Select(s => s.SkillId).Should().BeEquivalentTo(["swords", "archery", "stealth"]);
    }

    [Fact]
    public async Task SkillsOrderedByRankDescending_ThenByNameAscending()
    {
        // Arrange - 3 skills, one trained at rank 5, others at 0
        var handler = await CreateHandlerAsync([MakeSkillDef("zzz_skill"), MakeSkillDef("aaa_skill"), MakeSkillDef("mmm_skill")]);
        var character = new Character { Name = "Hero" };
        character.Skills["mmm_skill"] = new CharacterSkill
        {
            SkillId = "mmm_skill",
            Name = "mmm_skill",
            Category = "combat",
            CurrentRank = 5,
            CurrentXP = 0,
            XPToNextRank = 200
        };
        var query = new GetAllSkillsProgressQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - mmm_skill should be first (rank 5), then aaa_skill (rank 0), then zzz_skill (rank 0)
        result.Skills[0].SkillId.Should().Be("mmm_skill", "highest rank should be first");
        result.Skills[1].SkillId.Should().Be("aaa_skill", "rank 0 skills sorted by name ascending");
        result.Skills[2].SkillId.Should().Be("zzz_skill");
    }

    [Fact]
    public async Task ProgressPercent_IsZero_WhenSkillIsUnlearned()
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeSkillDef("fishing")]);
        var character = new Character { Name = "Hero" };
        var query = new GetAllSkillsProgressQuery { Character = character };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Skills[0].ProgressPercent.Should().Be(0);
    }
}
