namespace RealmEngine.Data.Entities;

/// <summary>
/// Junction: power unlocked when a character reaches a certain level in a class.
/// Merges the former <c>ClassAbilityUnlock</c> and <c>ClassSpellUnlock</c> tables.
/// </summary>
public class ClassPowerUnlock
{
    /// <summary>FK to the character class that unlocks this power.</summary>
    public Guid ClassId { get; set; }
    /// <summary>FK to the power that is unlocked.</summary>
    public Guid PowerId { get; set; }

    /// <summary>Character level at which this power becomes available.</summary>
    public int LevelRequired { get; set; }

    /// <summary>Starting rank of the unlocked power (usually 1).</summary>
    public int Rank { get; set; } = 1;

    /// <summary>Navigation property for the owning actor class.</summary>
    public ActorClass? Class { get; set; }
    /// <summary>Navigation property for the unlocked power.</summary>
    public Power? Power { get; set; }
}
