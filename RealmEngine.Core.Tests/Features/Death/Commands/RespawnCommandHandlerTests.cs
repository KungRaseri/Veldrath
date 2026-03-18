using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Death;
using RealmEngine.Core.Features.Death.Commands;
using RealmEngine.Core.Features.Death.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Death.Commands;

public class RespawnCommandHandlerTests
{
  private sealed class FakeGameStateService : GameStateService
  {
    public override void UpdateLocation(string location) => CurrentLocation = location;
  }

  private static RespawnCommandHandler CreateHandler(
      ISaveGameService? saveGameService = null,
      DeathService? deathService = null,
      GameStateService? gameState = null)
  {
    saveGameService ??= Mock.Of<ISaveGameService>();
    deathService ??= new DeathService(NullLogger<DeathService>.Instance);
    gameState ??= new FakeGameStateService();
    return new RespawnCommandHandler(
        gameState,
        deathService,
        saveGameService,
        NullLogger<RespawnCommandHandler>.Instance);
  }

  [Fact]
  public async Task Handle_ReturnsFailure_WhenPlayerIsNull()
  {
    var handler = CreateHandler();
    var command = new RespawnCommand { Player = null! };

    var result = await handler.Handle(command, default);

    result.Success.Should().BeFalse();
    result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Handle_RestoresFullHealthAndMana_OnSuccess()
  {
    var player = new Character
    {
      Name = "Hero",
      Health = 1,
      MaxHealth = 100,
      Mana = 5,
      MaxMana = 80
    };

    var mockSave = new Mock<ISaveGameService>();
    mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
    mockSave.Setup(s => s.GetDifficultySettings()).Returns(DifficultySettings.Normal);

    var handler = CreateHandler(mockSave.Object);
    var command = new RespawnCommand { Player = player };

    var result = await handler.Handle(command, default);

    result.Success.Should().BeTrue();
    result.Health.Should().Be(100);
    result.Mana.Should().Be(80);
  }

  [Fact]
  public async Task Handle_UsesRequestedRespawnLocation_WhenProvided()
  {
    var player = new Character { Name = "Hero", MaxHealth = 50, MaxMana = 50 };
    var mockSave = new Mock<ISaveGameService>();
    mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

    var gameState = new FakeGameStateService();
    var handler = CreateHandler(mockSave.Object, gameState: gameState);
    var command = new RespawnCommand { Player = player, RespawnLocation = "Mystic Sanctuary" };

    var result = await handler.Handle(command, default);

    result.Success.Should().BeTrue();
    result.RespawnLocation.Should().Be("Mystic Sanctuary");
  }

  [Fact]
  public async Task Handle_DefaultsToHubTown_WhenNoRespawnLocationSpecified()
  {
    var player = new Character { Name = "Hero", MaxHealth = 50, MaxMana = 50 };
    var mockSave = new Mock<ISaveGameService>();
    mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

    var handler = CreateHandler(mockSave.Object);
    var command = new RespawnCommand { Player = player };

    var result = await handler.Handle(command, default);

    result.Success.Should().BeTrue();
    result.RespawnLocation.Should().Be("Hub Town");
  }

  [Fact]
  public async Task Handle_SavesGame_WhenSaveExists()
  {
    var player = new Character { Name = "Hero", MaxHealth = 50, MaxMana = 50 };
    var saveGame = new SaveGame { Character = player };

    var mockSave = new Mock<ISaveGameService>();
    mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
    mockSave.Setup(s => s.GetDifficultySettings()).Returns(DifficultySettings.Easy);

    var handler = CreateHandler(mockSave.Object);
    var command = new RespawnCommand { Player = player };

    await handler.Handle(command, default);

    mockSave.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Once);
  }

  [Fact]
  public async Task Handle_DropsItems_AccordingToDifficulty()
  {
    var item = new Item { Id = "gem1", Name = "Ruby", Price = 10 };
    var player = new Character { Name = "Hero", MaxHealth = 50, MaxMana = 50 };
    player.Inventory.Add(item);

    var saveGame = new SaveGame { Character = player, LastDeathLocation = "Dungeon" };
    // Hard difficulty drops 1 item
    saveGame.DifficultyLevel = DifficultySettings.Hard.Name;

    var mockSave = new Mock<ISaveGameService>();
    mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
    mockSave.Setup(s => s.GetDifficultySettings()).Returns(DifficultySettings.Hard);

    var handler = CreateHandler(mockSave.Object);
    var command = new RespawnCommand { Player = player };

    var result = await handler.Handle(command, default);

    result.Success.Should().BeTrue();
    player.Inventory.Should().HaveCount(0); // 1 item dropped = empty
  }
}
