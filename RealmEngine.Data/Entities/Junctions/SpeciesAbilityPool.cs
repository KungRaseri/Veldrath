namespace RealmEngine.Data.Entities;

/// <summary>Junction: innate ability belonging to a species' natural ability pool.</summary>
public class SpeciesAbilityPool
{
    /// <summary>FK to the owning species.</summary>
    public Guid SpeciesId { get; set; }
    /// <summary>FK to the ability in the pool.</summary>
    public Guid AbilityId { get; set; }

    /// <summary>Navigation property for the owning species.</summary>
    public Species? Species { get; set; }
    /// <summary>Navigation property for the ability.</summary>
    public Ability? Ability { get; set; }
}
