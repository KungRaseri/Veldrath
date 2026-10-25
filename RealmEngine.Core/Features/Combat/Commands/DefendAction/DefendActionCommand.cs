using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.DefendAction;

/// <summary>
/// Command to have the player defend (reduce incoming damage).
/// </summary>
public record DefendActionCommand : IRequest<DefendActionResult>
{
    /// <summary>
    /// Gets the player character performing the defend action.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the combat log for recording combat events.
    /// </summary>
    public CombatLog? CombatLog { get; init; }
}

/// <summary>
/// Result of a defend action.
/// </summary>
public record DefendActionResult
{
    /// <summary>
    /// Gets the defense bonus provided by the defend action.
    /// </summary>
    public int DefenseBonus { get; init; }

    /// <summary>
    /// Gets a message describing the defend action result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
