namespace RealmEngine.Data.Entities;

/// <summary>Junction: power in an actor archetype's curated combat pool.</summary>
public class ArchetypePowerPool
{
    /// <summary>FK to the owning archetype.</summary>
    public Guid ArchetypeId { get; set; }
    /// <summary>FK to the power in the pool.</summary>
    public Guid PowerId { get; set; }

    /// <summary>Probability (0.0–1.0) that this power is used when the actor attacks.</summary>
    public float UseChance { get; set; } = 1.0f;

    /// <summary>Navigation property for the owning archetype.</summary>
    public ActorArchetype? Archetype { get; set; }
    /// <summary>Navigation property for the power.</summary>
    public Power? Power { get; set; }
}
