namespace RealmEngine.Data.Entities;

/// <summary>Junction: ability in an actor archetype's curated combat pool.</summary>
public class ArchetypeAbilityPool
{
    /// <summary>FK to the owning archetype.</summary>
    public Guid ArchetypeId { get; set; }
    /// <summary>FK to the ability in the pool.</summary>
    public Guid AbilityId { get; set; }

    /// <summary>Probability (0.0–1.0) that this ability is used when the actor attacks.</summary>
    public float UseChance { get; set; } = 1.0f;

    /// <summary>Navigation property for the owning archetype.</summary>
    public ActorArchetype? Archetype { get; set; }
    /// <summary>Navigation property for the ability.</summary>
    public Ability? Ability { get; set; }
}
