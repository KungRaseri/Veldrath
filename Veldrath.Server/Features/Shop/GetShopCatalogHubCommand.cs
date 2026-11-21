using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Shop;

/// <summary>Represents a single item available for purchase in a zone shop.</summary>
/// <param name="ItemRef">The item slug used as an inventory reference.</param>
/// <param name="DisplayName">The human-readable item name.</param>
/// <param name="BuyPrice">Gold cost for the player to purchase the item, at the band's markup.</param>
/// <param name="SellPrice">Gold the player receives when selling the item back, at the band's buyback rate.</param>
public record ShopItemDto(string ItemRef, string DisplayName, int BuyPrice, int SellPrice);

/// <summary>Hub command that returns what the merchant standing with a character offers in a zone.</summary>
/// <param name="CharacterId">The character looking at the shop.</param>
/// <param name="ZoneId">The zone the client believes the character is trading in.</param>
public record GetShopCatalogHubCommand(Guid CharacterId, string ZoneId) : IRequest<GetShopCatalogHubResult>;

/// <summary>Result returned by <see cref="GetShopCatalogHubCommandHandler"/>.</summary>
public record GetShopCatalogHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the typed reason the catalog was refused, or <see cref="ShopRefusal.None"/> on success.</summary>
    public ShopRefusal Refusal { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the list of items available in the shop.</summary>
    public IReadOnlyList<ShopItemDto> Items { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetShopCatalogHubCommand"/> by resolving the merchant standing with the character, reading the
/// zone's authored stock, and mapping those catalog items to <see cref="ShopItemDto"/> rows at the approved band's
/// prices.
/// </summary>
/// <remarks>
/// The catalog is the zone's stock list, not the whole item catalog. That is the difference between a shop and a
/// vending machine: a town offers between eight and sixteen of the twenty-four purchasable items, and two towns do not
/// offer the same shelf.
/// </remarks>
public class GetShopCatalogHubCommandHandler
    : IRequestHandler<GetShopCatalogHubCommand, GetShopCatalogHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ISender _mediator;
    private readonly IShopMerchantResolver _merchants;
    private readonly IZoneStockReader _stock;
    private readonly ILogger<GetShopCatalogHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetShopCatalogHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load the character whose position authorises the shop.</param>
    /// <param name="mediator">MediatR sender used to dispatch the item catalog query.</param>
    /// <param name="merchants">Resolver for the merchant standing with the character.</param>
    /// <param name="stock">Reader for the items this zone's merchants offer.</param>
    /// <param name="logger">Logger instance.</param>
    public GetShopCatalogHubCommandHandler(
        ICharacterRepository characterRepo,
        ISender mediator,
        IShopMerchantResolver merchants,
        IZoneStockReader stock,
        ILogger<GetShopCatalogHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _mediator = mediator;
        _merchants = merchants;
        _stock = stock;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the shop catalog.</summary>
    /// <param name="request">The command containing the character and zone IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GetShopCatalogHubResult"/> with the items the zone stocks.</returns>
    public async Task<GetShopCatalogHubResult> Handle(
        GetShopCatalogHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return new GetShopCatalogHubResult
            {
                Success = false,
                Refusal = ShopRefusal.InvalidRequest,
                ErrorMessage = "ZoneId is required.",
            };

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new GetShopCatalogHubResult
            {
                Success = false,
                Refusal = ShopRefusal.CharacterNotFound,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var merchant = await _merchants.ResolveAsync(character, request.ZoneId, cancellationToken);
        if (!merchant.Success)
        {
            return new GetShopCatalogHubResult
            {
                Success = false,
                Refusal = merchant.Refusal,
                ErrorMessage = merchant.Refusal switch
                {
                    ShopRefusal.ZoneMismatch => "You are not standing in that zone.",
                    _ => "There is no one to trade with here.",
                },
            };
        }

        var offered = await _stock.GetItemSlugsAsync(character.CurrentZoneId, cancellationToken);
        var offeredSet = offered.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = await _mediator.Send(new GetItemCatalogQuery(), cancellationToken);

        var catalog = items
            .Where(item => item.Price > 0 && offeredSet.Contains(item.Slug))
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new ShopItemDto(
                ItemRef: item.Slug,
                DisplayName: item.Name,
                BuyPrice: ShopPricing.BuyPrice(item.Price),
                SellPrice: ShopPricing.SellPrice(item.Price)))
            .ToList();

        _logger.LogInformation(
            "Shop catalog for zone {ZoneId} (merchant {Merchant}) returned {Count} of {Stock} stocked items.",
            character.CurrentZoneId, merchant.Merchant!.ArchetypeSlug, catalog.Count, offered.Count);

        return new GetShopCatalogHubResult { Success = true, Items = catalog };
    }
}
