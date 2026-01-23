using MediatR;
using System.Collections.Generic;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Command to explicitly level up a character (if XP requirements are met).
/// </summary>
public class LevelUpCommand : IRequest<LevelUpResult>
{
    /// <summary>
    /// The name of the character to level up.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;
}

/// <summary>
/// Result of level up operation.
/// </summary>
public class LevelUpResult
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
    /// The old level before leveling up.
    /// </summary>
    public int OldLevel { get; set; }

    /// <summary>
    /// The new level after leveling up.
    /// </summary>
    public int NewLevel { get; set; }

    /// <summary>
    /// Attribute points awarded from leveling up.
    /// </summary>
    public int AttributePointsGained { get; set; }

    /// <summary>
    /// Skill points awarded from leveling up.
    /// </summary>
    public int SkillPointsGained { get; set; }

    /// <summary>
    /// Stat increases from the level up (e.g., +10 HP, +5 Mana).
    /// </summary>
    public Dictionary<string, int> StatIncreases { get; set; } = new();

    /// <summary>
    /// New abilities unlocked at this level.
    /// </summary>
    public List<string> UnlockedAbilities { get; set; } = new();
}
