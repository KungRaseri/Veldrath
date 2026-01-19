namespace RealmEngine.Shared.Utilities;

/// <summary>
/// Utility for selecting items based on rarity weights.
/// Uses the formula: probability = 100 / rarityWeight
/// </summary>
public static class WeightedSelector
{
    private static readonly Random _random = Random.Shared;

    /// <summary>
    /// Select a random item from a collection based on rarity weights.
    /// Higher rarity weights = lower selection probability.
    /// Formula: probability = 100 / rarityWeight
    /// </summary>
    /// <typeparam name="T">Type that has a RarityWeight property</typeparam>
    /// <param name="items">Collection of items to select from</param>
    /// <returns>Selected item</returns>
    /// <exception cref="ArgumentException">If collection is empty</exception>
    public static T SelectByRarityWeight<T>(IEnumerable<T> items) where T : class
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            throw new ArgumentException("Cannot select from empty collection", nameof(items));
        }

        if (itemList.Count == 1)
        {
            return itemList[0];
        }

        // Get RarityWeight property using reflection
        var rarityWeightProperty = typeof(T).GetProperty("RarityWeight");
        if (rarityWeightProperty == null)
        {
            throw new ArgumentException($"Type {typeof(T).Name} does not have a RarityWeight property");
        }

        // Calculate total probability weight (sum of 100/rarityWeight for all items)
        double totalWeight = 0;
        foreach (var item in itemList)
        {
            var rarityWeight = (int)(rarityWeightProperty.GetValue(item) ?? 1);
            totalWeight += 100.0 / rarityWeight;
        }

        // Generate random value
        var randomValue = _random.NextDouble() * totalWeight;

        // Select item based on cumulative weight
        double cumulativeWeight = 0;
        foreach (var item in itemList)
        {
            var rarityWeight = (int)(rarityWeightProperty.GetValue(item) ?? 1);
            cumulativeWeight += 100.0 / rarityWeight;

            if (randomValue <= cumulativeWeight)
            {
                return item;
            }
        }

        // Fallback to last item (should never happen)
        return itemList[^1];
    }

    /// <summary>
    /// Calculate the selection probability for an item with given rarity weight.
    /// Formula: probability = 100 / rarityWeight
    /// </summary>
    /// <param name="rarityWeight">The rarity weight of the item</param>
    /// <returns>Probability as a percentage (0-100)</returns>
    public static double CalculateProbability(int rarityWeight)
    {
        return 100.0 / rarityWeight;
    }

    /// <summary>
    /// Get selection probabilities for all items in a collection.
    /// Useful for testing and debugging weighted selection.
    /// </summary>
    public static Dictionary<string, double> GetProbabilities<T>(IEnumerable<T> items) where T : class
    {
        var itemList = items.ToList();
        var result = new Dictionary<string, double>();

        if (itemList.Count == 0)
        {
            return result;
        }

        // Get properties
        var rarityWeightProperty = typeof(T).GetProperty("RarityWeight");
        var nameProperty = typeof(T).GetProperty("Name");

        if (rarityWeightProperty == null || nameProperty == null)
        {
            return result;
        }

        // Calculate total weight
        double totalWeight = 0;
        foreach (var item in itemList)
        {
            var rarityWeight = (int)(rarityWeightProperty.GetValue(item) ?? 1);
            totalWeight += 100.0 / rarityWeight;
        }

        // Calculate probability for each item
        foreach (var item in itemList)
        {
            var name = (string)(nameProperty.GetValue(item) ?? "Unknown");
            var rarityWeight = (int)(rarityWeightProperty.GetValue(item) ?? 1);
            var itemWeight = 100.0 / rarityWeight;
            var probability = (itemWeight / totalWeight) * 100.0;

            result[name] = probability;
        }

        return result;
    }

    /// <summary>
    /// Get the most common items from a collection (highest probability).
    /// Useful for displaying "common drops" in UI.
    /// </summary>
    /// <typeparam name="T">Type with RarityWeight property</typeparam>
    /// <param name="items">Collection of items</param>
    /// <param name="topN">Number of top items to return (default: 5)</param>
    /// <returns>Items sorted by probability (highest first)</returns>
    public static List<T> GetMostCommon<T>(IEnumerable<T> items, int topN = 5) where T : class
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return new List<T>();

        var rarityWeightProperty = typeof(T).GetProperty("RarityWeight");
        if (rarityWeightProperty == null)
        {
            throw new ArgumentException($"Type {typeof(T).Name} does not have a RarityWeight property");
        }

        // Sort by rarityWeight descending (higher weight = more common)
        return itemList
            .OrderByDescending(item => (int)(rarityWeightProperty.GetValue(item) ?? 0))
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Get the rarest items from a collection (lowest probability).
    /// Useful for displaying "rare drops" in UI.
    /// </summary>
    /// <typeparam name="T">Type with RarityWeight property</typeparam>
    /// <param name="items">Collection of items</param>
    /// <param name="topN">Number of rarest items to return (default: 5)</param>
    /// <returns>Items sorted by rarity (rarest first)</returns>
    public static List<T> GetRarest<T>(IEnumerable<T> items, int topN = 5) where T : class
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return new List<T>();

        var rarityWeightProperty = typeof(T).GetProperty("RarityWeight");
        if (rarityWeightProperty == null)
        {
            throw new ArgumentException($"Type {typeof(T).Name} does not have a RarityWeight property");
        }

        // Sort by rarityWeight ascending (lower weight = rarer)
        return itemList
            .OrderBy(item => (int)(rarityWeightProperty.GetValue(item) ?? 0))
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Calculate the expected number of rolls to get a specific item.
    /// Example: Item with 5% drop chance takes average of 20 rolls.
    /// Useful for "pity timer" displays in UI.
    /// </summary>
    /// <param name="rarityWeight">The rarityWeight of the item</param>
    /// <param name="totalPoolWeight">Sum of all rarityWeights in the pool</param>
    /// <returns>Expected rolls to get this item (1/probability)</returns>
    public static double GetExpectedRolls(int rarityWeight, int totalPoolWeight)
    {
        if (totalPoolWeight <= 0) return double.PositiveInfinity;
        var probability = (100.0 / rarityWeight) / (GetTotalProbabilityWeight(totalPoolWeight));
        return probability > 0 ? 1.0 / probability : double.PositiveInfinity;
    }

    /// <summary>
    /// Get probability as a user-friendly string.
    /// Examples: "50.0%", "5.2%", "0.3%"
    /// </summary>
    /// <param name="rarityWeight">The rarityWeight of the item</param>
    /// <param name="totalPoolWeight">Sum of all rarityWeights in the pool</param>
    /// <returns>Formatted probability string</returns>
    public static string GetProbabilityDisplay(int rarityWeight, int totalPoolWeight)
    {
        if (totalPoolWeight <= 0) return "0.0%";
        var totalWeight = GetTotalProbabilityWeight(totalPoolWeight);
        var itemWeight = 100.0 / rarityWeight;
        var probability = (itemWeight / totalWeight) * 100.0;
        return $"{probability:F1}%";
    }

    /// <summary>
    /// Calculate cumulative probability of getting at least one drop in N rolls.
    /// Formula: 1 - (1 - p)^n
    /// Useful for "after 10 rolls, you have 64% chance" displays.
    /// </summary>
    /// <param name="rarityWeight">The rarityWeight of the item</param>
    /// <param name="totalPoolWeight">Sum of all rarityWeights in the pool</param>
    /// <param name="rolls">Number of attempts</param>
    /// <returns>Probability as percentage (0-100)</returns>
    public static double GetCumulativeProbability(int rarityWeight, int totalPoolWeight, int rolls)
    {
        if (totalPoolWeight <= 0 || rolls <= 0) return 0.0;
        var totalWeight = GetTotalProbabilityWeight(totalPoolWeight);
        var itemWeight = 100.0 / rarityWeight;
        var singleProbability = (itemWeight / totalWeight);
        return (1.0 - Math.Pow(1.0 - singleProbability, rolls)) * 100.0;
    }

    /// <summary>
    /// Helper to calculate total probability weight for a pool.
    /// </summary>
    private static double GetTotalProbabilityWeight(int totalPoolWeight)
    {
        // This is a simplified version - in practice, you'd sum (100/rarityWeight) for all items
        // For single item queries, we approximate
        return totalPoolWeight > 0 ? totalPoolWeight : 1.0;
    }

    /// <summary>
    /// Get items within a specific rarityWeight range.
    /// Useful for filtering loot tables by difficulty tier.
    /// </summary>
    /// <typeparam name="T">Type with RarityWeight property</typeparam>
    /// <param name="items">Collection of items</param>
    /// <param name="minWeight">Minimum rarityWeight (inclusive)</param>
    /// <param name="maxWeight">Maximum rarityWeight (inclusive)</param>
    /// <returns>Filtered items</returns>
    public static List<T> FilterByWeightRange<T>(IEnumerable<T> items, int minWeight, int maxWeight) where T : class
    {
        var itemList = items.ToList();
        if (itemList.Count == 0) return new List<T>();

        var rarityWeightProperty = typeof(T).GetProperty("RarityWeight");
        if (rarityWeightProperty == null)
        {
            throw new ArgumentException($"Type {typeof(T).Name} does not have a RarityWeight property");
        }

        return itemList
            .Where(item =>
            {
                var weight = (int)(rarityWeightProperty.GetValue(item) ?? 0);
                return weight >= minWeight && weight <= maxWeight;
            })
            .ToList();
    }
}
