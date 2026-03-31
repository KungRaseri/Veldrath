using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that moves a character along a <see cref="ZoneLocationConnectionEntry"/>,
/// updating <c>CurrentZoneLocationSlug</c> and optionally <c>CurrentZoneId</c> when
/// the connection crosses into a different zone.
/// </summary>
/// <param name="CharacterId">The character traversing the connection.</param>
/// <param name="FromLocationSlug">The location the character is departing from.</param>
/// <param name="ConnectionType">The connection type being used (must match a known edge from this location).</param>
public record TraverseConnectionHubCommand(Guid CharacterId, string FromLocationSlug, string ConnectionType)
    : IRequest<TraverseConnectionHubResult>;

/// <summary>Result returned by <see cref="TraverseConnectionHubCommandHandler"/>.</summary>
public record TraverseConnectionHubResult
{
    /// <summary>Gets a value indicating whether the traversal succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the destination ZoneLocation slug, or <see langword="null"/> when the destination is a whole zone.</summary>
    public string? ToLocationSlug { get; init; }

    /// <summary>Gets the destination zone ID, or <see langword="null"/> when the destination is another location in the same zone.</summary>
    public string? ToZoneId { get; init; }

    /// <summary>True when the traversal moved the character into a different zone.</summary>
    public bool IsCrossZone { get; init; }

    /// <summary>Gets the connection type that was used.</summary>
    public string? ConnectionType { get; init; }

    /// <summary>Gets the outgoing connections available from the destination location, populated for intra-zone traversals.</summary>
    public IReadOnlyList<ZoneLocationConnectionEntry> AvailableConnections { get; init; } = [];
}

/// <summary>
/// Handles <see cref="TraverseConnectionHubCommand"/> by looking up the matching connection edge,
/// validating traversability, then persisting the character's new location state.
/// </summary>
public class TraverseConnectionHubCommandHandler
    : IRequestHandler<TraverseConnectionHubCommand, TraverseConnectionHubResult>
{
    private readonly IZoneLocationRepository _locationRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<TraverseConnectionHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="TraverseConnectionHubCommandHandler"/>.</summary>
    /// <param name="locationRepo">Repository for zone location and connection data.</param>
    /// <param name="characterRepo">Repository used to persist updated zone/location state.</param>
    /// <param name="logger">Logger instance.</param>
    public TraverseConnectionHubCommandHandler(
        IZoneLocationRepository locationRepo,
        ICharacterRepository characterRepo,
        ILogger<TraverseConnectionHubCommandHandler> logger)
    {
        _locationRepo  = locationRepo;
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the traverse command and returns the outcome.</summary>
    /// <param name="request">The command containing character, origin location, and connection type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TraverseConnectionHubResult"/> describing the outcome.</returns>
    public async Task<TraverseConnectionHubResult> Handle(
        TraverseConnectionHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FromLocationSlug))
            return new TraverseConnectionHubResult { Success = false, ErrorMessage = "Origin location slug cannot be empty" };

        if (string.IsNullOrWhiteSpace(request.ConnectionType))
            return new TraverseConnectionHubResult { Success = false, ErrorMessage = "Connection type cannot be empty" };

        var connections = await _locationRepo.GetConnectionsFromAsync(request.FromLocationSlug);
        var match = connections.FirstOrDefault(c =>
            string.Equals(c.ConnectionType, request.ConnectionType, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return new TraverseConnectionHubResult
            {
                Success      = false,
                ErrorMessage = $"No connection of type '{request.ConnectionType}' exists from '{request.FromLocationSlug}'",
            };

        if (!match.IsTraversable)
            return new TraverseConnectionHubResult
            {
                Success      = false,
                ErrorMessage = $"The '{request.ConnectionType}' connection from '{request.FromLocationSlug}' is currently blocked",
            };

        // Persist the destination. Cross-zone traversal updates both fields.
        if (match.ToZoneId is not null)
        {
            await _characterRepo.UpdateCurrentZoneAsync(request.CharacterId, match.ToZoneId, cancellationToken);
            await _characterRepo.UpdateCurrentZoneLocationAsync(request.CharacterId, match.ToLocationSlug, cancellationToken);
        }
        else
        {
            await _characterRepo.UpdateCurrentZoneLocationAsync(request.CharacterId, match.ToLocationSlug, cancellationToken);
        }

        _logger.LogInformation(
            "Character {CharacterIdPrefix} traversed {ConnectionType} from {From} → zone:{ToZone} location:{ToLocation}",
            request.CharacterId.ToString()[..8], match.ConnectionType,
            request.FromLocationSlug, match.ToZoneId ?? "(same)", match.ToLocationSlug ?? "(none)");

        // For intra-zone traversals, look up connections from the destination so the client
        // can render the "where can I go next?" buttons without a second round-trip.
        IReadOnlyList<ZoneLocationConnectionEntry> availableConnections = [];
        if (match.ToZoneId is null && match.ToLocationSlug is not null)
            availableConnections = await _locationRepo.GetConnectionsFromAsync(match.ToLocationSlug);

        return new TraverseConnectionHubResult
        {
            Success              = true,
            ToLocationSlug       = match.ToLocationSlug,
            ToZoneId             = match.ToZoneId,
            IsCrossZone          = match.ToZoneId is not null,
            ConnectionType       = match.ConnectionType,
            AvailableConnections = availableConnections,
        };
    }
}
