namespace RealmEngine.Data.Entities;

/// <summary>
/// Loot table. TypeKey = loot context (e.g. "enemies", "chests", "harvesting").
/// Items are fully relational via LootTableEntry.
/// </summary>
public class LootTable : ContentBase
{
    /// <summary>Boolean trait flags classifying this loot table's context.</summary>
    public LootTableTraits Traits { get; set; } = new();

    /// <summary>The items that can drop from this table.</summary>
    public ICollection<LootTableEntry> Entries { get; set; } = [];
}

/// <summary>Boolean trait flags classifying a LootTable's context.</summary>
public class LootTableTraits
{
    /// <summary>True if this is a boss-tier loot table.</summary>
    public bool? Boss { get; set; }
    /// <summary>True if this is an elite-tier loot table.</summary>
    public bool? Elite { get; set; }
    /// <summary>True if this is a common-tier loot table.</summary>
    public bool? Common { get; set; }
    /// <summary>True if this is a rare-tier loot table.</summary>
    public bool? Rare { get; set; }
    /// <summary>True if this table is used for chest containers rather than enemy drops.</summary>
    public bool? IsChest { get; set; }
    /// <summary>True if this table is used for resource node harvesting.</summary>
    public bool? IsHarvesting { get; set; }
}
