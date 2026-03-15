namespace RealmEngine.Data.Entities;

/// <summary>Junction: ability added to a specific actor instance on top of its archetype pool.</summary>
public class InstanceAbilityPool
{
    /// <summary>FK to the owning actor instance.</summary>
    public Guid InstanceId { get; set; }
    /// <summary>FK to the ability in the pool.</summary>
    public Guid AbilityId { get; set; }

    /// <summary>Navigation property for the owning actor instance.</summary>
    public ActorInstance? Instance { get; set; }
    /// <summary>Navigation property for the ability.</summary>
    public Ability? Ability { get; set; }
}
