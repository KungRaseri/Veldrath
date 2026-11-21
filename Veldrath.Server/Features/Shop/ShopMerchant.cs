using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Shop;

/// <summary>
/// A merchant who is actually standing where the character is: the person, not the zone flag.
/// </summary>
/// <remarks>
/// The purse is the archetype's authored <c>TradeGold</c>, which the approved band
/// (<c>game-context/budget.yaml</c> <c>merchants.gold_by_town_level</c>) bounds by the town's own level. It is a cap
/// on one buyback, not a balance that is spent and refilled: nothing in this card decrements it, the same way nothing
/// decrements stock.
/// </remarks>
/// <param name="ZoneId">The zone the person stands in.</param>
/// <param name="LocationSlug">The zone location the person stands at.</param>
/// <param name="ArchetypeSlug">The actor-archetype slug the placement presents.</param>
/// <param name="DisplayName">The name to show the player, taken from the placement override when one is authored.</param>
/// <param name="Gold">The merchant's trade purse, in gold.</param>
public sealed record ShopMerchant(
    string ZoneId,
    string LocationSlug,
    string ArchetypeSlug,
    string DisplayName,
    int Gold);

/// <summary>
/// The outcome of looking for a merchant: either one was found, or the reason none was.
/// </summary>
/// <param name="Merchant">The merchant, or <see langword="null"/> when the character has nobody to trade with.</param>
/// <param name="Refusal">Why there is no merchant, or <see cref="ShopRefusal.None"/> on success.</param>
public sealed record ShopMerchantResolution(ShopMerchant? Merchant, ShopRefusal Refusal)
{
    /// <summary>Gets a value indicating whether a merchant was found.</summary>
    public bool Success => Merchant is not null;

    /// <summary>Resolves to a merchant.</summary>
    /// <param name="merchant">The merchant standing with the character.</param>
    /// <returns>A successful resolution.</returns>
    public static ShopMerchantResolution Found(ShopMerchant merchant) => new(merchant, ShopRefusal.None);

    /// <summary>Resolves to a refusal.</summary>
    /// <param name="refusal">Why there is nobody to trade with.</param>
    /// <returns>A refused resolution.</returns>
    public static ShopMerchantResolution Refused(ShopRefusal refusal) => new(null, refusal);
}

/// <summary>
/// Finds the merchant standing with a character.
/// </summary>
/// <remarks>
/// A shop is a person. The zone flag says a town <em>has</em> a merchant; this asks whether one is placed at the
/// character's current location and offers trade. A player in the wilderness, or in a town square nobody trades in,
/// gets a refusal rather than a shop.
/// </remarks>
public interface IShopMerchantResolver
{
    /// <summary>Resolves the merchant the character is standing with.</summary>
    /// <param name="character">The character whose current zone and location are authoritative.</param>
    /// <param name="requestedZoneId">The zone the client asked to trade in, or an empty string when it did not say.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The merchant, or the reason there is none.</returns>
    Task<ShopMerchantResolution> ResolveAsync(
        Character character,
        string requestedZoneId,
        CancellationToken cancellationToken);
}
