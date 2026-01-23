namespace RealmEngine.Shared.Models.Harvesting;

/// <summary>
/// Lightweight reference to a node definition from configuration.
/// Used for spawning and node discovery.
/// </summary>
public class HarvestableNodeReference
{
    /// <summary>
    /// Node type identifier (e.g., "copper_vein", "oak_tree").
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI (e.g., "Copper Vein", "Oak Tree").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Material tier: "common", "uncommon", "rare", "epic", "legendary".
    /// </summary>
    public string Tier { get; set; } = "common";

    /// <summary>
    /// Required skill (e.g., "@skills/profession:mining").
    /// </summary>
    public string SkillRef { get; set; } = string.Empty;

    /// <summary>
    /// Minimum tool tier required (0 = no tool needed).
    /// </summary>
    public int MinToolTier { get; set; }

    /// <summary>
    /// Maximum node health.
    /// </summary>
    public int Health { get; set; } = 100;

    /// <summary>
    /// Base yield before bonuses.
    /// </summary>
    public int BaseYield { get; set; } = 1;

    /// <summary>
    /// Loot table identifier for material drops.
    /// </summary>
    public string LootTable { get; set; } = string.Empty;

    /// <summary>
    /// Biomes where this node can spawn.
    /// </summary>
    public List<string> Biomes { get; set; } = new();

    /// <summary>
    /// Rarity weight for spawn probability (higher = more common).
    /// </summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>
    /// Icon identifier for UI display.
    /// </summary>
    public string? Icon { get; set; }
}
