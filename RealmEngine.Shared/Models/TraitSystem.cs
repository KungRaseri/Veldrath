namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a trait value that can be of various types.
/// </summary>
public class TraitValue
{
    /// <summary>Gets or sets the value.</summary>
    public object? Value { get; set; }

    /// <summary>Gets or sets the type.</summary>
    public TraitType Type { get; set; }

    /// <summary>Initializes a new instance of the <see cref="TraitValue"/> class.</summary>
    public TraitValue() { }

    /// <summary>Initializes a new instance of the <see cref="TraitValue"/> class.</summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The type.</param>
    public TraitValue(object? value, TraitType type)
    {
        Value = value;
        Type = type;
    }

    /// <summary>
    /// Get the value as an integer.
    /// </summary>
    public int AsInt()
    {
        if (Value == null) return 0;
        return Convert.ToInt32(Value);
    }

    /// <summary>
    /// Get the value as a double.
    /// </summary>
    public double AsDouble()
    {
        if (Value == null) return 0.0;
        return Convert.ToDouble(Value);
    }

    /// <summary>
    /// Get the value as a string.
    /// </summary>
    public string AsString()
    {
        return Value?.ToString() ?? "";
    }

    /// <summary>
    /// Get the value as a boolean.
    /// </summary>
    public bool AsBool()
    {
        if (Value == null) return false;
        return Convert.ToBoolean(Value);
    }

    /// <summary>
    /// Get the value as a list of strings.
    /// </summary>
    public List<string> AsStringList()
    {
        if (Value is List<string> stringList)
            return stringList;

        if (Value is string[] stringArray)
            return stringArray.ToList();

        return new List<string>();
    }

    /// <summary>
    /// Get the value as a list of integers.
    /// </summary>
    public List<int> AsIntList()
    {
        if (Value is List<int> intList)
            return intList;

        if (Value is int[] intArray)
            return intArray.ToList();

        return new List<int>();
    }
}

/// <summary>
/// Defines the type of a trait value.
/// </summary>
public enum TraitType
{
    /// <summary>Numeric value (int or double).</summary>
    Number,

    /// <summary>String value.</summary>
    String,

    /// <summary>Boolean value.</summary>
    Boolean,

    /// <summary>Array of strings.</summary>
    StringArray,

    /// <summary>Array of numbers.</summary>
    NumberArray
}

/// <summary>
/// Interface for entities that can have traits applied to them.
/// </summary>
public interface ITraitable
{
    /// <summary>Gets the traits dictionary.</summary>
    Dictionary<string, TraitValue> Traits { get; }
}

/// <summary>
/// Standard trait names used across the game for consistency.
/// </summary>
public static class StandardTraits
{
    // Combat - Damage
    /// <summary>Flat damage bonus.</summary>
    public const string DamageBonus = "damageBonus";

    /// <summary>Damage multiplier percentage.</summary>
    public const string DamageMultiplier = "damageMultiplier";

    /// <summary>Physical damage bonus.</summary>
    public const string PhysicalDamage = "physicalDamage";

    /// <summary>Magic damage bonus.</summary>
    public const string MagicDamage = "magicDamage";

    /// <summary>Critical hit chance percentage.</summary>
    public const string CriticalChance = "criticalChance";

    /// <summary>Critical hit damage multiplier.</summary>
    public const string CriticalMultiplier = "criticalMultiplier";

    // Combat - Defense
    /// <summary>Flat defense bonus.</summary>
    public const string DefenseBonus = "defenseBonus";

    /// <summary>Armor rating value.</summary>
    public const string ArmorRating = "armorRating";

    /// <summary>Block chance percentage.</summary>
    public const string BlockChance = "blockChance";

    /// <summary>Dodge chance percentage.</summary>
    public const string DodgeChance = "dodgeChance";

    // Health & Resources
    /// <summary>Flat health bonus.</summary>
    public const string HealthBonus = "healthBonus";

    /// <summary>Health multiplier percentage.</summary>
    public const string HealthMultiplier = "healthMultiplier";

    /// <summary>Flat mana bonus.</summary>
    public const string ManaBonus = "manaBonus";

    /// <summary>Flat stamina bonus.</summary>
    public const string StaminaBonus = "staminaBonus";

    /// <summary>Health regeneration per tick.</summary>
    public const string HealthRegen = "healthRegen";

    /// <summary>Mana regeneration per tick.</summary>
    public const string ManaRegen = "manaRegen";

    // Resistances
    /// <summary>Fire resistance percentage.</summary>
    public const string ResistFire = "resistFire";

    /// <summary>Ice resistance percentage.</summary>
    public const string ResistIce = "resistIce";

    /// <summary>Lightning resistance percentage.</summary>
    public const string ResistLightning = "resistLightning";

    /// <summary>Poison resistance percentage.</summary>
    public const string ResistPoison = "resistPoison";

    /// <summary>Physical resistance percentage.</summary>
    public const string ResistPhysical = "resistPhysical";

    /// <summary>Magic resistance percentage.</summary>
    public const string ResistMagic = "resistMagic";

    // Stat Bonuses
    /// <summary>Strength stat bonus.</summary>
    public const string StrengthBonus = "strengthBonus";

    /// <summary>Dexterity stat bonus.</summary>
    public const string DexterityBonus = "dexterityBonus";

    /// <summary>Constitution stat bonus.</summary>
    public const string ConstitutionBonus = "constitutionBonus";

    /// <summary>Intelligence stat bonus.</summary>
    public const string IntelligenceBonus = "intelligenceBonus";

    /// <summary>Wisdom stat bonus.</summary>
    public const string WisdomBonus = "wisdomBonus";

    /// <summary>Charisma stat bonus.</summary>
    public const string CharismaBonus = "charismaBonus";

    // Item Properties
    /// <summary>Current durability value.</summary>
    public const string Durability = "durability";

    /// <summary>Maximum durability value.</summary>
    public const string MaxDurability = "maxDurability";

    /// <summary>Item weight.</summary>
    public const string Weight = "weight";

    /// <summary>Weight multiplier percentage.</summary>
    public const string WeightMultiplier = "weightMultiplier";

    /// <summary>Value multiplier percentage.</summary>
    public const string ValueMultiplier = "valueMultiplier";

    /// <summary>Rarity multiplier percentage.</summary>
    public const string RarityMultiplier = "rarityMultiplier";

    // Visual/Effects
    /// <summary>Glow effect name.</summary>
    public const string GlowEffect = "glowEffect";

    /// <summary>Particle effect name.</summary>
    public const string ParticleEffect = "particleEffect";

    /// <summary>Sound effect name.</summary>
    public const string SoundEffect = "soundEffect";

    /// <summary>Visual color code.</summary>
    public const string VisualColor = "visualColor";

    // Special Abilities
    /// <summary>Life steal percentage.</summary>
    public const string LifeSteal = "lifeSteal";

    /// <summary>Mana steal percentage.</summary>
    public const string ManaSteal = "manaSteal";

    /// <summary>Thorns damage reflection.</summary>
    public const string Thorns = "thorns";

    /// <summary>Vampiric effect.</summary>
    public const string Vampiric = "vampiric";

    /// <summary>Chain lightning effect.</summary>
    public const string ChainLightning = "chainLightning";

    // Weapon Specific
    /// <summary>Attack speed modifier.</summary>
    public const string AttackSpeed = "attackSpeed";

    /// <summary>Weapon range.</summary>
    public const string Range = "range";

    /// <summary>Indicates if weapon is two-handed.</summary>
    public const string IsTwoHanded = "isTwoHanded";

    /// <summary>Indicates if weapon can parry.</summary>
    public const string CanParry = "canParry";

    // Armor Specific
    /// <summary>Movement speed modifier.</summary>
    public const string MovementSpeed = "movementSpeed";

    /// <summary>Noise reduction percentage.</summary>
    public const string NoiseReduction = "noiseReduction";

    /// <summary>Visibility modifier.</summary>
    public const string Visibility = "visibility";

    // Economic
    /// <summary>Minimum gold value.</summary>
    public const string GoldMin = "goldMin";

    /// <summary>Maximum gold value.</summary>
    public const string GoldMax = "goldMax";

    /// <summary>Price modifier percentage.</summary>
    public const string PriceModifier = "priceModifier";

    /// <summary>Sell value modifier.</summary>
    public const string SellValue = "sellValue";

    // Behavior/AI
    /// <summary>Aggressive behavior flag.</summary>
    public const string Aggressive = "aggressive";

    /// <summary>Friendly behavior flag.</summary>
    public const string Friendly = "friendly";

    /// <summary>Fearful behavior flag.</summary>
    public const string Fearful = "fearful";

    /// <summary>Hostile behavior flag.</summary>
    public const string Hostile = "hostile";

    // Services
    /// <summary>Can repair items flag.</summary>
    public const string CanRepair = "canRepair";

    /// <summary>Can craft items flag.</summary>
    public const string CanCraft = "canCraft";

    /// <summary>Can enchant items flag.</summary>
    public const string CanEnchant = "canEnchant";

    /// <summary>Sells items flag.</summary>
    public const string SellsItems = "sellsItems";

    /// <summary>Buys items flag.</summary>
    public const string BuysItems = "buysItems";

    // Quality/Rarity
    /// <summary>Item quality level.</summary>
    public const string Quality = "quality";

    /// <summary>Loot quality level.</summary>
    public const string LootQuality = "lootQuality";

    /// <summary>Drop chance percentage.</summary>
    public const string DropChance = "dropChance";

    // Ability-Specific Traits
    /// <summary>Damage type (e.g., "fire", "ice", "poison", "physical", "magic").</summary>
    public const string DamageType = "damageType";

    /// <summary>Base damage in dice notation (e.g., "2d6", "1d10").</summary>
    public const string BaseDamage = "baseDamage";

    /// <summary>Cooldown in seconds between uses.</summary>
    public const string Cooldown = "cooldown";

    /// <summary>Cooldown reduction percentage or flat value.</summary>
    public const string CooldownReduction = "cooldownReduction";

    /// <summary>Effect duration in seconds.</summary>
    public const string Duration = "duration";

    /// <summary>Additional duration in seconds.</summary>
    public const string DurationBonus = "durationBonus";

    /// <summary>Cast time in seconds to activate.</summary>
    public const string CastTime = "castTime";

    /// <summary>Additional range units.</summary>
    public const string RangeBonus = "rangeBonus";

    /// <summary>Area of effect radius.</summary>
    public const string AreaOfEffect = "areaOfEffect";

    /// <summary>Maximum number of targets affected.</summary>
    public const string TargetCount = "targetCount";

    /// <summary>Status effect type (e.g., "burning", "frozen", "paralyzed", "poisoned", "stunned", "bleeding", "feared").</summary>
    public const string StatusEffect = "statusEffect";

    /// <summary>Percentage chance to apply status effect.</summary>
    public const string StatusChance = "statusChance";

    /// <summary>HP restored by healing.</summary>
    public const string HealAmount = "healAmount";

    /// <summary>Seconds between regeneration ticks.</summary>
    public const string RegenInterval = "regenInterval";

    /// <summary>Flat armor increase.</summary>
    public const string ArmorBonus = "armorBonus";

    /// <summary>Resistance percentage.</summary>
    public const string ResistBonus = "resistBonus";

    /// <summary>Damage reduction (flat or percentage).</summary>
    public const string DamageReduction = "damageReduction";

    /// <summary>Type of creature summoned.</summary>
    public const string SummonType = "summonType";

    /// <summary>Number of summons.</summary>
    public const string SummonCount = "summonCount";

    /// <summary>Indicates if ability hits multiple targets.</summary>
    public const string Cleave = "cleave";

    /// <summary>Indicates if ability ignores armor.</summary>
    public const string Piercing = "piercing";

    /// <summary>Ability class (e.g., "active", "passive", "toggle", "channeled", "ultimate").</summary>
    public const string AbilityClass = "abilityClass";

    /// <summary>Target type (e.g., "self", "enemy", "ally", "area", "all").</summary>
    public const string TargetType = "targetType";

    /// <summary>Category (e.g., "offensive", "defensive", "control", "utility", "legendary").</summary>
    public const string Category = "category";
}
