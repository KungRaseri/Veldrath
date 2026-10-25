using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Achievements.Commands;
using RealmEngine.Core.Features.Achievements.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Achievement.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for CheckAchievementProgressHandler - validates achievement progress checking.
/// </summary>
public class CheckAchievementProgressHandlerTests
{
    private readonly Mock<AchievementService> _mockAchievementService;
    private readonly CheckAchievementProgressHandler _handler;

    public CheckAchievementProgressHandlerTests()
    {
        _mockAchievementService = new Mock<AchievementService>();
        _handler = new CheckAchievementProgressHandler(_mockAchievementService.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Newly_Unlocked_Achievements()
    {
        // Arrange
        var newAchievements = new List<RealmEngine.Shared.Models.Achievement>
        {
            new()
            {
                Id = "first-quest",
                Title = "First Quest Complete",
                IsUnlocked = true
            },
            new()
            {
                Id = "level-10",
                Title = "Reach Level 10",
                IsUnlocked = true
            }
        };

        _mockAchievementService
            .Setup(x => x.CheckAllAchievementsAsync())
            .ReturnsAsync(newAchievements);

        var command = new CheckAchievementProgressCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Id == "first-quest");
        result.Should().Contain(a => a.Id == "level-10");
        result.Should().OnlyContain(a => a.IsUnlocked);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_New_Achievements()
    {
        // Arrange
        _mockAchievementService
            .Setup(x => x.CheckAllAchievementsAsync())
            .ReturnsAsync(new List<RealmEngine.Shared.Models.Achievement>());

        var command = new CheckAchievementProgressCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Call_CheckAllAchievementsAsync()
    {
        // Arrange
        _mockAchievementService
            .Setup(x => x.CheckAllAchievementsAsync())
            .ReturnsAsync(new List<RealmEngine.Shared.Models.Achievement>());

        var command = new CheckAchievementProgressCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockAchievementService.Verify(
            x => x.CheckAllAchievementsAsync(),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Multiple_Achievement_Types()
    {
        // Arrange
        var achievements = new List<RealmEngine.Shared.Models.Achievement>
        {
            new()
            {
                Id = "quest-achievement",
                Title = "Quest Master",
                Criteria = new AchievementCriteria { Type = AchievementType.CompleteQuest },
                IsUnlocked = true
            },
            new()
            {
                Id = "combat-achievement",
                Title = "Slayer",
                Criteria = new AchievementCriteria { Type = AchievementType.DefeatEnemies },
                IsUnlocked = true
            },
            new()
            {
                Id = "level-achievement",
                Title = "Hero",
                Criteria = new AchievementCriteria { Type = AchievementType.ReachLevel },
                IsUnlocked = true
            }
        };

        _mockAchievementService
            .Setup(x => x.CheckAllAchievementsAsync())
            .ReturnsAsync(achievements);

        var command = new CheckAchievementProgressCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.Criteria.Type == AchievementType.CompleteQuest);
        result.Should().Contain(a => a.Criteria.Type == AchievementType.DefeatEnemies);
        result.Should().Contain(a => a.Criteria.Type == AchievementType.ReachLevel);
    }

    [Fact]
    public async Task Handle_Should_Preserve_Achievement_Details()
    {
        // Arrange
        var achievement = new RealmEngine.Shared.Models.Achievement
        {
            Id = "test-achievement",
            Title = "Test Achievement",
            Description = "This is a test",
            Points = 50,
            IsUnlocked = true,
            UnlockedAt = DateTime.Now
        };

        _mockAchievementService
            .Setup(x => x.CheckAllAchievementsAsync())
            .ReturnsAsync(new List<RealmEngine.Shared.Models.Achievement> { achievement });

        var command = new CheckAchievementProgressCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var returned = result.First();
        returned.Id.Should().Be("test-achievement");
        returned.Title.Should().Be("Test Achievement");
        returned.Description.Should().Be("This is a test");
        returned.Points.Should().Be(50);
        returned.IsUnlocked.Should().BeTrue();
        returned.UnlockedAt.Should().NotBeNull();
    }
}
