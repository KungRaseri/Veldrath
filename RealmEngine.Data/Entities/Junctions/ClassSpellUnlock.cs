namespace RealmEngine.Data.Entities;

/// <summary>Junction: spell unlocked when a character reaches a certain level in a class.</summary>
public class ClassSpellUnlock
{
    /// <summary>FK to the character class that unlocks this spell.</summary>
    public Guid ClassId { get; set; }
    /// <summary>FK to the spell that is unlocked.</summary>
    public Guid SpellId { get; set; }

    /// <summary>Character level at which this spell becomes available.</summary>
    public int LevelRequired { get; set; }

    /// <summary>Starting rank of the unlocked spell (usually 1).</summary>
    public int Rank { get; set; } = 1;

    /// <summary>Navigation property for the owning actor class.</summary>
    public ActorClass? Class { get; set; }
    /// <summary>Navigation property for the unlocked spell.</summary>
    public Spell? Spell { get; set; }
}
