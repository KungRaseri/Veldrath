namespace RealmEngine.Data.Entities;

/// <summary>Junction: innate power belonging to a species' natural power pool.</summary>
public class SpeciesPowerPool
{
    /// <summary>FK to the owning species.</summary>
    public Guid SpeciesId { get; set; }
    /// <summary>FK to the power in the pool.</summary>
    public Guid PowerId { get; set; }

    /// <summary>Navigation property for the owning species.</summary>
    public Species? Species { get; set; }
    /// <summary>Navigation property for the power.</summary>
    public Power? Power { get; set; }
}
