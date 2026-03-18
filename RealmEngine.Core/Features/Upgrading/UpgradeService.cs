using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Upgrading;

/// <summary>
/// Service providing calculation and validation logic for the item upgrade system.
/// </summary>
public class UpgradeService
{
    private readonly ILogger<UpgradeService> _logger;

    /// <summary>Initializes a new instance of <see cref="UpgradeService"/>.</summary>
    public UpgradeService(ILogger<UpgradeService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates the success rate for upgrading to a given level.
    /// +1–+5 are safe (100%). +6–+10 have graduated risk down to 50%.
    /// </summary>
    /// <param name="targetLevel">The target upgrade level (1–10).</param>
    /// <returns>Success rate as a percentage (0–100).</returns>
    public double CalculateSuccessRate(int targetLevel) => targetLevel switch
    {
        <= 5 => 100.0,
        6 => 95.0,
        7 => 85.0,
        8 => 75.0,
        9 => 60.0,
        10 => 50.0,
        _ => 25.0
    };

    /// <summary>
    /// Calculates the stat multiplier applied at a given upgrade level.
    /// Formula: 1 + (level × 0.10) + (level² × 0.01)
    /// </summary>
    /// <param name="upgradeLevel">The upgrade level (0–10).</param>
    /// <returns>Stat multiplier (e.g. 1.75 at +5, 2.50 at +10).</returns>
    public double CalculateStatMultiplier(int upgradeLevel) =>
        1.0 + (upgradeLevel * 0.10) + (upgradeLevel * upgradeLevel * 0.01);

    /// <summary>
    /// Returns the essence type name required for a given item type.
    /// Weapons and off-hands → Weapon Essence; armor pieces → Armor Essence; jewelry → Accessory Essence.
    /// </summary>
    /// <param name="itemType">The type of item being upgraded.</param>
    /// <returns>Essence type name string.</returns>
    public string GetRequiredEssenceType(ItemType itemType) => itemType switch
    {
        ItemType.Weapon => "Weapon",
        ItemType.Shield => "Armor",
        ItemType.OffHand => "Weapon",
        ItemType.Helmet => "Armor",
        ItemType.Shoulders => "Armor",
        ItemType.Chest => "Armor",
        ItemType.Bracers => "Armor",
        ItemType.Gloves => "Armor",
        ItemType.Belt => "Armor",
        ItemType.Legs => "Armor",
        ItemType.Boots => "Armor",
        ItemType.Necklace => "Accessory",
        ItemType.Ring => "Accessory",
        _ => "Unknown"
    };

    /// <summary>
    /// Returns the list of essence tier names required to attempt a given upgrade level.
    /// </summary>
    /// <param name="targetLevel">The target upgrade level (1–10).</param>
    /// <returns>Ordered list of required essence tier names (e.g. "Minor", "Greater").</returns>
    public List<string> GetRequiredEssences(int targetLevel) => targetLevel switch
    {
        1 => ["Minor"],
        2 => ["Minor", "Minor"],
        3 => ["Minor", "Minor", "Minor"],
        4 => ["Greater", "Minor", "Minor"],
        5 => ["Greater", "Greater"],
        6 => ["Greater", "Greater", "Greater"],
        7 => ["Superior", "Greater", "Greater", "Greater"],
        8 => ["Superior", "Superior"],
        9 => ["Superior", "Superior", "Superior"],
        10 => ["Perfect", "Superior", "Superior", "Superior"],
        _ => []
    };

    /// <summary>
    /// Builds a full upgrade preview for the next level of an item.
    /// </summary>
    /// <param name="item">The item to preview.</param>
    /// <returns>A populated <see cref="UpgradePreviewInfo"/> or null if the item cannot be upgraded.</returns>
    public UpgradePreviewInfo? BuildPreview(Item item)
    {
        var maxLevel = item.GetMaxUpgradeLevel();
        if (item.UpgradeLevel >= maxLevel)
        {
            _logger.LogDebug("{ItemName} is already at max upgrade level {Level}", item.Name, maxLevel);
            return null;
        }

        var targetLevel = item.UpgradeLevel + 1;
        var successRate = CalculateSuccessRate(targetLevel);
        var currentMultiplier = CalculateStatMultiplier(item.UpgradeLevel);
        var projectedMultiplier = CalculateStatMultiplier(targetLevel);
        var requiredEssences = GetRequiredEssences(targetLevel);
        var essenceType = GetRequiredEssenceType(item.Type);

        return new UpgradePreviewInfo
        {
            CurrentLevel = item.UpgradeLevel,
            TargetLevel = targetLevel,
            MaxLevel = maxLevel,
            SuccessRate = successRate,
            IsSafeZone = targetLevel <= 5,
            CurrentStatMultiplier = currentMultiplier,
            ProjectedStatMultiplier = projectedMultiplier,
            StatIncreasePercent = (projectedMultiplier - currentMultiplier) * 100.0,
            EssenceType = essenceType,
            RequiredEssenceTiers = requiredEssences
        };
    }
}

/// <summary>
/// Preview information for an item upgrade attempt.
/// </summary>
public class UpgradePreviewInfo
{
    /// <summary>Gets or sets the item's current upgrade level.</summary>
    public int CurrentLevel { get; set; }

    /// <summary>Gets or sets the target upgrade level being previewed.</summary>
    public int TargetLevel { get; set; }

    /// <summary>Gets or sets the maximum upgrade level for the item's rarity.</summary>
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets the success rate for this upgrade attempt (0–100).</summary>
    public double SuccessRate { get; set; }

    /// <summary>Gets or sets a value indicating whether this level is in the safe zone (+1–+5, always succeeds).</summary>
    public bool IsSafeZone { get; set; }

    /// <summary>Gets or sets the current stat multiplier before upgrading.</summary>
    public double CurrentStatMultiplier { get; set; }

    /// <summary>Gets or sets the projected stat multiplier after a successful upgrade.</summary>
    public double ProjectedStatMultiplier { get; set; }

    /// <summary>Gets or sets the percentage point increase in stats from this upgrade.</summary>
    public double StatIncreasePercent { get; set; }

    /// <summary>Gets or sets the essence type required (Weapon, Armor, or Accessory).</summary>
    public string EssenceType { get; set; } = string.Empty;

    /// <summary>Gets or sets the tier names of each required essence (e.g. Minor, Greater).</summary>
    public List<string> RequiredEssenceTiers { get; set; } = new();
}
