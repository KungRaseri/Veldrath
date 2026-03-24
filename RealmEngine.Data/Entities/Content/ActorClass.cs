namespace RealmEngine.Data.Entities;

/// <summary>
/// Actor class definition — the role/job archetype that shapes combat style and growth.
/// Applies to both player characters and any actor archetype (replacing CharacterClass).
/// HitDie = sides of the health die per level (e.g. 10 for Fighter, 6 for Wizard).
/// </summary>
public class ActorClass : ContentBase
{
    /// <summary>Sides of the health die rolled per level (e.g. 10 = Fighter, 6 = Wizard).</summary>
    public int HitDie { get; set; } = 8;
    /// <summary>"strength" | "dexterity" | "intelligence" | "constitution" — governs primary scaling.</summary>
    public string PrimaryStat { get; set; } = string.Empty;

    /// <summary>Base and growth statistics for this class.</summary>
    public ActorClassStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this class's playstyle.</summary>
    public ActorClassTraits Traits { get; set; } = new();

    /// <summary>Powers (abilities, spells, talents) unlocked as the actor levels in this class.</summary>
    public ICollection<ClassPowerUnlock> PowerUnlocks { get; set; } = [];
    /// <summary>Archetypes that use this class.</summary>
    public ICollection<ActorArchetype> Archetypes { get; set; } = [];
}

/// <summary>Base and growth statistics owned by an ActorClass.</summary>
public class ActorClassStats
{
    /// <summary>Starting hit points at level 1.</summary>
    public int? BaseHealth { get; set; }
    /// <summary>Starting mana at level 1.</summary>
    public int? BaseMana { get; set; }
    /// <summary>HP gained per level (may be fractional if averaged).</summary>
    public float? HealthGrowth { get; set; }
    /// <summary>Mana gained per level.</summary>
    public float? ManaGrowth { get; set; }
    /// <summary>Strength gained per level.</summary>
    public float? StrengthGrowth { get; set; }
    /// <summary>Dexterity gained per level.</summary>
    public float? DexterityGrowth { get; set; }
    /// <summary>Intelligence gained per level.</summary>
    public float? IntelligenceGrowth { get; set; }
    /// <summary>Constitution gained per level.</summary>
    public float? ConstitutionGrowth { get; set; }
}

/// <summary>Boolean trait flags classifying an ActorClass's playstyle.</summary>
public class ActorClassTraits
{
    /// <summary>True if the class can wield two weapons simultaneously.</summary>
    public bool? CanDualWield { get; set; }
    /// <summary>True if the class is proficient with heavy armor.</summary>
    public bool? CanWearHeavy { get; set; }
    /// <summary>True if the class can cast spells.</summary>
    public bool? Spellcaster { get; set; }
    /// <summary>True if the class is proficient with shields.</summary>
    public bool? CanWearShield { get; set; }
    /// <summary>True if the class is primarily melee-focused.</summary>
    public bool? Melee { get; set; }
    /// <summary>True if the class is primarily ranged-focused.</summary>
    public bool? Ranged { get; set; }
    /// <summary>True if the class has stealth abilities.</summary>
    public bool? Stealth { get; set; }
}
