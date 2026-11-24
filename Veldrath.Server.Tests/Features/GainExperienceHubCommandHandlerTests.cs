using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.LevelUp;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for <see cref="GainExperienceHubCommandHandler"/>: the rolling <c>Level × 100</c> XP formula, the
/// validation guards, and the persistence of the new level and XP total.
/// </summary>
public class GainExperienceHubCommandHandlerTests
{
    private static (GainExperienceHubCommandHandler Handler, Mock<ICharacterRepository> Repo, Character Character)
        Arrange(long experience = 0, int level = 1, string? attributes = null)
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Experience = experience,
            Attributes = attributes ?? string.Empty,
        };

        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>())).ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GainExperienceHubCommandHandler(
            repo.Object,
            NullLogger<GainExperienceHubCommandHandler>.Instance);

        return (handler, repo, character);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public async Task Handle_RejectsNonPositiveAwards(int amount)
    {
        var (handler, repo, _) = Arrange();

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = Guid.NewGuid(), Amount = amount },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Experience amount must be positive");
        repo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsAnError_WhenTheCharacterDoesNotExist()
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Character?)null);
        var handler = new GainExperienceHubCommandHandler(
            repo.Object,
            NullLogger<GainExperienceHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = Guid.NewGuid(), Amount = 50 },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Character not found");
    }

    [Fact]
    public async Task Handle_AccumulatesExperienceWithoutLevellingUp()
    {
        var (handler, repo, character) = Arrange(experience: 30, level: 2);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 40, Source = "Combat" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(2);
        result.NewExperience.Should().Be(70);
        result.LeveledUp.Should().BeFalse();
        result.LeveledUpTo.Should().BeNull();
        character.Experience.Should().Be(70);
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LevelsUp_WhenTheAwardMeetsTheThresholdExactly()
    {
        var (handler, _, character) = Arrange(experience: 90, level: 1);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(2);
        result.NewExperience.Should().Be(0);
        result.LeveledUp.Should().BeTrue();
        result.LeveledUpTo.Should().Be(2);
        character.Level.Should().Be(2);
    }

    [Fact]
    public async Task Handle_LevelsUpRepeatedly_WhenTheAwardCarriesOver()
    {
        var (handler, _, character) = Arrange(experience: 50, level: 1);

        // 1 → 2 costs 100, 2 → 3 costs 200, 3 → 4 costs 300: 50 + 500 leaves 250 toward level 4.
        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 500 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(3);
        result.NewExperience.Should().Be(250);
        result.LeveledUp.Should().BeTrue();
        result.LeveledUpTo.Should().Be(3);
    }

    [Fact]
    public async Task Handle_LevelUp_RecomputesTheDerivedPoolsInTheAttributesBlob()
    {
        var attributes = JsonSerializer.Serialize(new Dictionary<string, int>
        {
            ["Constitution"] = 12,
            ["Wisdom"] = 10,
            ["CurrentHealth"] = 100,
            ["MaxHealth"] = 120,
            ["CurrentMana"] = 20,
            ["MaxMana"] = 50,
        });
        var (handler, _, character) = Arrange(experience: 90, level: 1, attributes: attributes);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeTrue();

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        // The engine formulas the creation wizard uses: Constitution × 10 + level × 5, Wisdom × 5 + level × 3.
        stored["MaxHealth"].Should().Be(130);
        stored["MaxMana"].Should().Be(56);

        // A bigger bar is a ceiling, not a heal: the current pools keep the value the fight left them at.
        stored["CurrentHealth"].Should().Be(100);
        stored["CurrentMana"].Should().Be(20);

        result.MaxHealth.Should().Be(130);
        result.CurrentHealth.Should().Be(100);
        result.MaxMana.Should().Be(56);
        result.CurrentMana.Should().Be(20);
    }

    [Fact]
    public async Task Handle_LevelUp_RebuildsACorruptAttributesBlobFromTheLevel()
    {
        var (handler, _, character) = Arrange(experience: 90, level: 1, attributes: "{not json");

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 10 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeTrue();

        var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        // No Constitution/Wisdom in the blob, so the engine defaults of 10 apply at the new level.
        stored["MaxHealth"].Should().Be(110);
        stored["MaxMana"].Should().Be(56);
    }

    [Fact]
    public async Task Handle_AwardInsideTheLevel_DoesNotRewriteTheAttributesBlob()
    {
        const string attributes = "{not json";
        var (handler, repo, character) = Arrange(experience: 30, level: 2, attributes: attributes);

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 40 },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeFalse();
        character.Attributes.Should().Be(attributes, "only a level-up changes the derived pools");
        repo.Verify(r => r.UpdateAsync(character, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StampsTheLastPlayedTime()
    {
        var (handler, _, character) = Arrange(experience: 0, level: 3);
        character.LastPlayedAt = DateTimeOffset.UtcNow.AddDays(-7);
        var before = character.LastPlayedAt;

        var result = await handler.Handle(
            new GainExperienceHubCommand { CharacterId = character.Id, Amount = 5, Source = null },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewLevel.Should().Be(3);
        result.NewExperience.Should().Be(5);
        character.LastPlayedAt.Should().BeAfter(before);
    }
}
