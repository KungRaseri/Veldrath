using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Combat.Services;

/// <summary>
/// Helper class to parse status effect traits from abilities and create StatusEffect instances.
/// </summary>
public static class StatusEffectParser
{
    /// <summary>
    /// Parse status effect from power traits and create a StatusEffect instance.
    /// </summary>
    /// <param name="ability">The power with status effect traits.</param>
    /// <param name="sourceName">Name of the power source (for display purposes).</param>
    /// <returns>StatusEffect instance if traits are present, null otherwise.</returns>
    public static StatusEffect? ParseStatusEffectFromAbility(Power ability, string sourceName)
    {
        if (!ability.Traits.TryGetValue("statusEffect", out var statusEffectValue))
        {
            return null;
        }

        // Get the status effect type string
        string effectTypeStr = statusEffectValue?.ToString()?.ToLower() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(effectTypeStr))
        {
            return null;
        }

        // Map string to StatusEffectType enum
        var effectType = MapStringToStatusEffectType(effectTypeStr);
        if (effectType == null)
        {
            return null;
        }

        // Get category from the effect type
        var category = effectType.Value.GetCategory();

        // Get duration (default to 3 turns for DoT, 2 for debuffs, 3 for buffs)
        int duration = category switch
        {
            StatusEffectCategory.DamageOverTime => 3,
            StatusEffectCategory.HealOverTime => 4,
            StatusEffectCategory.CrowdControl => 2,
            StatusEffectCategory.Debuff => 3,
            StatusEffectCategory.Buff => 3,
            _ => 2
        };

        // Check for duration trait override
        if (ability.Traits.TryGetValue("statusDuration", out var durationValue) &&
            int.TryParse(durationValue?.ToString(), out int parsedDuration))
        {
            duration = parsedDuration;
        }

        // Get tick damage for DoT effects (based on ability base damage or default)
        int tickDamage = 0;
        if (category == StatusEffectCategory.DamageOverTime)
        {
            // Use statusDamage trait if present
            if (ability.Traits.TryGetValue("statusDamage", out var statusDamageValue) &&
                int.TryParse(statusDamageValue?.ToString(), out int parsedStatusDamage))
            {
                tickDamage = parsedStatusDamage;
            }
            else
            {
                // Default DoT damage based on ability tier
                tickDamage = effectType.Value switch
                {
                    StatusEffectType.Burning => 5,
                    StatusEffectType.Poisoned => 4,
                    StatusEffectType.Bleeding => 3,
                    _ => 3
                };
            }
        }

        // Get tick healing for HoT effects
        int tickHealing = 0;
        if (category == StatusEffectCategory.HealOverTime)
        {
            // Use statusHealing trait if present
            if (ability.Traits.TryGetValue("statusHealing", out var statusHealingValue) &&
                int.TryParse(statusHealingValue?.ToString(), out int parsedStatusHealing))
            {
                tickHealing = parsedStatusHealing;
            }
            else
            {
                // Default HoT healing
                tickHealing = 8;
            }
        }

        // Get damage type from ability traits or effect type default
        string damageType = ability.Traits.TryGetValue("damageType", out var damageTypeValue)
            ? damageTypeValue?.ToString() ?? effectType.Value.GetDamageType()
            : effectType.Value.GetDamageType();

        // Create the status effect
        var statusEffect = new StatusEffect
        {
            Id = $"{ability.Id}-{effectType.Value.ToString().ToLower()}-{Guid.NewGuid():N}",
            Type = effectType.Value,
            Category = category,
            Name = GetEffectDisplayName(effectType.Value),
            Description = GetEffectDescription(effectType.Value),
            RemainingDuration = duration,
            OriginalDuration = duration,
            TickDamage = tickDamage,
            TickHealing = tickHealing,
            DamageType = damageType,
            CanStack = IsStackableEffect(effectType.Value),
            MaxStacks = GetMaxStacks(effectType.Value),
            StackCount = 1,
            Source = sourceName,
            IconName = effectType.Value.GetDefaultIcon()
        };

        // Add stat modifiers for buff/debuff effects
        if (category == StatusEffectCategory.Buff || category == StatusEffectCategory.Debuff)
        {
            statusEffect.StatModifiers = GetStatModifiers(effectType.Value, ability);
        }

        return statusEffect;
    }

    /// <summary>
    /// Get status effect application chance from power traits (0-100).
    /// </summary>
    public static int GetStatusEffectChance(Power ability)
    {
        if (ability.Traits.TryGetValue("statusChance", out var chanceValue) &&
            int.TryParse(chanceValue?.ToString(), out int chance))
        {
            return Math.Clamp(chance, 0, 100);
        }

        // Default chance if not specified
        return 100; // 100% chance by default
    }

    /// <summary>
    /// Map string to StatusEffectType enum.
    /// </summary>
    private static StatusEffectType? MapStringToStatusEffectType(string effectStr)
    {
        return effectStr switch
        {
            "burning" => StatusEffectType.Burning,
            "poisoned" => StatusEffectType.Poisoned,
            "bleeding" => StatusEffectType.Bleeding,
            "frozen" => StatusEffectType.Frozen,
            "stunned" => StatusEffectType.Stunned,
            "paralyzed" => StatusEffectType.Paralyzed,
            "feared" => StatusEffectType.Feared,
            "confused" => StatusEffectType.Confused,
            "silenced" => StatusEffectType.Silenced,
            "weakened" => StatusEffectType.Weakened,
            "cursed" => StatusEffectType.Cursed,
            "regenerating" => StatusEffectType.Regenerating,
            "shielded" => StatusEffectType.Shielded,
            "strengthened" => StatusEffectType.Strengthened,
            "hasted" => StatusEffectType.Hasted,
            "protected" => StatusEffectType.Protected,
            "blessed" => StatusEffectType.Blessed,
            "enraged" => StatusEffectType.Enraged,
            "invisible" => StatusEffectType.Invisible,
            "taunted" => StatusEffectType.Taunted,
            "slowed" => StatusEffectType.Weakened, // Map "slowed" to Weakened
            "charmed" => StatusEffectType.Confused, // Map "charmed" to Confused
            _ => null
        };
    }

    /// <summary>
    /// Get display name for a status effect type.
    /// </summary>
    private static string GetEffectDisplayName(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => "Burning",
            StatusEffectType.Poisoned => "Poisoned",
            StatusEffectType.Bleeding => "Bleeding",
            StatusEffectType.Frozen => "Frozen",
            StatusEffectType.Stunned => "Stunned",
            StatusEffectType.Paralyzed => "Paralyzed",
            StatusEffectType.Feared => "Feared",
            StatusEffectType.Confused => "Confused",
            StatusEffectType.Silenced => "Silenced",
            StatusEffectType.Weakened => "Weakened",
            StatusEffectType.Cursed => "Cursed",
            StatusEffectType.Regenerating => "Regeneration",
            StatusEffectType.Shielded => "Shielded",
            StatusEffectType.Strengthened => "Strengthened",
            StatusEffectType.Hasted => "Haste",
            StatusEffectType.Protected => "Protected",
            StatusEffectType.Blessed => "Blessed",
            StatusEffectType.Enraged => "Enraged",
            StatusEffectType.Invisible => "Invisible",
            StatusEffectType.Taunted => "Taunted",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Get description for a status effect type.
    /// </summary>
    private static string GetEffectDescription(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => "Taking fire damage over time.",
            StatusEffectType.Poisoned => "Taking poison damage over time.",
            StatusEffectType.Bleeding => "Taking physical damage over time.",
            StatusEffectType.Frozen => "Frozen solid and unable to act.",
            StatusEffectType.Stunned => "Stunned and unable to act.",
            StatusEffectType.Paralyzed => "Paralyzed and unable to move.",
            StatusEffectType.Feared => "Feared and may flee in terror.",
            StatusEffectType.Confused => "Confused and may attack allies.",
            StatusEffectType.Silenced => "Unable to cast spells.",
            StatusEffectType.Weakened => "Attack power reduced.",
            StatusEffectType.Cursed => "All stats reduced.",
            StatusEffectType.Regenerating => "Healing over time.",
            StatusEffectType.Shielded => "Protected by a magical shield.",
            StatusEffectType.Strengthened => "Attack power increased.",
            StatusEffectType.Hasted => "Movement and attack speed increased.",
            StatusEffectType.Protected => "Defense increased.",
            StatusEffectType.Blessed => "All stats increased.",
            StatusEffectType.Enraged => "Attack power greatly increased, defense reduced.",
            StatusEffectType.Invisible => "Cannot be targeted by most attacks.",
            StatusEffectType.Taunted => "Forced to attack the taunter.",
            _ => "Status effect active."
        };
    }

    /// <summary>
    /// Check if an effect type is stackable.
    /// </summary>
    private static bool IsStackableEffect(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => true,
            StatusEffectType.Poisoned => true,
            StatusEffectType.Bleeding => true,
            StatusEffectType.Regenerating => true,
            _ => false // Most effects don't stack
        };
    }

    /// <summary>
    /// Get maximum stacks for a stackable effect.
    /// </summary>
    private static int GetMaxStacks(StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => 5,
            StatusEffectType.Poisoned => 5,
            StatusEffectType.Bleeding => 3,
            StatusEffectType.Regenerating => 3,
            _ => 1
        };
    }

    /// <summary>
    /// Get stat modifiers for buff/debuff effects.
    /// </summary>
    private static Dictionary<string, int> GetStatModifiers(StatusEffectType type, Power ability)
    {
        var modifiers = new Dictionary<string, int>();

        switch (type)
        {
            case StatusEffectType.Weakened:
                modifiers["attack"] = -5;
                break;
            case StatusEffectType.Cursed:
                modifiers["attack"] = -3;
                modifiers["defense"] = -3;
                break;
            case StatusEffectType.Strengthened:
                modifiers["attack"] = 10;
                break;
            case StatusEffectType.Protected:
                modifiers["defense"] = 10;
                break;
            case StatusEffectType.Hasted:
                modifiers["speed"] = 15;
                break;
            case StatusEffectType.Blessed:
                modifiers["attack"] = 5;
                modifiers["defense"] = 5;
                break;
            case StatusEffectType.Enraged:
                modifiers["attack"] = 15;
                modifiers["defense"] = -5;
                break;
        }

        // Check for custom stat modifiers in ability traits
        if (ability.Traits.TryGetValue("statModifiers", out var modifiersValue) &&
            modifiersValue is Dictionary<string, object> customModifiers)
        {
            foreach (var (stat, value) in customModifiers)
            {
                if (int.TryParse(value?.ToString(), out int modifier))
                {
                    modifiers[stat] = modifier;
                }
            }
        }

        return modifiers;
    }
}
