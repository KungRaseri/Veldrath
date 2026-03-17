using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Party.Services;

/// <summary>
/// Service for handling AI-controlled party member actions in combat.
/// </summary>
public class PartyAIService
{
    private readonly Random _random = new();

    /// <summary>
    /// Determines the best action for a party member based on their role and behavior.
    /// </summary>
    public PartyMemberAction DetermineAction(PartyMember member, Enemy enemy, Character leader, List<PartyMember> allies)
    {
        // Check if member can act
        if (member.Health <= 0)
        {
            return new PartyMemberAction
            {
                Type = ActionType.None,
                Message = $"{member.Name} is unconscious."
            };
        }

        // Check if member has low health and needs healing (if healer)
        if (member.Role == PartyRole.Healer && ShouldHeal(member, leader, allies))
        {
            return DetermineHealAction(member, leader, allies);
        }

        // Default to attack based on behavior
        return DetermineAttackAction(member, enemy);
    }

    /// <summary>
    /// Checks if healer should heal instead of attacking.
    /// </summary>
    private bool ShouldHeal(PartyMember healer, Character leader, List<PartyMember> allies)
    {
        // Leader below 50% HP
        if (leader.Health < leader.MaxHealth * 0.5) return true;

        // Any ally below 30% HP
        if (allies.Any(a => a.IsAlive && a.Health < a.MaxHealth * 0.3)) return true;

        // Healer's own health below 40%
        if (healer.Health < healer.MaxHealth * 0.4) return true;

        return false;
    }

    /// <summary>
    /// Determines heal action target.
    /// </summary>
    private PartyMemberAction DetermineHealAction(PartyMember healer, Character leader, List<PartyMember> allies)
    {
        // Find most injured target
        var leaderHealthPercent = (double)leader.Health / leader.MaxHealth;
        var lowestAlly = allies.Where(a => a.IsAlive)
            .OrderBy(a => (double)a.Health / a.MaxHealth)
            .FirstOrDefault();

        var allyHealthPercent = lowestAlly != null ? (double)lowestAlly.Health / lowestAlly.MaxHealth : 1.0;

        // Heal leader if most injured
        if (leaderHealthPercent < allyHealthPercent && leaderHealthPercent < 0.7)
        {
            var healAmount = (int)(healer.Wisdom * 2.5);
            return new PartyMemberAction
            {
                Type = ActionType.Heal,
                HealTarget = "Leader",
                HealAmount = healAmount,
                Message = $"{healer.Name} casts Heal on {leader.Name} for {healAmount} HP!"
            };
        }

        // Heal ally if found
        if (lowestAlly != null && allyHealthPercent < 0.7)
        {
            var healAmount = (int)(healer.Wisdom * 2.5);
            return new PartyMemberAction
            {
                Type = ActionType.Heal,
                HealTarget = lowestAlly.Name,
                HealAmount = healAmount,
                Message = $"{healer.Name} casts Heal on {lowestAlly.Name} for {healAmount} HP!"
            };
        }

        // No one needs healing, attack instead
        return new PartyMemberAction
        {
            Type = ActionType.AttackFallback,
            Message = $"{healer.Name} decides to attack instead of healing."
        };
    }

    /// <summary>
    /// Determines attack action for a party member.
    /// </summary>
    private PartyMemberAction DetermineAttackAction(PartyMember member, Enemy enemy)
    {
        // Calculate base damage
        int baseDamage = member.GetAttack();

        // Apply behavior modifier
        double behaviorMultiplier = member.Behavior switch
        {
            AIBehavior.Aggressive => 1.3,
            AIBehavior.Balanced => 1.0,
            AIBehavior.Defensive => 0.8,
            AIBehavior.SupportFocus => 0.9,
            _ => 1.0
        };

        baseDamage = (int)(baseDamage * behaviorMultiplier);

        // Check for critical hit
        bool isCritical = _random.NextDouble() < member.GetCriticalChance();
        if (isCritical)
        {
            baseDamage = (int)(baseDamage * 2.0);
        }

        // Apply enemy defense
        int finalDamage = Math.Max(1, baseDamage - enemy.GetPhysicalDefense());

        return new PartyMemberAction
        {
            Type = ActionType.Attack,
            Damage = finalDamage,
            IsCritical = isCritical,
            Message = isCritical 
                ? $"{member.Name} lands a CRITICAL HIT on {enemy.Name} for {finalDamage} damage!"
                : $"{member.Name} attacks {enemy.Name} for {finalDamage} damage."
        };
    }

    /// <summary>
    /// Applies a heal action to the target.
    /// </summary>
    public void ApplyHeal(PartyMemberAction action, Character leader, List<PartyMember> allies)
    {
        if (action.Type != ActionType.Heal) return;

        if (action.HealTarget == "Leader")
        {
            leader.Health = Math.Min(leader.Health + action.HealAmount, leader.MaxHealth);
            _logger.LogInformation("Leader {Name} healed for {Amount} HP", leader.Name, action.HealAmount);
        }
        else
        {
            var ally = allies.FirstOrDefault(a => a.Name == action.HealTarget);
            if (ally != null)
            {
                ally.Heal(action.HealAmount);
                _logger.LogInformation("Ally {Name} healed for {Amount} HP", ally.Name, action.HealAmount);
            }
        }
    }
}

/// <summary>
/// Represents an action taken by a party member.
/// </summary>
public class PartyMemberAction
{
    /// <summary>
    /// Type of action.
    /// </summary>
    public ActionType Type { get; set; } = ActionType.None;

    /// <summary>
    /// Damage dealt (for attacks).
    /// </summary>
    public int Damage { get; set; }

    /// <summary>
    /// Whether attack was critical.
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Heal target name (for heals).
    /// </summary>
    public string? HealTarget { get; set; }

    /// <summary>
    /// Heal amount (for heals).
    /// </summary>
    public int HealAmount { get; set; }

    /// <summary>
    /// Action message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Action types for party members.
/// </summary>
public enum ActionType
{
    /// <summary>No action (unconscious).</summary>
    None,
    /// <summary>Attack enemy.</summary>
    Attack,
    /// <summary>Heal ally.</summary>
    Heal,
    /// <summary>Attack instead of heal (no one needs healing).</summary>
    AttackFallback
}
