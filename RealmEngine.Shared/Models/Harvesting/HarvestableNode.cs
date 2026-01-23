namespace RealmEngine.Shared.Models.Harvesting;

/// <summary>
/// Represents a resource node in the world that can be harvested for materials.
/// </summary>
public class HarvestableNode
{
    /// <summary>
    /// Unique identifier for this specific node instance.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the node definition (e.g., "copper_vein", "oak_tree").
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown to players (e.g., "Copper Vein", "Ancient Oak Tree").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Material tier: "common", "uncommon", "rare", "epic", "legendary".
    /// </summary>
    public string MaterialTier { get; set; } = "common";

    /// <summary>
    /// Current health of the node (0 = depleted, MaxHealth = pristine).
    /// </summary>
    public int CurrentHealth { get; set; }

    /// <summary>
    /// Maximum health of the node (typically 100-500 based on tier).
    /// </summary>
    public int MaxHealth { get; set; }

    /// <summary>
    /// Timestamp of the last harvest action.
    /// </summary>
    public DateTime LastHarvestedAt { get; set; }

    /// <summary>
    /// Total number of times this node has been harvested.
    /// </summary>
    public int TimesHarvested { get; set; }

    /// <summary>
    /// Location ID where this node is spawned.
    /// </summary>
    public string LocationId { get; set; } = string.Empty;

    /// <summary>
    /// Biome type (e.g., "forest", "mountains", "caves").
    /// </summary>
    public string BiomeType { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the loot table for material drops.
    /// </summary>
    public string LootTableRef { get; set; } = string.Empty;

    /// <summary>
    /// Minimum tool tier required to harvest this node (0 = no tool required).
    /// </summary>
    public int MinToolTier { get; set; }

    /// <summary>
    /// Base yield (number of materials before skill/tool bonuses).
    /// </summary>
    public int BaseYield { get; set; } = 1;

    /// <summary>
    /// Whether this is a "rich" node with bonus critical harvest chance.
    /// </summary>
    public bool IsRichNode { get; set; }

    /// <summary>
    /// Calculate the current state based on health percentage.
    /// </summary>
    public NodeState GetNodeState()
    {
        if (MaxHealth == 0) return NodeState.Empty;

        var healthPercent = (double)CurrentHealth / MaxHealth;

        return healthPercent switch
        {
            >= 0.80 => NodeState.Healthy,
            >= 0.40 => NodeState.Depleted,
            >= 0.10 => NodeState.Exhausted,
            _ => NodeState.Empty
        };
    }

    /// <summary>
    /// Check if the node can be harvested (health above threshold).
    /// </summary>
    public bool CanHarvest()
    {
        return GetNodeState() != NodeState.Empty;
    }

    /// <summary>
    /// Get health percentage as 0-100 integer.
    /// </summary>
    public int GetHealthPercent()
    {
        if (MaxHealth == 0) return 0;
        return (int)Math.Round((double)CurrentHealth / MaxHealth * 100);
    }
}
