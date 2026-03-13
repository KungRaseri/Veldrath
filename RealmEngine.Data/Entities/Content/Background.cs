namespace RealmEngine.Data.Entities;

/// <summary>Character background chosen at creation, granting starting bonuses and flavor.</summary>
public class Background : ContentBase
{
    /// <summary>Starting bonuses granted by this background.</summary>
    public BackgroundStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this background's archetype.</summary>
    public BackgroundTraits Traits { get; set; } = new();
}

/// <summary>Starting bonuses granted by a Background at character creation.</summary>
public class BackgroundStats
{
    /// <summary>Gold the character starts with from this background.</summary>
    public int? StartingGold { get; set; }
    /// <summary>Flat Strength bonus applied at character creation.</summary>
    public int? BonusStrength { get; set; }
    /// <summary>Flat Dexterity bonus applied at character creation.</summary>
    public int? BonusDexterity { get; set; }
    /// <summary>Flat Intelligence bonus applied at character creation.</summary>
    public int? BonusIntelligence { get; set; }
    /// <summary>Flat Constitution bonus applied at character creation.</summary>
    public int? BonusConstitution { get; set; }
    /// <summary>Skill slug that receives a starting bonus.</summary>
    public string? StartingSkillBonus { get; set; }
    /// <summary>Magnitude of the starting skill bonus.</summary>
    public int? SkillBonusValue { get; set; }
}

/// <summary>Boolean trait flags classifying a Background's archetype.</summary>
public class BackgroundTraits
{
    /// <summary>True if the background originates from a specific geographic region.</summary>
    public bool? Regional { get; set; }
    /// <summary>True if the background reflects noble birth or station.</summary>
    public bool? Noble { get; set; }
    /// <summary>True if the background involves a criminal past.</summary>
    public bool? Criminal { get; set; }
    /// <summary>True if the background involves trade or commerce.</summary>
    public bool? Merchant { get; set; }
    /// <summary>True if the background involves military service.</summary>
    public bool? Military { get; set; }
    /// <summary>True if the background involves academic study.</summary>
    public bool? Scholar { get; set; }
    /// <summary>True if the background involves religious devotion.</summary>
    public bool? Religious { get; set; }
}
