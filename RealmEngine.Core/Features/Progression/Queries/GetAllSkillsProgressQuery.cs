using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Query to get all skills progress for character sheet.
/// </summary>
public record GetAllSkillsProgressQuery : IRequest<GetAllSkillsProgressResult>
{
    /// <summary>Gets the character to get skills progress for.</summary>
    public required Character Character { get; init; }
}

/// <summary>
/// Result containing all skills progress.
/// </summary>
public record GetAllSkillsProgressResult
{
    /// <summary>Gets the list of skill progress displays.</summary>
    public required List<SkillProgressDisplay> Skills { get; init; }
}

/// <summary>
/// Display model for skill progress in character sheet.
/// </summary>
public record SkillProgressDisplay
{
    /// <summary>Gets the skill ID.</summary>
    public required string SkillId { get; init; }
    /// <summary>Gets the skill name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the skill category.</summary>
    public required string Category { get; init; }
    /// <summary>Gets the current rank.</summary>
    public int CurrentRank { get; init; }
    /// <summary>Gets the current XP.</summary>
    public int CurrentXP { get; init; }
    /// <summary>Gets the XP needed for next rank.</summary>
    public int XPToNextRank { get; init; }
    /// <summary>Gets the progress percentage to next rank.</summary>
    public double ProgressPercent { get; init; }
    /// <summary>Gets the current rank effect description.</summary>
    public required string CurrentEffect { get; init; }
    /// <summary>Gets the next rank effect description.</summary>
    public required string NextRankEffect { get; init; }
}
