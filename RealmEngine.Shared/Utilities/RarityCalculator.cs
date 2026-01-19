using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Utilities;

/// <summary>
/// Calculates rarity tier and associated display properties from rarityWeight values.
/// Eliminates the need for separate 'rarity' fields in game data.
/// Provides Godot-friendly methods for UI rendering and gameplay logic.
/// </summary>
public static class RarityCalculator
{
    /// <summary>
    /// Calculate the rarity tier from a rarityWeight value.
    /// Higher weight = more common = lower tier.
    /// </summary>
    /// <param name="rarityWeight">The rarity weight (1-100+)</param>
    /// <returns>The calculated rarity tier</returns>
    public static RarityTier GetRarityTier(int rarityWeight)
    {
        return rarityWeight switch
        {
            >= 50 => RarityTier.Common,
            >= 30 => RarityTier.Uncommon,
            >= 15 => RarityTier.Rare,
            >= 5 => RarityTier.Epic,
            _ => RarityTier.Legendary
        };
    }

    /// <summary>
    /// Get the display color (hex) for a rarity tier.
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Hex color string (e.g., "#FF8000")</returns>
    public static string GetRarityColor(RarityTier tier)
    {
        return tier switch
        {
            RarityTier.Common => "#FFFFFF",      // White
            RarityTier.Uncommon => "#1EFF00",    // Green
            RarityTier.Rare => "#0070DD",        // Blue
            RarityTier.Epic => "#A335EE",        // Purple
            RarityTier.Legendary => "#FF8000",   // Orange
            _ => "#FFFFFF"
        };
    }

    /// <summary>
    /// Get the display color for a given rarityWeight.
    /// Convenience method that calculates tier first.
    /// </summary>
    /// <param name="rarityWeight">The rarity weight</param>
    /// <returns>Hex color string</returns>
    public static string GetRarityColor(int rarityWeight)
    {
        var tier = GetRarityTier(rarityWeight);
        return GetRarityColor(tier);
    }

    /// <summary>
    /// Get RGB color values for Godot Color constructor.
    /// Returns normalized values (0.0-1.0) ready for: new Color(r, g, b)
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Tuple of (r, g, b) as floats 0.0-1.0</returns>
    public static (float r, float g, float b) GetRarityColorRGB(RarityTier tier)
    {
        return tier switch
        {
            RarityTier.Common => (1.0f, 1.0f, 1.0f),        // White
            RarityTier.Uncommon => (0.118f, 1.0f, 0.0f),    // Green
            RarityTier.Rare => (0.0f, 0.439f, 0.867f),      // Blue
            RarityTier.Epic => (0.639f, 0.208f, 0.933f),    // Purple
            RarityTier.Legendary => (1.0f, 0.502f, 0.0f),   // Orange
            _ => (1.0f, 1.0f, 1.0f)
        };
    }

    /// <summary>
    /// Get RGB color values for a given rarityWeight.
    /// Convenience method for Godot Color constructor.
    /// </summary>
    /// <param name="rarityWeight">The rarity weight</param>
    /// <returns>Tuple of (r, g, b) as floats 0.0-1.0</returns>
    public static (float r, float g, float b) GetRarityColorRGB(int rarityWeight)
    {
        var tier = GetRarityTier(rarityWeight);
        return GetRarityColorRGB(tier);
    }

    /// <summary>
    /// Get the display name for a rarity tier.
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Display name (e.g., "Legendary")</returns>
    public static string GetRarityName(RarityTier tier)
    {
        return tier.ToString();
    }

    /// <summary>
    /// Get the display name for a given rarityWeight.
    /// Convenience method that calculates tier first.
    /// </summary>
    /// <param name="rarityWeight">The rarity weight</param>
    /// <returns>Display name</returns>
    public static string GetRarityName(int rarityWeight)
    {
        var tier = GetRarityTier(rarityWeight);
        return GetRarityName(tier);
    }

    /// <summary>
    /// Get the minimum rarityWeight for a given tier.
    /// Useful for filtering and validation.
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Minimum rarityWeight value for this tier</returns>
    public static int GetMinWeight(RarityTier tier)
    {
        return tier switch
        {
            RarityTier.Common => 50,
            RarityTier.Uncommon => 30,
            RarityTier.Rare => 15,
            RarityTier.Epic => 5,
            RarityTier.Legendary => 1,
            _ => 1
        };
    }

    /// <summary>
    /// Get the maximum rarityWeight for a given tier.
    /// Returns null for open-ended ranges (Common can go above 100).
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Maximum rarityWeight value for this tier, or null if unbounded</returns>
    public static int? GetMaxWeight(RarityTier tier)
    {
        return tier switch
        {
            RarityTier.Common => null,      // No upper limit
            RarityTier.Uncommon => 49,
            RarityTier.Rare => 29,
            RarityTier.Epic => 14,
            RarityTier.Legendary => 4,
            _ => null
        };
    }

    /// <summary>
    /// Get the weight range for a tier as a display string.
    /// Example: "50+" for Common, "5-14" for Epic
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Weight range string for tooltips/UI</returns>
    public static string GetWeightRangeDisplay(RarityTier tier)
    {
        var min = GetMinWeight(tier);
        var max = GetMaxWeight(tier);
        return max.HasValue ? $"{min}-{max.Value}" : $"{min}+";
    }

    /// <summary>
    /// Compare two rarity tiers to determine which is rarer.
    /// </summary>
    /// <param name="tier1">First tier</param>
    /// <param name="tier2">Second tier</param>
    /// <returns>-1 if tier1 is rarer, 0 if equal, 1 if tier2 is rarer</returns>
    public static int Compare(RarityTier tier1, RarityTier tier2)
    {
        // Legendary (highest enum value) is rarest
        return tier1.CompareTo(tier2);
    }

    /// <summary>
    /// Compare two rarityWeight values to determine which is rarer.
    /// </summary>
    /// <param name="weight1">First weight</param>
    /// <param name="weight2">Second weight</param>
    /// <returns>-1 if weight1 is rarer, 0 if equal tier, 1 if weight2 is rarer</returns>
    public static int Compare(int weight1, int weight2)
    {
        var tier1 = GetRarityTier(weight1);
        var tier2 = GetRarityTier(weight2);
        return Compare(tier1, tier2);
    }

    /// <summary>
    /// Check if a rarityWeight produces a specific tier.
    /// Useful for filtering in queries.
    /// </summary>
    /// <param name="rarityWeight">The weight to check</param>
    /// <param name="tier">The tier to compare against</param>
    /// <returns>True if weight maps to the specified tier</returns>
    public static bool IsTier(int rarityWeight, RarityTier tier)
    {
        return GetRarityTier(rarityWeight) == tier;
    }

    /// <summary>
    /// Check if a rarityWeight is at least a certain tier (or rarer).
    /// Example: IsAtLeast(20, Rare) returns true for Rare/Epic/Legendary.
    /// </summary>
    /// <param name="rarityWeight">The weight to check</param>
    /// <param name="minimumTier">The minimum acceptable tier</param>
    /// <returns>True if weight is at or above minimum tier</returns>
    public static bool IsAtLeast(int rarityWeight, RarityTier minimumTier)
    {
        var actualTier = GetRarityTier(rarityWeight);
        return actualTier >= minimumTier;
    }

    /// <summary>
    /// Get all rarity tiers in order from common to legendary.
    /// Useful for UI dropdowns, filters, and progression displays.
    /// </summary>
    /// <returns>Array of all rarity tiers</returns>
    public static RarityTier[] GetAllTiers()
    {
        return new[]
        {
            RarityTier.Common,
            RarityTier.Uncommon,
            RarityTier.Rare,
            RarityTier.Epic,
            RarityTier.Legendary
        };
    }

    /// <summary>
    /// Get a formatted string for displaying rarity in UI.
    /// Example: "[Legendary] Ancient Sword" in orange color.
    /// </summary>
    /// <param name="rarityWeight">The rarity weight</param>
    /// <param name="itemName">The item name to format</param>
    /// <returns>Rich text formatted string (BBCode for Godot RichTextLabel)</returns>
    public static string GetFormattedName(int rarityWeight, string itemName)
    {
        var tier = GetRarityTier(rarityWeight);
        var color = GetRarityColor(tier);
        return $"[color={color}][{tier}] {itemName}[/color]";
    }

    /// <summary>
    /// Calculate relative drop chance percentage within a pool.
    /// Example: Item with weight 30 in pool of total 150 = 20% chance.
    /// </summary>
    /// <param name="itemWeight">The rarityWeight of this item</param>
    /// <param name="totalPoolWeight">Sum of all rarityWeights in the pool</param>
    /// <returns>Drop chance as percentage (0-100)</returns>
    public static double GetDropChancePercent(int itemWeight, int totalPoolWeight)
    {
        if (totalPoolWeight <= 0) return 0.0;
        return (double)itemWeight / totalPoolWeight * 100.0;
    }

    /// <summary>
    /// Get a contextual description of how rare an item is.
    /// Useful for tooltips and item inspection.
    /// </summary>
    /// <param name="tier">The rarity tier</param>
    /// <returns>Flavor text describing rarity</returns>
    public static string GetRarityDescription(RarityTier tier)
    {
        return tier switch
        {
            RarityTier.Common => "Found everywhere. Basic quality.",
            RarityTier.Uncommon => "Somewhat rare. Decent quality.",
            RarityTier.Rare => "Hard to find. High quality.",
            RarityTier.Epic => "Extremely rare. Exceptional quality.",
            RarityTier.Legendary => "Nearly impossible to find. Mythical quality.",
            _ => "Unknown rarity."
        };
    }
}
