namespace RealmEngine.Data.Entities;

/// <summary>
/// Named override of an ActorArchetype for quest-critical or unique actors.
/// TypeKey = origin category (e.g. "boss", "story", "unique").
/// Only fields with non-null values override the base archetype at runtime.
/// </summary>
public class ActorInstance : ContentBase
{
    /// <summary>FK to the base archetype this instance overrides.</summary>
    public Guid ArchetypeId { get; set; }

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

    /// <summary>Additional abilities specific to this instance, on top of the archetype pool.</summary>
    public ICollection<InstanceAbilityPool> AbilityPool { get; set; } = [];
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
    public int? ArmorClass { get; set; }
    /// <summary>Attack bonus override.</summary>
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
