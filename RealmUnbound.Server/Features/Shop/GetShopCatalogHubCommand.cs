using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.ItemCatalog.Queries;

namespace RealmUnbound.Server.Features.Shop;

/// <summary>Represents a single item available for purchase in a zone shop.</summary>
/// <param name="ItemRef">The item slug used as an inventory reference.</param>
/// <param name="DisplayName">The human-readable item name.</param>
/// <param name="BuyPrice">Gold cost for the player to purchase the item.</param>
/// <param name="SellPrice">Gold the player receives when selling the item back.</param>
public record ShopItemDto(string ItemRef, string DisplayName, int BuyPrice, int SellPrice);

/// <summary>Hub command that returns the current item catalog for a zone's merchant shop.</summary>
/// <param name="ZoneId">The zone whose shop is being queried.</param>
public record GetShopCatalogHubCommand(string ZoneId) : IRequest<GetShopCatalogHubResult>;

/// <summary>Result returned by <see cref="GetShopCatalogHubCommandHandler"/>.</summary>
public record GetShopCatalogHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the list of items available in the shop.</summary>
    public IReadOnlyList<ShopItemDto> Items { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetShopCatalogHubCommand"/> by dispatching <see cref="GetItemCatalogQuery"/>
/// via MediatR and mapping each result to a <see cref="ShopItemDto"/> with DB-driven pricing.
/// Buy price equals the item's catalog price; sell price is 50% (floor 1 gold).
/// </summary>
public class GetShopCatalogHubCommandHandler
    : IRequestHandler<GetShopCatalogHubCommand, GetShopCatalogHubResult>
{
    private readonly ISender _mediator;
    private readonly ILogger<GetShopCatalogHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetShopCatalogHubCommandHandler"/>.</summary>
    /// <param name="mediator">MediatR sender used to dispatch the item catalog query.</param>
    /// <param name="logger">Logger instance.</param>
    public GetShopCatalogHubCommandHandler(ISender mediator, ILogger<GetShopCatalogHubCommandHandler> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    /// <summary>Handles the command and returns the shop catalog.</summary>
    /// <param name="request">The command containing the zone ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GetShopCatalogHubResult"/> with all purchasable items.</returns>
    public async Task<GetShopCatalogHubResult> Handle(
        GetShopCatalogHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return new GetShopCatalogHubResult { Success = false, ErrorMessage = "ZoneId is required." };

        var items = await _mediator.Send(new GetItemCatalogQuery(), cancellationToken);

        var catalog = items
            .Where(i => i.Price > 0)
            .Select(i => new ShopItemDto(
                ItemRef:     i.Slug,
                DisplayName: i.Name,
                BuyPrice:    i.Price,
                SellPrice:   Math.Max(1, i.Price / 2)))
            .ToList();

        _logger.LogInformation(
            "Shop catalog for zone {ZoneId} returned {Count} items.", request.ZoneId, catalog.Count);

        return new GetShopCatalogHubResult { Success = true, Items = catalog };
    }
}
