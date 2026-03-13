namespace RealmEngine.Data.Entities;

/// <summary>Enemy entity. TypeKey = family slug (e.g. "wolves", "humanoids/bandits").</summary>
public class Enemy : ContentBase
{
    /// <summary>Minimum character level this enemy spawns at.</summary>
    public int MinLevel { get; set; }
    /// <summary>Maximum character level this enemy spawns at.</summary>
    public int MaxLevel { get; set; }

    /// <summary>FK to the loot table dropped on defeat. Nullable = no loot.</summary>
    public Guid? LootTableId { get; set; }

    /// <summary>Combat and reward statistics.</summary>
    public EnemyStats Stats { get; set; } = new();
    /// <summary>Boolean behaviour and type flags.</summary>
    public EnemyTraits Traits { get; set; } = new();
    /// <summary>Runtime physics and AI properties.</summary>
    public EnemyProperties Properties { get; set; } = new();

    /// <summary>Navigation property for the assigned loot table.</summary>
    public LootTable? LootTable { get; set; }
    /// <summary>Abilities this enemy can use during combat.</summary>
    public ICollection<EnemyAbilityPool> AbilityPool { get; set; } = [];
}

/// <summary>Combat and reward statistics owned by an Enemy.</summary>
public class EnemyStats
{
    /// <summary>Base maximum hit points.</summary>
    public int? Health { get; set; }
    /// <summary>Base maximum mana.</summary>
    public int? Mana { get; set; }
    /// <summary>Physical damage bonus.</summary>
    public int? Strength { get; set; }
    /// <summary>Attack speed and dodge modifier.</summary>
    public int? Dexterity { get; set; }
    /// <summary>Spellcasting power modifier.</summary>
    public int? Intelligence { get; set; }
    /// <summary>Health and stamina modifier.</summary>
    public int? Constitution { get; set; }
    /// <summary>Flat physical damage reduction.</summary>
    public int? Armor { get; set; }
    /// <summary>Flat magic damage reduction.</summary>
    public int? MagicResist { get; set; }
    /// <summary>Experience points awarded on kill.</summary>
    public int? XpReward { get; set; }
    /// <summary>Minimum gold dropped on kill.</summary>
    public int? GoldRewardMin { get; set; }
    /// <summary>Maximum gold dropped on kill.</summary>
    public int? GoldRewardMax { get; set; }
}

/// <summary>Boolean behaviour and type flags owned by an Enemy.</summary>
public class EnemyTraits
{
    /// <summary>True if the enemy attacks on sight without provocation.</summary>
    public bool? Aggressive { get; set; }
    /// <summary>True if the enemy calls nearby allies when engaged.</summary>
    public bool? PackHunter { get; set; }
    /// <summary>True if the enemy is immune to fire damage.</summary>
    public bool? FireImmune { get; set; }
    /// <summary>True if the enemy is immune to cold damage.</summary>
    public bool? ColdImmune { get; set; }
    /// <summary>True if the enemy is immune to poison damage.</summary>
    public bool? PoisonImmune { get; set; }
    /// <summary>True if the enemy is undead (affected by holy / turn-undead abilities).</summary>
    public bool? Undead { get; set; }
    /// <summary>True if this is a boss-tier enemy.</summary>
    public bool? Boss { get; set; }
    /// <summary>True if this is an elite-tier enemy.</summary>
    public bool? Elite { get; set; }
    /// <summary>True if the enemy uses ranged attacks.</summary>
    public bool? Ranged { get; set; }
    /// <summary>True if the enemy can cast spells.</summary>
    public bool? Spellcaster { get; set; }
}

/// <summary>Runtime physics and AI properties owned by an Enemy.</summary>
public class EnemyProperties
{
    /// <summary>Base movement speed in world units per second.</summary>
    public float? MovementSpeed { get; set; }
    /// <summary>Maximum distance at which the enemy can land a melee hit.</summary>
    public float? AttackRange { get; set; }
    /// <summary>Radius within which the enemy detects and aggros the player.</summary>
    public float? DetectRadius { get; set; }
    /// <summary>Attacks per second.</summary>
    public float? AttackSpeed { get; set; }
    /// <summary>Faction slug — soft reference, not a FK.</summary>
    public string? Faction { get; set; }
}
