using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
using Moq;
using FluentAssertions;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class GameplayServiceTests
{
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly GameplayService _service;

    public GameplayServiceTests()
    {
        _mockSaveGameService = new Mock<SaveGameService>();
        _service = new GameplayService(_mockSaveGameService.Object);
    }

    [Fact]
    public void Rest_Should_Restore_Health_To_Maximum()
    {
        // Arrange
        var player = new Character
        {
            Name = "Test Player",
            Health = 50,
            MaxHealth = 100,
            Mana = 30,
            MaxMana = 100
        };

        // Act
        _service.Rest(player);

        // Assert
        player.Health.Should().Be(player.MaxHealth, "rest should restore health to maximum");
    }

    [Fact]
    public void Rest_Should_Restore_Mana_To_Maximum()
    {
        // Arrange
        var player = new Character
        {
            Name = "Test Player",
            Health = 50,
            MaxHealth = 100,
            Mana = 30,
            MaxMana = 100
        };

        // Act
        _service.Rest(player);

        // Assert
        player.Mana.Should().Be(player.MaxMana, "rest should restore mana to maximum");
    }

    [Fact]
    public void Rest_Should_Return_Success_Result()
    {
        // Arrange
        var player = new Character { Name = "Test Player", Health = 50, MaxHealth = 100, Mana = 30, MaxMana = 100 };

        // Act
        var result = _service.Rest(player);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRecovered.Should().Be(50);
        result.ManaRecovered.Should().Be(70);
    }

    [Fact]
    public void Rest_Should_Handle_Null_Player_Gracefully()
    {
        // Act
        var result = _service.Rest(null!);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No player");
    }

    [Fact]
    public void SaveGame_Should_Call_SaveGameService()
    {
        // Arrange
        var player = new Character { Name = "Test Player" };
        var inventory = new List<Item>();
        var saveId = "save-123";

        // Act
        var result = _service.SaveGame(player, inventory, saveId);

        // Assert
        result.Success.Should().BeTrue();
        _mockSaveGameService.Verify(
            s => s.SaveGame(player, inventory, saveId),
            Times.Once,
            "should delegate to SaveGameService");
    }

    [Fact]
    public void SaveGame_Should_Return_Success_On_Successful_Save()
    {
        // Arrange
        var player = new Character { Name = "Test Player" };
        var inventory = new List<Item>();
        _mockSaveGameService.Setup(s => s.SaveGame(It.IsAny<Character>(), It.IsAny<List<Item>>(), It.IsAny<string>()));

        // Act
        var result = _service.SaveGame(player, inventory, null);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void SaveGame_Should_Return_Error_On_Failure()
    {
        // Arrange
        var player = new Character { Name = "Test Player" };
        var inventory = new List<Item>();
        _mockSaveGameService
            .Setup(s => s.SaveGame(It.IsAny<Character>(), It.IsAny<List<Item>>(), It.IsAny<string>()))
            .Throws(new Exception("Disk error"));

        // Act
        var result = _service.SaveGame(player, inventory, null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Disk error");
    }

    [Fact]
    public void SaveGame_Should_Return_Error_When_Player_Is_Null()
    {
        // Arrange
        var inventory = new List<Item>();

        // Act
        var result = _service.SaveGame(null!, inventory, null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active game");
        _mockSaveGameService.Verify(s => s.SaveGame(It.IsAny<Character>(), It.IsAny<List<Item>>(), It.IsAny<string>()),
            Times.Never,
            "should not attempt save when player is null");
    }
}
