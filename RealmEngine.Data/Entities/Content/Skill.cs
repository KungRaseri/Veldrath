namespace RealmEngine.Data.Entities;

/// <summary>Learnable skill. MaxRank drives the per-rank progression curve.</summary>
public class Skill : ContentBase
{
    /// <summary>Maximum learnable rank for this skill.</summary>
    public int MaxRank { get; set; } = 5;

    /// <summary>Progression statistics per rank.</summary>
    public SkillStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this skill.</summary>
    public SkillTraits Traits { get; set; } = new();
}

/// <summary>Progression statistics per rank owned by a Skill.</summary>
public class SkillStats
{
    /// <summary>Experience points required to advance one rank.</summary>
    public int? XpPerRank { get; set; }
    /// <summary>Attribute or power bonus granted per rank.</summary>
    public float? BonusPerRank { get; set; }
    /// <summary>Starting value before any ranks are applied.</summary>
    public int? BaseValue { get; set; }
}

/// <summary>Boolean trait flags classifying a Skill.</summary>
public class SkillTraits
{
    /// <summary>True if the skill provides a passive bonus without activation.</summary>
    public bool? Passive { get; set; }
    /// <summary>True if the skill is used in combat contexts.</summary>
    public bool? Combat { get; set; }
    /// <summary>True if the skill is used in crafting contexts.</summary>
    public bool? Crafting { get; set; }
    /// <summary>True if the skill is used in social/dialogue contexts.</summary>
    public bool? Social { get; set; }
    /// <summary>True if the skill is used in wilderness/travel contexts.</summary>
    public bool? Exploration { get; set; }
}
