namespace RealmEngine.Data.Entities;

/// <summary>
/// Persistent game-world record of a spawned harvestable resource node.
/// Tracks both the node's static definition and its current live state
/// (current health, harvest count, last harvest timestamp).
/// </summary>
public class HarvestableNodeRecord
{
    /// <summary>Primary key — unique string identifier for this node instance (e.g. "node-1", "node-42").</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>Reference to the node definition type (e.g. "copper_vein", "oak_tree").</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>Display name shown to players.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Material tier: "common" | "uncommon" | "rare" | "epic" | "legendary".</summary>
    public string MaterialTier { get; set; } = "common";

    /// <summary>Current health of the node (0 = depleted, MaxHealth = pristine).</summary>
    public int CurrentHealth { get; set; }

    /// <summary>Maximum health of the node (typically 100–500 based on tier).</summary>
    public int MaxHealth { get; set; }

    /// <summary>Timestamp of the last harvest action (UTC).</summary>
    public DateTime LastHarvestedAt { get; set; }

    /// <summary>Total number of times this node has been harvested.</summary>
    public int TimesHarvested { get; set; }

    /// <summary>Location identifier where this node is spawned.</summary>
    public string LocationId { get; set; } = string.Empty;

    /// <summary>Biome type (e.g. "forest", "mountains", "caves").</summary>
    public string BiomeType { get; set; } = string.Empty;

    /// <summary>Reference slug of the loot table that governs material drops.</summary>
    public string LootTableRef { get; set; } = string.Empty;

    /// <summary>Minimum tool tier required to harvest this node (0 = no tool required).</summary>
    public int MinToolTier { get; set; }

    /// <summary>Base number of material items yielded before skill/tool bonuses.</summary>
    public int BaseYield { get; set; } = 1;

    /// <summary>True if this is a "rich" node with bonus critical harvest chance.</summary>
    public bool IsRichNode { get; set; }
}
