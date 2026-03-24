using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to learn a new ability.
/// </summary>
public record LearnAbilityCommand : IRequest<LearnAbilityResult>
{
    /// <summary>Gets the character learning the ability.</summary>
    public required Character Character { get; init; }
    /// <summary>Gets the ability ID to learn.</summary>
    public required string AbilityId { get; init; }
}

/// <summary>
/// Result of learning an ability.
/// </summary>
public record LearnAbilityResult
{
    /// <summary>Gets a value indicating whether the ability was learned successfully.</summary>
    public bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the power that was learned.</summary>
    public Power? AbilityLearned { get; init; }
}
