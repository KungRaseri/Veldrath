using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Utilities;

/// <summary>
/// Calculates rarity tier and associated display properties from rarityWeight values.
/// Eliminates the need for separate 'rarity' fields in game data.
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
}
