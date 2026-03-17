using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Combat.Commands.DefendAction;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for DefendActionHandler.
/// </summary>
public class DefendActionHandlerTests
{
    private readonly DefendActionHandler _handler = new(NullLogger<DefendActionHandler>.Instance);

    [Fact]
    public async Task Handle_Should_Return_DefenseBonus_Based_On_Constitution()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Constitution = 20,
            Health = 100,
            MaxHealth = 100
        };
        var command = new DefendActionCommand
        {
            Player = player
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DefenseBonus.Should().Be(10); // Constitution (20) / 2
        result.Message.Should().Contain("Defense +10");
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(20, 10)]
    [InlineData(30, 15)]
    [InlineData(15, 7)]
    public async Task Handle_Should_Calculate_Correct_DefenseBonus(int constitution, int expectedBonus)
    {
        // Arrange
        var player = new Character
        {
            Name = "Defender",
            Constitution = constitution,
            Health = 80,
            MaxHealth = 100
        };
        var command = new DefendActionCommand
        {
            Player = player
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.DefenseBonus.Should().Be(expectedBonus);
    }

    [Fact]
    public async Task Handle_Should_Add_Entry_To_CombatLog_When_Provided()
    {
        // Arrange
        var player = new Character
        {
            Name = "Warrior",
            Constitution = 16,
            Health = 90,
            MaxHealth = 100
        };
        var combatLog = new CombatLog();
        var command = new DefendActionCommand
        {
            Player = player,
            CombatLog = combatLog
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        combatLog.Entries.Should().HaveCount(1);
        combatLog.Entries[0].Message.Should().Contain("Warrior takes a defensive stance!");
        combatLog.Entries[0].Message.Should().Contain("Defense +8");
    }

    [Fact]
    public async Task Handle_Should_Not_Throw_When_CombatLog_Is_Null()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Constitution = 12,
            Health = 70,
            MaxHealth = 100
        };
        var command = new DefendActionCommand
        {
            Player = player,
            CombatLog = null
        };

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        var defenseResult = await _handler.Handle(command, CancellationToken.None);
        defenseResult.DefenseBonus.Should().Be(6);
    }

    [Fact]
    public async Task Handle_Should_Return_Message_With_Player_Friendly_Text()
    {
        // Arrange
        var player = new Character
        {
            Name = "Knight",
            Constitution = 18,
            Health = 100,
            MaxHealth = 100
        };
        var command = new DefendActionCommand
        {
            Player = player
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Contain("You brace yourself for impact!");
    }

    [Fact]
    public async Task Handle_Should_Work_With_Low_Constitution()
    {
        // Arrange
        var player = new Character
        {
            Name = "Mage",
            Constitution = 5,
            Health = 50,
            MaxHealth = 50
        };
        var command = new DefendActionCommand
        {
            Player = player
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.DefenseBonus.Should().Be(2); // 5 / 2 = 2 (integer division)
    }

    [Fact]
    public async Task Handle_Should_Work_With_High_Constitution()
    {
        // Arrange
        var player = new Character
        {
            Name = "Tank",
            Constitution = 50,
            Health = 200,
            MaxHealth = 200
        };
        var command = new DefendActionCommand
        {
            Player = player
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.DefenseBonus.Should().Be(25);
    }
}
