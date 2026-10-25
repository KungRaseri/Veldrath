using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Commands;

/// <summary>
/// Command to process all active status effects on a target (tick damage, decrement durations, remove expired).
/// </summary>
public record ProcessStatusEffectsCommand : IRequest<ProcessStatusEffectsResult>
{
    /// <summary>Gets the target character (if processing player effects).</summary>
    public Character? TargetCharacter { get; init; }

    /// <summary>Gets the target enemy (if processing enemy effects).</summary>
    public Enemy? TargetEnemy { get; init; }
}

/// <summary>
/// Result of processing status effects.
/// </summary>
public record ProcessStatusEffectsResult
{
    /// <summary>Total damage dealt by DoT effects this tick.</summary>
    public int TotalDamageTaken { get; init; }

    /// <summary>Total healing from HoT effects this tick.</summary>
    public int TotalHealingReceived { get; init; }

    /// <summary>Number of effects that expired this tick.</summary>
    public int EffectsExpired { get; init; }

    /// <summary>List of effect types that expired.</summary>
    public List<StatusEffectType> ExpiredEffectTypes { get; init; } = new();

    /// <summary>List of active effect types still ongoing.</summary>
    public List<StatusEffectType> ActiveEffectTypes { get; init; } = new();

    /// <summary>Stat modifiers currently active from all effects.</summary>
    public Dictionary<string, int> TotalStatModifiers { get; init; } = new();

    /// <summary>Messages describing what happened (for combat log).</summary>
    public List<string> Messages { get; init; } = new();
}

/// <summary>
/// Handler for ProcessStatusEffectsCommand.
/// Processes all status effects on a target, applying DoT/HoT, decrementing durations, and removing expired effects.
/// </summary>
public class ProcessStatusEffectsHandler : IRequestHandler<ProcessStatusEffectsCommand, ProcessStatusEffectsResult>
{
    private readonly ILogger<ProcessStatusEffectsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessStatusEffectsHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ProcessStatusEffectsHandler(ILogger<ProcessStatusEffectsHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the ProcessStatusEffectsCommand request.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Result with damage, healing, and expired effects.</returns>
    public Task<ProcessStatusEffectsResult> Handle(ProcessStatusEffectsCommand request, CancellationToken cancellationToken)
    {
        var targetName = request.TargetCharacter?.Name ?? request.TargetEnemy?.Name ?? "Unknown";
        var activeEffects = request.TargetCharacter?.ActiveStatusEffects
            ?? request.TargetEnemy?.ActiveStatusEffects
            ?? new List<StatusEffect>();

        if (!activeEffects.Any())
        {
            return Task.FromResult(new ProcessStatusEffectsResult());
        }

        int totalDamage = 0;
        int totalHealing = 0;
        var expiredEffects = new List<StatusEffectType>();
        var messages = new List<string>();
        var totalStatModifiers = new Dictionary<string, int>();

        // Process each effect
        var effectsToRemove = new List<StatusEffect>();

        foreach (var effect in activeEffects)
        {
            // Apply DoT damage
            if (effect.TickDamage > 0)
            {
                var damage = effect.TickDamage * effect.StackCount;
                totalDamage += damage;

                // Apply damage to target
                if (request.TargetCharacter != null)
                {
                    request.TargetCharacter.Health = Math.Max(0, request.TargetCharacter.Health - damage);
                }
                else if (request.TargetEnemy != null)
                {
                    request.TargetEnemy.Health = Math.Max(0, request.TargetEnemy.Health - damage);
                }

                messages.Add($"{targetName} takes {damage} {effect.DamageType} damage from {effect.Name}");
                _logger.LogInformation("{Target} takes {Damage} {DamageType} damage from {Effect}",
                    targetName, damage, effect.DamageType, effect.Name);
            }

            // Apply HoT healing
            if (effect.TickHealing > 0)
            {
                var healing = effect.TickHealing * effect.StackCount;

                // Calculate actual healing applied (respect max health cap)
                int actualHealing = 0;
                if (request.TargetCharacter != null)
                {
                    var healthBefore = request.TargetCharacter.Health;
                    request.TargetCharacter.Health = Math.Min(request.TargetCharacter.MaxHealth,
                        request.TargetCharacter.Health + healing);
                    actualHealing = request.TargetCharacter.Health - healthBefore;
                }
                else if (request.TargetEnemy != null)
                {
                    var healthBefore = request.TargetEnemy.Health;
                    request.TargetEnemy.Health = Math.Min(request.TargetEnemy.MaxHealth,
                        request.TargetEnemy.Health + healing);
                    actualHealing = request.TargetEnemy.Health - healthBefore;
                }

                totalHealing += actualHealing;
                messages.Add($"{targetName} heals {actualHealing} HP from {effect.Name}");
                _logger.LogInformation("{Target} heals {Healing} HP from {Effect}",
                    targetName, actualHealing, effect.Name);
            }

            // Accumulate stat modifiers
            foreach (var modifier in effect.StatModifiers)
            {
                if (!totalStatModifiers.ContainsKey(modifier.Key))
                {
                    totalStatModifiers[modifier.Key] = 0;
                }
                totalStatModifiers[modifier.Key] += modifier.Value * effect.StackCount;
            }

            // Decrement duration
            effect.RemainingDuration--;

            // Mark expired effects for removal
            if (effect.RemainingDuration <= 0)
            {
                effectsToRemove.Add(effect);
                expiredEffects.Add(effect.Type);
                messages.Add($"{effect.Name} has worn off from {targetName}");
                _logger.LogInformation("{Effect} expired on {Target}", effect.Name, targetName);
            }
        }

        // Remove expired effects
        foreach (var effect in effectsToRemove)
        {
            activeEffects.Remove(effect);
        }

        // Get remaining active effect types
        var activeEffectTypes = activeEffects.Select(e => e.Type).Distinct().ToList();

        return Task.FromResult(new ProcessStatusEffectsResult
        {
            TotalDamageTaken = totalDamage,
            TotalHealingReceived = totalHealing,
            EffectsExpired = effectsToRemove.Count,
            ExpiredEffectTypes = expiredEffects,
            ActiveEffectTypes = activeEffectTypes,
            TotalStatModifiers = totalStatModifiers,
            Messages = messages
        });
    }
}
