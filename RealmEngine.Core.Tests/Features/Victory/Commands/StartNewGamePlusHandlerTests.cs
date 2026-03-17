using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Victory.Commands;
using RealmEngine.Core.Features.Victory.Services;
using RealmEngine.Shared.Models;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Victory.Commands;

public class StartNewGamePlusHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Success_When_New_Game_Plus_Started()
    {
        // Arrange
        var mockNgPlusService = new Mock<NewGamePlusService>(MockBehavior.Strict, (object)null!, NullLogger<NewGamePlusService>.Instance);

        var ngPlusSave = new SaveGame
        {
            PlayerName = "Hero (NG+)",
            Character = new Character
            {
                Name = "Hero",
                ClassName = "Warrior",
                Level = 1,
                MaxHealth = 200,
                Health = 200,
                Gold = 500
            },
            DifficultyLevel = "Normal NG+"
        };

        mockNgPlusService.Setup(x => x.StartNewGamePlusAsync())
            .ReturnsAsync((true, ngPlusSave));

        var handler = new StartNewGamePlusHandler(mockNgPlusService.Object);
        var command = new StartNewGamePlusCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("New Game+ started!");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_New_Game_Plus_Cannot_Start()
    {
        // Arrange
        var mockNgPlusService = new Mock<NewGamePlusService>(MockBehavior.Strict, (object)null!, NullLogger<NewGamePlusService>.Instance);

        mockNgPlusService.Setup(x => x.StartNewGamePlusAsync())
            .ReturnsAsync((false, (SaveGame?)null));

        var handler = new StartNewGamePlusHandler(mockNgPlusService.Object);
        var command = new StartNewGamePlusCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Failed to start New Game+");
    }

    [Fact]
    public async Task Handle_Should_Call_StartNewGamePlusAsync()
    {
        // Arrange
        var mockNgPlusService = new Mock<NewGamePlusService>(MockBehavior.Strict, (object)null!, NullLogger<NewGamePlusService>.Instance);

        var ngPlusSave = new SaveGame
        {
            PlayerName = "TestPlayer (NG+)",
            Character = new Character { Name = "TestPlayer" }
        };

        mockNgPlusService.Setup(x => x.StartNewGamePlusAsync())
            .ReturnsAsync((true, ngPlusSave));

        var handler = new StartNewGamePlusHandler(mockNgPlusService.Object);
        var command = new StartNewGamePlusCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNgPlusService.Verify(x => x.StartNewGamePlusAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message_With_Enhanced_Character()
    {
        // Arrange
        var mockNgPlusService = new Mock<NewGamePlusService>(MockBehavior.Strict, (object)null!, NullLogger<NewGamePlusService>.Instance);

        var ngPlusSave = new SaveGame
        {
            PlayerName = "Enhanced Hero (NG+)",
            Character = new Character
            {
                Name = "Enhanced Hero",
                ClassName = "Paladin",
                Level = 1,
                MaxHealth = 250, // Bonus HP
                Health = 250,
                MaxMana = 150, // Bonus Mana
                Mana = 150,
                Strength = 15, // Bonus stats
                Intelligence = 15,
                Dexterity = 15,
                Gold = 500 // Starting gold bonus
            },
            DifficultyLevel = "Hard NG+"
        };

        mockNgPlusService.Setup(x => x.StartNewGamePlusAsync())
            .ReturnsAsync((true, ngPlusSave));

        var handler = new StartNewGamePlusHandler(mockNgPlusService.Object);
        var command = new StartNewGamePlusCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("New Game+");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Service_Returns_Null_Save()
    {
        // Arrange
        var mockNgPlusService = new Mock<NewGamePlusService>(MockBehavior.Strict, (object)null!, NullLogger<NewGamePlusService>.Instance);

        mockNgPlusService.Setup(x => x.StartNewGamePlusAsync())
            .ReturnsAsync((false, (SaveGame?)null));

        var handler = new StartNewGamePlusHandler(mockNgPlusService.Object);
        var command = new StartNewGamePlusCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed");
    }

    [Theory]
    [InlineData("Warrior", "Normal")]
    [InlineData("Mage", "Hard")]
    [InlineData("Rogue", "Nightmare")]
    [InlineData("Paladin", "Permadeath")]
    public async Task Handle_Should_Work_With_Different_Classes_And_Difficulties(string className, string difficulty)
    {
        // Arrange
        var mockNgPlusService = new Mock<NewGamePlusService>(MockBehavior.Strict, (object)null!, NullLogger<NewGamePlusService>.Instance);

        var ngPlusSave = new SaveGame
        {
            PlayerName = $"{className} Hero (NG+)",
            Character = new Character
            {
                Name = $"{className} Hero",
                ClassName = className,
                Level = 1
            },
            DifficultyLevel = $"{difficulty} NG+"
        };

        mockNgPlusService.Setup(x => x.StartNewGamePlusAsync())
            .ReturnsAsync((true, ngPlusSave));

        var handler = new StartNewGamePlusHandler(mockNgPlusService.Object);
        var command = new StartNewGamePlusCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("New Game+ started!");
    }
}
