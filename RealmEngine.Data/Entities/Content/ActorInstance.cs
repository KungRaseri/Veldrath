namespace RealmEngine.Data.Entities;

/// <summary>
/// Named override of an ActorArchetype for quest-critical or unique actors.
/// TypeKey = origin category (e.g. "boss", "story", "unique").
/// Only fields with non-null values override the base archetype at runtime.
/// </summary>
/// <remarks>
/// <para>
/// A row with a <see cref="LocationSlug"/> is one named actor standing at one zone location: the spawn path emits it
/// exactly once per roster there, and composes <see cref="StatOverrides"/> (plus <see cref="LevelOverride"/>,
/// <see cref="DisplayName"/> and <see cref="LootTableOverride"/>) onto the archetype's projected template. A null
/// field inherits, and an absolute value replaces — nothing is added to or scaled by the archetype's number.
/// </para>
/// <para>
/// A row without a location is catalogue-only: Foundry and the editorial site can read it, but nothing in the world
/// places it, so it never spawns.
/// </para>
/// </remarks>
public class ActorInstance : ContentBase
{
    /// <summary>FK to the base archetype this instance overrides.</summary>
    public Guid ArchetypeId { get; set; }

    /// <summary>
    /// The one zone location this instance stands at, or <see langword="null"/> for a catalogue-only instance.
    /// Soft reference, not a FK — the same shape as <see cref="FactionOverride"/>, because a location lives in the
    /// content rows and a renamed slug must be catchable as a content error rather than deleted in a cascade.
    /// </summary>
    public string? LocationSlug { get; set; }

    /// <summary>Override for the actor's level. Null = use archetype's MinLevel.</summary>
    public int? LevelOverride { get; set; }
    /// <summary>FK override for the loot table. Null = use archetype's loot table.</summary>
    public Guid? LootTableOverride { get; set; }
    /// <summary>Faction slug override. Soft reference, not a FK.</summary>
    public string? FactionOverride { get; set; }

    /// <summary>Per-field stat overrides applied on top of the archetype's stats. Null fields are ignored.</summary>
    public InstanceStatOverrides StatOverrides { get; set; } = new();

    /// <summary>Navigation property for the base archetype.</summary>
    public ActorArchetype? Archetype { get; set; }
    /// <summary>Navigation property for the override loot table.</summary>
    public LootTable? LootTable { get; set; }

    /// <summary>Additional powers specific to this instance, on top of the archetype pool.</summary>
    /// <remarks>Not projected onto the spawned template: no consumer on the spawn or combat path reads it yet.</remarks>
    public ICollection<InstancePowerPool> PowerPool { get; set; } = [];
}

/// <summary>
/// Per-field stat overrides for an ActorInstance. Every field is nullable —
/// only non-null values replace the corresponding field on the base archetype.
/// </summary>
public class InstanceStatOverrides
{
    /// <summary>Health override. Null = inherit from archetype.</summary>
    public int? Health { get; set; }
    /// <summary>Mana override.</summary>
    public int? Mana { get; set; }
    /// <summary>Strength override.</summary>
    public int? Strength { get; set; }
    /// <summary>Agility override.</summary>
    public int? Agility { get; set; }
    /// <summary>Intelligence override.</summary>
    public int? Intelligence { get; set; }
    /// <summary>Constitution override.</summary>
    public int? Constitution { get; set; }
    /// <summary>Armor class override.</summary>
    /// <remarks>
    /// Reserved, like <see cref="ArchetypeStats.ArmorClass"/>: no projection reads it, because the spawned template has
    /// no armor-class member to carry it and mitigation there is Constitution.
    /// </remarks>
    public int? ArmorClass { get; set; }
    /// <summary>Attack bonus override.</summary>
    /// <remarks>
    /// Reserved, like <see cref="ArchetypeStats.AttackBonus"/>: no projection reads it, because no combat path rolls
    /// an attack for a bonus to modify.
    /// </remarks>
    public int? AttackBonus { get; set; }
    /// <summary>Damage override.</summary>
    public int? Damage { get; set; }
    /// <summary>Experience reward override.</summary>
    public int? ExperienceReward { get; set; }
    /// <summary>Gold reward minimum override.</summary>
    public int? GoldRewardMin { get; set; }
    /// <summary>Gold reward maximum override.</summary>
    public int? GoldRewardMax { get; set; }
}
