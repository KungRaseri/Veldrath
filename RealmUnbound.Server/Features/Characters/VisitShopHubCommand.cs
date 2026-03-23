using MediatR;
using Microsoft.Extensions.Logging;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Characters;

/// <summary>
/// Hub command that visits the merchant shop at a zone, validating that the zone has
/// <see cref="Data.Entities.Zone.HasMerchant"/> set to <see langword="true"/>.
/// </summary>
/// <param name="CharacterId">The ID of the character visiting the shop.</param>
/// <param name="ZoneId">The ID of the zone whose merchant to visit.</param>
public record VisitShopHubCommand(Guid CharacterId, string ZoneId) : IRequest<VisitShopHubResult>;

/// <summary>Result returned by <see cref="VisitShopHubCommandHandler"/>.</summary>
public record VisitShopHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the zone ID of the visited shop, or <see langword="null"/> on failure.</summary>
    public string? ZoneId { get; init; }

    /// <summary>Gets the display name of the zone, or <see langword="null"/> on failure.</summary>
    public string? ZoneName { get; init; }
}

/// <summary>
/// Handles <see cref="VisitShopHubCommand"/> by looking up the requested zone via
/// <see cref="IZoneRepository"/> and verifying it has a merchant. Returns the zone ID
/// and name on success.
/// </summary>
public class VisitShopHubCommandHandler : IRequestHandler<VisitShopHubCommand, VisitShopHubResult>
{
    private readonly IZoneRepository _zoneRepo;
    private readonly ILogger<VisitShopHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="VisitShopHubCommandHandler"/>.</summary>
    /// <param name="zoneRepo">Repository used to look up zone catalog entries.</param>
    /// <param name="logger">Logger instance.</param>
    public VisitShopHubCommandHandler(
        IZoneRepository zoneRepo,
        ILogger<VisitShopHubCommandHandler> logger)
    {
        _zoneRepo = zoneRepo;
        _logger   = logger;
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
            return new VisitShopHubResult { Success = false, ErrorMessage = "Zone ID cannot be empty" };

        var zone = await _zoneRepo.GetByIdAsync(request.ZoneId);
        if (zone is null)
            return new VisitShopHubResult { Success = false, ErrorMessage = $"Zone '{request.ZoneId}' not found" };

        if (!zone.HasMerchant)
            return new VisitShopHubResult { Success = false, ErrorMessage = $"{zone.Name} has no merchant" };

        _logger.LogInformation(
            "Character {CharacterIdPrefix} visited shop at {ZoneId} ({ZoneName})",
            request.CharacterId.ToString()[..8], zone.Id, zone.Name);

        return new VisitShopHubResult
        {
            Success  = true,
            ZoneId   = zone.Id,
            ZoneName = zone.Name,
        };
    }
}
