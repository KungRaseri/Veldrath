using System.Text.Json.Serialization;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Unified power — covers innate abilities, martial talents, spells, cantrips,
/// ultimates, passives, and reactions. Replaces the former <see cref="Ability"/>
/// and <see cref="Spell"/> shared models.
/// </summary>
public class Power
{
    /// <summary>Unique identifier for this power (kebab-case, equals <see cref="Slug"/>).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>URL-safe identifier (kebab-case). Used for lookups and catalog identification.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Internal name used in references (kebab-case).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Display name shown to players.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the base name of the power without modifiers.</summary>
    public string? BasePowerName { get; set; }

    /// <summary>Ordered list of prefix components that appear before the base power name.</summary>
    public List<NameComponent> Prefixes { get; set; } = [];

    /// <summary>Description of what the power does.</summary>
    public string Description { get; set; } = string.Empty;

    // Classification
    /// <summary>How the power is acquired or activated.</summary>
    public PowerType Type { get; set; } = PowerType.Talent;

    /// <summary>
    /// Optional magical school/tradition (null for non-magical powers).
    /// "fire" | "frost" | "arcane" | "holy" | "divine" | "shadow" | "nature" | null.
    /// </summary>
    public string? School { get; set; }

    /// <summary>
    /// Magical tradition derived from <see cref="School"/>. Null for non-magical powers.
    /// Used by the spell-learning system.
    /// </summary>
    public MagicalTradition? Tradition { get; set; }

    /// <summary>
    /// Primary effect category — what the power does when activated.
    /// Used by enemy combat AI to prioritise powers.
    /// </summary>
    public PowerEffectType EffectType { get; set; } = PowerEffectType.None;

    // Stats
    /// <summary>Cooldown in turns/seconds.</summary>
    public int Cooldown { get; set; }

    /// <summary>Mana/resource cost to use this power.</summary>
    public int ManaCost { get; set; }

    /// <summary>Range in world units (null for melee/self).</summary>
    public int? Range { get; set; }

    /// <summary>Duration in turns/seconds (null for instant).</summary>
    public int? Duration { get; set; }

    /// <summary>AoE radius in world units.</summary>
    public int? Radius { get; set; }

    /// <summary>Maximum number of simultaneous targets.</summary>
    public int? MaxTargets { get; set; }

    /// <summary>Power tier/power level (1–5). Higher tiers indicate more powerful powers.</summary>
    public int Tier { get; set; } = 1;

    /// <summary>Rarity weight for procedural generation (lower = more common).</summary>
    public int RarityWeight { get; set; } = 1;

    /// <summary>Level requirement to unlock this power.</summary>
    public int RequiredLevel { get; set; } = 1;

    // Spell-system fields
    /// <summary>
    /// Spell rank: 0 (Cantrip) through 10. Defaults to 0 for non-spell powers.
    /// In the spell-learning system, higher ranks require higher tradition skill.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Minimum tradition skill rank required to cast effectively (0–100).
    /// Characters can attempt spells below this rank but with a reduced success rate.
    /// Defaults to 0 for non-spell powers.
    /// </summary>
    public int MinimumSkillRank { get; set; }

    /// <summary>Selection weight for spellbook/scroll generation (1–1000).</summary>
    public int SelectionWeight { get; set; } = 50;

    /// <summary>
    /// Base effect value (damage dice, healing amount, etc.).
    /// Examples: "8d6", "1d8+WIS", "2d10+5". Used for spell-style powers.
    /// </summary>
    public string? BaseEffectValue { get; set; }

    // Ability-system fields
    /// <summary>Base damage dice for ability-style powers (e.g. "2d6", "4d8+2").</summary>
    public string? BaseDamage { get; set; }

    // Effects
    /// <summary>Elemental or physical damage type applied.</summary>
    public string? DamageType { get; set; }

    /// <summary>Status condition applied on hit (e.g. "poisoned", "stunned").</summary>
    public string? ConditionApplied { get; set; }

    /// <summary>Buff effect slug applied to the target or caster.</summary>
    public string? BuffApplied { get; set; }

    /// <summary>Debuff effect slug applied to the target.</summary>
    public string? DebuffApplied { get; set; }

    // Requirements
    /// <summary>
    /// Optional item type required to use this power.
    /// "staff" | "wand" | "focus" | "catalyst" | "weapon" | "shield" | null.
    /// </summary>
    public string? RequiredItem { get; set; }

    /// <summary>Whether this is a passive power (always active, never activated).</summary>
    public bool IsPassive { get; set; }

    /// <summary>Class restrictions (empty = available to all classes).</summary>
    public List<string> AllowedClasses { get; set; } = [];

    /// <summary>Item reference IDs required to use this power.</summary>
    public List<string> RequiredItemIds { get; set; } = [];

    /// <summary>Power reference IDs that must be learned first (prerequisites).</summary>
    public List<string> RequiredPowerIds { get; set; } = [];

    // Traits
    /// <summary>Traits/properties specific to this power (arbitrary key-value pairs).</summary>
    public Dictionary<string, object> Traits { get; set; } = new();

    // Helpers
    /// <summary>Gets the value of a specific prefix component by token name.</summary>
    public string? GetPrefixValue(string token) =>
        Prefixes.FirstOrDefault(p => p.Token == token)?.Value;

    /// <summary>Composes the power display name from individual naming components.</summary>
    public string ComposeDisplayNameFromComponents()
    {
        var parts = Prefixes.Select(p => p.Value).ToList();
        if (!string.IsNullOrWhiteSpace(BasePowerName)) parts.Add(BasePowerName);
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}

// Enums
/// <summary>
/// How a power is acquired or activated. Replaces the former <c>AbilityTypeEnum</c>
/// and the ability/spell categorical split.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PowerType
{
    /// <summary>Born with — species or archetype, no learning required.</summary>
    Innate,
    /// <summary>Physical or martial technique learned through class training.</summary>
    Talent,
    /// <summary>Formally learned magical invocation via study and tradition.</summary>
    Spell,
    /// <summary>Simple magical ability — free to use, no resource cost.</summary>
    Cantrip,
    /// <summary>Powerful, long-recharge signature power.</summary>
    Ultimate,
    /// <summary>Always-on passive bonus, never manually activated.</summary>
    Passive,
    /// <summary>Triggered automatically in response to specific combat events.</summary>
    Reaction
}

/// <summary>
/// Primary effect category of a power — what it does when activated.
/// Used by enemy combat AI to prioritise powers. Replaces <c>SpellEffectType</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PowerEffectType
{
    /// <summary>No specific effect category.</summary>
    None,
    /// <summary>Deals damage to targets.</summary>
    Damage,
    /// <summary>Restores health or removes negative conditions.</summary>
    Heal,
    /// <summary>Provides positive bonuses to stats or capabilities.</summary>
    Buff,
    /// <summary>Applies negative effects or reduces effectiveness.</summary>
    Debuff,
    /// <summary>Summons creatures or creates objects.</summary>
    Summon,
    /// <summary>Restricts movement or actions.</summary>
    Control,
    /// <summary>Protective barriers or shields.</summary>
    Protection,
    /// <summary>Non-combat or situational effects.</summary>
    Utility
}

/// <summary>
/// Magical tradition categories. Drives the spell-learning system (tradition skill requirement).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MagicalTradition
{
    /// <summary>INT-based: force, transmutation, teleportation, raw magical power.</summary>
    Arcane,
    /// <summary>WIS-based: healing, holy power, protection, faith magic.</summary>
    Divine,
    /// <summary>CHA-based: mind control, illusion, psychic, shadow magic.</summary>
    Occult,
    /// <summary>WIS-based: elements, beasts, nature, weather.</summary>
    Primal
}
