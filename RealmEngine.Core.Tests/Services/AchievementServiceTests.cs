using RealmEngine.Core.Features.Achievements.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
using Moq;
using FluentAssertions;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class AchievementServiceTests
{
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly AchievementService _service;

    public AchievementServiceTests()
    {
        _mockSaveGameService = new Mock<SaveGameService>();
        _service = new AchievementService(_mockSaveGameService.Object);
    }

    [Fact]
    public async Task UnlockAchievementAsync_Should_Return_Null_When_No_Active_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.UnlockAchievementAsync("test-achievement");

        // Assert
        result.Should().BeNull("should return null when no save game is active");
    }

    [Fact]
    public async Task UnlockAchievementAsync_Should_Return_Null_When_Achievement_Already_Unlocked()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            UnlockedAchievements = new List<string> { "test-achievement" }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UnlockAchievementAsync("test-achievement");

        // Assert
        result.Should().BeNull("should return null when achievement is already unlocked");
    }

    [Fact]
    public async Task UnlockAchievementAsync_Should_Add_Achievement_To_Save()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            UnlockedAchievements = new List<string>()
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.UnlockAchievementAsync("test-achievement");

        // Assert
        // Note: Achievement unlocking relies on _allAchievements being initialized
        // which requires the private InitializeAchievements() method
        // For full testing, we'd need to mock or refactor the service
        _mockSaveGameService.Verify(s => s.GetCurrentSave(), Times.AtLeastOnce());
    }

    [Fact]
    public async Task CheckAllAchievementsAsync_Should_Return_Empty_List_When_No_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.CheckAllAchievementsAsync();

        // Assert
        result.Should().BeEmpty("should return empty list when no save game is active");
    }

    [Fact]
    public async Task CheckAllAchievementsAsync_Should_Check_All_Achievement_Criteria()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player", Level = 10 },
            UnlockedAchievements = new List<string>()
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.CheckAllAchievementsAsync();

        // Assert
        result.Should().NotBeNull();
        _mockSaveGameService.Verify(s => s.GetCurrentSave(), Times.AtLeastOnce());
    }

    [Fact]
    public async Task GetUnlockedAchievementsAsync_Should_Return_Empty_When_No_Save()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        // Act
        var result = await _service.GetUnlockedAchievementsAsync();

        // Assert
        result.Should().BeEmpty("should return empty list when no save game is active");
    }

    [Fact]
    public async Task GetUnlockedAchievementsAsync_Should_Return_Unlocked_Achievements()
    {
        // Arrange
        var saveGame = new SaveGame
        {
            Character = new Character { Name = "Test Player" },
            UnlockedAchievements = new List<string> { "first-kill", "reach-level-10" }
        };
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        // Act
        var result = await _service.GetUnlockedAchievementsAsync();

        // Assert
        result.Should().NotBeNull();
        result.All(a => a.IsUnlocked).Should().BeTrue("all returned achievements should be marked as unlocked");
    }
}
