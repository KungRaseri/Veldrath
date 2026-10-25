using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands.UseCombatItem;

/// <summary>
/// Command to use a consumable item during combat.
/// </summary>
public record UseCombatItemCommand : IRequest<UseCombatItemResult>
{
    /// <summary>
    /// Gets the player character using the item.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the item being used.
    /// </summary>
    public required Item Item { get; init; }

    /// <summary>
    /// Gets the combat log for recording combat events.
    /// </summary>
    public CombatLog? CombatLog { get; init; }
}

/// <summary>
/// Result of using a combat item.
/// </summary>
public record UseCombatItemResult
{
    /// <summary>
    /// Gets a value indicating whether the item was successfully used.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the amount of health restored.
    /// </summary>
    public int HealthRestored { get; init; }

    /// <summary>
    /// Gets the amount of mana restored.
    /// </summary>
    public int ManaRestored { get; init; }

    /// <summary>
    /// Gets a message describing the item use result.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
