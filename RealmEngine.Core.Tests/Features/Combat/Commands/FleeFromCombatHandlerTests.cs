using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Combat.Commands.FleeFromCombat;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for FleeFromCombatHandler.
/// </summary>
public class FleeFromCombatHandlerTests
{
    private readonly FleeFromCombatHandler _handler = new(NullLogger<FleeFromCombatHandler>.Instance);

    [Fact]
    public async Task Should_Return_Success_When_Player_Has_Max_Dexterity()
    {
        // Arrange - Dexterity = 30 → flee chance = min(90%, 30% + 60%) = 90%
        // Run many iterations; statistically essentially guaranteed to succeed at least once
        // We test determinism by using Dexterity = 30 (capped at 90%) which means
        // 9 out of 10 attempts succeed; we verify at least one of 20 succeeds.
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        int successCount = 0;

        for (int i = 0; i < 20; i++)
        {
            // Reset health each time in case of counters
            player.Health = 100;
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };
            var result = await _handler.Handle(command, CancellationToken.None);
            if (result.Success)
                successCount++;
        }

        // Assert - with 90% flee chance across 20 tries, P(zero successes) = 0.1^20 ≈ 0
        successCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Return_Success_Result_With_Message_When_Flee_Succeeds()
    {
        // Arrange - Use a character with extremely high dexterity so flee always caps at 90%
        // and retry until we get a success to validate the success path
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Orc" };
        FleeFromCombatResult? successResult = null;

        for (int attempt = 0; attempt < 100 && successResult == null; attempt++)
        {
            var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30, Name = "Hero" };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };
            var result = await _handler.Handle(command, CancellationToken.None);
            if (result.Success)
                successResult = result;
        }

        // Assert
        successResult.Should().NotBeNull("Expected at least one successful flee in 100 attempts");
        successResult!.Message.Should().Contain("Orc");
    }

    [Fact]
    public async Task Should_Not_Reduce_Player_Health_When_Flee_Succeeds()
    {
        // Arrange
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        int initialHealth = 100;

        for (int attempt = 0; attempt < 100; attempt++)
        {
            var player = new Character { Health = initialHealth, MaxHealth = 100, Dexterity = 30 };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };
            var result = await _handler.Handle(command, CancellationToken.None);

            if (result.Success)
            {
                // Assert - health should be unchanged on successful flee
                player.Health.Should().Be(initialHealth);
                return;
            }
        }

        // If we never got a success in 100 tries with 90% chance, something is wrong
        Assert.Fail("Expected a successful flee in 100 attempts with Dexterity=30");
    }

    [Fact]
    public async Task Should_Reduce_Player_Health_When_Flee_Fails()
    {
        // Arrange - with Dexterity=0, flee chance is 30%, so failure is 70% likely
        // We retry until we get a failure to validate the failure path
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Dragon" };

        for (int attempt = 0; attempt < 100; attempt++)
        {
            var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 0 };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };
            var result = await _handler.Handle(command, CancellationToken.None);

            if (!result.Success)
            {
                // Assert - health should be reduced on failed flee (enemy counterattacks)
                // counterDamage = max(1, BasePhysicalDamage / 2) = max(1, 5) = 5
                player.Health.Should().Be(95);
                result.Message.Should().Contain("Dragon");
                return;
            }
        }

        // P(zero failures in 100 attempts with 70% failure rate) ≈ 0
        Assert.Fail("Expected at least one failed flee in 100 attempts with Dexterity=0");
    }

    [Fact]
    public async Task Should_Apply_Minimum_One_Counter_Damage_When_Flee_Fails_And_Enemy_Has_Zero_Damage()
    {
        // Arrange - Enemy with 0 BasePhysicalDamage → counterDamage = max(1, 0/2) = max(1, 0) = 1
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 0, Name = "Rat" };

        for (int attempt = 0; attempt < 100; attempt++)
        {
            var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 0 };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };
            var result = await _handler.Handle(command, CancellationToken.None);

            if (!result.Success)
            {
                player.Health.Should().Be(99, "minimum 1 counter damage must be applied");
                return;
            }
        }

        Assert.Fail("Expected at least one failed flee in 100 attempts with Dexterity=0");
    }

    [Fact]
    public async Task Should_Log_To_CombatLog_When_Flee_Succeeds()
    {
        // Arrange
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Wolf" };
        var combatLog = new CombatLog();

        for (int attempt = 0; attempt < 100; attempt++)
        {
            combatLog = new CombatLog();
            var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30, Name = "Hero" };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy, CombatLog = combatLog };
            var result = await _handler.Handle(command, CancellationToken.None);

            if (result.Success)
            {
                // Assert - a log entry should have been added
                combatLog.Entries.Should().HaveCountGreaterThan(0);
                combatLog.Entries[0].Message.Should().Contain("Wolf");
                return;
            }
        }

        Assert.Fail("Expected at least one successful flee in 100 attempts with Dexterity=30");
    }

    [Fact]
    public async Task Should_Log_To_CombatLog_When_Flee_Fails()
    {
        // Arrange
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Troll" };

        for (int attempt = 0; attempt < 100; attempt++)
        {
            var combatLog = new CombatLog();
            var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 0, Name = "Hero" };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy, CombatLog = combatLog };
            var result = await _handler.Handle(command, CancellationToken.None);

            if (!result.Success)
            {
                // Assert - a log entry should have been added
                combatLog.Entries.Should().HaveCountGreaterThan(0);
                combatLog.Entries[0].Message.Should().Contain("Troll");
                return;
            }
        }

        Assert.Fail("Expected at least one failed flee in 100 attempts with Dexterity=0");
    }

    [Fact]
    public async Task Should_Work_Without_CombatLog()
    {
        // Arrange - CombatLog is optional
        var player = new Character { Health = 100, MaxHealth = 100, Dexterity = 30 };
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 10, Name = "Goblin" };
        var command = new FleeFromCombatCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act - should not throw even with no combat log
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_Cap_Player_Health_At_Zero_When_Counter_Damage_Exceeds_Health()
    {
        // Arrange - player has 1 HP, enemy deals big counter damage
        var enemy = new Enemy { Health = 50, BasePhysicalDamage = 100, Name = "Giant" };

        for (int attempt = 0; attempt < 100; attempt++)
        {
            var player = new Character { Health = 1, MaxHealth = 100, Dexterity = 0 };
            var command = new FleeFromCombatCommand { Player = player, Enemy = enemy };
            var result = await _handler.Handle(command, CancellationToken.None);

            if (!result.Success)
            {
                // counterDamage = max(1, 100/2) = 50; player.Health = max(0, 1-50) = 0
                player.Health.Should().Be(0, "health must not go below zero");
                return;
            }
        }

        Assert.Fail("Expected at least one failed flee in 100 attempts with Dexterity=0");
    }
}
