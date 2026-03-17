using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Victory.Commands;
using RealmEngine.Core.Features.Victory.Services;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Victory.Commands;

public class TriggerVictoryHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Success_With_Statistics_When_Victory_Achieved()
    {
        // Arrange
        var mockVictoryService = new Mock<VictoryService>(MockBehavior.Strict, (object)null!, NullLogger<VictoryService>.Instance);

        var expectedStatistics = new VictoryStatistics(
            PlayerName: "Legendary Hero",
            ClassName: "Paladin",
            FinalLevel: 50,
            Difficulty: "Nightmare",
            PlayTimeMinutes: 3600,
            QuestsCompleted: 75,
            EnemiesDefeated: 2500,
            DeathCount: 5,
            AchievementsUnlocked: 45,
            TotalGoldEarned: 500000
        );

        mockVictoryService.Setup(x => x.CalculateVictoryStatisticsAsync())
            .ReturnsAsync(expectedStatistics);
        mockVictoryService.Setup(x => x.MarkGameCompleteAsync())
            .Returns(Task.CompletedTask);

        var handler = new TriggerVictoryHandler(mockVictoryService.Object);
        var command = new TriggerVictoryCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Statistics.Should().NotBeNull();
        result.Statistics!.PlayerName.Should().Be("Legendary Hero");
        result.Statistics.ClassName.Should().Be("Paladin");
        result.Statistics.FinalLevel.Should().Be(50);
        result.Statistics.Difficulty.Should().Be("Nightmare");
        result.Statistics.PlayTimeMinutes.Should().Be(3600);
        result.Statistics.QuestsCompleted.Should().Be(75);
        result.Statistics.EnemiesDefeated.Should().Be(2500);
        result.Statistics.DeathCount.Should().Be(5);
        result.Statistics.AchievementsUnlocked.Should().Be(45);
        result.Statistics.TotalGoldEarned.Should().Be(500000);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_No_Statistics_Available()
    {
        // Arrange
        var mockVictoryService = new Mock<VictoryService>(MockBehavior.Strict, (object)null!, NullLogger<VictoryService>.Instance);

        mockVictoryService.Setup(x => x.CalculateVictoryStatisticsAsync())
            .ReturnsAsync((VictoryStatistics?)null);

        var handler = new TriggerVictoryHandler(mockVictoryService.Object);
        var command = new TriggerVictoryCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Statistics.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Call_CalculateVictoryStatisticsAsync()
    {
        // Arrange
        var mockVictoryService = new Mock<VictoryService>(MockBehavior.Strict, (object)null!, NullLogger<VictoryService>.Instance);

        var statistics = new VictoryStatistics(
            PlayerName: "Hero",
            ClassName: "Warrior",
            FinalLevel: 20,
            Difficulty: "Normal",
            PlayTimeMinutes: 600,
            QuestsCompleted: 10,
            EnemiesDefeated: 100,
            DeathCount: 2,
            AchievementsUnlocked: 5,
            TotalGoldEarned: 10000
        );

        mockVictoryService.Setup(x => x.CalculateVictoryStatisticsAsync())
            .ReturnsAsync(statistics);
        mockVictoryService.Setup(x => x.MarkGameCompleteAsync())
            .Returns(Task.CompletedTask);

        var handler = new TriggerVictoryHandler(mockVictoryService.Object);
        var command = new TriggerVictoryCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockVictoryService.Verify(x => x.CalculateVictoryStatisticsAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Call_MarkGameCompleteAsync_When_Statistics_Available()
    {
        // Arrange
        var mockVictoryService = new Mock<VictoryService>(MockBehavior.Strict, (object)null!, NullLogger<VictoryService>.Instance);

        var statistics = new VictoryStatistics(
            PlayerName: "Champion",
            ClassName: "Mage",
            FinalLevel: 30,
            Difficulty: "Hard",
            PlayTimeMinutes: 1200,
            QuestsCompleted: 25,
            EnemiesDefeated: 500,
            DeathCount: 3,
            AchievementsUnlocked: 15,
            TotalGoldEarned: 50000
        );

        mockVictoryService.Setup(x => x.CalculateVictoryStatisticsAsync())
            .ReturnsAsync(statistics);
        mockVictoryService.Setup(x => x.MarkGameCompleteAsync())
            .Returns(Task.CompletedTask);

        var handler = new TriggerVictoryHandler(mockVictoryService.Object);
        var command = new TriggerVictoryCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockVictoryService.Verify(x => x.MarkGameCompleteAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Not_Call_MarkGameCompleteAsync_When_No_Statistics()
    {
        // Arrange
        var mockVictoryService = new Mock<VictoryService>(MockBehavior.Strict, (object)null!, NullLogger<VictoryService>.Instance);

        mockVictoryService.Setup(x => x.CalculateVictoryStatisticsAsync())
            .ReturnsAsync((VictoryStatistics?)null);

        var handler = new TriggerVictoryHandler(mockVictoryService.Object);
        var command = new TriggerVictoryCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockVictoryService.Verify(x => x.MarkGameCompleteAsync(), Times.Never);
    }

    [Theory]
    [InlineData("Normal", 1, 50, 5, 100)]
    [InlineData("Hard", 5, 500, 50, 1000)]
    [InlineData("Nightmare", 15, 2000, 100, 5000)]
    [InlineData("Permadeath", 0, 5000, 200, 10000)]
    public async Task Handle_Should_Work_With_Different_Difficulty_Levels(
        string difficulty, int deathCount, int enemiesDefeated, int questsCompleted, int goldEarned)
    {
        // Arrange
        var mockVictoryService = new Mock<VictoryService>(MockBehavior.Strict, (object)null!, NullLogger<VictoryService>.Instance);

        var statistics = new VictoryStatistics(
            PlayerName: "TestHero",
            ClassName: "TestClass",
            FinalLevel: 50,
            Difficulty: difficulty,
            PlayTimeMinutes: 1000,
            QuestsCompleted: questsCompleted,
            EnemiesDefeated: enemiesDefeated,
            DeathCount: deathCount,
            AchievementsUnlocked: 10,
            TotalGoldEarned: goldEarned
        );

        mockVictoryService.Setup(x => x.CalculateVictoryStatisticsAsync())
            .ReturnsAsync(statistics);
        mockVictoryService.Setup(x => x.MarkGameCompleteAsync())
            .Returns(Task.CompletedTask);

        var handler = new TriggerVictoryHandler(mockVictoryService.Object);
        var command = new TriggerVictoryCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Statistics!.Difficulty.Should().Be(difficulty);
        result.Statistics.DeathCount.Should().Be(deathCount);
        result.Statistics.EnemiesDefeated.Should().Be(enemiesDefeated);
        result.Statistics.QuestsCompleted.Should().Be(questsCompleted);
        result.Statistics.TotalGoldEarned.Should().Be(goldEarned);
    }
}
