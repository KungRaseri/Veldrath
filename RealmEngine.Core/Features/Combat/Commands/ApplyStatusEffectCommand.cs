using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands;

/// <summary>
/// Command to apply a status effect to a character or enemy.
/// </summary>
public record ApplyStatusEffectCommand : IRequest<ApplyStatusEffectResult>
{
    /// <summary>Gets the target character (if applying to player).</summary>
    public Character? TargetCharacter { get; init; }

    /// <summary>Gets the target enemy (if applying to enemy).</summary>
    public Enemy? TargetEnemy { get; init; }

    /// <summary>Gets the status effect to apply.</summary>
    public required StatusEffect Effect { get; init; }

    /// <summary>Gets whether to allow stacking if effect already exists.</summary>
    public bool AllowStacking { get; init; } = false;

    /// <summary>Gets whether to refresh duration if effect already exists.</summary>
    public bool RefreshDuration { get; init; } = true;
}

/// <summary>
/// Result of applying a status effect.
/// </summary>
public record ApplyStatusEffectResult
{
    /// <summary>Indicates if the effect was successfully applied.</summary>
    public bool Success { get; init; }

    /// <summary>Indicates if the effect was resisted.</summary>
    public bool Resisted { get; init; }

    /// <summary>Indicates if the effect was stacked.</summary>
    public bool Stacked { get; init; }

    /// <summary>Indicates if the duration was refreshed.</summary>
    public bool DurationRefreshed { get; init; }

    /// <summary>The current stack count after application.</summary>
    public int CurrentStacks { get; init; }

    /// <summary>The resistance percentage that was applied (0-100).</summary>
    public int ResistancePercentage { get; init; }

    /// <summary>Result message for logging/display.</summary>
    public string Message { get; init; } = string.Empty;
}
