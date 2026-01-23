namespace RealmEngine.Shared.Models.Harvesting;

/// <summary>
/// Result of a harvest action containing materials gained, XP awarded, and node state changes.
/// </summary>
public class HarvestResult
{
    /// <summary>
    /// Whether the harvest action succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Materials obtained from the harvest.
    /// </summary>
    public List<ItemDrop> MaterialsGained { get; set; } = new();

    /// <summary>
    /// Skill experience points awarded.
    /// </summary>
    public int SkillXPGained { get; set; }

    /// <summary>
    /// Whether this was a critical harvest (double yield, bonus materials).
    /// </summary>
    public bool WasCritical { get; set; }

    /// <summary>
    /// Node health remaining after harvest.
    /// </summary>
    public int NodeHealthRemaining { get; set; }

    /// <summary>
    /// Node health percentage (0-100).
    /// </summary>
    public int NodeHealthPercent { get; set; }

    /// <summary>
    /// Current node state after harvest.
    /// </summary>
    public NodeState NodeState { get; set; }

    /// <summary>
    /// Tool durability points lost during harvest.
    /// </summary>
    public int ToolDurabilityLost { get; set; }

    /// <summary>
    /// Message to display to the player.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed failure reason if Success = false.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Name of the skill that gained XP.
    /// </summary>
    public string? SkillName { get; set; }

    /// <summary>
    /// Bonus materials gained from critical harvest.
    /// </summary>
    public List<ItemDrop>? BonusMaterials { get; set; }
}

/// <summary>
/// Represents a material item dropped from harvesting.
/// </summary>
public class ItemDrop
{
    /// <summary>
    /// Item reference (e.g., "@items/materials/ore:copper-ore").
    /// </summary>
    public string ItemRef { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the item.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity dropped.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Whether this was a bonus drop (from critical harvest or rare proc).
    /// </summary>
    public bool IsBonus { get; set; }
}
