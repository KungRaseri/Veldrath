using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to learn a new power.
/// </summary>
public record LearnPowerCommand : IRequest<LearnPowerResult>
{
    /// <summary>Gets the character learning the ability.</summary>
    public required Character Character { get; init; }
    /// <summary>Gets the power ID to learn.</summary>
    public required string PowerId { get; init; }
}

/// <summary>
/// Result of learning a power.
/// </summary>
public record LearnPowerResult
{
    /// <summary>Gets a value indicating whether the power was learned successfully.</summary>
    public bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the power that was learned.</summary>
    public Power? PowerLearned { get; init; }
}
