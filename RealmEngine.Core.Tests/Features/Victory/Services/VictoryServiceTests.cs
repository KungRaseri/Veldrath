using FluentAssertions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Victory.Services;
using RealmEngine.Shared.Models;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Victory.Services;

[Trait("Category", "Feature")]
public class VictoryServiceTests
{
    private readonly Mock<ISaveGameService> _mockSaveGameService;
    private readonly VictoryService _victoryService;

    public VictoryServiceTests()
    {
        _mockSaveGameService = new Mock<ISaveGameService>();
        _victoryService = new VictoryService(_mockSaveGameService.Object, NullLogger<VictoryService>.Instance);
    }

    #region CalculateVictoryStatisticsAsync Tests

    [Fact]
    public async Task CalculateVictoryStatisticsAsync_Should_Return_Null_When_No_Save_Game()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _victoryService.CalculateVictoryStatisticsAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CalculateVictoryStatisticsAsync_Should_Calculate_Statistics_Correctly()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character
            {
                Name = "TestHero",
                ClassName = "Warrior",
                Level = 20
            },
            DifficultyLevel = "Hard",
            PlayTimeMinutes = 180,
            QuestsCompleted = 15,
            TotalEnemiesDefeated = 250,
            DeathCount = 3,
            UnlockedAchievements = new List<string> { "ach1", "ach2", "ach3", "ach4", "ach5" },
            TotalGoldEarned = 50000
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _victoryService.CalculateVictoryStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result!.PlayerName.Should().Be("TestHero");
        result.ClassName.Should().Be("Warrior");
        result.FinalLevel.Should().Be(20);
        result.Difficulty.Should().Be("Hard");
        result.PlayTimeMinutes.Should().Be(180);
        result.QuestsCompleted.Should().Be(15);
        result.EnemiesDefeated.Should().Be(250);
        result.DeathCount.Should().Be(3);
        result.AchievementsUnlocked.Should().Be(5);
        result.TotalGoldEarned.Should().Be(50000);
    }

    [Fact]
    public async Task CalculateVictoryStatisticsAsync_Should_Handle_Zero_Values()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character
            {
                Name = "NewHero",
                ClassName = "Mage",
                Level = 1
            },
            DifficultyLevel = "Easy",
            PlayTimeMinutes = 0,
            QuestsCompleted = 0,
            TotalEnemiesDefeated = 0,
            DeathCount = 0,
            UnlockedAchievements = new List<string>(),
            TotalGoldEarned = 0
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _victoryService.CalculateVictoryStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result!.QuestsCompleted.Should().Be(0);
        result.EnemiesDefeated.Should().Be(0);
        result.DeathCount.Should().Be(0);
        result.AchievementsUnlocked.Should().Be(0);
        result.TotalGoldEarned.Should().Be(0);
    }

    [Fact]
    public async Task CalculateVictoryStatisticsAsync_Should_Handle_High_Values()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character
            {
                Name = "Legend",
                ClassName = "Assassin",
                Level = 99
            },
            DifficultyLevel = "Apocalypse",
            PlayTimeMinutes = 10000,
            QuestsCompleted = 500,
            TotalEnemiesDefeated = 99999,
            DeathCount = 0,
            UnlockedAchievements = Enumerable.Range(1, 50).Select(i => $"ach{i}").ToList(),
            TotalGoldEarned = 999999999
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _victoryService.CalculateVictoryStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result!.FinalLevel.Should().Be(99);
        result.QuestsCompleted.Should().Be(500);
        result.EnemiesDefeated.Should().Be(99999);
        result.AchievementsUnlocked.Should().Be(50);
    }

    #endregion

    #region MarkGameCompleteAsync Tests

    [Fact]
    public async Task MarkGameCompleteAsync_Should_Do_Nothing_When_No_Save_Game()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        await _victoryService.MarkGameCompleteAsync();

        // Assert
        _mockSaveGameService.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Never);
    }

    [Fact]
    public async Task MarkGameCompleteAsync_Should_Set_Game_Completed_Flag()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool>()
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        await _victoryService.MarkGameCompleteAsync();

        // Assert
        saveGame.GameFlags.Should().ContainKey("GameCompleted");
        saveGame.GameFlags["GameCompleted"].Should().BeTrue();
        saveGame.GameFlags.Should().ContainKey("CompletionDate");
        saveGame.GameFlags["CompletionDate"].Should().BeTrue();

        _mockSaveGameService.Verify(s => s.SaveGame(saveGame), Times.Once);
    }

    [Fact]
    public async Task MarkGameCompleteAsync_Should_Preserve_Existing_Flags()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool>
            {
                { "CustomFlag1", true },
                { "CustomFlag2", false }
            }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        await _victoryService.MarkGameCompleteAsync();

        // Assert
        saveGame.GameFlags.Should().HaveCount(4);
        saveGame.GameFlags["CustomFlag1"].Should().BeTrue();
        saveGame.GameFlags["CustomFlag2"].Should().BeFalse();
        saveGame.GameFlags["GameCompleted"].Should().BeTrue();
    }

    [Fact]
    public async Task MarkGameCompleteAsync_Should_Overwrite_If_Already_Marked()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Hero" },
            GameFlags = new Dictionary<string, bool>
            {
                { "GameCompleted", false }
            }
        };

        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        await _victoryService.MarkGameCompleteAsync();

        // Assert
        saveGame.GameFlags["GameCompleted"].Should().BeTrue();
        _mockSaveGameService.Verify(s => s.SaveGame(saveGame), Times.Once);
    }

    #endregion
}
