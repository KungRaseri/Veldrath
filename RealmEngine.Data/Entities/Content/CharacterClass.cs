namespace RealmEngine.Data.Entities;

/// <summary>
/// Playable character class. Named CharacterClass to avoid conflict with C# keyword.
/// HitDie = sides of the health die per level (e.g. 10 for Fighter, 6 for Wizard).
/// </summary>
public class CharacterClass : ContentBase
{
    /// <summary>Sides of the health die rolled per level (e.g. 10 for Fighter, 6 for Wizard).</summary>
    public int HitDie { get; set; } = 8;
    /// <summary>"strength" | "dexterity" | "intelligence" | "constitution" — governs primary scaling.</summary>
    public string PrimaryStat { get; set; } = string.Empty;

    /// <summary>Base and growth statistics for this class.</summary>
    public ClassStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this class's playstyle.</summary>
    public ClassTraits Traits { get; set; } = new();

    /// <summary>Abilities unlocked as the character levels in this class.</summary>
    public ICollection<ClassAbilityUnlock> AbilityUnlocks { get; set; } = [];
}

/// <summary>Base and growth statistics owned by a CharacterClass.</summary>
public class ClassStats
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

/// <summary>Boolean trait flags classifying a CharacterClass's playstyle.</summary>
public class ClassTraits
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
