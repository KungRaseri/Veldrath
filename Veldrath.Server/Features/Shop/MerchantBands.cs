namespace Veldrath.Server.Features.Shop;

/// <summary>
/// One tier of the approved merchant band: the purse a town's merchants may hold at that town level.
/// </summary>
/// <param name="TownLevel">The town level this row starts at (<see cref="Data.Entities.Zone.MinLevel"/>).</param>
/// <param name="GoldMin">The smallest purse, in gold, a merchant of this tier may have.</param>
/// <param name="GoldMax">The largest purse, in gold, a merchant of this tier may have.</param>
public sealed record MerchantBand(int TownLevel, int GoldMin, int GoldMax);

/// <summary>
/// The approved merchant band, mirroring <c>game-context/budget.yaml</c> <c>merchants:</c>.
/// </summary>
/// <remarks>
/// <para>
/// These numbers are approved data, not tuning: <c>MerchantBandTests</c> parses the YAML and fails when this table
/// and it disagree, so the code cannot quietly drift from the band C2 approved.
/// </para>
/// <para>
/// Pricing is applied at the band's own endpoints rather than at an invented midpoint. The buy markup is the band's
/// floor (<c>1.0</c> — the price the shop already charges) and the buyback is the band's ceiling (<c>0.50</c> — the
/// rate the shop already pays). Both were live before this card; what is new is that they are read from one place and
/// that the ordering that makes a buy→sell loop a loss is asserted instead of assumed.
/// </para>
/// <para>
/// A town authored above the top tier uses the top tier, the same way the hostile bands stop at level 26: no band is
/// invented for it.
/// </para>
/// </remarks>
public static class MerchantBands
{
    /// <summary>The lowest number of distinct items a merchant town may author.</summary>
    public const int StockMin = 8;

    /// <summary>The highest number of distinct items a merchant town may author.</summary>
    public const int StockMax = 16;

    /// <summary>The multiplier a merchant charges over an item's printed value, as a percentage (band floor, 1.0).</summary>
    public const int PriceMarkupPercent = 100;

    /// <summary>The fraction of an item's printed value a merchant pays when buying it back, as a percentage (band ceiling, 0.50).</summary>
    public const int BuybackRatePercent = 50;

    private static readonly MerchantBand[] _bands =
    [
        new(TownLevel: 0, GoldMin: 100, GoldMax: 250),
        new(TownLevel: 5, GoldMin: 250, GoldMax: 650),
        new(TownLevel: 10, GoldMin: 650, GoldMax: 1600),
        new(TownLevel: 18, GoldMin: 1300, GoldMax: 3200),
    ];

    /// <summary>Gets the approved purse band per town level, lowest level first.</summary>
    public static IReadOnlyList<MerchantBand> GoldByTownLevel => _bands;

    /// <summary>Gets the band that applies to a town of the given level.</summary>
    /// <param name="townLevel">The town's own minimum level; a level below the first tier uses the first tier.</param>
    /// <returns>The band for the highest tier at or below <paramref name="townLevel"/>.</returns>
    public static MerchantBand ForTownLevel(int townLevel)
    {
        var band = _bands[0];
        foreach (var candidate in _bands)
        {
            if (candidate.TownLevel > townLevel)
                break;

            band = candidate;
        }

        return band;
    }

    /// <summary>Answers whether a purse sits inside the band for its town.</summary>
    /// <param name="townLevel">The town's own minimum level.</param>
    /// <param name="gold">The merchant's authored trade purse.</param>
    /// <returns>True when the purse is inside the approved range for that tier.</returns>
    public static bool Contains(int townLevel, int gold)
    {
        var band = ForTownLevel(townLevel);
        return gold >= band.GoldMin && gold <= band.GoldMax;
    }
}
