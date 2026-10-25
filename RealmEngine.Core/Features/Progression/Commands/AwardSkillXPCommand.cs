using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to award XP to a character's skill.
/// </summary>
public record AwardSkillXPCommand : IRequest<AwardSkillXPResult>
{
    /// <summary>Gets the character receiving skill XP.</summary>
    public required Character Character { get; init; }
    /// <summary>Gets the skill ID to award XP to.</summary>
    public required string SkillId { get; init; }
    /// <summary>Gets the amount of XP to award.</summary>
    public required int XPAmount { get; init; }
    /// <summary>Gets the action source that generated the XP.</summary>
    public string ActionSource { get; init; } = string.Empty;
}

/// <summary>
/// Result of awarding skill XP.
/// </summary>
public record AwardSkillXPResult
{
    /// <summary>Gets the skill ID that received XP.</summary>
    public required string SkillId { get; init; }
    /// <summary>Gets the new rank after awarding XP.</summary>
    public int NewRank { get; init; }
    /// <summary>Gets the number of ranks gained.</summary>
    public int RanksGained { get; init; }
    /// <summary>Gets the list of notifications about rank-ups.</summary>
    public List<string> Notifications { get; init; } = new();
    /// <summary>Gets a value indicating whether the skill ranked up.</summary>
    public bool DidRankUp => RanksGained > 0;
}
