namespace RealmEngine.Data.Entities;

/// <summary>
/// Biological species definition. TypeKey = species family (e.g. "humanoid", "beast", "undead").
/// Provides innate stat ranges and natural ability pools that all actors of this species share.
/// </summary>
public class Species : ContentBase
{
    /// <summary>Lore description of the species shown during character creation.</summary>
    public string? Description { get; set; }
    /// <summary>When true, this species may be selected by players during character creation.</summary>
    public bool IsPlayerSelectable { get; set; }
    /// <summary>Base and range statistics intrinsic to this species.</summary>
    public SpeciesStats Stats { get; set; } = new();
    /// <summary>Boolean biological and physical trait flags.</summary>
    public SpeciesTraits Traits { get; set; } = new();

    /// <summary>Natural / innate powers shared by all actors of this species.</summary>
    public ICollection<SpeciesPowerPool> PowerPool { get; set; } = [];
    /// <summary>Archetypes that belong to this species.</summary>
    public ICollection<ActorArchetype> Archetypes { get; set; } = [];
}

/// <summary>Base and range statistics intrinsic to a species.</summary>
public class SpeciesStats
{
    /// <summary>Base strength value for an average member of the species (NPC stat floor).</summary>
    public int? BaseStrength { get; set; }
    /// <summary>Base agility value (NPC stat floor).</summary>
    public int? BaseAgility { get; set; }
    /// <summary>Base intelligence value (NPC stat floor).</summary>
    public int? BaseIntelligence { get; set; }
    /// <summary>Base constitution value (NPC stat floor).</summary>
    public int? BaseConstitution { get; set; }
    /// <summary>Base maximum hit points at level 1.</summary>
    public int? BaseHealth { get; set; }
    /// <summary>Flat physical damage reduction from natural armor (hide, scales, etc.).</summary>
    public int? NaturalArmor { get; set; }
    /// <summary>Base movement speed in world units per second.</summary>
    public float? MovementSpeed { get; set; }
    /// <summary>Typical creature size category (e.g. "small", "medium", "large", "huge").</summary>
    public string? SizeCategory { get; set; }
    /// <summary>Flat Strength bonus applied to player characters of this species.</summary>
    public int? PlayerBonusStrength { get; set; }
    /// <summary>Flat Dexterity bonus applied to player characters of this species.</summary>
    public int? PlayerBonusDexterity { get; set; }
    /// <summary>Flat Constitution bonus applied to player characters of this species.</summary>
    public int? PlayerBonusConstitution { get; set; }
    /// <summary>Flat Intelligence bonus applied to player characters of this species.</summary>
    public int? PlayerBonusIntelligence { get; set; }
    /// <summary>Flat Wisdom bonus applied to player characters of this species.</summary>
    public int? PlayerBonusWisdom { get; set; }
    /// <summary>Flat Charisma bonus applied to player characters of this species.</summary>
    public int? PlayerBonusCharisma { get; set; }
}

/// <summary>Boolean biological and physical trait flags for a species.</summary>
public class SpeciesTraits
{
    /// <summary>True if the species is undead (affected by holy / turn-undead effects).</summary>
    public bool? Undead { get; set; }
    /// <summary>True if the species is classified as a beast.</summary>
    public bool? Beast { get; set; }
    /// <summary>True if the species is humanoid.</summary>
    public bool? Humanoid { get; set; }
    /// <summary>True if the species is demonic in origin.</summary>
    public bool? Demon { get; set; }
    /// <summary>True if the species is draconic.</summary>
    public bool? Dragon { get; set; }
    /// <summary>True if the species is an elemental.</summary>
    public bool? Elemental { get; set; }
    /// <summary>True if the species is a construct (golem, automaton, etc.).</summary>
    public bool? Construct { get; set; }
    /// <summary>True if the species has natural darkvision.</summary>
    public bool? Darkvision { get; set; }
    /// <summary>True if the species is fully aquatic or amphibious.</summary>
    public bool? Aquatic { get; set; }
    /// <summary>True if the species can fly.</summary>
    public bool? Flying { get; set; }
}
