using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Combat.Services;

/// <summary>
/// Service for determining when and which abilities enemies should use in combat.
/// </summary>
public class EnemyAbilityAIService
{
    private readonly AbilityDataService? _abilityCatalogService;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnemyAbilityAIService"/> class.
    /// </summary>
    /// <param name="abilityCatalogService">The ability catalog service.</param>
    public EnemyAbilityAIService(AbilityDataService? abilityCatalogService = null)
    {
        _abilityCatalogService = abilityCatalogService;
        _random = new Random();
    }

    /// <summary>
    /// Decides whether an enemy should use an ability this turn, and if so, which one.
    /// </summary>
    /// <param name="enemy">The enemy making the decision</param>
    /// <param name="player">The target player character</param>
    /// <param name="abilityStates">Dictionary of ability IDs and their cooldown states (turns remaining)</param>
    /// <returns>Ability to use, or null if should use basic attack</returns>
    public string? DecideAbilityUsage(Enemy enemy, Character player, Dictionary<string, int> abilityStates)
    {
        if (enemy.Abilities.Count == 0)
        {
            return null; // No abilities available
        }

        // Get available abilities (not on cooldown)
        var availableAbilities = enemy.Abilities
            .Where(a => !abilityStates.ContainsKey(a.Id) || abilityStates[a.Id] == 0)
            .ToList();

        if (availableAbilities.Count == 0)
        {
            return null; // All abilities on cooldown
        }

        // Calculate enemy's current health percentage
        double healthPercent = (double)enemy.Health / enemy.MaxHealth * 100;

        // Create priority list for abilities based on current situation
        var abilityPriorities = new List<(Ability ability, int priority)>();

        foreach (var ability in availableAbilities)
        {
            int priority = 0;

            // Defensive/healing abilities when health is low
            if (healthPercent < 30 && IsDefensiveAbility(ability))
            {
                priority = 70; // High priority
            }
            // Offensive abilities when health is high
            else if (healthPercent > 50 && IsOffensiveAbility(ability))
            {
                priority = 40; // Moderate priority
            }
            // Buff abilities at start of combat (health near full)
            else if (healthPercent > 80 && IsBuffAbility(ability))
            {
                priority = 50; // Medium priority
            }
            // Debuff abilities when player is strong
            else if (player.Health > player.MaxHealth * 0.7 && IsDebuffAbility(ability))
            {
                priority = 45; // Medium priority
            }
            else
            {
                priority = 20; // Low priority fallback
            }

            abilityPriorities.Add((ability, priority));
        }

        // Select ability based on priorities (higher priority = more likely to be selected)
        foreach (var (ability, priority) in abilityPriorities.OrderByDescending(x => x.priority))
        {
            if (_random.Next(100) < priority)
            {
                return ability.Id;
            }
        }

        // If no ability was selected, 20% chance to use random ability as fallback
        if (availableAbilities.Count > 0 && _random.Next(100) < 20)
        {
            int randomIndex = _random.Next(availableAbilities.Count);
            return availableAbilities[randomIndex].Id;
        }

        // Default: use basic attack
        return null;
    }

    /// <summary>
    /// Checks if an ability is defensive/healing in nature.
    /// </summary>
    private bool IsDefensiveAbility(Ability ability)
    {
        // Check if ability has defensive or healing effects
        var lowerName = ability.Name.ToLowerInvariant();
        var lowerDesc = ability.Description.ToLowerInvariant();

        return lowerName.Contains("heal") ||
               lowerName.Contains("shield") ||
               lowerName.Contains("defend") ||
               lowerName.Contains("barrier") ||
               lowerDesc.Contains("heal") ||
               lowerDesc.Contains("restore") ||
               lowerDesc.Contains("shield") ||
               lowerDesc.Contains("defense") ||
               ability.Type == AbilityTypeEnum.Defensive ||
               ability.Type == AbilityTypeEnum.Healing;
    }

    /// <summary>
    /// Checks if an ability is offensive/damaging in nature.
    /// </summary>
    private bool IsOffensiveAbility(Ability ability)
    {
        // Check if ability deals damage
        var lowerName = ability.Name.ToLowerInvariant();
        var lowerDesc = ability.Description.ToLowerInvariant();

        return lowerName.Contains("attack") ||
               lowerName.Contains("strike") ||
               lowerName.Contains("slash") ||
               lowerName.Contains("blast") ||
               lowerName.Contains("bolt") ||
               lowerDesc.Contains("damage") ||
               lowerDesc.Contains("attack") ||
               ability.Type == AbilityTypeEnum.Offensive;
    }

    /// <summary>
    /// Checks if an ability is a buff (self-enhancement).
    /// </summary>
    private bool IsBuffAbility(Ability ability)
    {
        var lowerName = ability.Name.ToLowerInvariant();
        var lowerDesc = ability.Description.ToLowerInvariant();

        return lowerName.Contains("buff") ||
               lowerName.Contains("enhance") ||
               lowerName.Contains("empower") ||
               lowerName.Contains("strengthen") ||
               lowerDesc.Contains("increase") ||
               lowerDesc.Contains("boost") ||
               lowerDesc.Contains("enhance") ||
               ability.Type == AbilityTypeEnum.Buff;
    }

    /// <summary>
    /// Checks if an ability is a debuff (weakening the opponent).
    /// </summary>
    private bool IsDebuffAbility(Ability ability)
    {
        var lowerName = ability.Name.ToLowerInvariant();
        var lowerDesc = ability.Description.ToLowerInvariant();

        return lowerName.Contains("weaken") ||
               lowerName.Contains("curse") ||
               lowerName.Contains("poison") ||
               lowerName.Contains("slow") ||
               lowerDesc.Contains("reduce") ||
               lowerDesc.Contains("weaken") ||
               lowerDesc.Contains("debuff") ||
               ability.Type == AbilityTypeEnum.Debuff;
    }
}
