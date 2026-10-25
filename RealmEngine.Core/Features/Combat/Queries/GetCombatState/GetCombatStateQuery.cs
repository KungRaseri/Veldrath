using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Queries.GetCombatState;

/// <summary>
/// Query to get the current state of combat.
/// </summary>
public record GetCombatStateQuery : IRequest<CombatStateDto>
{
    /// <summary>
    /// Gets the player character in combat.
    /// </summary>
    public required Character Player { get; init; }

    /// <summary>
    /// Gets the enemy in combat.
    /// </summary>
    public required Enemy Enemy { get; init; }
}

/// <summary>
/// Data transfer object for combat state.
/// </summary>
public record CombatStateDto
{
    /// <summary>
    /// Gets the player's health as a percentage.
    /// </summary>
    public int PlayerHealthPercentage { get; init; }

    /// <summary>
    /// Gets the enemy's health as a percentage.
    /// </summary>
    public int EnemyHealthPercentage { get; init; }

    /// <summary>
    /// Gets a value indicating whether the player can flee.
    /// </summary>
    public bool PlayerCanFlee { get; init; }

    /// <summary>
    /// Gets a value indicating whether the player has usable items.
    /// </summary>
    public bool PlayerHasItems { get; init; }

    /// <summary>
    /// Gets the list of available combat actions.
    /// </summary>
    public List<string> AvailableActions { get; init; } = new();

    /// <summary>
    /// Gets the list of available abilities.
    /// </summary>
    public List<string> AvailableAbilities { get; init; } = new();

    /// <summary>
    /// Gets the list of available spells.
    /// </summary>
    public List<string> AvailableSpells { get; init; } = new();
}
