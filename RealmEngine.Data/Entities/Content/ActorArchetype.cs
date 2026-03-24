namespace RealmEngine.Data.Entities;

/// <summary>
/// Composed actor template. TypeKey = role category (e.g. "humanoids/bandits", "beasts/wolves").
/// Combines Species + ActorClass + Background with authored flat stats and behaviour trait flags.
/// Replaces the old separate Enemy and Npc tables — hostility is just a trait flag.
/// </summary>
public class ActorArchetype : ContentBase
{
    // Definition references
    /// <summary>FK to the species that provides biological base stats. Nullable = species-agnostic archetype.</summary>
    public Guid? SpeciesId { get; set; }
    /// <summary>FK to the actor class that drives growth curves and combat style.</summary>
    public Guid? ClassId { get; set; }
    /// <summary>FK to the background that grants origin bonuses.</summary>
    public Guid? BackgroundId { get; set; }

    // Level range
    /// <summary>Minimum level at which this archetype spawns or is available.</summary>
    public int MinLevel { get; set; }
    /// <summary>Maximum level for this archetype.</summary>
    public int MaxLevel { get; set; }

    // Authored stats + traits
    /// <summary>Flat authored combat and reward statistics for this archetype.</summary>
    public ArchetypeStats Stats { get; set; } = new();
    /// <summary>Behaviour and role trait flags (replaces separate Hostile/Shopkeeper/etc. tables).</summary>
    public ArchetypeTraits Traits { get; set; } = new();

    // Loot
    /// <summary>FK to the loot table dropped on defeat or interaction. Nullable = no loot.</summary>
    public Guid? LootTableId { get; set; }

    // Navigation properties
    /// <summary>Navigation property for the species definition.</summary>
    public Species? Species { get; set; }
    /// <summary>Navigation property for the actor class definition.</summary>
    public ActorClass? Class { get; set; }
    /// <summary>Navigation property for the background definition.</summary>
    public Background? Background { get; set; }
    /// <summary>Navigation property for the assigned loot table.</summary>
    public LootTable? LootTable { get; set; }

    /// <summary>Powers in this archetype's combat pool.</summary>
    public ICollection<ArchetypePowerPool> PowerPool { get; set; } = [];
    /// <summary>Named actor instances that override this archetype.</summary>
    public ICollection<ActorInstance> Instances { get; set; } = [];
}

/// <summary>Flat authored combat and reward statistics for an ActorArchetype.</summary>
public class ArchetypeStats
{
    /// <summary>Base maximum hit points.</summary>
    public int? Health { get; set; }
    /// <summary>Base maximum mana.</summary>
    public int? Mana { get; set; }
    /// <summary>Physical damage bonus.</summary>
    public int? Strength { get; set; }
    /// <summary>Attack speed and dodge modifier.</summary>
    public int? Agility { get; set; }
    /// <summary>Spellcasting power modifier.</summary>
    public int? Intelligence { get; set; }
    /// <summary>Health and stamina modifier.</summary>
    public int? Constitution { get; set; }
    /// <summary>Armor class (physical damage reduction).</summary>
    public int? ArmorClass { get; set; }
    /// <summary>Flat attack bonus applied to hit rolls.</summary>
    public int? AttackBonus { get; set; }
    /// <summary>Damage dealt per hit.</summary>
    public int? Damage { get; set; }
    /// <summary>Experience points awarded when this archetype is defeated.</summary>
    public int? ExperienceReward { get; set; }
    /// <summary>Minimum gold dropped.</summary>
    public int? GoldRewardMin { get; set; }
    /// <summary>Maximum gold dropped.</summary>
    public int? GoldRewardMax { get; set; }
    /// <summary>Trade skill level (used when archetype is a shopkeeper).</summary>
    public int? TradeSkill { get; set; }
    /// <summary>Gold available for trading (used when archetype is a shopkeeper).</summary>
    public int? TradeGold { get; set; }
}

/// <summary>Behaviour and role trait flags for an ActorArchetype.</summary>
public class ArchetypeTraits
{
    // Hostility flags
    /// <summary>True if this actor is hostile and attacks on sight.</summary>
    public bool? Hostile { get; set; }
    /// <summary>True if this actor attacks without provocation.</summary>
    public bool? Aggressive { get; set; }
    /// <summary>True if this actor calls nearby allies when engaged.</summary>
    public bool? PackHunter { get; set; }

    // NPC role flags
    /// <summary>True if this actor operates a shop.</summary>
    public bool? Shopkeeper { get; set; }
    /// <summary>True if this actor can offer quests.</summary>
    public bool? QuestGiver { get; set; }
    /// <summary>True if this actor participates in dialogue interactions.</summary>
    public bool? HasDialogue { get; set; }
    /// <summary>True if this actor cannot be killed.</summary>
    public bool? Immortal { get; set; }
    /// <summary>True if this actor moves through the world on a schedule.</summary>
    public bool? Wanderer { get; set; }

    // Tier flags
    /// <summary>True if this is a boss-tier actor.</summary>
    public bool? Boss { get; set; }
    /// <summary>True if this is an elite-tier actor.</summary>
    public bool? Elite { get; set; }

    // Combat style flags
    /// <summary>True if this actor uses ranged attacks.</summary>
    public bool? Ranged { get; set; }
    /// <summary>True if this actor can cast spells.</summary>
    public bool? Caster { get; set; }

    // Immunity flags
    /// <summary>True if this actor is immune to fire damage.</summary>
    public bool? FireImmune { get; set; }
    /// <summary>True if this actor is immune to cold damage.</summary>
    public bool? ColdImmune { get; set; }
    /// <summary>True if this actor is immune to poison damage.</summary>
    public bool? PoisonImmune { get; set; }
}
