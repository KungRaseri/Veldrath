namespace RealmEngine.Shared.Models;

/// <summary>
/// Configuration and validation rules for the point-buy character attribute allocation system.
/// Uses a D&amp;D 5e-style cost table: 1 point per rank up to 13, 2 points per rank for 14–15.
/// </summary>
public class PointBuyConfig
{
    /// <summary>Gets the total number of points available to spend across all stats.</summary>
    public int TotalPoints { get; init; } = 27;

    /// <summary>Gets the minimum allowed value for any single stat before bonuses are applied.</summary>
    public int MinStatValue { get; init; } = 8;

    /// <summary>Gets the maximum allowed value for any single stat before bonuses are applied.</summary>
    public int MaxStatValue { get; init; } = 15;

    private static readonly IReadOnlyDictionary<int, int> CostTable = new Dictionary<int, int>
    {
        [8]  = 0,
        [9]  = 1,
        [10] = 2,
        [11] = 3,
        [12] = 4,
        [13] = 5,
        [14] = 7,
        [15] = 9,
    };

    /// <summary>
    /// Returns the total point cost to purchase a stat at <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The target stat value.</param>
    /// <returns>The cumulative point cost for that value, or -1 if the value is out of range.</returns>
    public int GetCost(int value)
    {
        return CostTable.TryGetValue(value, out var cost) ? cost : -1;
    }

    /// <summary>
    /// Calculates the total point cost of a full set of stat allocations.
    /// </summary>
    /// <param name="allocations">Dictionary mapping stat name to allocated value.</param>
    /// <returns>The sum of costs for all allocated stats.</returns>
    public int CalculateTotalCost(Dictionary<string, int> allocations)
    {
        int total = 0;
        foreach (var (_, value) in allocations)
        {
            int cost = GetCost(value);
            if (cost < 0) return int.MaxValue; // out-of-range value makes total invalid
            total += cost;
        }
        return total;
    }

    /// <summary>
    /// Returns <see langword="true"/> if each allocated value is within <see cref="MinStatValue"/>–<see cref="MaxStatValue"/>
    /// and the total cost does not exceed <see cref="TotalPoints"/>.
    /// </summary>
    /// <param name="allocations">Dictionary mapping stat name to allocated value.</param>
    /// <returns><see langword="true"/> if the allocation is valid; otherwise <see langword="false"/>.</returns>
    public bool IsValid(Dictionary<string, int> allocations)
    {
        foreach (var (_, value) in allocations)
        {
            if (value < MinStatValue || value > MaxStatValue) return false;
        }
        return CalculateTotalCost(allocations) <= TotalPoints;
    }
}
