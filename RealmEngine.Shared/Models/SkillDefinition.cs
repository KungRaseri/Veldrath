namespace RealmEngine.Shared.Models;

/// <summary>
/// Defines the progression rules and properties for a learnable character skill.
/// </summary>
public class SkillDefinition
{
    /// <summary>Gets or sets the unique skill identifier.</summary>
    public required string SkillId { get; set; }
    /// <summary>Gets or sets the internal skill name.</summary>
    public required string Name { get; set; }
    /// <summary>Gets or sets the display name shown to players.</summary>
    public required string DisplayName { get; set; }
    /// <summary>Gets or sets the skill description.</summary>
    public required string Description { get; set; }
    /// <summary>Gets or sets the skill category (combat, magic, stealth, etc.).</summary>
    public required string Category { get; set; }
    /// <summary>Gets or sets the base XP cost for rank 1.</summary>
    public int BaseXPCost { get; set; } = 100;
    /// <summary>Gets or sets the multiplier applied to XP cost per rank.</summary>
    public double CostMultiplier { get; set; } = 0.5;
    /// <summary>Gets or sets the maximum achievable rank.</summary>
    public int MaxRank { get; set; } = 100;
    /// <summary>Gets or sets the governing attribute (strength, dexterity, etc.).</summary>
    public string GoverningAttribute { get; set; } = "none";
    /// <summary>Gets or sets the list of skill effects.</summary>
    public List<SkillEffect> Effects { get; set; } = [];
    /// <summary>Gets or sets the XP awards for specific actions.</summary>
    public Dictionary<string, int> XPActions { get; set; } = [];
}

/// <summary>
/// Represents a skill effect (damage bonus, speed increase, etc.).
/// </summary>
public class SkillEffect
{
    /// <summary>Gets or sets the effect type (damage, speed, etc.).</summary>
    public required string EffectType { get; set; }
    /// <summary>Gets or sets the numeric effect value.</summary>
    public double EffectValue { get; set; }
    /// <summary>Gets or sets what the effect applies to (general, weapon, spell, etc.).</summary>
    public string AppliesTo { get; set; } = "general";
}
