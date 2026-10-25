using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Queries.GetEnemyInfo;

/// <summary>
/// Query to get detailed information about an enemy.
/// </summary>
public record GetEnemyInfoQuery : IRequest<EnemyInfoDto>
{
    /// <summary>
    /// Gets the enemy to get information about.
    /// </summary>
    public required Enemy Enemy { get; init; }
}

/// <summary>
/// Data transfer object for enemy information.
/// </summary>
public record EnemyInfoDto
{
    /// <summary>
    /// Gets the name of the enemy.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the level of the enemy.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// Gets the current health of the enemy.
    /// </summary>
    public int Health { get; init; }

    /// <summary>
    /// Gets the maximum health of the enemy.
    /// </summary>
    public int MaxHealth { get; init; }

    /// <summary>
    /// Gets the attack power of the enemy.
    /// </summary>
    public int Attack { get; init; }

    /// <summary>
    /// Gets the defense rating of the enemy.
    /// </summary>
    public int Defense { get; init; }

    /// <summary>
    /// Gets the difficulty level of the enemy.
    /// </summary>
    public EnemyDifficulty Difficulty { get; init; }

    /// <summary>
    /// Gets a description of the enemy.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
