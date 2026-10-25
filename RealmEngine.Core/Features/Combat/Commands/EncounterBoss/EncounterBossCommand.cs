using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.EncounterBoss;

/// <summary>
/// Command to initiate a boss encounter.
/// Returns special information about the boss including enhanced rewards and difficulty warnings.
/// </summary>
public record EncounterBossCommand : IRequest<BossEncounterResult>
{
    /// <summary>
    /// Gets the category of the boss (e.g., "dragon", "demon").
    /// </summary>
    public required string BossCategory { get; init; }

    /// <summary>
    /// Gets the name of the specific boss to encounter.
    /// </summary>
    public required string BossName { get; init; }
}

/// <summary>
/// Result of a boss encounter command with detailed boss information.
/// </summary>
public record BossEncounterResult
{
    /// <summary>
    /// Gets a value indicating whether the boss was successfully encountered.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the boss enemy instance.
    /// </summary>
    public Enemy? Boss { get; init; }

    /// <summary>
    /// Gets an error message if the encounter failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets detailed information about the boss.
    /// </summary>
    public BossInfo? Info { get; init; }
}

/// <summary>
/// Detailed information about a boss enemy.
/// </summary>
public record BossInfo
{
    /// <summary>
    /// Gets the name of the boss.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the title of the boss.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the level of the boss.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// Gets the recommended player level for this boss.
    /// </summary>
    public int RecommendedPlayerLevel { get; init; }

    /// <summary>
    /// Gets the difficulty rating of the boss.
    /// </summary>
    public EnemyDifficulty Difficulty { get; init; }

    /// <summary>
    /// Gets the estimated experience points for defeating the boss.
    /// </summary>
    public int EstimatedXP { get; init; }

    /// <summary>
    /// Gets the estimated gold reward for defeating the boss.
    /// </summary>
    public int EstimatedGold { get; init; }

    /// <summary>
    /// Gets the total health of the boss.
    /// </summary>
    public int HealthTotal { get; init; }

    /// <summary>
    /// Gets the attack power of the boss.
    /// </summary>
    public int AttackPower { get; init; }

    /// <summary>
    /// Gets the defense rating of the boss.
    /// </summary>
    public int DefenseRating { get; init; }

    /// <summary>
    /// Gets the list of abilities the boss can use.
    /// </summary>
    public List<string> Abilities { get; init; } = new();

    /// <summary>
    /// Gets the list of special traits the boss possesses.
    /// </summary>
    public List<string> SpecialTraits { get; init; } = new();

    /// <summary>
    /// Gets a warning message about the boss's difficulty.
    /// </summary>
    public string WarningMessage { get; init; } = string.Empty;
}
