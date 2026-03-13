namespace RealmEngine.Data.Entities;

/// <summary>Weapon catalog entry. TypeKey = weapon category (e.g. "swords", "axes", "bows").</summary>
public class Weapon : ContentBase
{
    /// <summary>"sword" | "axe" | "mace" | "bow" | "staff" | "dagger" | etc.</summary>
    public string WeaponType { get; set; } = string.Empty;

    /// <summary>"physical" | "fire" | "cold" | "lightning" | "poison" | "arcane"</summary>
    public string DamageType { get; set; } = string.Empty;

    /// <summary>1 = one-handed, 2 = two-handed.</summary>
    public int HandsRequired { get; set; } = 1;

    /// <summary>Combat performance statistics.</summary>
    public WeaponStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this weapon.</summary>
    public WeaponTraits Traits { get; set; } = new();
}

/// <summary>Combat performance statistics owned by a Weapon.</summary>
public class WeaponStats
{
    /// <summary>Minimum base damage per hit.</summary>
    public int? DamageMin { get; set; }
    /// <summary>Maximum base damage per hit.</summary>
    public int? DamageMax { get; set; }
    /// <summary>Attacks per second.</summary>
    public float? AttackSpeed { get; set; }
    /// <summary>Base critical hit probability (0.0–1.0).</summary>
    public float? CritChance { get; set; }
    /// <summary>Maximum attack reach in world units.</summary>
    public float? Range { get; set; }
    /// <summary>Item weight in lbs — affects encumbrance.</summary>
    public float? Weight { get; set; }
    /// <summary>Maximum durability before the weapon breaks.</summary>
    public int? Durability { get; set; }
    /// <summary>Base sell/buy value in gold.</summary>
    public int? Value { get; set; }
}

/// <summary>Boolean trait flags owned by a Weapon.</summary>
public class WeaponTraits
{
    /// <summary>True if the weapon occupies both hands.</summary>
    public bool? TwoHanded { get; set; }
    /// <summary>True if the weapon can be thrown as a ranged attack.</summary>
    public bool? Throwable { get; set; }
    /// <summary>True if the weapon is silvered (effective against lycanthropes etc.).</summary>
    public bool? Silvered { get; set; }
    /// <summary>True if the weapon has a magical enchantment.</summary>
    public bool? Magical { get; set; }
    /// <summary>True if the weapon uses Dexterity instead of Strength for attack rolls.</summary>
    public bool? Finesse { get; set; }
    /// <summary>True if the weapon has extended reach (e.g. polearm).</summary>
    public bool? Reach { get; set; }
    /// <summary>True if the weapon can be used one- or two-handed.</summary>
    public bool? Versatile { get; set; }
}
