namespace RealmEngine.Data.Entities;

/// <summary>Junction: power added to a specific actor instance on top of its archetype pool.</summary>
public class InstancePowerPool
{
    /// <summary>FK to the owning actor instance.</summary>
    public Guid InstanceId { get; set; }
    /// <summary>FK to the power in the pool.</summary>
    public Guid PowerId { get; set; }

    /// <summary>Navigation property for the owning actor instance.</summary>
    public ActorInstance? Instance { get; set; }
    /// <summary>Navigation property for the power.</summary>
    public Power? Power { get; set; }
}
