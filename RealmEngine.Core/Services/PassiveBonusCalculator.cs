using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for calculating passive ability bonuses applied to character stats.
/// </summary>
public class PassiveBonusCalculator : IPassiveBonusCalculator
{
    private readonly AbilityDataService _abilityCatalogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassiveBonusCalculator"/> class.
    /// </summary>
    /// <param name="abilityCatalogService">The ability catalog service.</param>
    public PassiveBonusCalculator(AbilityDataService abilityCatalogService)
    {
        _abilityCatalogService = abilityCatalogService;
    }

    /// <summary>
    /// Calculate total physical damage bonus from passive abilities.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The physical damage bonus.</returns>
    public int GetPhysicalDamageBonus(Character character)
    {
        int bonus = 0;

        foreach (var learnedAbility in character.LearnedAbilities.Values)
        {
            var ability = _abilityCatalogService.GetAbility(learnedAbility.AbilityId);
            if (ability == null || !ability.IsPassive)
                continue;

            // Abilities with offensive/combat traits provide physical damage
            if (ability.Traits.TryGetValue("category", out var categoryObj) && categoryObj is Dictionary<string, object> categoryDict)
            {
                if (categoryDict.TryGetValue("value", out var value))
                {
                    var category = value?.ToString();
                    if (category == "offensive_traits" || category == "combat_traits" || category == "combat")
                    {
                        // Fixed bonus per passive ability
                        bonus += 5;
                    }
                }
            }
        }

        return bonus;
    }

    /// <summary>
    /// Calculate total magic damage bonus from passive abilities.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The magic damage bonus.</returns>
    public int GetMagicDamageBonus(Character character)
    {
        int bonus = 0;

        foreach (var learnedAbility in character.LearnedAbilities.Values)
        {
            var ability = _abilityCatalogService.GetAbility(learnedAbility.AbilityId);
            if (ability == null || !ability.IsPassive)
                continue;

            // Abilities with magic/elemental traits provide magic damage
            if (ability.Traits.TryGetValue("category", out var categoryObj) && categoryObj is Dictionary<string, object> categoryDict)
            {
                if (categoryDict.TryGetValue("value", out var value))
                {
                    var category = value?.ToString();
                    if (category == "magical" || category == "elemental" || category == "divine")
                    {
                        bonus += 5;
                    }
                }
            }
        }

        return bonus;
    }

    /// <summary>
    /// Calculate total critical chance bonus from passive abilities.
    /// </summary>
    public double GetCriticalChanceBonus(Character character)
    {
        double bonus = 0.0;

        foreach (var learnedAbility in character.LearnedAbilities.Values)
        {
            var ability = _abilityCatalogService.GetAbility(learnedAbility.AbilityId);
            if (ability == null || !ability.IsPassive)
                continue;

            if (ability.Traits.TryGetValue("category", out var categoryObj) && categoryObj is Dictionary<string, object> categoryDict)
            {
                if (categoryDict.TryGetValue("value", out var value))
                {
                    var category = value?.ToString();
                    if (category == "offensive_traits" || category == "combat_traits")
                    {
                        // +2% crit chance per passive ability
                        bonus += 2.0;
                    }
                }
            }
        }

        return bonus;
    }

    /// <summary>
    /// Calculate total dodge chance bonus from passive abilities.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The dodge chance bonus percentage.</returns>
    public double GetDodgeChanceBonus(Character character)
    {
        double bonus = 0.0;

        foreach (var learnedAbility in character.LearnedAbilities.Values)
        {
            var ability = _abilityCatalogService.GetAbility(learnedAbility.AbilityId);
            if (ability == null || !ability.IsPassive)
                continue;

            if (ability.Traits.TryGetValue("category", out var categoryObj) && categoryObj is Dictionary<string, object> categoryDict)
            {
                if (categoryDict.TryGetValue("value", out var value))
                {
                    var category = value?.ToString();
                    if (category == "defensive" || category == "stealth")
                    {
                        // +3% dodge chance per passive ability
                        bonus += 3.0;
                    }
                }
            }
        }

        return bonus;
    }

    /// <summary>
    /// Calculate total defense bonus from passive abilities.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The defense bonus.</returns>
    public int GetDefenseBonus(Character character)
    {
        int bonus = 0;

        foreach (var learnedAbility in character.LearnedAbilities.Values)
        {
            var ability = _abilityCatalogService.GetAbility(learnedAbility.AbilityId);
            if (ability == null || !ability.IsPassive)
                continue;

            if (ability.Traits.TryGetValue("category", out var categoryObj) && categoryObj is Dictionary<string, object> categoryDict)
            {
                if (categoryDict.TryGetValue("value", out var value))
                {
                    var category = value?.ToString();
                    if (category == "defensive" || category == "combat")
                    {
                        // +5 defense per passive ability
                        bonus += 5;
                    }
                }
            }
        }

        return bonus;
    }
}
