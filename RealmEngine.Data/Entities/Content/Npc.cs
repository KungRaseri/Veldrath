namespace RealmEngine.Data.Entities;

/// <summary>Non-player character. TypeKey = NPC category (e.g. "merchants", "guards", "nobles").</summary>
public class Npc : ContentBase
{
    /// <summary>Faction slug — soft reference, not a FK.</summary>
    public string? Faction { get; set; }

    /// <summary>Combat and social statistics.</summary>
    public NpcStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags classifying this NPC's role.</summary>
    public NpcTraits Traits { get; set; } = new();
    /// <summary>Location schedule across the four times of day.</summary>
    public NpcSchedule Schedule { get; set; } = new();

    /// <summary>Abilities this NPC can use in combat.</summary>
    public ICollection<NpcAbility> Abilities { get; set; } = [];
}

/// <summary>Combat and social statistics owned by an Npc.</summary>
public class NpcStats
{
    /// <summary>Maximum hit points.</summary>
    public int? Health { get; set; }
    /// <summary>0 = neutral, negative = hostile, positive = friendly.</summary>
    public int? Disposition { get; set; }
    /// <summary>Skill level used when determining trade prices.</summary>
    public int? TradeSkill { get; set; }
    /// <summary>Gold available in the NPC's inventory for trading.</summary>
    public int? Gold { get; set; }
}

/// <summary>Boolean trait flags classifying an Npc's role.</summary>
public class NpcTraits
{
    /// <summary>True if the NPC attacks the player on sight.</summary>
    public bool? Hostile { get; set; }
    /// <summary>True if the NPC operates a shop the player can trade with.</summary>
    public bool? Shopkeeper { get; set; }
    /// <summary>True if the NPC can offer quests to the player.</summary>
    public bool? QuestGiver { get; set; }
    /// <summary>True if the NPC participates in dialogue interactions.</summary>
    public bool? HasDialogue { get; set; }
    /// <summary>True if the NPC cannot be killed.</summary>
    public bool? Immortal { get; set; }
    /// <summary>True if the NPC moves through the world on a schedule.</summary>
    public bool? Wanderer { get; set; }
}

/// <summary>Where the NPC is located during each period of the day.</summary>
public class NpcSchedule
{
    /// <summary>Location slug for the morning period.</summary>
    public string? Morning { get; set; }
    /// <summary>Location slug for the afternoon period.</summary>
    public string? Afternoon { get; set; }
    /// <summary>Location slug for the evening period.</summary>
    public string? Evening { get; set; }
    /// <summary>Location slug for the night period.</summary>
    public string? Night { get; set; }
}
