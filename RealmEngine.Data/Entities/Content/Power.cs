namespace RealmEngine.Data.Entities;

/// <summary>
/// Unified Power entity — covers innate abilities, martial talents, spells, cantrips, ultimates,
/// passives, and reactions. Replaces the former <c>Ability</c> and <c>Spell</c> tables.
/// TypeKey = acquisition category (e.g. "active/offensive", "passive/defensive", "fire", "arcane").
/// </summary>
public class Power : ContentBase
{
    /// <summary>
    /// How the power is acquired or activated.
    /// "innate" | "talent" | "spell" | "cantrip" | "ultimate" | "passive" | "reaction"
    /// </summary>
    public string PowerType { get; set; } = string.Empty;

    /// <summary>
    /// Optional magical school/tradition for spells and school-aligned abilities.
    /// "fire" | "frost" | "arcane" | "holy" | "divine" | "shadow" | "nature" | null.
    /// </summary>
    public string? School { get; set; }

    /// <summary>
    /// Optional item prerequisite to use this power.
    /// "staff" | "wand" | "focus" | "catalyst" | "weapon" | "shield" | null.
    /// Replaces the former <c>RequiresStaff</c> / <c>RequiresWeapon</c> trait flags.
    /// </summary>
    public string? RequiresItem { get; set; }

    /// <summary>Cast and usage statistics for this power.</summary>
    public PowerStats Stats { get; set; } = new();
    /// <summary>Damage/condition effects applied when the power fires.</summary>
    public PowerEffects Effects { get; set; } = new();
    /// <summary>Boolean trait flags for this power.</summary>
    public PowerTraits Traits { get; set; } = new();

    /// <summary>Species that have this power as a natural innate power.</summary>
    public ICollection<SpeciesPowerPool> SpeciesPool { get; set; } = [];
    /// <summary>Archetype combat pools that include this power.</summary>
    public ICollection<ArchetypePowerPool> ArchetypePool { get; set; } = [];
    /// <summary>Actor instances that have this power in their override pool.</summary>
    public ICollection<InstancePowerPool> InstancePool { get; set; } = [];
    /// <summary>Class levels that unlock this power.</summary>
    public ICollection<ClassPowerUnlock> ClassUnlocks { get; set; } = [];
}

/// <summary>Cast and usage statistics owned by a <see cref="Power"/>.</summary>
public class PowerStats
{
    /// <summary>Seconds between uses (0 = no cooldown).</summary>
    public float? Cooldown { get; set; }
    /// <summary>Mana consumed on use.</summary>
    public int? ManaCost { get; set; }
    /// <summary>Seconds to finish casting.</summary>
    public float? CastTime { get; set; }
    /// <summary>Maximum reach in world units.</summary>
    public float? Range { get; set; }
    /// <summary>Effect active duration in seconds.</summary>
    public float? Duration { get; set; }
    /// <summary>AoE radius in world units.</summary>
    public int? Radius { get; set; }
    /// <summary>Maximum number of simultaneous targets.</summary>
    public int? MaxTargets { get; set; }
    /// <summary>Minimum damage dealt.</summary>
    public int? DamageMin { get; set; }
    /// <summary>Maximum damage dealt.</summary>
    public int? DamageMax { get; set; }
    /// <summary>Minimum health restored.</summary>
    public int? HealMin { get; set; }
    /// <summary>Maximum health restored.</summary>
    public int? HealMax { get; set; }
}

/// <summary>Damage and condition effect data owned by a <see cref="Power"/>.</summary>
public class PowerEffects
{
    /// <summary>Elemental or physical damage type applied.</summary>
    public string? DamageType { get; set; }
    /// <summary>Status condition applied on hit (e.g. "poisoned", "stunned").</summary>
    public string? ConditionApplied { get; set; }
    /// <summary>Probability (0.0–1.0) of applying the condition.</summary>
    public float? ConditionChance { get; set; }
    /// <summary>Buff effect slug applied to the target or caster.</summary>
    public string? BuffApplied { get; set; }
    /// <summary>Debuff effect slug applied to the target.</summary>
    public string? DebuffApplied { get; set; }
}

/// <summary>Boolean trait flags owned by a <see cref="Power"/>.</summary>
public class PowerTraits
{
    /// <summary>True if the power requires a valid target to fire.</summary>
    public bool? RequiresTarget { get; set; }
    /// <summary>True if the power hits an area rather than a single target.</summary>
    public bool? IsAoe { get; set; }
    /// <summary>True if the power has a non-zero cooldown.</summary>
    public bool? HasCooldown { get; set; }
    /// <summary>True if the power requires continuous input to maintain.</summary>
    public bool? IsChanneled { get; set; }
    /// <summary>True if the power resolves in the same frame it is used.</summary>
    public bool? IsInstant { get; set; }
    /// <summary>True if the power can deal critical hits.</summary>
    public bool? CanCrit { get; set; }
    /// <summary>True if the power is always active and requires no activation.</summary>
    public bool? IsPassive { get; set; }
    /// <summary>True if the power requires concentration — breaks if the caster is interrupted.</summary>
    public bool? RequiresConcentration { get; set; }
}
