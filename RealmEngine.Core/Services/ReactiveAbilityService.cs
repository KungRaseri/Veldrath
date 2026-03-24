using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Progression.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Service for checking and triggering reactive abilities during combat.
/// </summary>
public class ReactiveAbilityService
{
    private readonly PowerDataService? _powerCatalogService;
    private readonly ILogger<ReactiveAbilityService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveAbilityService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="powerCatalogService">The power catalog service.</param>
    public ReactiveAbilityService(ILogger<ReactiveAbilityService> logger, PowerDataService? powerCatalogService = null)
    {
        _powerCatalogService = powerCatalogService;
        _logger = logger;
    }

    /// <summary>
    /// Check and trigger reactive abilities for a character based on combat event.
    /// </summary>
    /// <param name="character">The character whose reactive abilities to check</param>
    /// <param name="trigger">The trigger condition ("onDamageTaken", "onDodge", "onBlock", "onCrit")</param>
    /// <param name="combatLog">Optional combat log to record triggered abilities</param>
    /// <returns>True if any reactive ability was triggered</returns>
    public bool CheckAndTriggerReactiveAbilities(Character character, string trigger, CombatLog? combatLog = null)
    {
        if (_powerCatalogService == null)
            return false; // No catalog service available (e.g., in tests)

        bool anyTriggered = false;

        // Check all learned abilities for reactive ones matching the trigger
        foreach (var learnedAbility in character.LearnedAbilities.Values)
        {
            var ability = _powerCatalogService.GetPower(learnedAbility.AbilityId);
            if (ability == null)
                continue;
            
            // Skip non-reactive abilities
            if (!IsReactiveAbility(ability))
                continue;

            // Check if trigger condition matches
            if (GetTriggerCondition(ability) == trigger)
            {
                // Check if ability is on cooldown
                int cooldown = character.AbilityCooldowns.GetValueOrDefault(ability.Id, 0);
                if (cooldown > 0)
                    continue;

                // Trigger the ability
                _logger.LogInformation("Reactive ability triggered: {AbilityName} ({Trigger})", ability.DisplayName, trigger);
                combatLog?.AddEntry($"[cyan]{ability.DisplayName}[/] activated! ({ability.Description})");

                // Apply cooldown
                if (ability.Cooldown > 0)
                {
                    character.AbilityCooldowns[ability.Id] = ability.Cooldown;
                }

                anyTriggered = true;
            }
        }

        return anyTriggered;
    }

    /// <summary>
    /// Checks if an ability is reactive (has abilityClass trait = "reactive").
    /// </summary>
    private bool IsReactiveAbility(Power ability)
    {
        if (!ability.Traits.TryGetValue("abilityClass", out var classObj))
            return false;

        // Handle both string and dictionary trait formats
        if (classObj is string classStr)
            return classStr == "reactive";

        if (classObj is Dictionary<string, object> classDict && 
            classDict.TryGetValue("value", out var value))
        {
            return value?.ToString() == "reactive";
        }

        return false;
    }

    /// <summary>
    /// Gets the trigger condition from an ability's traits.
    /// </summary>
    private string? GetTriggerCondition(Power ability)
    {
        if (!ability.Traits.TryGetValue("triggerCondition", out var triggerObj))
            return null;

        // Handle both string and dictionary trait formats
        if (triggerObj is string triggerStr)
            return triggerStr;

        if (triggerObj is Dictionary<string, object> triggerDict && 
            triggerDict.TryGetValue("value", out var value))
        {
            return value?.ToString();
        }

        return null;
    }
}
