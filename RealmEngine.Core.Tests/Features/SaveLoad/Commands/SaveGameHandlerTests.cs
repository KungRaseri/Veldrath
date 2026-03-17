using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.SaveLoad.Commands;
using RealmEngine.Shared.Models;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Core.Tests.Features.SaveLoad.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for SaveGameHandler.
/// </summary>
public class SaveGameHandlerTests : IDisposable
{
    private readonly Mock<IGameUI> _mockGameUI;
    private readonly SaveGameService _saveGameService;

    public SaveGameHandlerTests()
    {
        _mockGameUI = new Mock<IGameUI>();
        var mockApocalypseTimer = new Mock<IApocalypseTimer>();
        _saveGameService = new SaveGameService(new InMemorySaveGameRepository(), mockApocalypseTimer.Object);
    }

    [Fact]
    public async Task Handle_Should_Save_Game_Successfully()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character
        {
            Name = "TestPlayer",
            Level = 5,
            Health = 100,
            MaxHealth = 100
        };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        var command = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = saveGame.Id
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Game saved successfully!");
        result.SaveId.Should().Be(saveGame.Id);
    }

    [Fact]
    public async Task Handle_Should_Save_Game_With_Inventory()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character
        {
            Name = "TestPlayer",
            Level = 5,
            Health = 100,
            MaxHealth = 100
        };
        var items = new List<Item>
        {
            new Item { Name = "Health Potion", Type = ItemType.Consumable },
            new Item { Name = "Iron Sword", Type = ItemType.Weapon }
        };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        var command = new SaveGameCommand
        {
            Player = player,
            Inventory = items,
            SaveId = saveGame.Id
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.SaveId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_Should_Update_Existing_Save()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character
        {
            Name = "TestPlayer",
            Level = 5,
            Health = 100,
            MaxHealth = 100
        };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);

        // Save first time
        var command1 = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = saveGame.Id
        };
        await handler.Handle(command1, CancellationToken.None);

        // Update player
        player.Level = 10;
        player.Gold = 500;

        // Save second time
        var command2 = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = saveGame.Id
        };

        // Act
        var result = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.SaveId.Should().Be(saveGame.Id);
    }

    [Fact]
    public async Task Handle_Should_Persist_Player_Stats()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character
        {
            Name = "StatPlayer",
            Level = 10,
            Health = 75,
            MaxHealth = 150,
            Mana = 50,
            MaxMana = 100,
            Gold = 1000,
            Experience = 2500
        };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        var command = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = saveGame.Id
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var loaded = _saveGameService.GetCurrentSave();
        loaded.Should().NotBeNull();
        loaded!.Character.Level.Should().Be(10);
        loaded.Character.Health.Should().Be(75);
        loaded.Character.Gold.Should().Be(1000);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character { Name = "TestPlayer", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        var command = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = saveGame.Id
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Contain("success");
    }

    [Fact]
    public async Task Handle_Should_Handle_Multiple_Saves()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);

        // Create first save
        var player1 = new Character { Name = "Player1", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Normal);
        var command1 = new SaveGameCommand
        {
            Player = player1,
            Inventory = new List<Item>(),
            SaveId = saveGame1.Id
        };

        // Create second save
        var player2 = new Character { Name = "Player2", Level = 5, Health = 200, MaxHealth = 200 };
        var saveGame2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Hard);
        var command2 = new SaveGameCommand
        {
            Player = player2,
            Inventory = new List<Item>(),
            SaveId = saveGame2.Id
        };

        // Act
        var result1 = await handler.Handle(command1, CancellationToken.None);
        var result2 = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result1.SaveId.Should().NotBe(result2.SaveId);
    }

    [Fact]
    public async Task Handle_Should_Save_With_Different_Difficulty_Settings()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character { Name = "HardcorePlayer", Level = 1, Health = 50, MaxHealth = 50 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Expert);
        var command = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = saveGame.Id
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var loaded = _saveGameService.GetCurrentSave();
        loaded.Should().NotBeNull();
        loaded!.DifficultyLevel.Should().Be("Expert");
    }

    [Fact]
    public async Task Handle_Should_Return_Same_SaveId_On_Update()
    {
        // Arrange
        var handler = new SaveGameHandler(_saveGameService, NullLogger<SaveGameHandler>.Instance);
        var player = new Character { Name = "TestPlayer", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        var originalId = saveGame.Id;

        var command = new SaveGameCommand
        {
            Player = player,
            Inventory = new List<Item>(),
            SaveId = originalId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SaveId.Should().Be(originalId);
    }

    public void Dispose()
    {
        _saveGameService?.Dispose();
    }
}
