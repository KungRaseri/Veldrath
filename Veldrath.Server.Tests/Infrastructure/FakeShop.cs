using Veldrath.Server.Features.Shop;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// A merchant resolver that answers from an authored field instead of the database.
/// </summary>
/// <remarks>
/// The real resolver reads two contexts and three tables, so handler tests should not re-implement it: they need to
/// say "there is a merchant with this purse" or "there is nobody here" and assert what the handler did about it. The
/// resolver's own behaviour is covered against real rows in <c>ShopMerchantResolverTests</c>.
/// </remarks>
public sealed class FakeShopMerchantResolver : IShopMerchantResolver
{
    /// <summary>Gets or sets the merchant every resolution returns while <see cref="HasMerchant"/> is true.</summary>
    public ShopMerchant Merchant { get; set; } = DefaultMerchant;

    /// <summary>Gets or sets a value indicating whether a merchant is standing with the character.</summary>
    public bool HasMerchant { get; set; } = true;

    /// <summary>Gets or sets the refusal a resolution carries while <see cref="HasMerchant"/> is false.</summary>
    public ShopRefusal Refusal { get; set; } = ShopRefusal.NoMerchant;

    /// <summary>Gets the character the resolver was last asked about, for assertions.</summary>
    public Character? LastCharacter { get; private set; }

    /// <summary>Gets the zone the handler asked about, for assertions.</summary>
    public string? LastRequestedZoneId { get; private set; }

    /// <summary>A crestfall blacksmith whose purse sits at the top of the L0 band.</summary>
    public static ShopMerchant DefaultMerchant { get; } =
        new("crestfall", "aurelian-market", "village-blacksmith", "Vaenern", 250);

    /// <summary>Creates a resolver that finds nobody, for the refusal paths.</summary>
    /// <param name="refusal">Why nobody was found.</param>
    /// <returns>A resolver with no merchant.</returns>
    public static FakeShopMerchantResolver Nobody(ShopRefusal refusal = ShopRefusal.NoMerchant) =>
        new() { HasMerchant = false, Refusal = refusal };

    /// <inheritdoc />
    public Task<ShopMerchantResolution> ResolveAsync(
        Character character,
        string requestedZoneId,
        CancellationToken cancellationToken)
    {
        LastCharacter = character;
        LastRequestedZoneId = requestedZoneId;

        return Task.FromResult(HasMerchant
            ? ShopMerchantResolution.Found(Merchant)
            : ShopMerchantResolution.Refused(Refusal));
    }
}

/// <summary>
/// A stock reader that answers from slug lists added by the test.
/// </summary>
/// <remarks>
/// Nothing is offered until a test says so: a fake that offered "everything" by default would hide a handler that
/// forgot to consult the stock list at all.
/// </remarks>
public sealed class FakeZoneStockReader : IZoneStockReader
{
    private readonly List<string> _slugs = [];

    /// <summary>Gets the zone ids the handler asked about, in the order it asked.</summary>
    public List<string> RequestedZones { get; } = [];

    /// <summary>Declares the items this fake zone stocks.</summary>
    /// <param name="slugs">Item slugs to offer.</param>
    /// <returns>The same reader, so construction can chain.</returns>
    public FakeZoneStockReader Offering(params string[] slugs)
    {
        _slugs.AddRange(slugs);
        return this;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetItemSlugsAsync(string zoneId, CancellationToken cancellationToken)
    {
        RequestedZones.Add(zoneId);
        return Task.FromResult<IReadOnlyList<string>>(_slugs.ToArray());
    }
}
