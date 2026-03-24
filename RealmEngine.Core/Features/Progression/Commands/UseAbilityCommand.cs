using RealmEngine.Shared.Models;
using MediatR;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Command to use a power in combat or exploration.
/// </summary>
public record UsePowerCommand : IRequest<UsePowerResult>
{
    /// <summary>Gets the character using the ability.</summary>
    public required Character User { get; init; }
    /// <summary>Gets the ability ID to use.</summary>
    public required string PowerId { get; init; }
    /// <summary>Gets the target character, if any.</summary>
    public Character? TargetCharacter { get; init; }
    /// <summary>Gets the target enemy, if any.</summary>
    public Enemy? TargetEnemy { get; init; }
}

/// <summary>
/// Result of using a power.
/// </summary>
public record UsePowerResult
{
    /// <summary>Gets a value indicating whether the ability use succeeded.</summary>
    public bool Success { get; init; }
    /// <summary>Gets the result message.</summary>
    public required string Message { get; init; }
    /// <summary>Gets the damage dealt by the ability.</summary>
    public int DamageDealt { get; init; }
    /// <summary>Gets the healing done by the ability.</summary>
    public int HealingDone { get; init; }
    /// <summary>Gets the mana cost of the ability.</summary>
    public int ManaCost { get; init; }
    /// <summary>Gets the power that was used.</summary>
    public Power? PowerUsed { get; init; }
}
