namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a faction in the game world (guilds, kingdoms, organizations).
/// </summary>
public class Faction
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the faction name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the faction description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the faction type.
    /// </summary>
    public FactionType Type { get; set; } = FactionType.Neutral;

    /// <summary>
    /// Gets or sets the faction's home location (town/region).
    /// </summary>
    public string? HomeLocation { get; set; }

    /// <summary>
    /// Gets or sets factions that are enemies of this faction.
    /// </summary>
    public List<string> EnemyFactionIds { get; set; } = new();

    /// <summary>
    /// Gets or sets factions that are allies of this faction.
    /// </summary>
    public List<string> AllyFactionIds { get; set; } = new();

    /// <summary>
    /// Gets or sets reputation level names (localized).
    /// </summary>
    public Dictionary<ReputationLevel, string> LevelNames { get; set; } = new()
    {
        { ReputationLevel.Hostile, "Hostile" },
        { ReputationLevel.Unfriendly, "Unfriendly" },
        { ReputationLevel.Neutral, "Neutral" },
        { ReputationLevel.Friendly, "Friendly" },
        { ReputationLevel.Honored, "Honored" },
        { ReputationLevel.Revered, "Revered" },
        { ReputationLevel.Exalted, "Exalted" }
    };

    /// <summary>
    /// Gets or sets the starting reputation with this faction.
    /// </summary>
    public int StartingReputation { get; set; } = 0; // Neutral

    /// <summary>
    /// Gets or sets quest IDs associated with this faction.
    /// </summary>
    public List<string> QuestIds { get; set; } = new();

    /// <summary>
    /// Gets or sets NPC IDs that belong to this faction.
    /// </summary>
    public List<string> MemberNpcIds { get; set; } = new();

    /// <summary>
    /// Gets or sets rarity weight for procedural generation.
    /// </summary>
    public int RarityWeight { get; set; } = 50;
}

/// <summary>
/// Faction types.
/// </summary>
public enum FactionType
{
    /// <summary>Kingdom or government faction.</summary>
    Kingdom,
    
    /// <summary>Guild or trade organization.</summary>
    Guild,
    
    /// <summary>Religious order.</summary>
    Religious,
    
    /// <summary>Criminal organization.</summary>
    Criminal,
    
    /// <summary>Monster or enemy faction.</summary>
    Monster,
    
    /// <summary>Neutral faction.</summary>
    Neutral
}

/// <summary>
/// Reputation levels with player.
/// </summary>
public enum ReputationLevel
{
    /// <summary>-6000 to -3000: Will attack on sight.</summary>
    Hostile = -6000,
    
    /// <summary>-3000 to -500: Distrustful, limited services.</summary>
    Unfriendly = -3000,
    
    /// <summary>-500 to 500: Starting level, basic services.</summary>
    Neutral = 0,
    
    /// <summary>500 to 3000: Helpful, discounts available.</summary>
    Friendly = 500,
    
    /// <summary>3000 to 6000: Respected, special quests.</summary>
    Honored = 3000,
    
    /// <summary>6000 to 12000: Highly respected, rare rewards.</summary>
    Revered = 6000,
    
    /// <summary>12000+: Maximum reputation, unique content.</summary>
    Exalted = 12000
}
