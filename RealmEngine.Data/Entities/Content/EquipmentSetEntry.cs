namespace RealmEngine.Data.Entities;

/// <summary>
/// Equipment set catalog entry. TypeKey = "equipment-set".
/// A set grants bonuses when multiple pieces are worn simultaneously.
/// </summary>
public class EquipmentSetEntry : ContentBase
{
    /// <summary>Flavor text describing the set's lore or origin.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Slugs of the items that belong to this set.</summary>
    public EquipmentSetData Data { get; set; } = new();
}

/// <summary>JSON-owned data for an equipment set entry.</summary>
public class EquipmentSetData
{
    /// <summary>Slugs of items that are part of this set.</summary>
    public List<string> ItemSlugs { get; set; } = [];

    /// <summary>Ordered list of set bonuses unlocked as more pieces are equipped.</summary>
    public List<EquipmentSetBonus> Bonuses { get; set; } = [];
}

/// <summary>A bonus granted when a threshold of set pieces are worn.</summary>
public class EquipmentSetBonus
{
    /// <summary>Number of set pieces required to activate this bonus.</summary>
    public int PiecesRequired { get; set; }

    /// <summary>Human-readable description of the bonus.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Bonus to Strength.</summary>
    public int BonusStrength { get; set; }
    /// <summary>Bonus to Dexterity.</summary>
    public int BonusDexterity { get; set; }
    /// <summary>Bonus to Constitution.</summary>
    public int BonusConstitution { get; set; }
    /// <summary>Bonus to Intelligence.</summary>
    public int BonusIntelligence { get; set; }
    /// <summary>Bonus to Wisdom.</summary>
    public int BonusWisdom { get; set; }
    /// <summary>Bonus to Charisma.</summary>
    public int BonusCharisma { get; set; }
    /// <summary>Optional special effect applied at this tier.</summary>
    public string? SpecialEffect { get; set; }
}
