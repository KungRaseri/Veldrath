namespace RealmEngine.Data.Entities;

/// <summary>Junction: ability available to an NPC.</summary>
public class NpcAbility
{
    /// <summary>FK to the NPC that has this ability.</summary>
    public Guid NpcId { get; set; }
    /// <summary>FK to the assigned ability.</summary>
    public Guid AbilityId { get; set; }

    /// <summary>Navigation property for the owning NPC.</summary>
    public Npc? Npc { get; set; }
    /// <summary>Navigation property for the assigned ability.</summary>
    public Ability? Ability { get; set; }
}
