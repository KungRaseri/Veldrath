using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.AttackEnemy;

/// <summary>
/// Command to execute a player attack against an enemy.
/// </summary>
public record AttackEnemyCommand : IRequest<AttackEnemyResult>
{
    /// <summary>
    /// Gets the player character performing the attack.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the enemy being attacked.
    /// </summary>
    public required Enemy Enemy { get; init; }

    /// <summary>
    /// Gets the combat log for recording combat events.
    /// </summary>
    public CombatLog? CombatLog { get; init; }
}

/// <summary>
/// Result of an attack command.
/// </summary>
public record AttackEnemyResult
{
    /// <summary>
    /// Gets the amount of damage dealt.
    /// </summary>
    public int Damage { get; init; }

    /// <summary>
    /// Gets a value indicating whether the attack was a critical hit.
    /// </summary>
    public bool IsCritical { get; init; }

    /// <summary>
    /// Gets a value indicating whether the enemy was defeated.
    /// </summary>
    public bool IsEnemyDefeated { get; init; }

    /// <summary>
    /// Gets the experience gained from defeating the enemy.
    /// </summary>
    public int ExperienceGained { get; init; }

    /// <summary>
    /// Gets the gold gained from defeating the enemy.
    /// </summary>
    public int GoldGained { get; init; }
}
