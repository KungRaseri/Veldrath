namespace RealmEngine.Data.Entities;

/// <summary>Armor catalog entry. TypeKey = armor slot (e.g. "chest", "helm", "boots").</summary>
public class Armor : ContentBase
{
    /// <summary>"light" | "medium" | "heavy" | "shield"</summary>
    public string ArmorType { get; set; } = string.Empty;

    /// <summary>"head" | "chest" | "legs" | "feet" | "hands" | "offhand"</summary>
    public string EquipSlot { get; set; } = string.Empty;

    /// <summary>Defense and encumbrance statistics.</summary>
    public ArmorStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this armor.</summary>
    public ArmorTraits Traits { get; set; } = new();
}

/// <summary>Defense and encumbrance statistics owned by an Armor entry.</summary>
public class ArmorStats
{
    /// <summary>Flat physical damage reduction.</summary>
    public int? ArmorRating { get; set; }
    /// <summary>Flat magic damage reduction.</summary>
    public int? MagicResist { get; set; }
    /// <summary>Item weight in lbs — affects encumbrance.</summary>
    public float? Weight { get; set; }
    /// <summary>Maximum durability before the armor breaks.</summary>
    public int? Durability { get; set; }
    /// <summary>Movement speed reduction applied while worn (0.0–1.0).</summary>
    public float? MovementPenalty { get; set; }
    /// <summary>Base sell/buy value in gold.</summary>
    public int? Value { get; set; }
}

/// <summary>Boolean trait flags owned by an Armor entry.</summary>
public class ArmorTraits
{
    /// <summary>True if wearing this armor imposes a stealth disadvantage.</summary>
    public bool? StealthPenalty { get; set; }
    /// <summary>True if this armor provides fire resistance.</summary>
    public bool? FireResist { get; set; }
    /// <summary>True if this armor provides cold resistance.</summary>
    public bool? ColdResist { get; set; }
    /// <summary>True if this armor provides lightning resistance.</summary>
    public bool? LightningResist { get; set; }
    /// <summary>True if this armor is cursed and cannot be unequipped normally.</summary>
    public bool? Cursed { get; set; }
    /// <summary>True if this armor has a magical enchantment.</summary>
    public bool? Magical { get; set; }
}
