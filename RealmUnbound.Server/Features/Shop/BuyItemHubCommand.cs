using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;

namespace Veldrath.Server.Features.Shop;

/// <summary>Hub command that purchases one unit of an item from a zone merchant.</summary>
/// <param name="CharacterId">The character making the purchase.</param>
/// <param name="ZoneId">The zone whose merchant shop is open.</param>
/// <param name="ItemRef">The item slug to purchase.</param>
public record BuyItemHubCommand(Guid CharacterId, string ZoneId, string ItemRef)
    : IRequest<BuyItemHubResult>;

/// <summary>Result returned by <see cref="BuyItemHubCommandHandler"/>.</summary>
public record BuyItemHubResult
{
    /// <summary>Gets a value indicating whether the purchase succeeded.</summary>
    public bool Success { get; init; }

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
/// Handles <see cref="BuyItemHubCommand"/> by verifying the character has sufficient gold,
/// deducting the item's catalog price from the attributes blob, and appending one unit of
/// the item to the character's inventory blob.
/// </summary>
public class BuyItemHubCommandHandler : IRequestHandler<BuyItemHubCommand, BuyItemHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICharacterRepository _characterRepo;
    private readonly ISender _mediator;
    private readonly ILogger<BuyItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="BuyItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="mediator">MediatR sender for the item catalog query.</param>
    /// <param name="logger">Logger instance.</param>
    public BuyItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ISender mediator,
        ILogger<BuyItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _mediator      = mediator;
        _logger        = logger;
    }

    /// <summary>Handles the purchase and returns the outcome.</summary>
    /// <param name="request">The buy command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="BuyItemHubResult"/> describing the outcome.</returns>
    public async Task<BuyItemHubResult> Handle(BuyItemHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemRef))
            return Fail("ItemRef is required.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        // Look up the item in the catalog to get its DB-driven price.
        var catalog = await _mediator.Send(new GetItemCatalogQuery(), cancellationToken);
        var item = catalog.FirstOrDefault(i =>
            string.Equals(i.Slug, request.ItemRef, StringComparison.OrdinalIgnoreCase));
        if (item is null)
            return Fail($"Item '{request.ItemRef}' not found in catalog.");

        // Parse attributes blob.
        var attrs = ParseAttrs(character.Attributes);
        var gold = attrs.TryGetValue("Gold", out var g) ? g : 0;

        if (gold < item.Price)
            return Fail($"Not enough gold. Have {gold}, need {item.Price}.");

        // Deduct gold.
        attrs["Gold"] = gold - item.Price;
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
            "Character {CharacterId} bought '{ItemRef}' for {Price} gold; remaining {Gold}",
            request.CharacterId, request.ItemRef, item.Price, attrs["Gold"]);

        return new BuyItemHubResult
        {
            Success       = true,
            ItemRef       = request.ItemRef,
            GoldSpent     = item.Price,
            RemainingGold = attrs["Gold"],
        };
    }

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

    private static BuyItemHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
