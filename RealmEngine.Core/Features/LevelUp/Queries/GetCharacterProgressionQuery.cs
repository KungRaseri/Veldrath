using MediatR;
using System.Collections.Generic;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Query to get complete character progression information.
/// </summary>
public class GetCharacterProgressionQuery : IRequest<GetCharacterProgressionResult>
{
    /// <summary>
    /// The name of the character to query.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;
}

/// <summary>
/// Result containing complete character progression details.
/// </summary>
public class GetCharacterProgressionResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Character level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Total experience points.
    /// </summary>
    public int Experience { get; set; }

    /// <summary>
    /// Experience required for next level.
    /// </summary>
    public int ExperienceToNextLevel { get; set; }

    /// <summary>
    /// Unallocated attribute points.
    /// </summary>
    public int UnallocatedAttributePoints { get; set; }

    /// <summary>
    /// Unallocated skill points.
    /// </summary>
    public int UnallocatedSkillPoints { get; set; }

    /// <summary>
    /// Current attribute values.
    /// </summary>
    public Dictionary<string, int> Attributes { get; set; } = new();

    /// <summary>
    /// Current skill values.
    /// </summary>
    public Dictionary<string, int> Skills { get; set; } = new();

    /// <summary>
    /// List of learned ability references.
    /// </summary>
    public List<string> LearnedAbilities { get; set; } = new();

    /// <summary>
    /// List of learned spell references.
    /// </summary>
    public List<string> LearnedSpells { get; set; } = new();

    /// <summary>
    /// Total playtime in seconds.
    /// </summary>
    public int PlaytimeSeconds { get; set; }

    /// <summary>
    /// Number of enemies defeated.
    /// </summary>
    public int EnemiesDefeated { get; set; }

    /// <summary>
    /// Number of quests completed.
    /// </summary>
    public int QuestsCompleted { get; set; }
}
