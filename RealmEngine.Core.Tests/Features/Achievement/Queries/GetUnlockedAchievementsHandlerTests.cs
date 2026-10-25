using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Achievements.Queries;
using RealmEngine.Core.Features.Achievements.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Achievement.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetUnlockedAchievementsHandler - validates retrieval of unlocked achievements.
/// </summary>
public class GetUnlockedAchievementsHandlerTests
{
    private readonly Mock<AchievementService> _mockAchievementService;
    private readonly GetUnlockedAchievementsHandler _handler;

    public GetUnlockedAchievementsHandlerTests()
    {
        _mockAchievementService = new Mock<AchievementService>();
        _handler = new GetUnlockedAchievementsHandler(_mockAchievementService.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Unlocked_Achievements()
    {
        // Arrange
        var unlockedAchievements = new List<RealmEngine.Shared.Models.Achievement>
        {
            new()
            {
                Id = "first-quest",
                Title = "First Quest",
                IsUnlocked = true
            },
            new()
            {
                Id = "level-5",
                Title = "Reach Level 5",
                IsUnlocked = true
            }
        };

        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(unlockedAchievements);

        var query = new GetUnlockedAchievementsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.IsUnlocked);
        result.Should().Contain(a => a.Id == "first-quest");
        result.Should().Contain(a => a.Id == "level-5");
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Achievements_Unlocked()
    {
        // Arrange
        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(new List<RealmEngine.Shared.Models.Achievement>());

        var query = new GetUnlockedAchievementsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Call_GetUnlockedAchievementsAsync()
    {
        // Arrange
        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(new List<RealmEngine.Shared.Models.Achievement>());

        var query = new GetUnlockedAchievementsQuery();

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockAchievementService.Verify(
            x => x.GetUnlockedAchievementsAsync(),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Achievements_With_Unlock_Times()
    {
        // Arrange
        var unlockTime1 = DateTime.Now.AddDays(-5);
        var unlockTime2 = DateTime.Now.AddDays(-2);

        var achievements = new List<RealmEngine.Shared.Models.Achievement>
        {
            new()
            {
                Id = "old-achievement",
                Title = "Old Achievement",
                IsUnlocked = true,
                UnlockedAt = unlockTime1
            },
            new()
            {
                Id = "recent-achievement",
                Title = "Recent Achievement",
                IsUnlocked = true,
                UnlockedAt = unlockTime2
            }
        };

        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(achievements);

        var query = new GetUnlockedAchievementsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.UnlockedAt.HasValue);
        result.First(a => a.Id == "old-achievement").UnlockedAt.Should().Be(unlockTime1);
        result.First(a => a.Id == "recent-achievement").UnlockedAt.Should().Be(unlockTime2);
    }

    [Fact]
    public async Task Handle_Should_Return_Different_Achievement_Categories()
    {
        // Arrange
        var achievements = new List<RealmEngine.Shared.Models.Achievement>
        {
            new()
            {
                Id = "exploration-achievement",
                Title = "Explorer",
                Category = AchievementCategory.Exploration,
                IsUnlocked = true
            },
            new()
            {
                Id = "combat-achievement",
                Title = "Warrior",
                Category = AchievementCategory.Combat,
                IsUnlocked = true
            },
            new()
            {
                Id = "progression-achievement",
                Title = "Hero",
                Category = AchievementCategory.Quests,
                IsUnlocked = true
            }
        };

        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(achievements);

        var query = new GetUnlockedAchievementsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.Category == AchievementCategory.Exploration);
        result.Should().Contain(a => a.Category == AchievementCategory.Combat);
        result.Should().Contain(a => a.Category == AchievementCategory.Quests);
    }

    [Fact]
    public async Task Handle_Should_Preserve_Achievement_Points()
    {
        // Arrange
        var achievements = new List<RealmEngine.Shared.Models.Achievement>
        {
            new()
            {
                Id = "achievement-1",
                Title = "Achievement 1",
                Points = 10,
                IsUnlocked = true
            },
            new()
            {
                Id = "achievement-2",
                Title = "Achievement 2",
                Points = 50,
                IsUnlocked = true
            },
            new()
            {
                Id = "achievement-3",
                Title = "Achievement 3",
                Points = 100,
                IsUnlocked = true
            }
        };

        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(achievements);

        var query = new GetUnlockedAchievementsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var totalPoints = result.Sum(a => a.Points);
        totalPoints.Should().Be(160);
        result.Should().Contain(a => a.Points == 10);
        result.Should().Contain(a => a.Points == 50);
        result.Should().Contain(a => a.Points == 100);
    }

    [Fact]
    public async Task Handle_Should_Return_Achievements_With_Complete_Details()
    {
        // Arrange
        var achievement = new RealmEngine.Shared.Models.Achievement
        {
            Id = "complete-achievement",
            Title = "Complete Achievement",
            Description = "This achievement has all details",
            Points = 75,
            Category = AchievementCategory.Combat,
            IsUnlocked = true,
            UnlockedAt = DateTime.Now,
            Criteria = new AchievementCriteria
            {
                Type = AchievementType.DefeatEnemies,
                RequiredValue = 100
            }
        };

        _mockAchievementService
            .Setup(x => x.GetUnlockedAchievementsAsync())
            .ReturnsAsync(new List<RealmEngine.Shared.Models.Achievement> { achievement });

        var query = new GetUnlockedAchievementsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var returned = result.First();
        returned.Id.Should().Be("complete-achievement");
        returned.Title.Should().Be("Complete Achievement");
        returned.Description.Should().Be("This achievement has all details");
        returned.Points.Should().Be(75);
        returned.Category.Should().Be(AchievementCategory.Combat);
        returned.IsUnlocked.Should().BeTrue();
        returned.UnlockedAt.Should().NotBeNull();
        returned.Criteria.Type.Should().Be(AchievementType.DefeatEnemies);
        returned.Criteria.RequiredValue.Should().Be(100);
    }
}
