namespace RealmEngine.Data.Entities;

/// <summary>
/// Entry in a loot table. Item is a soft cross-domain reference resolved via content_registry.
/// DropWeight is a relative weight within the table (not a global rarity weight).
/// </summary>
public class LootTableEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>FK to the loot table this entry belongs to.</summary>
    public Guid LootTableId { get; set; }

    /// <summary>Domain of the item being dropped — resolved via content_registry.</summary>
    public string ItemDomain { get; set; } = string.Empty;
    /// <summary>Slug of the item being dropped — resolved via content_registry.</summary>
    public string ItemSlug { get; set; } = string.Empty;

    /// <summary>Relative selection weight within this table — higher = more likely.</summary>
    public int DropWeight { get; set; } = 50;

    /// <summary>Minimum number of this item dropped per roll.</summary>
    public int QuantityMin { get; set; } = 1;
    /// <summary>Maximum number of this item dropped per roll.</summary>
    public int QuantityMax { get; set; } = 1;

    /// <summary>True = always included regardless of weight roll.</summary>
    public bool IsGuaranteed { get; set; }

    /// <summary>Navigation property for the owning loot table.</summary>
    public LootTable? LootTable { get; set; }
}
