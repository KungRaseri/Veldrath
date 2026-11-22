using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Shop;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that visits the merchant shop at a zone, requiring an actual merchant standing with the character.
/// </summary>
/// <param name="CharacterId">The ID of the character visiting the shop.</param>
/// <param name="ZoneId">The ID of the zone whose merchant to visit.</param>
public record VisitShopHubCommand(Guid CharacterId, string ZoneId) : IRequest<VisitShopHubResult>;

/// <summary>Result returned by <see cref="VisitShopHubCommandHandler"/>.</summary>
public record VisitShopHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the typed reason the visit was refused, or <see cref="ShopRefusal.None"/> on success.</summary>
    public ShopRefusal Refusal { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the zone ID of the visited shop, or <see langword="null"/> on failure.</summary>
    public string? ZoneId { get; init; }

    /// <summary>Gets the display name of the zone, or <see langword="null"/> on failure.</summary>
    public string? ZoneName { get; init; }
}

/// <summary>
/// Handles <see cref="VisitShopHubCommand"/> by looking up the requested zone via
/// <see cref="IZoneRepository"/> and resolving the merchant standing with the character. A zone flag alone is not a
/// shop: the character has to be at a location where a shopkeeper is actually placed.
/// </summary>
public class VisitShopHubCommandHandler : IRequestHandler<VisitShopHubCommand, VisitShopHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly IZoneRepository _zoneRepo;
    private readonly IShopMerchantResolver _merchants;
    private readonly ILogger<VisitShopHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="VisitShopHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load the character whose position authorises the shop.</param>
    /// <param name="zoneRepo">Repository used to look up zone catalog entries.</param>
    /// <param name="merchants">Resolver for the merchant standing with the character.</param>
    /// <param name="logger">Logger instance.</param>
    public VisitShopHubCommandHandler(
        ICharacterRepository characterRepo,
        IZoneRepository zoneRepo,
        IShopMerchantResolver merchants,
        ILogger<VisitShopHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _zoneRepo = zoneRepo;
        _merchants = merchants;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the shop-visit outcome.</summary>
    /// <param name="request">The command containing the character ID and zone ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="VisitShopHubResult"/> describing the outcome.</returns>
    public async Task<VisitShopHubResult> Handle(
        VisitShopHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return Refuse(ShopRefusal.InvalidRequest, "Zone ID cannot be empty");

        var zone = await _zoneRepo.GetByIdAsync(request.ZoneId);
        if (zone is null)
            return Refuse(ShopRefusal.ZoneNotFound, $"Zone '{request.ZoneId}' not found");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Refuse(ShopRefusal.CharacterNotFound, $"Character {request.CharacterId} not found");

        var merchant = await _merchants.ResolveAsync(character, request.ZoneId, cancellationToken);
        if (!merchant.Success)
        {
            _logger.LogDebug(
                "Character {CharacterId} found no merchant to visit at {ZoneId}/{LocationSlug}",
                request.CharacterId, character.CurrentZoneId, character.CurrentZoneLocationSlug);

            return Refuse(
                merchant.Refusal,
                merchant.Refusal == ShopRefusal.ZoneMismatch
                    ? "You are not standing in that zone."
                    : "There is no one to trade with here.");
        }

        _logger.LogInformation(
            "Character {CharacterIdPrefix} visited shop at {ZoneId} ({ZoneName}), served by {Merchant}",
            request.CharacterId.ToString()[..8], zone.Id, zone.Name, merchant.Merchant!.ArchetypeSlug);

        return new VisitShopHubResult
        {
            Success = true,
            ZoneId = zone.Id,
            ZoneName = zone.Name,
        };
    }

    private static VisitShopHubResult Refuse(ShopRefusal refusal, string message) =>
        new() { Success = false, Refusal = refusal, ErrorMessage = message };
}
