using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands;

/// <summary>
/// Handler for ApplyStatusEffectCommand.
/// Applies status effects with resistance, immunity, stacking, and duration refresh logic.
/// </summary>
public class ApplyStatusEffectHandler : IRequestHandler<ApplyStatusEffectCommand, ApplyStatusEffectResult>
{
    private readonly ILogger<ApplyStatusEffectHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyStatusEffectHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ApplyStatusEffectHandler(ILogger<ApplyStatusEffectHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the ApplyStatusEffectCommand request.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Result indicating if the effect was applied.</returns>
    public Task<ApplyStatusEffectResult> Handle(ApplyStatusEffectCommand request, CancellationToken cancellationToken)
    {
        // Validate target
        if (request.TargetCharacter == null && request.TargetEnemy == null)
        {
            return Task.FromResult(new ApplyStatusEffectResult
            {
                Success = false,
                Message = "No valid target specified for status effect."
            });
        }

        var targetName = request.TargetCharacter?.Name ?? request.TargetEnemy?.Name ?? "Unknown";
        var activeEffects = request.TargetCharacter?.ActiveStatusEffects
            ?? request.TargetEnemy?.ActiveStatusEffects
            ?? new List<StatusEffect>();

        // Get target traits for resistance/immunity calculations (only enemies have traits)
        var traits = request.TargetEnemy?.Traits ?? new Dictionary<string, TraitValue>();

        // Beneficial effects (buffs and heal-over-time) are always applied — they cannot be resisted.
        // Without this guard, the Wisdom-derived resistance (1% per 10 Wisdom) could randomly
        // reject a buff, making buff application non-deterministic.
        var isBeneficial = request.Effect.Category is
            StatusEffectCategory.Buff or StatusEffectCategory.HealOverTime;

        // Check for immunity
        if (!isBeneficial && IsImmune(request.Effect, traits, request.TargetCharacter, request.TargetEnemy))
        {
            _logger.LogInformation("{Target} is immune to {EffectType}", targetName, request.Effect.Type);
            return Task.FromResult(new ApplyStatusEffectResult
            {
                Success = false,
                Resisted = true,
                ResistancePercentage = 100,
                Message = $"{targetName} is immune to {request.Effect.Name}!"
            });
        }

        // Calculate resistance (beneficial effects cannot be resisted)
        var resistancePercentage = isBeneficial
            ? 0
            : CalculateResistance(request.Effect, traits, request.TargetCharacter, request.TargetEnemy);
        
        // Roll for resistance (if resistance > 0, there's a chance to resist)
        if (!isBeneficial && resistancePercentage >= 100)
        {
            _logger.LogInformation("{Target} resisted {EffectType} (100% resistance)", targetName, request.Effect.Type);
            return Task.FromResult(new ApplyStatusEffectResult
            {
                Success = false,
                Resisted = true,
                ResistancePercentage = resistancePercentage,
                Message = $"{targetName} resisted {request.Effect.Name}!"
            });
        }

        // Random resistance check (resistancePercentage is chance to resist)
        if (!isBeneficial && resistancePercentage > 0 && Random.Shared.Next(100) < resistancePercentage)
        {
            _logger.LogInformation("{Target} resisted {EffectType} ({Resistance}% chance)",
                targetName, request.Effect.Type, resistancePercentage);
            return Task.FromResult(new ApplyStatusEffectResult
            {
                Success = false,
                Resisted = true,
                ResistancePercentage = resistancePercentage,
                Message = $"{targetName} resisted {request.Effect.Name}!"
            });
        }

        // Check if effect already exists
        var existingEffect = activeEffects.FirstOrDefault(e => e.Type == request.Effect.Type);
        
        if (existingEffect != null)
        {
            // Handle stacking
            if (request.AllowStacking && existingEffect.CanStack && existingEffect.StackCount < existingEffect.MaxStacks)
            {
                existingEffect.StackCount++;
                existingEffect.RemainingDuration = request.Effect.OriginalDuration;
                
                _logger.LogInformation("{Target} stacked {EffectType} to {Stacks} stacks", 
                    targetName, request.Effect.Type, existingEffect.StackCount);
                
                return Task.FromResult(new ApplyStatusEffectResult
                {
                    Success = true,
                    Stacked = true,
                    CurrentStacks = existingEffect.StackCount,
                    ResistancePercentage = resistancePercentage,
                    Message = $"{request.Effect.Name} stacked on {targetName} ({existingEffect.StackCount} stacks)!"
                });
            }
            else if (request.AllowStacking && existingEffect.CanStack && existingEffect.StackCount >= existingEffect.MaxStacks)
            {
                // Already at max stacks
                return Task.FromResult(new ApplyStatusEffectResult
                {
                    Success = false,
                    CurrentStacks = existingEffect.StackCount,
                    Message = $"{request.Effect.Name} is already at max stacks on {targetName}."
                });
            }
            
            // Handle duration refresh
            if (request.RefreshDuration)
            {
                existingEffect.RemainingDuration = request.Effect.OriginalDuration;
                
                _logger.LogInformation("{Target} refreshed {EffectType} duration", targetName, request.Effect.Type);
                
                return Task.FromResult(new ApplyStatusEffectResult
                {
                    Success = true,
                    DurationRefreshed = true,
                    CurrentStacks = existingEffect.StackCount,
                    ResistancePercentage = resistancePercentage,
                    Message = $"{request.Effect.Name} duration refreshed on {targetName}!"
                });
            }
            
            // Effect already exists and not stacking/refreshing
            return Task.FromResult(new ApplyStatusEffectResult
            {
                Success = false,
                CurrentStacks = existingEffect.StackCount,
                Message = $"{targetName} is already affected by {request.Effect.Name}."
            });
        }

        // Apply new effect
        request.Effect.RemainingDuration = request.Effect.OriginalDuration;
        request.Effect.StackCount = 1;
        activeEffects.Add(request.Effect);
        
        _logger.LogInformation("{Target} afflicted with {EffectType} for {Duration} turns", 
            targetName, request.Effect.Type, request.Effect.OriginalDuration);
        
        return Task.FromResult(new ApplyStatusEffectResult
        {
            Success = true,
            CurrentStacks = 1,
            ResistancePercentage = resistancePercentage,
            Message = $"{targetName} is now affected by {request.Effect.Name}!"
        });
    }

    /// <summary>
    /// Checks if a target is immune to a status effect based on traits.
    /// </summary>
    private bool IsImmune(StatusEffect effect, Dictionary<string, TraitValue> traits, Character? character, Enemy? enemy)
    {
        // Check enemy traits for immunity
        if (traits.TryGetValue("immuneToStatusEffects", out var immunityTrait))
        {
            var immuneList = immunityTrait.AsStringList();
            if (immuneList.Contains(effect.Type.ToString().ToLower()) || immuneList.Contains("all"))
            {
                return true;
            }
        }

        // Specific immunity checks based on effect type (from enemy traits)
        var isImmuneByTrait = effect.Type switch
        {
            StatusEffectType.Poisoned when traits.ContainsKey("immuneToPoison") => true,
            StatusEffectType.Burning when traits.ContainsKey("immuneToFire") => true,
            StatusEffectType.Frozen when traits.ContainsKey("immuneToIce") => true,
            StatusEffectType.Bleeding when traits.ContainsKey("immuneToBleeding") => true,
            StatusEffectType.Stunned when traits.ContainsKey("immuneToStun") => true,
            StatusEffectType.Feared when traits.ContainsKey("immuneToFear") => true,
            StatusEffectType.Confused when traits.ContainsKey("immuneToConfusion") => true,
            _ => false
        };
        
        // Characters don't have immunity traits (could be added to equipment later)
        return isImmuneByTrait;
    }

    /// <summary>
    /// Calculates resistance percentage against a status effect based on traits and stats.
    /// </summary>
    /// <returns>Resistance percentage (0-100).</returns>
    private int CalculateResistance(StatusEffect effect, Dictionary<string, TraitValue> traits, Character? character, Enemy? enemy)
    {
        var resistance = 0;

        // Get damage type resistance from enemy traits (skip if it's 'magic')
        var damageType = effect.DamageType.ToLower();
        if (!string.IsNullOrEmpty(damageType) && damageType != "magic")
        {
            var resistanceKey = $"resist{char.ToUpper(damageType[0])}{damageType.Substring(1)}";
            
            if (traits.TryGetValue(resistanceKey, out var resistTrait))
            {
                resistance += resistTrait.AsInt();
            }
        }

        // Get specific status effect resistance
        var statusResistanceKey = $"resist{effect.Type}";
        if (traits.TryGetValue(statusResistanceKey, out var statusResistTrait))
        {
            resistance += statusResistTrait.AsInt();
        }

        // General magic resistance applies to all status effects (half value) if damageType is 'magic' or empty
        if (damageType == "magic" || string.IsNullOrEmpty(damageType))
        {
            if (traits.TryGetValue(StandardTraits.ResistMagic, out var magicResistTrait))
            {
                resistance += magicResistTrait.AsInt() / 2;
            }
        }

        // For characters, derive resistance from Wisdom stat (1% per 10 Wisdom)
        if (character != null)
        {
            resistance += character.Wisdom / 10;
        }

        // Cap resistance at 100%
        return Math.Min(resistance, 100);
    }
}
