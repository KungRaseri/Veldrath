namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>One participant's cut of a defeated enemy's rewards.</summary>
/// <param name="CharacterId">Character that contributed to the kill.</param>
/// <param name="Xp">Experience awarded to that character.</param>
/// <param name="Gold">Gold awarded to that character.</param>
public sealed record ContributionShare(Guid CharacterId, int Xp, int Gold);

/// <summary>
/// Splits a defeated enemy's rewards across its damage contributors. The parts always add up to the whole,
/// so a group can never mint or lose experience and gold through rounding.
/// </summary>
public static class CombatRewards
{
    /// <summary>
    /// Splits <paramref name="total"/> across <paramref name="weights"/> in proportion to each weight, using
    /// largest-remainder rounding so the returned parts sum to exactly <paramref name="total"/>.
    /// </summary>
    /// <param name="total">Amount to distribute. Zero or negative yields all zeros.</param>
    /// <param name="weights">Non-negative weights, in output order.</param>
    /// <returns>
    /// One share per weight. The shares sum to <paramref name="total"/> whenever at least one weight is positive,
    /// and every weight of zero receives nothing.
    /// </returns>
    public static int[] SplitByWeight(int total, IReadOnlyList<int> weights)
    {
        var shares = new int[weights.Count];
        if (total <= 0 || weights.Count == 0) return shares;

        long weightSum = 0;
        foreach (var weight in weights) weightSum += Math.Max(0, weight);
        if (weightSum <= 0) return shares;

        var remainders = new (int Index, long Remainder, int Weight)[weights.Count];
        long assigned = 0;

        for (var index = 0; index < weights.Count; index++)
        {
            var weight = Math.Max(0, weights[index]);
            long exact = (long)total * weight;
            shares[index] = (int)(exact / weightSum);
            assigned += shares[index];
            remainders[index] = (index, exact % weightSum, weight);
        }

        // Hand the leftover units to the largest fractional parts. Ties go to the earlier contributor so the
        // split is deterministic; a zero contributor is never rounded up into a payout.
        var leftover = total - (int)assigned;
        if (leftover > 0)
        {
            foreach (var (index, _, _) in remainders
                .Where(remainder => remainder.Weight > 0)
                .OrderByDescending(remainder => remainder.Remainder)
                .ThenBy(remainder => remainder.Index)
                .Take(leftover))
            {
                shares[index] += 1;
            }
        }

        return shares;
    }

    /// <summary>Splits both reward pools of one enemy across its contributors.</summary>
    /// <param name="totalXp">Experience the enemy is worth before splitting.</param>
    /// <param name="totalGold">Gold the enemy is worth before splitting.</param>
    /// <param name="contributions">Cumulative damage per character, in broadcast order.</param>
    /// <returns>One share per contribution, in the same order.</returns>
    public static IReadOnlyList<ContributionShare> Split(
        int totalXp,
        int totalGold,
        IReadOnlyList<(Guid CharacterId, int Damage)> contributions)
    {
        var weights = new int[contributions.Count];
        for (var index = 0; index < contributions.Count; index++)
            weights[index] = Math.Max(0, contributions[index].Damage);

        var xp = SplitByWeight(totalXp, weights);
        var gold = SplitByWeight(totalGold, weights);

        var shares = new ContributionShare[contributions.Count];
        for (var index = 0; index < contributions.Count; index++)
            shares[index] = new ContributionShare(contributions[index].CharacterId, xp[index], gold[index]);

        return shares;
    }
}
