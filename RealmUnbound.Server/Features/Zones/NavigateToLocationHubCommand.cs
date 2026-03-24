using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that moves a character to a specific <see cref="Data.Entities.ZoneLocation"/>
/// within their current zone, validating that the location exists and belongs to that zone.
/// </summary>
/// <param name="CharacterId">The ID of the character navigating.</param>
/// <param name="LocationSlug">The slug of the target zone location.</param>
/// <param name="ZoneId">The ID of the zone the character is currently in.</param>
public record NavigateToLocationHubCommand(Guid CharacterId, string LocationSlug, string ZoneId)
    : IRequest<NavigateToLocationHubResult>;

/// <summary>Result returned by <see cref="NavigateToLocationHubCommandHandler"/>.</summary>
public record NavigateToLocationHubResult
{
    /// <summary>Gets a value indicating whether the navigation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the slug of the location navigated to, or <see langword="null"/> on failure.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the display name of the location, or <see langword="null"/> on failure.</summary>
    public string? LocationDisplayName { get; init; }

    /// <summary>Gets the location type (e.g. "dungeon", "location", "environment"), or <see langword="null"/> on failure.</summary>
    public string? LocationType { get; init; }
}

/// <summary>
/// Handles <see cref="NavigateToLocationHubCommand"/> by verifying the location exists within the
/// character's current zone via <see cref="IZoneLocationRepository"/>, then persisting the new
/// current location via <see cref="ICharacterRepository"/>.
/// </summary>
public class NavigateToLocationHubCommandHandler
    : IRequestHandler<NavigateToLocationHubCommand, NavigateToLocationHubResult>
{
    private readonly IZoneLocationRepository _locationRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<NavigateToLocationHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="NavigateToLocationHubCommandHandler"/>.</summary>
    /// <param name="locationRepo">Repository used to look up zone location catalog entries.</param>
    /// <param name="characterRepo">Repository used to persist the character's current location.</param>
    /// <param name="logger">Logger instance.</param>
    public NavigateToLocationHubCommandHandler(
        IZoneLocationRepository locationRepo,
        ICharacterRepository characterRepo,
        ILogger<NavigateToLocationHubCommandHandler> logger)
    {
        _locationRepo  = locationRepo;
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the navigation outcome.</summary>
    /// <param name="request">The command containing the character ID, location slug, and zone ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="NavigateToLocationHubResult"/> describing the outcome.</returns>
    public async Task<NavigateToLocationHubResult> Handle(
        NavigateToLocationHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LocationSlug))
            return new NavigateToLocationHubResult { Success = false, ErrorMessage = "Location slug cannot be empty" };

        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return new NavigateToLocationHubResult { Success = false, ErrorMessage = "Zone ID cannot be empty" };

        var locationsInZone = await _locationRepo.GetByZoneIdAsync(request.ZoneId);
        var location = locationsInZone.FirstOrDefault(l =>
            l.Slug.Equals(request.LocationSlug, StringComparison.OrdinalIgnoreCase));

        if (location is null)
            return new NavigateToLocationHubResult
            {
                Success      = false,
                ErrorMessage = $"Location '{request.LocationSlug}' is not available in zone '{request.ZoneId}'",
            };

        await _characterRepo.UpdateCurrentZoneLocationAsync(request.CharacterId, location.Slug, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} navigated to {LocationSlug} in zone {ZoneId}",
            request.CharacterId.ToString()[..8], location.Slug, request.ZoneId);

        return new NavigateToLocationHubResult
        {
            Success             = true,
            LocationSlug        = location.Slug,
            LocationDisplayName = location.DisplayName,
            LocationType        = location.LocationType,
        };
    }
}
