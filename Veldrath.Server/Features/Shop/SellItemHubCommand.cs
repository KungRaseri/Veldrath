using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;

namespace Veldrath.Server.Features.Shop;

/// <summary>Hub command that sells one unit of an item from a character's inventory to the merchant standing with them.</summary>
/// <param name="CharacterId">The character selling the item.</param>
/// <param name="ZoneId">The zone the client believes the character is trading in.</param>
/// <param name="ItemRef">The item slug to sell.</param>
public record SellItemHubCommand(Guid CharacterId, string ZoneId, string ItemRef)
    : IRequest<SellItemHubResult>;

/// <summary>Result returned by <see cref="SellItemHubCommandHandler"/>.</summary>
public record SellItemHubResult
{
    /// <summary>Gets a value indicating whether the sale succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the typed reason the sale was refused, or <see cref="ShopRefusal.None"/> on success.</summary>
    public ShopRefusal Refusal { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the item slug that was sold.</summary>
    public string ItemRef { get; init; } = string.Empty;

    /// <summary>Gets the gold received for the sale.</summary>
    public int GoldReceived { get; init; }

    /// <summary>Gets the character's total gold after the sale.</summary>
    public int NewGoldTotal { get; init; }
}

/// <summary>
/// Handles <see cref="SellItemHubCommand"/> by resolving the merchant standing with the character, checking the
/// merchant's purse can cover the buyback, then removing one unit of the item from the character's inventory blob and
/// crediting the band's buyback price.
/// </summary>
/// <remarks>
/// The purse is a single-payout cap read from the merchant's authored <c>TradeGold</c>, not a balance that is spent and
/// refilled. Before this card a sale credited the player without ever asking whether a merchant existed or could afford
/// it, so liquidity was infinite in every zone.
/// </remarks>
public class SellItemHubCommandHandler : IRequestHandler<SellItemHubCommand, SellItemHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICharacterRepository _characterRepo;
    private readonly ISender _mediator;
    private readonly IShopMerchantResolver _merchants;
    private readonly ILogger<SellItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="SellItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="mediator">MediatR sender for the item catalog query.</param>
    /// <param name="merchants">Resolver for the merchant standing with the character.</param>
    /// <param name="logger">Logger instance.</param>
    public SellItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ISender mediator,
        IShopMerchantResolver merchants,
        ILogger<SellItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _mediator = mediator;
        _merchants = merchants;
        _logger = logger;
    }

    /// <summary>Handles the sale and returns the outcome.</summary>
    /// <param name="request">The sell command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SellItemHubResult"/> describing the outcome.</returns>
    public async Task<SellItemHubResult> Handle(SellItemHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemRef))
            return Fail(ShopRefusal.InvalidRequest, "ItemRef is required.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail(ShopRefusal.CharacterNotFound, $"Character {request.CharacterId} not found.");

        var merchant = await _merchants.ResolveAsync(character, request.ZoneId, cancellationToken);
        if (!merchant.Success)
            return Fail(merchant.Refusal, RefusalMessage(merchant.Refusal));

        // Remove one unit from inventory.
        var inventory = ParseInventory(character.InventoryBlob);
        var existing = inventory.FirstOrDefault(i =>
            string.Equals(i.ItemRef, request.ItemRef, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return Fail(ShopRefusal.ItemNotHeld, $"Item '{request.ItemRef}' not found in inventory.");

        // Determine sell price from the catalog at the band's buyback rate.
        var catalog = await _mediator.Send(new GetItemCatalogQuery(), cancellationToken);
        var item = catalog.FirstOrDefault(i =>
            string.Equals(i.Slug, request.ItemRef, StringComparison.OrdinalIgnoreCase));
        var sellPrice = item is not null ? ShopPricing.SellPrice(item.Price) : 1;

        if (sellPrice > merchant.Merchant!.Gold)
        {
            _logger.LogDebug(
                "Merchant {Merchant} cannot afford {Price} gold for '{ItemRef}' (purse {Gold})",
                merchant.Merchant.ArchetypeSlug, sellPrice, request.ItemRef, merchant.Merchant.Gold);

            return Fail(
                ShopRefusal.MerchantCannotAfford,
                $"The merchant cannot afford that. They have {merchant.Merchant.Gold} gold.");
        }

        if (existing.Quantity > 1)
        {
            var idx = inventory.IndexOf(existing);
            inventory[idx] = existing with { Quantity = existing.Quantity - 1 };
        }
        else
        {
            inventory.Remove(existing);
        }
        character.InventoryBlob = JsonSerializer.Serialize(inventory);

        // Credit gold.
        var attrs = ParseAttrs(character.Attributes);
        var gold = attrs.TryGetValue("Gold", out var g) ? g : 0;
        attrs["Gold"] = gold + sellPrice;
        character.Attributes = JsonSerializer.Serialize(attrs);

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} sold '{ItemRef}' to {Merchant} for {Price} gold; total {Total}",
            request.CharacterId, request.ItemRef, merchant.Merchant.ArchetypeSlug, sellPrice, attrs["Gold"]);

        return new SellItemHubResult
        {
            Success = true,
            ItemRef = request.ItemRef,
            GoldReceived = sellPrice,
            NewGoldTotal = attrs["Gold"],
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

    private static SellItemHubResult Fail(ShopRefusal refusal, string message) =>
        new() { Success = false, Refusal = refusal, ErrorMessage = message };
}
