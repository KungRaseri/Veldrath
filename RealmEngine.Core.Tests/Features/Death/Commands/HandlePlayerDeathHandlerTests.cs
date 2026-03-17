using FluentAssertions;
using MediatR;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Death;
using RealmEngine.Core.Features.Death.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Death.Commands;

public class HandlePlayerDeathHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Result_With_Permadeath_When_Permadeath_Difficulty()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                var player = new Character
        {
            Name = "Hero",
            Level = 5,
            Health = 0,
            Gold = 100,
            Experience = 500
        };

        var saveGame = new SaveGame
        {
            PlayerName = "Hero",
            DeathCount = 0,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        };

        var difficulty = new DifficultySettings
        {
            Name = "Permadeath",
            IsPermadeath = true
        };

        mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns(saveGame);
        mockSaveGameService.Setup(x => x.GetDifficultySettings()).Returns(difficulty);

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Dark Cave",
            Killer = null
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsPermadeath.Should().BeTrue();
        result.SaveDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Drop_Items_When_Difficulty_Requires()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                var player = new Character
        {
            Name = "Hero",
            Level = 5,
            Health = 0,
            Gold = 100,
            Experience = 500,
            Inventory = new List<Item>
            {
                new() { Name = "Health Potion", Type = ItemType.Consumable },
                new() { Name = "Iron Sword", Type = ItemType.Weapon }
            }
        };

        var saveGame = new SaveGame
        {
            PlayerName = "Hero",
            DeathCount = 0,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        };

        var difficulty = new DifficultySettings
        {
            Name = "Hard",
            IsPermadeath = false,
            ItemsDroppedOnDeath = 1
        };

        mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns(saveGame);
        mockSaveGameService.Setup(x => x.GetDifficultySettings()).Returns(difficulty);
        mockSaveGameService.Setup(x => x.SaveGame(It.IsAny<SaveGame>()));
        
        mockDeathService.Setup(x => x.HandleItemDropping(player, saveGame, "Dark Cave", difficulty))
            .Returns(new List<Item> { new() { Name = "Health Potion", Type = ItemType.Consumable } });

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Dark Cave",
            Killer = null
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DroppedItems.Should().HaveCount(1);
        result.DroppedItems.First().Name.Should().Be("Health Potion");
    }

    [Fact]
    public async Task Handle_Should_Apply_Gold_Penalty_When_Configured()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                var player = new Character
        {
            Name = "Hero",
            Level = 5,
            Health = 0,
            Gold = 100,
            Experience = 500
        };

        var saveGame = new SaveGame
        {
            PlayerName = "Hero",
            DeathCount = 0,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        };

        var difficulty = new DifficultySettings
        {
            Name = "Normal",
            IsPermadeath = false,
            GoldLossPercentage = 0.25
        };

        mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns(saveGame);
        mockSaveGameService.Setup(x => x.GetDifficultySettings()).Returns(difficulty);
        mockSaveGameService.Setup(x => x.SaveGame(It.IsAny<SaveGame>()));
        
        mockDeathService.Setup(x => x.HandleItemDropping(It.IsAny<Character>(), It.IsAny<SaveGame>(), It.IsAny<string>(), It.IsAny<DifficultySettings>()))
            .Returns(new List<Item>());

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Forest",
            Killer = null
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GoldLost.Should().Be(25);
    }

    [Fact]
    public async Task Handle_Should_Apply_XP_Penalty_When_Configured()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                var player = new Character
        {
            Name = "Hero",
            Level = 5,
            Health = 0,
            Gold = 100,
            Experience = 500
        };

        var saveGame = new SaveGame
        {
            PlayerName = "Hero",
            DeathCount = 0,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        };

        var difficulty = new DifficultySettings
        {
            Name = "Normal",
            IsPermadeath = false,
            XPLossPercentage = 0.10
        };

        mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns(saveGame);
        mockSaveGameService.Setup(x => x.GetDifficultySettings()).Returns(difficulty);
        mockSaveGameService.Setup(x => x.SaveGame(It.IsAny<SaveGame>()));
        
        mockDeathService.Setup(x => x.HandleItemDropping(It.IsAny<Character>(), It.IsAny<SaveGame>(), It.IsAny<string>(), It.IsAny<DifficultySettings>()))
            .Returns(new List<Item>());

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Mountain Pass",
            Killer = null
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.XPLost.Should().Be(50);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Result_When_No_SaveGame()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns((SaveGame?)null);

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var player = new Character { Name = "Hero" };
        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Unknown",
            Killer = null
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsPermadeath.Should().BeFalse();
        result.SaveDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Increment_Death_Count_In_SaveGame()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                var player = new Character
        {
            Name = "Hero",
            Level = 5,
            Health = 0
        };

        var saveGame = new SaveGame
        {
            PlayerName = "Hero",
            DeathCount = 2,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        };

        var difficulty = new DifficultySettings
        {
            Name = "Normal",
            IsPermadeath = false
        };

        mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns(saveGame);
        mockSaveGameService.Setup(x => x.GetDifficultySettings()).Returns(difficulty);
        mockSaveGameService.Setup(x => x.SaveGame(It.IsAny<SaveGame>()));
        
        mockDeathService.Setup(x => x.HandleItemDropping(It.IsAny<Character>(), It.IsAny<SaveGame>(), It.IsAny<string>(), It.IsAny<DifficultySettings>()))
            .Returns(new List<Item>());

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Dungeon",
            Killer = null
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        saveGame.DeathCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_Should_Create_Hall_Of_Fame_Entry_On_Permadeath()
    {
        // Arrange
        var mockDeathService = new Mock<DeathService>();
        var mockSaveGameService = new Mock<SaveGameService>();
        var mockHallOfFame = new Mock<IHallOfFameRepository>();
                var player = new Character
        {
            Name = "Legendary Hero",
            Level = 20,
            Health = 0,
            Gold = 5000,
            Experience = 10000
        };

        var killer = new Enemy
        {
            Name = "Dragon",
            Level = 25
        };

        var saveGame = new SaveGame
        {
            PlayerName = "Legendary Hero",
            DeathCount = 0,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        };

        var difficulty = new DifficultySettings
        {
            Name = "Permadeath",
            IsPermadeath = true
        };

        mockSaveGameService.Setup(x => x.GetCurrentSave()).Returns(saveGame);
        mockSaveGameService.Setup(x => x.GetDifficultySettings()).Returns(difficulty);
        mockSaveGameService.Setup(x => x.DeleteSave(It.IsAny<string>())).Returns(true);

        var handler = new HandlePlayerDeathHandler(
            mockDeathService.Object,
            mockSaveGameService.Object,
            mockHallOfFame.Object,
            NullLogger<HandlePlayerDeathHandler>.Instance);

        var command = new HandlePlayerDeathCommand
        {
            Player = player,
            DeathLocation = "Dragon's Lair",
            Killer = killer
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.HallOfFameId.Should().NotBeNullOrEmpty();
        mockHallOfFame.Verify(x => x.AddEntry(It.IsAny<HallOfFameEntry>()), Times.Once);
    }
}
