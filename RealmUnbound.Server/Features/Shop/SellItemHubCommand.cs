using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters;

namespace RealmUnbound.Server.Features.Shop;

/// <summary>Hub command that sells one unit of an item from a character's inventory to the merchant.</summary>
/// <param name="CharacterId">The character selling the item.</param>
/// <param name="ZoneId">The zone whose merchant shop is open.</param>
/// <param name="ItemRef">The item slug to sell.</param>
public record SellItemHubCommand(Guid CharacterId, string ZoneId, string ItemRef)
    : IRequest<SellItemHubResult>;

/// <summary>Result returned by <see cref="SellItemHubCommandHandler"/>.</summary>
public record SellItemHubResult
{
    /// <summary>Gets a value indicating whether the sale succeeded.</summary>
    public bool Success { get; init; }

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
/// Handles <see cref="SellItemHubCommand"/> by removing one unit of the item from the
/// character's inventory blob and crediting gold at 50% of the catalog buy price (floor 1 gold).
/// </summary>
public class SellItemHubCommandHandler : IRequestHandler<SellItemHubCommand, SellItemHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICharacterRepository _characterRepo;
    private readonly ISender _mediator;
    private readonly ILogger<SellItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="SellItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="mediator">MediatR sender for the item catalog query.</param>
    /// <param name="logger">Logger instance.</param>
    public SellItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ISender mediator,
        ILogger<SellItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _mediator      = mediator;
        _logger        = logger;
    }

    /// <summary>Handles the sale and returns the outcome.</summary>
    /// <param name="request">The sell command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SellItemHubResult"/> describing the outcome.</returns>
    public async Task<SellItemHubResult> Handle(SellItemHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemRef))
            return Fail("ItemRef is required.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        // Remove one unit from inventory.
        var inventory = ParseInventory(character.InventoryBlob);
        var existing = inventory.FirstOrDefault(i =>
            string.Equals(i.ItemRef, request.ItemRef, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return Fail($"Item '{request.ItemRef}' not found in inventory.");

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

        // Determine sell price from catalog (50% of buy price, floor 1).
        var catalog = await _mediator.Send(new GetItemCatalogQuery(), cancellationToken);
        var item = catalog.FirstOrDefault(i =>
            string.Equals(i.Slug, request.ItemRef, StringComparison.OrdinalIgnoreCase));
        var sellPrice = item is not null ? Math.Max(1, item.Price / 2) : 1;

        // Credit gold.
        var attrs = ParseAttrs(character.Attributes);
        var gold = attrs.TryGetValue("Gold", out var g) ? g : 0;
        attrs["Gold"] = gold + sellPrice;
        character.Attributes = JsonSerializer.Serialize(attrs);

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} sold '{ItemRef}' for {Price} gold; total {Total}",
            request.CharacterId, request.ItemRef, sellPrice, attrs["Gold"]);

        return new SellItemHubResult
        {
            Success      = true,
            ItemRef      = request.ItemRef,
            GoldReceived = sellPrice,
            NewGoldTotal = attrs["Gold"],
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

    private static SellItemHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
