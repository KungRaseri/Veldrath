namespace RealmEngine.Data.Entities;

/// <summary>Quest. Objectives and rewards stored as owned JSON collections.</summary>
public class Quest : ContentBase
{
    /// <summary>Minimum character level required to accept this quest.</summary>
    public int MinLevel { get; set; }

    /// <summary>Experience, gold, and reputation reward totals.</summary>
    public QuestStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this quest.</summary>
    public QuestTraits Traits { get; set; } = new();
    /// <summary>Owned JSON collection of objective definitions.</summary>
    public QuestObjectives Objectives { get; set; } = new();
    /// <summary>Owned JSON collection of reward definitions.</summary>
    public QuestRewards Rewards { get; set; } = new();
}

/// <summary>Experience, gold, and reputation reward totals owned by a Quest.</summary>
public class QuestStats
{
    /// <summary>Experience points awarded on completion.</summary>
    public int? XpReward { get; set; }
    /// <summary>Gold awarded on completion.</summary>
    public int? GoldReward { get; set; }
    /// <summary>Faction reputation points awarded on completion.</summary>
    public int? ReputationReward { get; set; }
}

/// <summary>Boolean trait flags classifying a Quest.</summary>
public class QuestTraits
{
    /// <summary>True if the quest can be completed more than once.</summary>
    public bool? Repeatable { get; set; }
    /// <summary>True if this quest is part of the main storyline.</summary>
    public bool? MainStory { get; set; }
    /// <summary>True if the quest must be completed within a time limit.</summary>
    public bool? Timed { get; set; }
    /// <summary>True if the quest requires or is designed for a group of players.</summary>
    public bool? GroupQuest { get; set; }
    /// <summary>True if the quest does not appear in the quest log until triggered.</summary>
    public bool? HiddenUntilDiscovered { get; set; }
}

/// <summary>Owned JSON: list of quest objective definitions.</summary>
public class QuestObjectives
{
    /// <summary>Ordered list of objectives that must be completed.</summary>
    public List<QuestObjective> Items { get; set; } = [];
}

/// <summary>A single objective step within a Quest.</summary>
public class QuestObjective
{
    /// <summary>"kill" | "collect" | "escort" | "reach" | "interact"</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Slug of the target entity (enemy, item, location, etc.).</summary>
    public string Target { get; set; } = string.Empty;
    /// <summary>Number of times the objective must be fulfilled.</summary>
    public int Quantity { get; set; } = 1;
    /// <summary>Optional display description shown to the player.</summary>
    public string? Description { get; set; }
}

/// <summary>Owned JSON: list of quest reward definitions.</summary>
public class QuestRewards
{
    /// <summary>List of rewards granted when the quest is turned in.</summary>
    public List<QuestReward> Items { get; set; } = [];
}

/// <summary>A single reward item granted when a Quest is completed.</summary>
public class QuestReward
{
    /// <summary>"item" | "gold" | "xp" | "reputation"</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Resolved via content_registry for item rewards.</summary>
    public string? ItemDomain { get; set; }
    /// <summary>Slug of the item reward — resolved via content_registry.</summary>
    public string? ItemSlug { get; set; }
    /// <summary>Amount of gold, xp, or reputation granted.</summary>
    public int? Amount { get; set; }
    /// <summary>Number of item copies granted.</summary>
    public int? Quantity { get; set; }
}
