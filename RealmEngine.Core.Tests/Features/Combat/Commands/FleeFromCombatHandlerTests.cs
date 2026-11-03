using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Combat.Commands.FleeFromCombat;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for FleeFromCombatHandler.
/// </summary>
/// <remarks>
/// Every test scripts the single <see cref="RealmEngine.Shared.Abstractions.IRandomSource.NextDouble"/>
/// draw the handler makes, so both arms of the flee roll are asserted directly instead of retrying
/// until the wanted arm happens to land.
/// </remarks>
public class FleeFromCombatHandlerTests
{
    /// <summary>Flee chance is <c>min(0.90, 0.30 + 0.02 * dexterity)</c>.</summary>
    private const double RollThatSucceeds = 0.0;
    private const double RollThatFails = 0.99;

    private static (FleeFromCombatHandler Handler, FakeRandomSource Random) MakeHandler(double roll)
    {
        var random = FakeRandomSource.ReturningDoubles(roll);
        return (new FleeFromCombatHandler(NullLogger<FleeFromCombatHandler>.Instance, random), random);
    }

    [Fact]
    public async Task Should_Return_Success_When_Roll_Is_Below_Flee_Chance()
    {
        // Arrange - Dexterity = 30 → flee chance = min(0.90, 0.30 + 0.60) = 0.90; roll 0.0 succeeds.
        var (handler, random) = MakeHandler(RollThatSucceeds);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        random.DoubleCallCount.Should().Be(1, "the handler makes exactly one flee roll");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Return_Failure_When_Roll_Reaches_Flee_Chance()
    {
        // Arrange - Dexterity = 30 → flee chance = 0.90; roll 0.99 fails.
        var (handler, random) = MakeHandler(RollThatFails);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        random.DoubleCallCount.Should().Be(1);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Return_Success_Result_With_Message_When_Flee_Succeeds()
    {
        // Arrange
        var (handler, _) = MakeHandler(RollThatSucceeds);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30, Name = "Hero" };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Orc" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Orc");
    }

    [Fact]
    public async Task Should_Not_Reduce_Player_Health_When_Flee_Succeeds()
    {
        // Arrange
        var (handler, _) = MakeHandler(RollThatSucceeds);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - health should be unchanged on a successful flee
        player.Health.Should().Be(100);
    }

    [Fact]
    public async Task Should_Reduce_Player_Health_When_Flee_Fails()
    {
        // Arrange - Dexterity = 0 → flee chance 0.30; roll 0.99 fails.
        // counterDamage = max(1, BasePhysicalDamage / 2) = max(1, 5) = 5
        var (handler, _) = MakeHandler(RollThatFails);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 0 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Dragon" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - health should be reduced on a failed flee (enemy counterattacks)
        result.Success.Should().BeFalse();
        player.Health.Should().Be(95);
        result.Message.Should().Contain("Dragon");
    }

    [Fact]
    public async Task Should_Apply_Minimum_One_Counter_Damage_When_Flee_Fails_And_Enemy_Has_Zero_Damage()
    {
        // Arrange - Enemy with 0 BasePhysicalDamage → counterDamage = max(1, 0/2) = 1
        var (handler, _) = MakeHandler(RollThatFails);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 0 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 0, Name = "Rat" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        player.Health.Should().Be(99, "minimum 1 counter damage must be applied");
    }

    [Fact]
    public async Task Should_Log_To_CombatLog_When_Flee_Succeeds()
    {
        // Arrange
        var (handler, _) = MakeHandler(RollThatSucceeds);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30, Name = "Hero" };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Wolf" };
        var combatLog = new CombatLog();
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy, CombatLog = combatLog };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - a log entry should have been added
        combatLog.Entries.Should().HaveCountGreaterThan(0);
        combatLog.Entries[0].Message.Should().Contain("Wolf");
    }

    [Fact]
    public async Task Should_Log_To_CombatLog_When_Flee_Fails()
    {
        // Arrange
        var (handler, _) = MakeHandler(RollThatFails);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 0, Name = "Hero" };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Troll" };
        var combatLog = new CombatLog();
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy, CombatLog = combatLog };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - a log entry should have been added
        combatLog.Entries.Should().HaveCountGreaterThan(0);
        combatLog.Entries[0].Message.Should().Contain("Troll");
    }

    [Fact]
    public async Task Should_Work_Without_CombatLog()
    {
        // Arrange - CombatLog is optional
        var (handler, _) = MakeHandler(RollThatSucceeds);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act - should not throw even with no combat log
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_Cap_Player_Health_At_Zero_When_Counter_Damage_Exceeds_Health()
    {
        // Arrange - player has 1 HP, enemy deals big counter damage
        var (handler, _) = MakeHandler(RollThatFails);
        var player = new Character { Health = 1, MaxHealth = 100, Dexterity = 0 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 100, Name = "Giant" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert - counterDamage = max(1, 100/2) = 50; player.Health = max(0, 1-50) = 0
        player.Health.Should().Be(0, "health must not go below zero");
    }

    [Fact]
    public async Task Should_Use_Capped_Chance_When_Dexterity_Would_Exceed_Ninety_Percent()
    {
        // Arrange - Dexterity = 100 → raw chance 2.30, capped at 0.90; a 0.95 roll must still fail.
        var (handler, _) = MakeHandler(0.95);
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 100 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Ogre" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }
}
