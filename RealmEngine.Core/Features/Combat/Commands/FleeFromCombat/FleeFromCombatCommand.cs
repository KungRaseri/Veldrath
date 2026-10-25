using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.FleeFromCombat;

/// <summary>
/// Command to attempt to flee from combat.
/// </summary>
public record FleeFromCombatCommand : IRequest<FleeFromCombatResult>
{
    /// <summary>
    /// Gets the player character attempting to flee.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the enemy the player is fleeing from.
    /// </summary>
    public required Enemy Enemy { get; init; }

    /// <summary>
    /// Gets the combat log for recording combat events.
    /// </summary>
    public CombatLog? CombatLog { get; init; }
}

/// <summary>
/// Result of a flee attempt.
/// </summary>
public record FleeFromCombatResult
{
    /// <summary>
    /// Gets a value indicating whether the flee attempt was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the flee attempt result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
