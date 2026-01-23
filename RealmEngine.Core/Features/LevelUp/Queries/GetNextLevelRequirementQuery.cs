using MediatR;

namespace RealmEngine.Core.Features.LevelUp.Queries;

/// <summary>
/// Query to get the experience required for the next level.
/// </summary>
public class GetNextLevelRequirementQuery : IRequest<GetNextLevelRequirementResult>
{
    /// <summary>
    /// The name of the character to query.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;
}

/// <summary>
/// Result containing next level experience requirements.
/// </summary>
public class GetNextLevelRequirementResult
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
    /// Current character level.
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// Current experience points.
    /// </summary>
    public int CurrentExperience { get; set; }

    /// <summary>
    /// Total experience required for next level.
    /// </summary>
    public int RequiredExperience { get; set; }

    /// <summary>
    /// Remaining experience needed to level up.
    /// </summary>
    public int RemainingExperience { get; set; }

    /// <summary>
    /// Progress percentage toward next level (0-100).
    /// </summary>
    public double ProgressPercentage { get; set; }
}
