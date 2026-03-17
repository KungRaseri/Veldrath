using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.SaveLoad.Commands;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Services;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Repositories;
using Moq;

namespace RealmEngine.Core.Tests.Features.SaveLoad.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for DeleteSaveHandler.
/// </summary>
public class DeleteSaveHandlerTests : IDisposable
{
    private readonly Mock<IGameUI> _mockConsoleUI;
    private readonly Mock<IApocalypseTimer> _mockApocalypseTimer;
    private readonly SaveGameService _saveGameService;

    public DeleteSaveHandlerTests()
    {
        _mockConsoleUI = new Mock<IGameUI>();
        _mockApocalypseTimer = new Mock<IApocalypseTimer>();
        var repository = new InMemorySaveGameRepository();
        _saveGameService = new SaveGameService(repository, _mockApocalypseTimer.Object);
    }

    [Fact]
    public async Task Handle_Should_Delete_Existing_Save()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);
        var player = new Character { Name = "DeleteMe", Level = 5, Health = 100, MaxHealth = 100 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        _saveGameService.SaveGame(player, new List<Item>(), saveGame.Id);

        var command = new DeleteSaveCommand { SaveId = saveGame.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Save game deleted successfully");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Save_Not_Found()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);
        var command = new DeleteSaveCommand { SaveId = "non-existent-save-id" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Should_Remove_Save_From_Database()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);
        var player = new Character { Name = "TestPlayer", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        _saveGameService.SaveGame(player, new List<Item>(), saveGame.Id);

        var command = new DeleteSaveCommand { SaveId = saveGame.Id };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Verify it's deleted
        var loadedSave = _saveGameService.LoadGame(saveGame.Id);

        // Assert
        loadedSave.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);
        var player = new Character { Name = "TestPlayer", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        _saveGameService.SaveGame(player, new List<Item>(), saveGame.Id);

        var command = new DeleteSaveCommand { SaveId = saveGame.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Contain("success");
    }

    [Fact]
    public async Task Handle_Should_Delete_Only_Specified_Save()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);

        // Create two saves
        var player1 = new Character { Name = "Player1", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Normal);
        _saveGameService.SaveGame(player1, new List<Item>(), saveGame1.Id);

        var player2 = new Character { Name = "Player2", Level = 5, Health = 150, MaxHealth = 150 };
        var saveGame2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Hard);
        _saveGameService.SaveGame(player2, new List<Item>(), saveGame2.Id);

        var command = new DeleteSaveCommand { SaveId = saveGame1.Id };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var deleted = _saveGameService.LoadGame(saveGame1.Id);
        var remaining = _saveGameService.LoadGame(saveGame2.Id);

        deleted.Should().BeNull();
        remaining.Should().NotBeNull();
        remaining!.Character.Name.Should().Be("Player2");
    }

    [Fact]
    public async Task Handle_Should_Handle_Empty_SaveId()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);
        var command = new DeleteSaveCommand { SaveId = "" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Handle_Null_SaveId()
    {
        // Arrange
        var handler = new DeleteSaveHandler(_saveGameService, NullLogger<DeleteSaveHandler>.Instance);
        var command = new DeleteSaveCommand { SaveId = null! };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    public void Dispose()
    {
        _saveGameService?.Dispose();
    }
}
