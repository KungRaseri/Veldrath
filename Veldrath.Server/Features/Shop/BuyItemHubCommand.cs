using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;

namespace Veldrath.Server.Features.Shop;

/// <summary>Hub command that purchases one unit of an item from the merchant standing with the character.</summary>
/// <param name="CharacterId">The character making the purchase.</param>
/// <param name="ZoneId">The zone the client believes the character is trading in.</param>
/// <param name="ItemRef">The item slug to purchase.</param>
public record BuyItemHubCommand(Guid CharacterId, string ZoneId, string ItemRef)
    : IRequest<BuyItemHubResult>;

/// <summary>Result returned by <see cref="BuyItemHubCommandHandler"/>.</summary>
public record BuyItemHubResult
{
    /// <summary>Gets a value indicating whether the purchase succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the typed reason the purchase was refused, or <see cref="ShopRefusal.None"/> on success.</summary>
    public ShopRefusal Refusal { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the item slug that was purchased.</summary>
    public string ItemRef { get; init; } = string.Empty;

    /// <summary>Gets the gold spent on the purchase.</summary>
    public int GoldSpent { get; init; }

    /// <summary>Gets the character's remaining gold after the purchase.</summary>
    public int RemainingGold { get; init; }
}

/// <summary>
/// Handles <see cref="BuyItemHubCommand"/> by resolving the merchant standing with the character, checking the zone
/// actually stocks the item, then deducting the band's buy price from the attributes blob and appending one unit of
/// the item to the character's inventory blob.
/// </summary>
/// <remarks>
/// The merchant check comes before the item and the purse: a purchase in the wilderness is refused for the honest
/// reason ("there is no one to trade with here"), not for a price the player cannot pay.
/// </remarks>
public class BuyItemHubCommandHandler : IRequestHandler<BuyItemHubCommand, BuyItemHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICharacterRepository _characterRepo;
    private readonly ISender _mediator;
    private readonly IShopMerchantResolver _merchants;
    private readonly IZoneStockReader _stock;
    private readonly ILogger<BuyItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="BuyItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="mediator">MediatR sender for the item catalog query.</param>
    /// <param name="merchants">Resolver for the merchant standing with the character.</param>
    /// <param name="stock">Reader for the items this zone's merchants offer.</param>
    /// <param name="logger">Logger instance.</param>
    public BuyItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ISender mediator,
        IShopMerchantResolver merchants,
        IZoneStockReader stock,
        ILogger<BuyItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _mediator = mediator;
        _merchants = merchants;
        _stock = stock;
        _logger = logger;
    }

    /// <summary>Handles the purchase and returns the outcome.</summary>
    /// <param name="request">The buy command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BuyItemHubResult"/> describing the outcome.</returns>
    public async Task<BuyItemHubResult> Handle(BuyItemHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemRef))
            return Fail(ShopRefusal.InvalidRequest, "ItemRef is required.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail(ShopRefusal.CharacterNotFound, $"Character {request.CharacterId} not found.");

        var merchant = await _merchants.ResolveAsync(character, request.ZoneId, cancellationToken);
        if (!merchant.Success)
            return Fail(merchant.Refusal, RefusalMessage(merchant.Refusal));

        // Look up the item in the catalog to get its DB-driven value.
        var catalog = await _mediator.Send(new GetItemCatalogQuery(), cancellationToken);
        var item = catalog.FirstOrDefault(i =>
            string.Equals(i.Slug, request.ItemRef, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return Fail(ShopRefusal.ItemNotFound, $"Item '{request.ItemRef}' not found in catalog.");

        var offered = await _stock.GetItemSlugsAsync(character.CurrentZoneId, cancellationToken);
        if (!offered.Any(slug => string.Equals(slug, item.Slug, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogDebug(
                "Character {CharacterId} asked for '{ItemRef}', which {ZoneId} does not stock",
                request.CharacterId, request.ItemRef, character.CurrentZoneId);

            return Fail(ShopRefusal.ItemNotInStock, $"The merchant here does not stock '{request.ItemRef}'.");
        }

        var price = ShopPricing.BuyPrice(item.Price);

        // Parse attributes blob.
        var attrs = ParseAttrs(character.Attributes);
        var gold = attrs.TryGetValue("Gold", out var g) ? g : 0;

        if (gold < price)
            return Fail(ShopRefusal.InsufficientGold, $"Not enough gold. Have {gold}, need {price}.");

        // Deduct gold.
        attrs["Gold"] = gold - price;
        character.Attributes = JsonSerializer.Serialize(attrs);

        // Add item to inventory blob (stack if ItemRef already exists and has no durability).
        var inventory = ParseInventory(character.InventoryBlob);
        var existing = inventory.FirstOrDefault(i =>
            string.Equals(i.ItemRef, request.ItemRef, StringComparison.OrdinalIgnoreCase)
            && i.Durability is null);
        if (existing is not null)
        {
            var idx = inventory.IndexOf(existing);
            inventory[idx] = existing with { Quantity = existing.Quantity + 1 };
        }
        else
        {
            inventory.Add(new InventoryItemDto(request.ItemRef, 1, null));
        }
        character.InventoryBlob = JsonSerializer.Serialize(inventory);

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} bought '{ItemRef}' from {Merchant} for {Price} gold; remaining {Gold}",
            request.CharacterId, request.ItemRef, merchant.Merchant!.ArchetypeSlug, price, attrs["Gold"]);

        return new BuyItemHubResult
        {
            Success = true,
            ItemRef = request.ItemRef,
            GoldSpent = price,
            RemainingGold = attrs["Gold"],
        };
    }

    private static string RefusalMessage(ShopRefusal refusal) => refusal switch
    {
        ShopRefusal.ZoneMismatch => "You are not standing in that zone.",
        ShopRefusal.NoMerchant => "There is no one to trade with here.",
        _ => "The trade could not be completed.",
    };

    private static Dictionary<string, int> ParseAttrs(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "{}") return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(blob, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static List<InventoryItemDto> ParseInventory(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "[]") return [];
        try { return JsonSerializer.Deserialize<List<InventoryItemDto>>(blob, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static BuyItemHubResult Fail(ShopRefusal refusal, string message) =>
        new() { Success = false, Refusal = refusal, ErrorMessage = message };
}
