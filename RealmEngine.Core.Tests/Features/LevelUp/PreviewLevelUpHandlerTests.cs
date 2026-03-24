using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.LevelUp.Queries;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.LevelUp;

/// <summary>
/// Unit tests for <see cref="PreviewLevelUpHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class PreviewLevelUpHandlerTests
{
    private static PreviewLevelUpHandler CreateHandler(SaveGame? save = null, ICharacterClassRepository? classRepo = null)
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(save);
        return new PreviewLevelUpHandler(
            mockSave.Object,
            new LevelUpService(Mock.Of<ISkillRepository>()),
            classRepo ?? Mock.Of<ICharacterClassRepository>(),
            NullLogger<PreviewLevelUpHandler>.Instance);
    }

    private static SaveGame SaveWith(string name = "Hero", int level = 1, int xp = 0, string className = "Warrior") =>
        new() { Character = new Character { Name = name, Level = level, Experience = xp, ClassName = className } };

    // Validation
    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameEmpty()
    {
        var handler = CreateHandler();
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "" }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenWhitespaceCharacterName()
    {
        var handler = CreateHandler();
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "   " }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var handler = CreateHandler(save: null);
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameMismatch()
    {
        var handler = CreateHandler(SaveWith("Frodo"));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Gandalf" }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // Happy paths
    [Fact]
    public async Task Handle_ReturnsCorrectLevels_ForLevel1Character()
    {
        var handler = CreateHandler(SaveWith("Hero", level: 1, xp: 0));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.CurrentLevel.Should().Be(1);
        result.NextLevel.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ReturnsStatGains_WithHealthAndMana()
    {
        var handler = CreateHandler(SaveWith("Hero", level: 3, xp: 0));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.StatGains.Should().ContainKey("MaxHealth");
        result.StatGains.Should().ContainKey("MaxMana");
        result.StatGains["MaxHealth"].Should().BePositive();
        result.StatGains["MaxMana"].Should().BePositive();
    }

    [Fact]
    public async Task Handle_ReturnsAttributeAndSkillPoints()
    {
        var handler = CreateHandler(SaveWith("Hero", level: 1, xp: 0));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.AttributePointsGain.Should().BeGreaterThan(0);
        result.SkillPointsGain.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_ReturnsCanLevelUp_WhenSufficientXP()
    {
        // Level 1 requires 100 XP (level * 100)
        var handler = CreateHandler(SaveWith("Hero", level: 1, xp: 100));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.CanLevelUp.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsCannotLevelUp_WhenInsufficientXP()
    {
        var handler = CreateHandler(SaveWith("Hero", level: 1, xp: 50));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.CanLevelUp.Should().BeFalse();
        result.RequiredExperience.Should().Be(100);
    }

    [Fact]
    public async Task Handle_ReturnsRequiredXP_BasedOnCharacterLevel()
    {
        // Level N requires N*100 XP
        var handler = CreateHandler(SaveWith("Hero", level: 5, xp: 0));
        var result = await handler.Handle(new PreviewLevelUpQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.RequiredExperience.Should().Be(500);
    }
}
