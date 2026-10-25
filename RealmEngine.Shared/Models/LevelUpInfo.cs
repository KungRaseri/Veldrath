namespace RealmEngine.Shared.Models;

/// <summary>
/// Information about a pending level-up that needs player choices.
/// </summary>
public class LevelUpInfo
{
    /// <summary>Gets or sets the new level.</summary>
    public int NewLevel { get; set; }

    /// <summary>Gets or sets the attribute points gained.</summary>
    public int AttributePointsGained { get; set; }

    /// <summary>Gets or sets the skill points gained.</summary>
    public int SkillPointsGained { get; set; }

    /// <summary>Gets or sets a value indicating whether this level-up has been processed.</summary>
    public bool IsProcessed { get; set; }
}

/// <summary>
/// Tracks attribute point allocations during level-up.
/// </summary>
public class AttributePointAllocation
{
    /// <summary>Gets or sets the strength points.</summary>
    public int StrengthPoints { get; set; }

    /// <summary>Gets or sets the dexterity points.</summary>
    public int DexterityPoints { get; set; }

    /// <summary>Gets or sets the constitution points.</summary>
    public int ConstitutionPoints { get; set; }

    /// <summary>Gets or sets the intelligence points.</summary>
    public int IntelligencePoints { get; set; }

    /// <summary>Gets or sets the wisdom points.</summary>
    public int WisdomPoints { get; set; }

    /// <summary>Gets or sets the charisma points.</summary>
    public int CharismaPoints { get; set; }

    /// <summary>Gets the total points allocated.</summary>
    public int TotalPointsAllocated =>
        StrengthPoints + DexterityPoints + ConstitutionPoints +
        IntelligencePoints + WisdomPoints + CharismaPoints;

    /// <summary>
    /// Resets all allocated points to zero.
    /// </summary>
    public void Reset()
    {
        StrengthPoints = 0;
        DexterityPoints = 0;
        ConstitutionPoints = 0;
        IntelligencePoints = 0;
        WisdomPoints = 0;
        CharismaPoints = 0;
    }
}

/// <summary>
/// Represents a skill that can be learned or upgraded.
/// </summary>
public class Skill
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the required level.</summary>
    public int RequiredLevel { get; set; }

    /// <summary>Gets or sets the maximum rank.</summary>
    public int MaxRank { get; set; } = 5;

    /// <summary>Gets or sets the current rank.</summary>
    public int CurrentRank { get; set; }

    /// <summary>Gets or sets the skill type.</summary>
    public SkillType Type { get; set; }

    /// <summary>Gets or sets the effect description.</summary>
    public string Effect { get; set; } = string.Empty;
}

/// <summary>
/// Categories of skills available.
/// </summary>
public enum SkillType
{
    /// <summary>Combat skill.</summary>
    Combat,
    /// <summary>Defense skill.</summary>
    Defense,
    /// <summary>Magic skill.</summary>
    Magic,
    /// <summary>Utility skill.</summary>
    Utility,
    /// <summary>Passive skill.</summary>
    Passive
}
