namespace RealmEngine.Data.Entities;

/// <summary>
/// Organization (faction, guild, business, shop).
/// TypeKey = org type (e.g. "factions", "guilds", "businesses", "shops").
/// </summary>
public class Organization : ContentBase
{
    /// <summary>"faction" | "guild" | "business" | "shop"</summary>
    public string OrgType { get; set; } = string.Empty;

    /// <summary>Size and relationship statistics.</summary>
    public OrganizationStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this organization.</summary>
    public OrganizationTraits Traits { get; set; } = new();
}

/// <summary>Size and relationship statistics owned by an Organization.</summary>
public class OrganizationStats
{
    /// <summary>Approximate number of members in the organization.</summary>
    public int? MemberCount { get; set; }
    /// <summary>Organization's accumulated wealth (affects trade prices).</summary>
    public int? Wealth { get; set; }
    /// <summary>Minimum reputation score for the player to be considered friendly.</summary>
    public int? ReputationThresholdFriendly { get; set; }
    /// <summary>Reputation score below which the organization treats the player as hostile.</summary>
    public int? ReputationThresholdHostile { get; set; }
}

/// <summary>Boolean trait flags classifying an Organization.</summary>
public class OrganizationTraits
{
    /// <summary>True if the organization is hostile to the player by default.</summary>
    public bool? Hostile { get; set; }
    /// <summary>True if the player can join this organization.</summary>
    public bool? Joinable { get; set; }
    /// <summary>True if the organization operates a shop the player can trade with.</summary>
    public bool? HasShop { get; set; }
    /// <summary>True if NPCs in this organization can offer quests.</summary>
    public bool? QuestGiver { get; set; }
    /// <summary>True if this is a political faction that affects world state.</summary>
    public bool? PoliticalFaction { get; set; }
}
