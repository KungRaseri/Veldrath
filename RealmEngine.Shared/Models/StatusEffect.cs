namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a status effect (buff, debuff, or damage-over-time) applied to a character or enemy.
/// </summary>
public class StatusEffect
{
    /// <summary>Gets or sets the unique identifier for this status effect instance.</summary>
    public required string Id { get; set; }

    /// <summary>Gets or sets the status effect type.</summary>
    public required StatusEffectType Type { get; set; }

    /// <summary>Gets or sets the category (buff, debuff, or DoT).</summary>
    public required StatusEffectCategory Category { get; set; }

    /// <summary>Gets or sets the name of the status effect.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the remaining duration in turns.</summary>
    public int RemainingDuration { get; set; }

    /// <summary>Gets or sets the original duration in turns.</summary>
    public int OriginalDuration { get; set; }

    /// <summary>Gets or sets the damage dealt per turn (for DoT effects).</summary>
    public int TickDamage { get; set; }

    /// <summary>Gets or sets the healing per turn (for HoT effects).</summary>
    public int TickHealing { get; set; }

    /// <summary>Gets or sets the stat modifiers applied by this effect.</summary>
    public Dictionary<string, int> StatModifiers { get; set; } = new();

    /// <summary>Gets or sets the damage type for resistance calculations (fire, ice, poison, etc.).</summary>
    public string DamageType { get; set; } = string.Empty;

    /// <summary>Gets or sets the stack count (how many times this effect has been applied).</summary>
    public int StackCount { get; set; } = 1;

    /// <summary>Gets or sets the maximum stacks allowed.</summary>
    public int MaxStacks { get; set; } = 1;

    /// <summary>Gets or sets whether this effect can be dispelled.</summary>
    public bool CanDispel { get; set; } = true;

    /// <summary>Gets or sets whether this effect stacks with itself.</summary>
    public bool CanStack { get; set; } = false;

    /// <summary>Gets or sets the source of the effect (ability name, enemy name, etc.).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the icon name for UI display.</summary>
    public string IconName { get; set; } = string.Empty;
}

/// <summary>
/// Defines the type of status effect.
/// </summary>
public enum StatusEffectType
{
    /// <summary>Burning damage over time (fire).</summary>
    Burning,

    /// <summary>Poisoned damage over time (poison).</summary>
    Poisoned,

    /// <summary>Bleeding damage over time (physical).</summary>
    Bleeding,

    /// <summary>Frozen - unable to act, increased damage taken.</summary>
    Frozen,

    /// <summary>Stunned - unable to act for duration.</summary>
    Stunned,

    /// <summary>Paralyzed - reduced defense, chance to miss attacks.</summary>
    Paralyzed,

    /// <summary>Feared - reduced attack, may flee combat.</summary>
    Feared,

    /// <summary>Confused - attacks random target (enemy or ally).</summary>
    Confused,

    /// <summary>Silenced - unable to cast spells.</summary>
    Silenced,

    /// <summary>Weakened - reduced attack damage.</summary>
    Weakened,

    /// <summary>Cursed - reduced resistances and defenses.</summary>
    Cursed,

    /// <summary>Regenerating - healing over time.</summary>
    Regenerating,

    /// <summary>Shielded - absorbs incoming damage.</summary>
    Shielded,

    /// <summary>Strengthened - increased attack damage.</summary>
    Strengthened,

    /// <summary>Hasted - increased attack speed, dodge chance.</summary>
    Hasted,

    /// <summary>Protected - increased defense and resistances.</summary>
    Protected,

    /// <summary>Blessed - increased all stats.</summary>
    Blessed,

    /// <summary>Enraged - increased damage but reduced defense.</summary>
    Enraged,

    /// <summary>Invisible - cannot be targeted by most attacks.</summary>
    Invisible,

    /// <summary>Taunted - forced to attack specific target.</summary>
    Taunted
}

/// <summary>
/// Defines the category of status effect for organizational purposes.
/// </summary>
public enum StatusEffectCategory
{
    /// <summary>Negative effect that harms or hinders.</summary>
    Debuff,

    /// <summary>Positive effect that helps or enhances.</summary>
    Buff,

    /// <summary>Damage dealt over multiple turns.</summary>
    DamageOverTime,

    /// <summary>Healing received over multiple turns.</summary>
    HealOverTime,

    /// <summary>Control effect that prevents or restricts actions.</summary>
    CrowdControl
}

/// <summary>
/// Extension methods for StatusEffectType enum.
/// </summary>
public static class StatusEffectTypeExtensions
{
    /// <summary>
    /// Gets the damage type associated with a status effect for resistance calculations.
    /// </summary>
    public static string GetDamageType(this StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => "fire",
            StatusEffectType.Poisoned => "poison",
            StatusEffectType.Bleeding => "physical",
            StatusEffectType.Frozen => "ice",
            _ => "physical"
        };
    }

    /// <summary>
    /// Gets the category for a status effect type.
    /// </summary>
    public static StatusEffectCategory GetCategory(this StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => StatusEffectCategory.DamageOverTime,
            StatusEffectType.Poisoned => StatusEffectCategory.DamageOverTime,
            StatusEffectType.Bleeding => StatusEffectCategory.DamageOverTime,
            StatusEffectType.Regenerating => StatusEffectCategory.HealOverTime,
            StatusEffectType.Frozen => StatusEffectCategory.CrowdControl,
            StatusEffectType.Stunned => StatusEffectCategory.CrowdControl,
            StatusEffectType.Paralyzed => StatusEffectCategory.CrowdControl,
            StatusEffectType.Feared => StatusEffectCategory.CrowdControl,
            StatusEffectType.Confused => StatusEffectCategory.CrowdControl,
            StatusEffectType.Silenced => StatusEffectCategory.CrowdControl,
            StatusEffectType.Taunted => StatusEffectCategory.CrowdControl,
            StatusEffectType.Weakened => StatusEffectCategory.Debuff,
            StatusEffectType.Cursed => StatusEffectCategory.Debuff,
            StatusEffectType.Shielded => StatusEffectCategory.Buff,
            StatusEffectType.Strengthened => StatusEffectCategory.Buff,
            StatusEffectType.Hasted => StatusEffectCategory.Buff,
            StatusEffectType.Protected => StatusEffectCategory.Buff,
            StatusEffectType.Blessed => StatusEffectCategory.Buff,
            StatusEffectType.Enraged => StatusEffectCategory.Buff,
            StatusEffectType.Invisible => StatusEffectCategory.Buff,
            _ => StatusEffectCategory.Debuff
        };
    }

    /// <summary>
    /// Gets the default icon name for a status effect type.
    /// </summary>
    public static string GetDefaultIcon(this StatusEffectType type)
    {
        return type switch
        {
            StatusEffectType.Burning => "fire",
            StatusEffectType.Poisoned => "poison",
            StatusEffectType.Bleeding => "blood",
            StatusEffectType.Frozen => "snowflake",
            StatusEffectType.Stunned => "dizzy",
            StatusEffectType.Paralyzed => "lightning",
            StatusEffectType.Feared => "scared",
            StatusEffectType.Confused => "question",
            StatusEffectType.Silenced => "mute",
            StatusEffectType.Weakened => "arrow-down",
            StatusEffectType.Cursed => "skull",
            StatusEffectType.Regenerating => "heart-plus",
            StatusEffectType.Shielded => "shield",
            StatusEffectType.Strengthened => "sword",
            StatusEffectType.Hasted => "fast-forward",
            StatusEffectType.Protected => "shield-check",
            StatusEffectType.Blessed => "star",
            StatusEffectType.Enraged => "angry",
            StatusEffectType.Invisible => "eye-off",
            StatusEffectType.Taunted => "target",
            _ => "circle"
        };
    }
}
