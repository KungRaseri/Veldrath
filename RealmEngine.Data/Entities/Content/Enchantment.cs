namespace RealmEngine.Data.Entities;

/// <summary>Enchantment that can be applied to weapons or armor.</summary>
public class Enchantment : ContentBase
{
    /// <summary>"weapon" | "armor" | "any" — null means unrestricted.</summary>
    public string? TargetSlot { get; set; }

    /// <summary>Stat bonuses granted by this enchantment.</summary>
    public EnchantmentStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this enchantment.</summary>
    public EnchantmentTraits Traits { get; set; } = new();
}

/// <summary>Stat bonuses granted by an Enchantment.</summary>
public class EnchantmentStats
{
    /// <summary>Flat bonus damage added to the item's damage rolls.</summary>
    public int? BonusDamage { get; set; }
    /// <summary>Flat bonus armor rating added to the item.</summary>
    public int? BonusArmor { get; set; }
    /// <summary>Flat Strength attribute bonus while the enchanted item is equipped.</summary>
    public int? BonusStrength { get; set; }
    /// <summary>Flat Dexterity attribute bonus while the enchanted item is equipped.</summary>
    public int? BonusDexterity { get; set; }
    /// <summary>Flat Intelligence attribute bonus while the enchanted item is equipped.</summary>
    public int? BonusIntelligence { get; set; }
    /// <summary>Fractional reduction to ability mana costs (0.0–1.0).</summary>
    public float? ManaCostReduction { get; set; }
    /// <summary>Fractional bonus to attack speed (0.0–1.0).</summary>
    public float? AttackSpeedBonus { get; set; }
    /// <summary>Base sell/buy value in gold.</summary>
    public int? Value { get; set; }
}

/// <summary>Boolean trait flags owned by an Enchantment.</summary>
public class EnchantmentTraits
{
    /// <summary>True if multiple copies can be applied to the same item.</summary>
    public bool? Stackable { get; set; }
    /// <summary>True if this enchantment cannot coexist with other enchantments on the same slot.</summary>
    public bool? Exclusive { get; set; }
    /// <summary>True if this enchantment can only be applied to already-magical items.</summary>
    public bool? RequiresMagicItem { get; set; }
    /// <summary>True if the enchantment is cursed and cannot be removed normally.</summary>
    public bool? Cursed { get; set; }
    /// <summary>True if the enchantment cannot be dispelled or removed.</summary>
    public bool? Permanent { get; set; }
}
