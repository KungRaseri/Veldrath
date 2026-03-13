namespace RealmEngine.Data.Entities;

/// <summary>Spell. TypeKey = school (e.g. "fire", "frost", "arcane", "holy").</summary>
public class Spell : ContentBase
{
    /// <summary>"fire" | "frost" | "arcane" | "holy" | "shadow" | "nature" | etc.</summary>
    public string School { get; set; } = string.Empty;

    /// <summary>Cast and damage statistics.</summary>
    public SpellStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this spell.</summary>
    public SpellTraits Traits { get; set; } = new();
}

/// <summary>Cast and damage statistics owned by a Spell.</summary>
public class SpellStats
{
    /// <summary>Mana consumed on cast.</summary>
    public int? ManaCost { get; set; }
    /// <summary>Seconds to finish casting.</summary>
    public float? CastTime { get; set; }
    /// <summary>Seconds between casts (0 = no cooldown).</summary>
    public float? Cooldown { get; set; }
    /// <summary>Maximum reach in world units.</summary>
    public float? Range { get; set; }
    /// <summary>Minimum damage dealt.</summary>
    public int? DamageMin { get; set; }
    /// <summary>Maximum damage dealt.</summary>
    public int? DamageMax { get; set; }
    /// <summary>Minimum health restored.</summary>
    public int? HealMin { get; set; }
    /// <summary>Maximum health restored.</summary>
    public int? HealMax { get; set; }
    /// <summary>Effect active duration in seconds.</summary>
    public float? Duration { get; set; }
}

/// <summary>Boolean trait flags owned by a Spell.</summary>
public class SpellTraits
{
    /// <summary>True if the spell requires a staff to cast.</summary>
    public bool? RequiresStaff { get; set; }
    /// <summary>True if the spell hits an area rather than a single target.</summary>
    public bool? IsAoe { get; set; }
    /// <summary>True if the spell requires continuous input to maintain.</summary>
    public bool? IsChanneled { get; set; }
    /// <summary>True if the spell resolves in the same frame it is cast.</summary>
    public bool? Instant { get; set; }
    /// <summary>True if the spell can deal critical hits.</summary>
    public bool? CanCrit { get; set; }
    /// <summary>True if the spell requires concentration — breaks if the caster is interrupted.</summary>
    public bool? RequiresConcentration { get; set; }
}
