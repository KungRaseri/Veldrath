using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Party.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Party.Services;

/// <summary>
/// Behavioural tests for <see cref="PartyAIService"/> enemy/turn selection: the healer's
/// triage thresholds, the per-behaviour attack multipliers, criticals and the heal application.
/// </summary>
/// <remarks>
/// <see cref="PartyAIService"/> owns a private <see cref="Random"/>, so criticals are forced
/// through the member's dexterity (crit chance = dexterity/10%), which makes both arms deterministic
/// without a seam: dexterity 0 never crits, dexterity 1000 always crits.
/// </remarks>
[Trait("Category", "Service")]
public class PartyAIServiceTests
{
    private static PartyAIService CreateService() => new(NullLogger<PartyAIService>.Instance);

    private static PartyMember MakeMember(
        string name,
        PartyRole role = PartyRole.DPS,
        AIBehavior behavior = AIBehavior.Balanced,
        int health = 100,
        int maxHealth = 100,
        int strength = 20,
        int dexterity = 0,
        int wisdom = 10,
        int constitution = 10) => new()
        {
            Id = name,
            Name = name,
            ClassName = role.ToString(),
            Role = role,
            Behavior = behavior,
            Health = health,
            MaxHealth = maxHealth,
            Strength = strength,
            Dexterity = dexterity,
            Wisdom = wisdom,
            Constitution = constitution
        };

    private static Character MakeLeader(int health = 100, int maxHealth = 100) => new()
    {
        Name = "Hero",
        Health = health,
        MaxHealth = maxHealth
    };

    private static Enemy MakeEnemy(int constitution = 0, int health = 100) => new()
    {
        Name = "Goblin",
        Health = health,
        MaxHealth = health,
        Constitution = constitution
    };

    [Fact]
    public void DetermineAction_ReturnsNone_WhenMemberIsUnconscious()
    {
        var service = CreateService();
        var member = MakeMember("Dozer", health: 0);

        var action = service.DetermineAction(member, MakeEnemy(), MakeLeader(), []);

        action.Type.Should().Be(ActionType.None);
        action.Message.Should().Contain("unconscious");
    }

    [Fact]
    public void DetermineAction_Healer_HealsLeader_WhenLeaderIsBelowHalfHealth()
    {
        var service = CreateService();
        var healer = MakeMember("Cleric", PartyRole.Healer, wisdom: 10);
        var leader = MakeLeader(health: 20);

        var action = service.DetermineAction(healer, MakeEnemy(), leader, []);

        action.Type.Should().Be(ActionType.Heal);
        action.HealTarget.Should().Be("Leader");
        action.HealAmount.Should().Be(25, "heal amount is wisdom * 2.5");
        action.Message.Should().Contain("Hero").And.Contain("25");
    }

    [Fact]
    public void DetermineAction_Healer_HealsMostInjuredAlly_WhenLeaderIsHealthy()
    {
        var service = CreateService();
        var healer = MakeMember("Cleric", PartyRole.Healer, wisdom: 8);
        var wounded = MakeMember("Scout", health: 10);
        var healthy = MakeMember("Guard", health: 100);

        var action = service.DetermineAction(healer, MakeEnemy(), MakeLeader(), [wounded, healthy]);

        action.Type.Should().Be(ActionType.Heal);
        action.HealTarget.Should().Be("Scout");
        action.HealAmount.Should().Be(20);
    }

    [Fact]
    public void DetermineAction_Healer_FallsBackToAttack_WhenOnlySelfIsHurt()
    {
        var service = CreateService();
        var healer = MakeMember("Cleric", PartyRole.Healer, health: 20, maxHealth: 100, dexterity: 0);

        var action = service.DetermineAction(healer, MakeEnemy(), MakeLeader(), [MakeMember("Scout")]);

        action.Type.Should().Be(ActionType.AttackFallback, "the healer's own low health triggers the heal check but nobody else needs healing");
        action.Message.Should().Contain("attack instead of healing");
    }

    [Fact]
    public void DetermineAction_Healer_Attacks_WhenEveryoneIsHealthy()
    {
        var service = CreateService();
        var healer = MakeMember("Cleric", PartyRole.Healer, dexterity: 0);

        var action = service.DetermineAction(healer, MakeEnemy(), MakeLeader(), [MakeMember("Scout")]);

        action.Type.Should().Be(ActionType.Attack);
        action.Damage.Should().Be(22, "strength 20 + level 1 * 2, strength-based attack, no enemy defence");
    }

    [Theory]
    [InlineData(AIBehavior.Aggressive, 28)]
    [InlineData(AIBehavior.Balanced, 22)]
    [InlineData(AIBehavior.Defensive, 17)]
    [InlineData(AIBehavior.SupportFocus, 19)]
    public void DetermineAction_AppliesBehaviorMultiplierToAttackDamage(AIBehavior behavior, int expectedDamage)
    {
        var service = CreateService();
        var member = MakeMember("Fighter", behavior: behavior, dexterity: 0);

        var action = service.DetermineAction(member, MakeEnemy(), MakeLeader(), []);

        action.Type.Should().Be(ActionType.Attack);
        action.IsCritical.Should().BeFalse();
        action.Damage.Should().Be(expectedDamage);
    }

    [Fact]
    public void DetermineAction_DoublesDamage_WhenCritical()
    {
        var service = CreateService();
        // Critical chance = (dexterity / 100) * 0.10, so dexterity 1000 guarantees a critical.
        var member = MakeMember("Assassin", dexterity: 1000);

        var action = service.DetermineAction(member, MakeEnemy(), MakeLeader(), []);

        action.IsCritical.Should().BeTrue();
        action.Damage.Should().Be(44, "22 base damage doubled by the critical");
        action.Message.Should().Contain("CRITICAL HIT");
    }

    [Fact]
    public void DetermineAction_FloorsDamageAtOne_WhenEnemyDefenceExceedsAttack()
    {
        var service = CreateService();
        var member = MakeMember("Novice", dexterity: 0);

        var action = service.DetermineAction(member, MakeEnemy(constitution: 500), MakeLeader(), []);

        action.Damage.Should().Be(1);
        action.Message.Should().Contain("for 1 damage");
    }

    [Fact]
    public void DetermineAction_Healer_DoesNotHealUnconsciousAllies()
    {
        var service = CreateService();
        var healer = MakeMember("Cleric", PartyRole.Healer, health: 20, maxHealth: 100);
        var dead = MakeMember("Fallen", health: 0);

        var action = service.DetermineAction(healer, MakeEnemy(), MakeLeader(), [dead]);

        action.Type.Should().Be(ActionType.AttackFallback, "dead allies are not valid heal targets");
    }

    [Fact]
    public void ApplyHeal_IgnoresActionsThatAreNotHeals()
    {
        var service = CreateService();
        var leader = MakeLeader(health: 50, maxHealth: 100);
        var ally = MakeMember("Scout", health: 50, maxHealth: 100);
        var action = new PartyMemberAction { Type = ActionType.Attack, Damage = 10 };

        service.ApplyHeal(action, leader, [ally]);

        leader.Health.Should().Be(50);
        ally.Health.Should().Be(50);
    }

    [Fact]
    public void ApplyHeal_HealsLeader_AndClampsAtMaxHealth()
    {
        var service = CreateService();
        var leader = MakeLeader(health: 90, maxHealth: 100);
        var action = new PartyMemberAction { Type = ActionType.Heal, HealTarget = "Leader", HealAmount = 50 };

        service.ApplyHeal(action, leader, []);

        leader.Health.Should().Be(100);
    }

    [Fact]
    public void ApplyHeal_HealsNamedAlly()
    {
        var service = CreateService();
        var leader = MakeLeader();
        var ally = MakeMember("Scout", health: 30, maxHealth: 100);
        var action = new PartyMemberAction { Type = ActionType.Heal, HealTarget = "Scout", HealAmount = 25 };

        service.ApplyHeal(action, leader, [ally]);

        ally.Health.Should().Be(55);
        leader.Health.Should().Be(100);
    }

    [Fact]
    public void ApplyHeal_WithUnknownTarget_ChangesNothing()
    {
        var service = CreateService();
        var leader = MakeLeader();
        var ally = MakeMember("Scout", health: 30);
        var action = new PartyMemberAction { Type = ActionType.Heal, HealTarget = "Ghost", HealAmount = 25 };

        var act = () => service.ApplyHeal(action, leader, [ally]);

        act.Should().NotThrow();
        ally.Health.Should().Be(30);
        leader.Health.Should().Be(100);
    }
}
