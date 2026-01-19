namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents requirements that must be met to equip or use an item.
/// Requirements are static and do not scale with item enhancement level.
/// </summary>
public class ItemRequirements
{
    /// <summary>
    /// Minimum character level required to use this item.
    /// </summary>
    public int Level { get; set; } = 1;
    
    /// <summary>
    /// Minimum attribute values required (sparse dictionary - only required attributes included).
    /// Keys: "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"
    /// </summary>
    public Dictionary<string, int> Attributes { get; set; } = new();
    
    /// <summary>
    /// Optional skill requirement for this item.
    /// </summary>
    public SkillRequirement? Skill { get; set; }
}

/// <summary>
/// Represents a skill-based requirement for item usage.
/// </summary>
public class SkillRequirement
{
    /// <summary>
    /// Reference to the required skill using v4.1 format.
    /// Example: "@skills/weapon:heavy-blades"
    /// </summary>
    public string Reference { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum skill rank required.
    /// </summary>
    public int Rank { get; set; } = 1;
}
