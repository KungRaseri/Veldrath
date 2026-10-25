namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a character's proficiency in a specific skill.
/// Skills rank from 0 (untrained) to 100 (master) through practice-based XP gain.
/// Maps to skills/catalog.json structure in JSON v4.2.
/// </summary>
public class CharacterSkill
{
    /// <summary>
    /// Unique identifier matching JSON catalog (e.g., "athletics", "light-blades", "arcane").
    /// </summary>
    public required string SkillId { get; set; }

    /// <summary>
    /// Display name for UI (loaded from catalog).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category for organization (Attribute, Weapon, Armor, Magic, Profession).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Current rank (0-100). Starts at 0 for all skills.
    /// </summary>
    public int CurrentRank { get; set; } = 0;

    /// <summary>
    /// Current XP progress toward next rank.
    /// </summary>
    public int CurrentXP { get; set; } = 0;

    /// <summary>
    /// XP required to reach next rank (calculated from formula).
    /// </summary>
    public int XPToNextRank { get; set; } = 100;

    /// <summary>
    /// Total XP earned in this skill (lifetime stat).
    /// </summary>
    public int TotalXP { get; set; } = 0;

    /// <summary>
    /// Governing attribute for this skill (strength, dexterity, etc.).
    /// Skill checks combine skill rank + attribute modifier.
    /// </summary>
    public string GoverningAttribute { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Track which actions contributed XP (for analytics).
    /// Key = action name (e.g., "melee_hit", "cast_spell"), Value = XP gained.
    /// </summary>
    public Dictionary<string, int> XPSources { get; set; } = new();
}

/// <summary>
/// Represents a character ability tracking entry.
/// Tracks usage statistics and cooldown state for learned abilities.
/// </summary>
public class CharacterAbility
{
    /// <summary>
    /// Unique ability identifier (e.g., "active/offensive:charge").
    /// </summary>
    public required string AbilityId { get; set; }

    /// <summary>
    /// When this ability was learned.
    /// </summary>
    public DateTime LearnedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times successfully used.
    /// </summary>
    public int TimesUsed { get; set; } = 0;

    /// <summary>
    /// Current cooldown remaining (in turns/seconds).
    /// </summary>
    public int CooldownRemaining { get; set; } = 0;

    /// <summary>
    /// Is this ability favorited for quick access?
    /// </summary>
    public bool IsFavorite { get; set; } = false;
}

/// <summary>
/// Represents a spell a character has learned.
/// </summary>
public class CharacterSpell
{
    /// <summary>
    /// Reference to spell definition (e.g., "fireball", "heal").
    /// </summary>
    public required string SpellId { get; set; }

    /// <summary>
    /// When was this spell learned.
    /// </summary>
    public DateTime LearnedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times successfully cast.
    /// </summary>
    public int TimesCast { get; set; } = 0;

    /// <summary>
    /// Number of times casting failed (fizzle).
    /// </summary>
    public int TimesFizzled { get; set; } = 0;

    /// <summary>
    /// Is this spell favorited for quick access?
    /// </summary>
    public bool IsFavorite { get; set; } = false;
}
