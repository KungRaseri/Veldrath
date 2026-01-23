using MediatR;
using System.Collections.Generic;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Query to preview stat gains at the next level.
/// </summary>
public class PreviewLevelUpQuery : IRequest<PreviewLevelUpResult>
{
    /// <summary>
    /// The name of the character to preview.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;
}

/// <summary>
/// Result containing preview of next level's stat gains.
/// </summary>
public class PreviewLevelUpResult
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
    /// Current level.
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// Next level after leveling up.
    /// </summary>
    public int NextLevel { get; set; }

    /// <summary>
    /// Attribute points that will be gained.
    /// </summary>
    public int AttributePointsGain { get; set; }

    /// <summary>
    /// Skill points that will be gained.
    /// </summary>
    public int SkillPointsGain { get; set; }

    /// <summary>
    /// Stat increases at next level (e.g., +10 MaxHealth, +5 MaxMana).
    /// </summary>
    public Dictionary<string, int> StatGains { get; set; } = new();

    /// <summary>
    /// Abilities that will be unlocked at next level.
    /// </summary>
    public List<string> UnlockedAbilities { get; set; } = new();

    /// <summary>
    /// Whether the character can currently level up (has sufficient XP).
    /// </summary>
    public bool CanLevelUp { get; set; }

    /// <summary>
    /// Experience required for next level.
    /// </summary>
    public int RequiredExperience { get; set; }
}
