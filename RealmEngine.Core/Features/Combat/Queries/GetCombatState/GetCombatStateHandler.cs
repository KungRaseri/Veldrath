using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Queries.GetCombatState;

/// <summary>
/// Handles the GetCombatState query.
/// </summary>
public class GetCombatStateHandler : IRequestHandler<GetCombatStateQuery, CombatStateDto>
{
    /// <summary>
    /// Handles the get combat state query and returns the current combat state.
    /// </summary>
    /// <param name="request">The get combat state query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the combat state.</returns>
    public Task<CombatStateDto> Handle(GetCombatStateQuery request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var enemy = request.Enemy;

        // Get base available actions
        var availableActions = new List<string> { "Attack", "Defend", "Use Item", "Flee" };

        // Get available abilities (not on cooldown and have mana)
        var availableAbilities = player.LearnedAbilities.Values
            .Where(a =>
            {
                var cooldownRemaining = player.AbilityCooldowns.GetValueOrDefault(a.AbilityId, 0);
                return cooldownRemaining == 0;
            })
            .Select(a => a.AbilityId)
            .ToList();

        // Get available spells (not on cooldown and have mana)
        var availableSpells = player.LearnedSpells.Values
            .Where(s =>
            {
                var cooldownRemaining = player.SpellCooldowns.GetValueOrDefault(s.SpellId, 0);
                return cooldownRemaining == 0;
            })
            .Select(s => s.SpellId)
            .ToList();

        // Add to actions list if there are available abilities or spells
        if (availableAbilities.Any())
        {
            availableActions.Insert(1, "Use Ability");
        }

        if (availableSpells.Any())
        {
            availableActions.Insert(availableAbilities.Any() ? 2 : 1, "Cast Spell");
        }

        var state = new CombatStateDto
        {
            PlayerHealthPercentage = (int)((double)player.Health / player.MaxHealth * 100),
            EnemyHealthPercentage = (int)((double)enemy.Health / enemy.MaxHealth * 100),
            PlayerCanFlee = true, // Always can attempt to flee
            PlayerHasItems = player.Inventory.Any(i => i.Type == ItemType.Consumable),
            AvailableActions = availableActions,
            AvailableAbilities = availableAbilities,
            AvailableSpells = availableSpells
        };

        return Task.FromResult(state);
    }
}
