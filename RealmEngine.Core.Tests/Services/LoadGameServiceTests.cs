using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class LoadGameServiceTests
{
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly Mock<IApocalypseTimer> _mockApocalypseTimer;
    private readonly LoadGameService _service;

    public LoadGameServiceTests()
    {
        _mockSaveGameService = new Mock<SaveGameService>();
        _mockApocalypseTimer = new Mock<IApocalypseTimer>();
        _service = new LoadGameService(
            _mockSaveGameService.Object,
            _mockApocalypseTimer.Object,
            NullLogger<LoadGameService>.Instance);
    }

    [Fact]
    public void LoadGame_Should_Return_Error_When_Save_Not_Found()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.LoadGame(It.IsAny<string>())).Returns((SaveGame?)null);

        // Act
        var result = _service.LoadGame(999);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        result.SaveGame.Should().BeNull();
    }

    [Fact]
    public void LoadGame_Should_Return_SaveGame_When_Found()
    {
        // Arrange
        var testSave = new SaveGame
        {
            Id = "1",
            Character = new Character { Name = "TestHero", Level = 5, ClassName = "Warrior" },
            SaveDate = DateTime.Now
        };
        _mockSaveGameService.Setup(s => s.LoadGame("1")).Returns(testSave);

        // Act
        var result = _service.LoadGame(1);

        // Assert
        result.Success.Should().BeTrue();
        result.SaveGame.Should().NotBeNull();
        result.SaveGame!.Character.Name.Should().Be("TestHero");
        result.ApocalypseMode.Should().BeFalse();
    }

    [Fact]
    public void LoadGame_Should_Restore_Apocalypse_Timer_When_ApocalypseMode_Active()
    {
        // Arrange
        var testSave = new SaveGame
        {
            Id = "1",
            Character = new Character { Name = "TestHero", Level = 10, ClassName = "Mage" },
            ApocalypseMode = true,
            ApocalypseStartTime = DateTime.Now.AddMinutes(-30),
            ApocalypseBonusMinutes = 10
        };
        _mockSaveGameService.Setup(s => s.LoadGame("1")).Returns(testSave);
        _mockApocalypseTimer.Setup(t => t.IsExpired()).Returns(false);
        _mockApocalypseTimer.Setup(t => t.GetRemainingMinutes()).Returns(220);

        // Act
        var result = _service.LoadGame(1);

        // Assert
        result.Success.Should().BeTrue();
        result.ApocalypseMode.Should().BeTrue();
        result.ApocalypseTimeExpired.Should().BeFalse();
        result.ApocalypseRemainingMinutes.Should().Be(220);
        _mockApocalypseTimer.Verify(t => t.StartFromSave(
            testSave.ApocalypseStartTime.Value,
            testSave.ApocalypseBonusMinutes), Times.Once);
    }

    [Fact]
    public void LoadGame_Should_Detect_Expired_Apocalypse_Timer()
    {
        // Arrange
        var testSave = new SaveGame
        {
            Id = "1",
            Character = new Character { Name = "TestHero", Level = 15, ClassName = "Rogue" },
            ApocalypseMode = true,
            ApocalypseStartTime = DateTime.Now.AddMinutes(-300), // 5 hours ago
            ApocalypseBonusMinutes = 0
        };
        _mockSaveGameService.Setup(s => s.LoadGame("1")).Returns(testSave);
        _mockApocalypseTimer.Setup(t => t.IsExpired()).Returns(true);

        // Act
        var result = _service.LoadGame(1);

        // Assert
        result.Success.Should().BeTrue();
        result.ApocalypseMode.Should().BeTrue();
        result.ApocalypseTimeExpired.Should().BeTrue();
        result.ApocalypseRemainingMinutes.Should().BeNull();
    }

    [Fact]
    public void GetAllSaves_Should_Return_All_Save_Games()
    {
        // Arrange
        var saves = new List<SaveGame>
        {
            new() { Id = "1", Character = new Character { Name = "Hero1", Level = 5 } },
            new() { Id = "2", Character = new Character { Name = "Hero2", Level = 10 } }
        };
        _mockSaveGameService.Setup(s => s.GetAllSaves()).Returns(saves);

        // Act
        var result = _service.GetAllSaves();

        // Assert
        result.Should().HaveCount(2);
        result[0].Character.Name.Should().Be("Hero1");
        result[1].Character.Name.Should().Be("Hero2");
    }
}
