namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents the result of a crafting attempt.
/// </summary>
public class CraftingResult
{
    /// <summary>
    /// Whether the crafting attempt succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The crafted item (present on success and partial success).
    /// </summary>
    public Item? Item { get; set; }

    /// <summary>
    /// Whether a critical success occurred.
    /// </summary>
    public bool WasCritical { get; set; }

    /// <summary>
    /// Quality bonus tiers applied (0-3).
    /// </summary>
    public int QualityBonus { get; set; }

    /// <summary>
    /// The actual quality/rarity of the crafted item.
    /// </summary>
    public ItemRarity ActualQuality { get; set; }

    /// <summary>
    /// Failure reason if crafting was not possible.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Failure severity level (0=success, 1=marginal, 2=moderate, 3=critical).
    /// </summary>
    public int FailureSeverity { get; set; }

    /// <summary>
    /// Materials that were refunded due to critical failure.
    /// </summary>
    public List<(string ItemReference, int Quantity)> RefundedMaterials { get; set; } = new();
}
