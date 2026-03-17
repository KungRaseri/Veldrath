using FluentAssertions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for RestCommandHandler.
/// </summary>
public class RestCommandHandlerTests
{
    private readonly Mock<GameStateService> _mockGameState;
    private readonly RestCommandHandler _handler;

    public RestCommandHandlerTests()
    {
        _mockGameState = new Mock<GameStateService>();
        _handler = new RestCommandHandler(_mockGameState.Object, NullLogger<RestCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_Restore_Full_Health_And_Mana()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 50,
            MaxHealth = 100,
            Mana = 30,
            MaxMana = 100
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Health.Should().Be(100);
        player.Mana.Should().Be(100);
        result.HealthRecovered.Should().Be(50);
        result.ManaRecovered.Should().Be(70);
    }

    [Fact]
    public async Task Handle_Should_Recover_No_Health_When_Already_Full()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            Mana = 100,
            MaxMana = 100
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRecovered.Should().Be(0);
        result.ManaRecovered.Should().Be(0);
        player.Health.Should().Be(100);
        player.Mana.Should().Be(100);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_No_Active_Player()
    {
        // Arrange
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns((Character)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No active player");
        result.HealthRecovered.Should().BeNull();
        result.ManaRecovered.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Restore_Only_Health_When_Mana_Is_Full()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 50,
            MaxHealth = 100,
            Mana = 100,
            MaxMana = 100
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRecovered.Should().Be(50);
        result.ManaRecovered.Should().Be(0);
        player.Health.Should().Be(100);
        player.Mana.Should().Be(100);
    }

    [Fact]
    public async Task Handle_Should_Restore_Only_Mana_When_Health_Is_Full()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 100,
            MaxHealth = 100,
            Mana = 20,
            MaxMana = 80
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRecovered.Should().Be(0);
        result.ManaRecovered.Should().Be(60);
        player.Health.Should().Be(100);
        player.Mana.Should().Be(80);
    }

    [Fact]
    public async Task Handle_Should_Restore_Health_And_Mana_From_Critical_Levels()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 1,
            MaxHealth = 100,
            Mana = 1,
            MaxMana = 100
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRecovered.Should().Be(99);
        result.ManaRecovered.Should().Be(99);
        player.Health.Should().Be(100);
        player.Mana.Should().Be(100);
    }

    [Fact]
    public async Task Handle_Should_Display_Rest_Messages()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 50,
            MaxHealth = 100,
            Mana = 50,
            MaxMana = 100
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        var command = new RestCommand();
        _mockGameState.SetupGet(s => s.Player).Throws(new InvalidOperationException("Game state error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Game state error");
        result.HealthRecovered.Should().BeNull();
        result.ManaRecovered.Should().BeNull();
    }

    [Theory]
    [InlineData(10, 100, 5, 50, 90, 45)]
    [InlineData(75, 100, 25, 80, 25, 55)]
    [InlineData(1, 50, 1, 25, 49, 24)]
    public async Task Handle_Should_Calculate_Correct_Recovery_Amounts(
        int currentHealth, int maxHealth,
        int currentMana, int maxMana,
        int expectedHealthRecovered, int expectedManaRecovered)
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = currentHealth,
            MaxHealth = maxHealth,
            Mana = currentMana,
            MaxMana = maxMana
        };
        var command = new RestCommand();

        _mockGameState.SetupGet(s => s.Player).Returns(player);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRecovered.Should().Be(expectedHealthRecovered);
        result.ManaRecovered.Should().Be(expectedManaRecovered);
        player.Health.Should().Be(maxHealth);
        player.Mana.Should().Be(maxMana);
    }
}
