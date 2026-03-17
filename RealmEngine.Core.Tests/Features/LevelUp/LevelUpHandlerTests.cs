using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.LevelUp.Commands;
using RealmEngine.Core.Features.LevelUp.Queries;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.LevelUp;

[Trait("Category", "Feature")]
public class GainExperienceHandlerTests
{
    private static (GainExperienceHandler Handler, Mock<ISaveGameService> SaveSvc) CreateHandler(SaveGame? save = null)
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        var handler = new GainExperienceHandler(
            mockSaveSvc.Object,
            new LevelUpService(),
            NullLogger<GainExperienceHandler>.Instance,
            Mock.Of<MediatR.IMediator>());
        return (handler, mockSaveSvc);
    }

    private static SaveGame SaveWith(string name = "Hero", int level = 1, int xp = 0) =>
        new() { Character = new Character { Name = name, Level = level, Experience = xp } };

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameEmpty()
    {
        var (handler, _) = CreateHandler();
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "", ExperienceAmount = 10 }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenExperienceNotPositive()
    {
        var (handler, _) = CreateHandler();
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "Hero", ExperienceAmount = 0 }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNegativeExperience()
    {
        var (handler, _) = CreateHandler();
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "Hero", ExperienceAmount = -50 }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSession()
    {
        var (handler, _) = CreateHandler(save: null);
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "Hero", ExperienceAmount = 10 }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameMismatch()
    {
        var (handler, _) = CreateHandler(SaveWith("Frodo"));
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "Gandalf", ExperienceAmount = 10 }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AwardsExperience_WithoutLevelUp()
    {
        var save = SaveWith("Hero", level: 1, xp: 0);
        var (handler, _) = CreateHandler(save);
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "Hero", ExperienceAmount = 50, Source = "Combat" }, default);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeFalse();
        result.NewExperience.Should().Be(50);
        result.CurrentLevel.Should().Be(1);
        result.ExperienceToNextLevel.Should().Be(50); // level 1 requires 100 XP, 50 remaining
    }

    [Fact]
    public async Task Handle_DetectsLevelUp_WhenXPThresholdCrossed()
    {
        // Level 1 requires 100 XP to level up
        var save = SaveWith("Hero", level: 1, xp: 90);
        var (handler, _) = CreateHandler(save);
        var result = await handler.Handle(new GainExperienceCommand { CharacterName = "Hero", ExperienceAmount = 20 }, default);

        result.Success.Should().BeTrue();
        result.LeveledUp.Should().BeTrue();
        result.NewLevel.Should().Be(2);
    }
}

[Trait("Category", "Feature")]
public class LevelUpHandlerTests
{
    private static LevelUpHandler CreateHandler(SaveGame? save = null)
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        return new LevelUpHandler(
            mockSaveSvc.Object,
            new LevelUpService(),
            Mock.Of<ICharacterClassRepository>(),
            NullLogger<LevelUpHandler>.Instance,
            Mock.Of<MediatR.IMediator>());
    }

    private static SaveGame SaveWithXP(string name = "Hero", int level = 1, int xp = 0) =>
        new() { Character = new Character { Name = name, Level = level, Experience = xp } };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameEmpty()
    {
        var result = await CreateHandler().Handle(new LevelUpCommand { CharacterName = "" }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSession()
    {
        var result = await CreateHandler(save: null).Handle(new LevelUpCommand { CharacterName = "Hero" }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNotFound()
    {
        var result = await CreateHandler(SaveWithXP("Frodo")).Handle(new LevelUpCommand { CharacterName = "Gandalf" }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenInsufficientXP()
    {
        // Level 1 needs 100 XP; character only has 50
        var result = await CreateHandler(SaveWithXP("Hero", level: 1, xp: 50)).Handle(new LevelUpCommand { CharacterName = "Hero" }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Insufficient experience");
    }

    [Fact]
    public async Task Handle_Succeeds_WhenSufficientXP()
    {
        // Level 1 needs 100 XP; give exactly 100
        var save = SaveWithXP("Hero", level: 1, xp: 100);
        var result = await CreateHandler(save).Handle(new LevelUpCommand { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.OldLevel.Should().Be(1);
        result.NewLevel.Should().Be(2);
        result.AttributePointsGained.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Feature")]
public class AllocateAttributePointsHandlerTests
{
    private static (AllocateAttributePointsHandler Handler, Character Character) CreateHandler(int unspentPoints = 5, string name = "Hero")
    {
        var character = new Character { Name = name, UnspentAttributePoints = unspentPoints };
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(new SaveGame { Character = character });
        var handler = new AllocateAttributePointsHandler(mockSaveSvc.Object, NullLogger<AllocateAttributePointsHandler>.Instance);
        return (handler, character);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameEmpty()
    {
        var (handler, _) = CreateHandler();
        var result = await handler.Handle(new AllocateAttributePointsCommand { CharacterName = "" }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenAllocationsEmpty()
    {
        var (handler, _) = CreateHandler();
        var result = await handler.Handle(new AllocateAttributePointsCommand { CharacterName = "Hero", AttributeAllocations = [] }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNegativeAllocation()
    {
        var (handler, _) = CreateHandler();
        var result = await handler.Handle(new AllocateAttributePointsCommand
        {
            CharacterName = "Hero",
            AttributeAllocations = new() { ["Strength"] = -1 }
        }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("negative");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenInsufficientPoints()
    {
        var (handler, _) = CreateHandler(unspentPoints: 2);
        var result = await handler.Handle(new AllocateAttributePointsCommand
        {
            CharacterName = "Hero",
            AttributeAllocations = new() { ["Strength"] = 3 }
        }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Insufficient attribute points");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSession()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = new AllocateAttributePointsHandler(mockSaveSvc.Object, NullLogger<AllocateAttributePointsHandler>.Instance);
        var result = await handler.Handle(new AllocateAttributePointsCommand
        {
            CharacterName = "Hero",
            AttributeAllocations = new() { ["Strength"] = 1 }
        }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AllocatesStrength_Successfully()
    {
        var (handler, character) = CreateHandler(unspentPoints: 5);
        character.Strength = 10;
        var result = await handler.Handle(new AllocateAttributePointsCommand
        {
            CharacterName = "Hero",
            AttributeAllocations = new() { ["Strength"] = 2 }
        }, default);
        result.Success.Should().BeTrue();
        result.PointsSpent.Should().Be(2);
        result.RemainingPoints.Should().Be(3);
        character.Strength.Should().Be(12);
    }

    [Fact]
    public async Task Handle_AllocatesMultipleAttributes_Successfully()
    {
        var (handler, character) = CreateHandler(unspentPoints: 5);
        character.Dexterity = 8;
        character.Intelligence = 10;
        var result = await handler.Handle(new AllocateAttributePointsCommand
        {
            CharacterName = "Hero",
            AttributeAllocations = new() { ["Dexterity"] = 2, ["Intelligence"] = 3 }
        }, default);
        result.Success.Should().BeTrue();
        result.PointsSpent.Should().Be(5);
        result.RemainingPoints.Should().Be(0);
        character.Dexterity.Should().Be(10);
        character.Intelligence.Should().Be(13);
    }
}

[Trait("Category", "Feature")]
public class GetCharacterProgressionHandlerTests
{
    private static GetCharacterProgressionHandler CreateHandler(SaveGame? save = null)
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        return new GetCharacterProgressionHandler(mockSaveSvc.Object, new LevelUpService(), NullLogger<GetCharacterProgressionHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameEmpty()
    {
        var result = await CreateHandler().Handle(new GetCharacterProgressionQuery { CharacterName = "" }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSession()
    {
        var result = await CreateHandler().Handle(new GetCharacterProgressionQuery { CharacterName = "Hero" }, default);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active game session");
    }

    [Fact]
    public async Task Handle_ReturnsProgression_WithCoreStats()
    {
        var save = new SaveGame
        {
            Character = new Character
            {
                Name = "Aria",
                Level = 3,
                Experience = 150,
                Strength = 12,
                Dexterity = 14,
                UnspentAttributePoints = 2,
                UnspentSkillPoints = 1
            }
        };
        var result = await CreateHandler(save).Handle(new GetCharacterProgressionQuery { CharacterName = "Aria" }, default);

        result.Success.Should().BeTrue();
        result.Level.Should().Be(3);
        result.Experience.Should().Be(150);
        result.UnallocatedAttributePoints.Should().Be(2);
        result.UnallocatedSkillPoints.Should().Be(1);
        result.Attributes["Strength"].Should().Be(12);
        result.Attributes["Dexterity"].Should().Be(14);
    }
}

[Trait("Category", "Feature")]
public class GetNextLevelRequirementHandlerTests
{
    private static GetNextLevelRequirementHandler CreateHandler(SaveGame? save = null)
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        return new GetNextLevelRequirementHandler(mockSaveSvc.Object, new LevelUpService(), NullLogger<GetNextLevelRequirementHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCharacterNameEmpty()
    {
        var result = await CreateHandler().Handle(new GetNextLevelRequirementQuery { CharacterName = "" }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSession()
    {
        var result = await CreateHandler().Handle(new GetNextLevelRequirementQuery { CharacterName = "Hero" }, default);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsRequirements_ForLevel1Character()
    {
        // Level 1 → needs 100 XP; has 0 → 100 remaining, 0% progress
        var save = new SaveGame { Character = new Character { Name = "Hero", Level = 1, Experience = 0 } };
        var result = await CreateHandler(save).Handle(new GetNextLevelRequirementQuery { CharacterName = "Hero" }, default);

        result.Success.Should().BeTrue();
        result.CurrentLevel.Should().Be(1);
        result.CurrentExperience.Should().Be(0);
        result.RequiredExperience.Should().Be(100);
        result.RemainingExperience.Should().Be(100);
        result.ProgressPercentage.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_ReturnsHalfProgress_ForLevel1CharacterWith50XP()
    {
        var save = new SaveGame { Character = new Character { Name = "Hero", Level = 1, Experience = 50 } };
        var result = await CreateHandler(save).Handle(new GetNextLevelRequirementQuery { CharacterName = "Hero" }, default);

        result.RemainingExperience.Should().Be(50);
        result.ProgressPercentage.Should().BeApproximately(50.0, 0.1);
    }
}
