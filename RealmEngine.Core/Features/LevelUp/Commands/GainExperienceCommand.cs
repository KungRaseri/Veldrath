using MediatR;

namespace RealmEngine.Core.Features.LevelUp.Commands;

/// <summary>
/// Command to award experience points to a character.
/// </summary>
public class GainExperienceCommand : IRequest<GainExperienceResult>
{
    /// <summary>
    /// The name of the character gaining experience.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// The amount of experience points to award.
    /// </summary>
    public int ExperienceAmount { get; set; }

    /// <summary>
    /// Optional source of the experience (e.g., "Combat", "Quest", "Crafting").
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Result of gaining experience.
/// </summary>
public class GainExperienceResult
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
    /// Character's new experience total.
    /// </summary>
    public int NewExperience { get; set; }

    /// <summary>
    /// Character's current level.
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// Whether the character leveled up.
    /// </summary>
    public bool LeveledUp { get; set; }

    /// <summary>
    /// The new level if leveled up.
    /// </summary>
    public int? NewLevel { get; set; }

    /// <summary>
    /// Experience required for next level.
    /// </summary>
    public int ExperienceToNextLevel { get; set; }
}
