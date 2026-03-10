using FluentAssertions;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.SaveLoad.Queries;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Repositories;
using Moq;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Tests.Features.SaveLoad.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetAllSavesHandler.
/// </summary>
public class GetAllSavesHandlerTests : IDisposable
{
    private readonly Mock<IGameUI> _mockConsoleUI;
    private readonly Mock<IApocalypseTimer> _mockApocalypseTimer;
    private readonly SaveGameService _saveGameService;

    public GetAllSavesHandlerTests()
    {
        _mockConsoleUI = new Mock<IGameUI>();
        _mockApocalypseTimer = new Mock<IApocalypseTimer>();
        _saveGameService = new SaveGameService(new InMemorySaveGameRepository(), _mockApocalypseTimer.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_All_Saved_Games()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);

        // Create first save
        var player1 = new Character { Name = "Player1", ClassName = "Warrior", Level = 5, Health = 100, MaxHealth = 100 };
        var save1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Normal);
        _saveGameService.SaveGame(save1);

        // Create second save using CreateNewGame to ensure proper initialization
        var player2 = new Character { Name = "Player2", ClassName = "Mage", Level = 10, Health = 150, MaxHealth = 150 };
        var save2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Hard);
        _saveGameService.SaveGame(save2);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().NotBeNull();
        result.SaveGames.Should().HaveCount(2);
        result.SaveGames.Should().Contain(s => s.Character.Name == "Player1");
        result.SaveGames.Should().Contain(s => s.Character.Name == "Player2");
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Saves()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);
        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().NotBeNull();
        result.SaveGames.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Return_Saves_With_Player_Names()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);
        var player = new Character { Name = "TestHero", Level = 1, Health = 100, MaxHealth = 100 };
        var saveGame = _saveGameService.CreateNewGame(player, DifficultySettings.Normal);
        _saveGameService.SaveGame(player, new List<Item>(), saveGame.Id);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().HaveCount(1);
        result.SaveGames[0].PlayerName.Should().Be("TestHero");
    }

    [Fact]
    public async Task Handle_Should_Return_Saves_With_Levels()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);
        var player1 = new Character { Name = "Newbie", Level = 1, Health = 100, MaxHealth = 100 };
        var save1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Normal);
        _saveGameService.SaveGame(player1, new List<Item>(), save1.Id);

        var player2 = new Character { Name = "Veteran", Level = 50, Health = 500, MaxHealth = 500 };
        var save2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Expert);
        _saveGameService.SaveGame(player2, new List<Item>(), save2.Id);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().HaveCount(2);
        result.SaveGames.Should().Contain(s => s.Character.Level == 1);
        result.SaveGames.Should().Contain(s => s.Character.Level == 50);
    }

    [Fact]
    public async Task Handle_Should_Return_Saves_With_Difficulty_Settings()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);
        var player1 = new Character { Name = "Easy", Level = 1, Health = 100, MaxHealth = 100 };
        var save1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Easy);
        _saveGameService.SaveGame(player1, new List<Item>(), save1.Id);

        var player2 = new Character { Name = "Expert", Level = 1, Health = 50, MaxHealth = 50 };
        var save2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Expert);
        _saveGameService.SaveGame(player2, new List<Item>(), save2.Id);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().HaveCount(2);
        result.SaveGames.Should().Contain(s => s.DifficultyLevel == "Easy");
        result.SaveGames.Should().Contain(s => s.DifficultyLevel == "Expert");
    }

    [Fact]
    public async Task Handle_Should_Return_Saves_With_Character_Classes()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);
        var warrior = new Character { Name = "Tank", ClassName = "Warrior", Level = 5, Health = 200, MaxHealth = 200 };
        var save1 = _saveGameService.CreateNewGame(warrior, DifficultySettings.Normal);
        _saveGameService.SaveGame(warrior, new List<Item>(), save1.Id);

        var mage = new Character { Name = "Wizard", ClassName = "Mage", Level = 5, Health = 100, MaxHealth = 100 };
        var save2 = _saveGameService.CreateNewGame(mage, DifficultySettings.Normal);
        _saveGameService.SaveGame(mage, new List<Item>(), save2.Id);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().HaveCount(2);
        result.SaveGames.Should().Contain(s => s.Character.ClassName == "Warrior");
        result.SaveGames.Should().Contain(s => s.Character.ClassName == "Mage");
    }

    [Fact]
    public async Task Handle_Should_Return_Multiple_Saves_From_Same_Player()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);

        // Create two different saves with same player name
        var player1 = new Character { Name = "MultiSave", Level = 1, Health = 100, MaxHealth = 100 };
        var save1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Normal);
        _saveGameService.SaveGame(player1, new List<Item>(), save1.Id);

        var player2 = new Character { Name = "MultiSave", Level = 10, Health = 200, MaxHealth = 200 };
        var save2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Hard);
        _saveGameService.SaveGame(player2, new List<Item>(), save2.Id);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().HaveCount(2);
        result.SaveGames.Where(s => s.PlayerName == "MultiSave").Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_Return_Saves_With_Unique_Ids()
    {
        // Arrange
        var handler = new GetAllSavesHandler(_saveGameService);
        var player1 = new Character { Name = "Player1", Level = 1, Health = 100, MaxHealth = 100 };
        var save1 = _saveGameService.CreateNewGame(player1, DifficultySettings.Normal);
        _saveGameService.SaveGame(player1, new List<Item>(), save1.Id);

        var player2 = new Character { Name = "Player2", Level = 1, Health = 100, MaxHealth = 100 };
        var save2 = _saveGameService.CreateNewGame(player2, DifficultySettings.Normal);
        _saveGameService.SaveGame(player2, new List<Item>(), save2.Id);

        var query = new GetAllSavesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SaveGames.Should().HaveCount(2);
        var ids = result.SaveGames.Select(s => s.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    public void Dispose()
    {
        _saveGameService?.Dispose();
    }
}
