namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents the rarity tier of game content (items, enemies, materials, etc.)
/// Calculated from rarityWeight ranges - no separate rarity field needed in data.
/// </summary>
public enum RarityTier
{
    /// <summary>Common items (rarityWeight: 50-100)</summary>
    Common = 0,

    /// <summary>Uncommon items (rarityWeight: 30-49)</summary>
    Uncommon = 1,

    /// <summary>Rare items (rarityWeight: 15-29)</summary>
    Rare = 2,

    /// <summary>Epic items (rarityWeight: 5-14)</summary>
    Epic = 3,

    /// <summary>Legendary items (rarityWeight: 1-4)</summary>
    Legendary = 4
}
