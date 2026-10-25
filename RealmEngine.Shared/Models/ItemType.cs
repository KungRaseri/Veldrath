namespace RealmEngine.Shared.Models;

/// <summary>
/// Item types.
/// </summary>
public enum ItemType
{
    /// <summary>Consumable item.</summary>
    Consumable,

    /// <summary>Weapon item.</summary>
    Weapon,
    /// <summary>Shield item.</summary>
    Shield,
    /// <summary>Off-hand item (orbs, tomes, etc.).</summary>
    OffHand,

    /// <summary>Helmet armor piece.</summary>
    Helmet,
    /// <summary>Shoulders armor piece.</summary>
    Shoulders,
    /// <summary>Chest armor piece.</summary>
    Chest,
    /// <summary>Bracers armor piece.</summary>
    Bracers,
    /// <summary>Gloves armor piece.</summary>
    Gloves,
    /// <summary>Belt armor piece.</summary>
    Belt,
    /// <summary>Legs armor piece.</summary>
    Legs,
    /// <summary>Boots armor piece.</summary>
    Boots,

    /// <summary>Necklace jewelry.</summary>
    Necklace,
    /// <summary>Ring jewelry.</summary>
    Ring,

    /// <summary>Quest item.</summary>
    QuestItem,

    /// <summary>Enchantment scroll (consumable that applies permanent enchantment to equipment).</summary>
    EnchantmentScroll,

    /// <summary>Crafting material.</summary>
    Material
}
