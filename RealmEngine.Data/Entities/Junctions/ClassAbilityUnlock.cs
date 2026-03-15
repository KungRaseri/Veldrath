namespace RealmEngine.Data.Entities;

/// <summary>Junction: ability unlocked when a character reaches a certain level in a class.</summary>
public class ClassAbilityUnlock
{
    /// <summary>FK to the character class that unlocks this ability.</summary>
    public Guid ClassId { get; set; }
    /// <summary>FK to the ability that is unlocked.</summary>
    public Guid AbilityId { get; set; }

    /// <summary>Character level at which this ability becomes available.</summary>
    public int LevelRequired { get; set; }

    /// <summary>Starting rank of the unlocked ability (usually 1).</summary>
    public int Rank { get; set; } = 1;

    /// <summary>Navigation property for the owning actor class.</summary>
    public ActorClass? Class { get; set; }
    /// <summary>Navigation property for the unlocked ability.</summary>
    public Ability? Ability { get; set; }
}
