using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Services;

/// <summary>
/// Tests for <see cref="SkillProgressionService"/>.
/// </summary>
public class SkillProgressionServiceTests
{
    private static async Task<SkillDataService> BuildSkillDataServiceAsync(params SkillDefinition[] skills)
    {
        var mockRepo = new Mock<ISkillRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(skills.ToList());
        var service = new SkillDataService(mockRepo.Object, NullLogger<SkillDataService>.Instance);
        await service.InitializeAsync();
        return service;
    }

    private static SkillDefinition MakeSkillDef(string id) =>
        new()
        {
            SkillId = id,
            Name = id,
            DisplayName = id,
            Description = id,
            Category = "combat",
            BaseXPCost = 1_000,
            CostMultiplier = 0.5,
            MaxRank = 5
        };

    /// <summary>
    /// Production awards the same source repeatedly (every weapon hit, every successful cast), so
    /// the "source already recorded" arm of the XP-source check must be covered by a direct,
    /// deterministic test rather than borrowed from an unseeded combat loop.
    /// </summary>
    [Fact]
    public async Task AwardSkillXP_AccumulatesXPSource_WhenTheSameSourceIsAwardedTwice()
    {
        // Arrange
        var service = new SkillProgressionService(await BuildSkillDataServiceAsync(MakeSkillDef("swords")));
        var character = new Character { Name = "Hero" };

        // Act — the second award re-uses an action source the skill has already recorded.
        var first = service.AwardSkillXP(character, "swords", 30, "combat");
        var second = service.AwardSkillXP(character, "swords", 20, "combat");

        // Assert
        first.RanksGained.Should().Be(0);
        second.RanksGained.Should().Be(0);
        character.Skills["swords"].XPSources.Should().ContainKey("combat").WhoseValue.Should().Be(50);
        character.Skills["swords"].XPSources.Should().HaveCount(1);
        character.Skills["swords"].CurrentXP.Should().Be(50);
        character.Skills["swords"].TotalXP.Should().Be(50);
    }
}
