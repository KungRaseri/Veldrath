namespace Veldrath.Server.Features.Shop;

/// <summary>
/// Applies the approved merchant band to the two prices a shop quotes.
/// </summary>
/// <remarks>
/// <para>
/// A buy→sell loop must not print gold. The band keeps the buy markup above the buyback rate
/// (<see cref="MerchantBands.PriceMarkupPercent"/> &gt; <see cref="MerchantBands.BuybackRatePercent"/>), and
/// <see cref="SellPrice"/> is additionally clamped below <see cref="BuyPrice"/> so the guarantee holds for every price,
/// including the one-gold floor where integer division alone would make the two equal.
/// </para>
/// <para>
/// These are the only two pricing rules in the shop. There is no dynamic pricing, no per-merchant cut and no rounding
/// invented on top: the buy price is the item's printed value at the band's markup, the buyback is that value at the
/// band's rate.
/// </para>
/// </remarks>
public static class ShopPricing
{
    /// <summary>The gold a character pays to buy one unit of an item worth <paramref name="itemValue"/>.</summary>
    /// <param name="itemValue">The item's printed value in gold.</param>
    /// <returns>The buy price, rounded up so the markup can never fall under the band's floor.</returns>
    public static int BuyPrice(int itemValue)
    {
        if (itemValue <= 0)
            return 0;

        var scaled = (itemValue * MerchantBands.PriceMarkupPercent + 99) / 100;
        return Math.Max(1, scaled);
    }

    /// <summary>The gold a merchant pays for one unit of an item worth <paramref name="itemValue"/>.</summary>
    /// <param name="itemValue">The item's printed value in gold.</param>
    /// <returns>The buyback price, always at least one gold below the buy price.</returns>
    public static int SellPrice(int itemValue)
    {
        var scaled = Math.Max(1, itemValue * MerchantBands.BuybackRatePercent / 100);
        var buy = BuyPrice(itemValue);

        // A 1-gold item would otherwise buy and sell for the same coin. Paying zero for it is the honest answer:
        // the merchant is not a bank.
        return Math.Max(0, Math.Min(scaled, buy - 1));
    }
}
