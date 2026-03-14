namespace RealmEngine.Shared.Models;

/// <summary>
/// Lightweight representation of a loot table catalog entry.
/// Used by <see cref="RealmEngine.Shared.Abstractions.ILootTableRepository"/>.
/// </summary>
public class LootTableData
{
    /// <summary>URL-safe slug unique within the loot-tables domain.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Human-readable display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Context category: "enemies", "chests", "harvesting", etc.</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>Selection weight for random draws.</summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>True if this is a boss-tier table.</summary>
    public bool IsBoss { get; set; }

    /// <summary>True if this is used for chest containers.</summary>
    public bool IsChest { get; set; }

    /// <summary>True if this is used for resource node harvesting.</summary>
    public bool IsHarvesting { get; set; }

    /// <summary>Drop entries contained in this table.</summary>
    public List<LootTableEntryData> Entries { get; set; } = [];
}

/// <summary>A single drop entry within a <see cref="LootTableData"/>.</summary>
public class LootTableEntryData
{
    /// <summary>Domain of the item being dropped (e.g. "items/weapons/swords").</summary>
    public string ItemDomain { get; set; } = string.Empty;

    /// <summary>Slug of the item being dropped.</summary>
    public string ItemSlug { get; set; } = string.Empty;

    /// <summary>Relative drop weight within this table.</summary>
    public int DropWeight { get; set; } = 50;

    /// <summary>Minimum quantity dropped per roll.</summary>
    public int QuantityMin { get; set; } = 1;

    /// <summary>Maximum quantity dropped per roll.</summary>
    public int QuantityMax { get; set; } = 1;

    /// <summary>True if this entry always drops regardless of weight roll.</summary>
    public bool IsGuaranteed { get; set; }
}
