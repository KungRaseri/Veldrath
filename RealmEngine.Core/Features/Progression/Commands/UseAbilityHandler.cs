using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;
using RealmEngine.Core.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Combat.Services;
using RealmEngine.Core.Features.Combat.Commands;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles using an ability.
/// </summary>
public class UseAbilityHandler : IRequestHandler<UseAbilityCommand, UseAbilityResult>
{
    private readonly PowerDataService _powerCatalog;
    private readonly ILogger<UseAbilityHandler> _logger;
    private readonly IMediator? _mediator;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="UseAbilityHandler"/> class.
    /// </summary>
    /// <param name="powerCatalog">The power catalog service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="mediator">The mediator for sending commands (optional for testing).</param>
    public UseAbilityHandler(
        PowerDataService powerCatalog,
        ILogger<UseAbilityHandler>? logger = null,
        IMediator? mediator = null)
    {
        _powerCatalog = powerCatalog ?? throw new ArgumentNullException(nameof(powerCatalog));
        _logger = logger ?? NullLogger<UseAbilityHandler>.Instance;
        _mediator = mediator;
    }

    /// <summary>
    /// Handles using an ability.
    /// </summary>
    /// <param name="request">The use ability command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The use ability result.</returns>
    public async Task<UseAbilityResult> Handle(UseAbilityCommand request, CancellationToken cancellationToken)
    {
        // Verify ability is known
        if (!request.User.LearnedAbilities.TryGetValue(request.AbilityId, out var learnedAbility))
        {
            return new UseAbilityResult
            {
                Success = false,
                Message = "You don't know that ability!"
            };
        }

        var ability = _powerCatalog.GetPower(request.AbilityId);
        if (ability == null)
        {
            return new UseAbilityResult
            {
                Success = false,
                Message = "Ability not found!"
            };
        }

        // Check cooldown
        if (request.User.AbilityCooldowns.TryGetValue(request.AbilityId, out var cooldownRemaining) && cooldownRemaining > 0)
        {
            return new UseAbilityResult
            {
                Success = false,
                Message = $"{ability.DisplayName} is still cooling down ({cooldownRemaining} turns)."
            };
        }

        // Check mana cost
        if (request.User.Mana < ability.ManaCost)
        {
            return new UseAbilityResult
            {
                Success = false,
                Message = $"Not enough mana! {ability.DisplayName} requires {ability.ManaCost} mana."
            };
        }

        // Deduct mana
        request.User.Mana -= ability.ManaCost;

        // Calculate damage/healing based on ability type
        int damageDealt = 0;
        int healingDone = 0;

        if (!string.IsNullOrEmpty(ability.BaseDamage))
        {
            damageDealt = DiceRoller.RollDiceString(ability.BaseDamage);
            
            // Apply character's magic damage bonus if it's a magical ability
            if (ability.Traits.ContainsKey("damageType") && 
                ability.Traits["damageType"]?.ToString()?.Contains("magic", StringComparison.OrdinalIgnoreCase) == true)
            {
                damageDealt += request.User.GetMagicDamageBonus();
            }
            else
            {
                damageDealt += request.User.GetPhysicalDamageBonus();
            }

            // Apply damage to target
            if (request.TargetEnemy != null)
            {
                request.TargetEnemy.Health = Math.Max(0, request.TargetEnemy.Health - damageDealt);
            }
            else if (request.TargetCharacter != null)
            {
                request.TargetCharacter.Health = Math.Max(0, request.TargetCharacter.Health - damageDealt);
            }
        }

        // Check for healing abilities
        if (ability.EffectType == PowerEffectType.Heal || 
            (ability.Traits.ContainsKey("effect") && 
             ability.Traits["effect"]?.ToString()?.Contains("heal", StringComparison.OrdinalIgnoreCase) == true))
        {
            if (!string.IsNullOrEmpty(ability.BaseDamage))
            {
                healingDone = DiceRoller.RollDiceString(ability.BaseDamage);
                
                // Apply to target or self
                var healTarget = request.TargetCharacter ?? request.User;
                healTarget.Health = Math.Min(healTarget.MaxHealth, healTarget.Health + healingDone);
            }
        }

        // Apply cooldown
        if (ability.Cooldown > 0)
        {
            request.User.AbilityCooldowns[request.AbilityId] = ability.Cooldown;
        }

        // Update usage statistics
        learnedAbility.TimesUsed++;

        _logger.LogInformation(
            "Character {Character} used ability {Ability} (Damage: {Damage}, Healing: {Healing})",
            request.User.Name, ability.DisplayName, damageDealt, healingDone);

        // Apply status effects if the ability has any
        await TryApplyStatusEffect(ability, request);

        // Build result message
        string message = $"Used {ability.DisplayName}!";
        if (damageDealt > 0)
            message += $" Dealt {damageDealt} damage.";
        if (healingDone > 0)
            message += $" Healed {healingDone} HP.";

        return new UseAbilityResult
        {
            Success = true,
            Message = message,
            DamageDealt = damageDealt,
            HealingDone = healingDone,
            ManaCost = ability.ManaCost,
            PowerUsed = ability
        };
    }

    /// <summary>
    /// Try to apply status effect from ability to target.
    /// </summary>
    private async Task TryApplyStatusEffect(Power ability, UseAbilityCommand request)
    {
        if (_mediator == null)
            return;

        // Parse status effect from ability traits
        var statusEffect = StatusEffectParser.ParseStatusEffectFromAbility(ability, ability.DisplayName);
        if (statusEffect == null)
            return;

        // Check status effect chance
        int chance = StatusEffectParser.GetStatusEffectChance(ability);
        if (chance < 100 && _random.Next(100) >= chance)
        {
            _logger.LogInformation(
                "Status effect {Effect} from {Ability} failed to apply ({Chance}% chance)",
                statusEffect.Type, ability.DisplayName, chance);
            return;
        }

        // Apply to target enemy or character
        Character? targetCharacter = request.TargetCharacter;
        Enemy? targetEnemy = request.TargetEnemy;

        // If no target specified, apply to user (for buffs)
        if (targetCharacter == null && targetEnemy == null && 
            (statusEffect.Category == StatusEffectCategory.Buff || statusEffect.Category == StatusEffectCategory.HealOverTime))
        {
            targetCharacter = request.User;
        }

        if (targetCharacter != null || targetEnemy != null)
        {
            var applyCommand = new ApplyStatusEffectCommand
            {
                TargetCharacter = targetCharacter,
                TargetEnemy = targetEnemy,
                Effect = statusEffect,
                AllowStacking = statusEffect.CanStack,
                RefreshDuration = true
            };

            var result = await _mediator.Send(applyCommand);
            
            if (result.Success)
            {
                _logger.LogInformation(
                    "Applied {Effect} to {Target} from {Ability}",
                    statusEffect.Type,
                    targetCharacter?.Name ?? targetEnemy?.Name ?? "Unknown",
                    ability.DisplayName);
            }
        }
    }
}
