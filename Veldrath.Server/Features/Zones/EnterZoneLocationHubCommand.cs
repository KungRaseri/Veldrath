using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Map;

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// Enters a named location inside a zone. This is the one entry path for locations: walking onto a
/// location node, the party action and the dungeon command all end up here, so the arrival payload,
/// the discovery sweep and the enemy roster are produced the same way regardless of who asked.
/// </summary>
/// <param name="CharacterId">Character entering the location.</param>
/// <param name="ZoneId">Zone that owns the location.</param>
/// <param name="LocationSlug">Location to enter, by content slug.</param>
/// <param name="ZoneGroup">SignalR group used to key the enemy roster; derived from the zone when empty.</param>
public record EnterZoneLocationHubCommand(
    Guid CharacterId,
    string ZoneId,
    string LocationSlug,
    string ZoneGroup = "")
    : IRequest<EnterZoneLocationHubResult>;

/// <summary>Result returned by <see cref="EnterZoneLocationHubCommandHandler"/>.</summary>
public record EnterZoneLocationHubResult
{
    /// <summary>Gets a value indicating whether the character arrived.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the arrival produced by the move, including the soft-gate figures.</summary>
    public TravelToNodeHubResult? Arrival { get; init; }
}

/// <summary>
/// Handles <see cref="EnterZoneLocationHubCommand"/> by resolving the location's node on the zone map and
/// delegating the actual move to <see cref="TravelToNodeHubCommand"/>. Nothing is teleported: a character
/// who is not standing in the zone is told where the way in is instead of being moved.
/// </summary>
public class EnterZoneLocationHubCommandHandler
    : IRequestHandler<EnterZoneLocationHubCommand, EnterZoneLocationHubResult>
{
    private readonly IZoneRepository _zoneRepo;
    private readonly IZoneLocationRepository _locationRepo;
    private readonly IMapGraphRepository _mapRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IMediator _mediator;
    private readonly ILogger<EnterZoneLocationHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EnterZoneLocationHubCommandHandler"/>.</summary>
    /// <param name="zoneRepo">Zone catalog repository.</param>
    /// <param name="locationRepo">Zone-location repository used to resolve the location and the way in.</param>
    /// <param name="mapRepo">Map graph repository used to find the location's node.</param>
    /// <param name="characterRepo">Character repository used to check where the character stands.</param>
    /// <param name="mediator">Dispatcher for the move itself.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public EnterZoneLocationHubCommandHandler(
        IZoneRepository zoneRepo,
        IZoneLocationRepository locationRepo,
        IMapGraphRepository mapRepo,
        ICharacterRepository characterRepo,
        IMediator mediator,
        ILogger<EnterZoneLocationHubCommandHandler> logger)
    {
        _zoneRepo = zoneRepo;
        _locationRepo = locationRepo;
        _mapRepo = mapRepo;
        _characterRepo = characterRepo;
        _mediator = mediator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnterZoneLocationHubResult> Handle(
        EnterZoneLocationHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CharacterId == Guid.Empty)
            return Fail("Character ID cannot be empty");
        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return Fail("Zone ID cannot be empty");
        if (string.IsNullOrWhiteSpace(request.LocationSlug))
            return Fail("Location slug cannot be empty");

        var zone = await _zoneRepo.GetByIdAsync(request.ZoneId);
        if (zone is null)
            return Fail($"Zone '{request.ZoneId}' was not found");

        var location = await _locationRepo.GetBySlugAsync(request.LocationSlug);
        if (location is null)
            return Fail($"Location '{request.LocationSlug}' was not found");

        if (!string.Equals(location.ZoneId, zone.Id, StringComparison.OrdinalIgnoreCase))
            return Fail($"'{location.DisplayName}' is not in {zone.Name}");

        var map = await _mapRepo.GetMapForZoneAsync(zone.Id);
        if (map is null)
            return Fail($"{zone.Name} has no map");

        var node = map.Nodes.FirstOrDefault(candidate =>
            candidate.NodeType == MapNodeType.Location &&
            string.Equals(candidate.RefId, location.Slug, StringComparison.OrdinalIgnoreCase));

        if (node is null)
            return Fail($"'{location.DisplayName}' has no node on the {zone.Name} map");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character '{request.CharacterId}' was not found");

        // D3: one code path, and no silent teleport. Being elsewhere is a refusal that names the way in.
        var inZone = string.Equals(character.CurrentZoneId, zone.Id, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(character.CurrentMapId, map.Id, StringComparison.OrdinalIgnoreCase);

        if (!inZone)
        {
            var entrance = await ResolveEntranceNameAsync(zone, map);
            return Fail($"Travel to {zone.Name} first; {entrance} is the way in.");
        }

        var arrival = await _mediator.Send(
            new TravelToNodeHubCommand(request.CharacterId, map.Id, node.Id, request.ZoneGroup),
            cancellationToken);

        if (!arrival.Success)
            return Fail(arrival.ErrorMessage ?? $"Failed to enter {location.DisplayName}");

        _logger.LogInformation(
            "Character {CharacterIdPrefix} entered location {LocationSlug} in {ZoneId} (gate: {RecommendedLevel})",
            request.CharacterId.ToString()[..8],
            location.Slug,
            zone.Id,
            arrival.RecommendedLevel);

        return new EnterZoneLocationHubResult { Success = true, Arrival = arrival };
    }

    private async Task<string> ResolveEntranceNameAsync(Zone zone, MapGraph map)
    {
        var locations = await _locationRepo.GetByZoneIdAsync(zone.Id);
        var lobby = locations.FirstOrDefault(candidate => candidate.IsSafeRespawn);
        if (lobby is not null)
            return lobby.DisplayName;

        var entryNode = map.Nodes.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, map.EntryNodeId, StringComparison.OrdinalIgnoreCase));

        return entryNode?.DisplayName ?? zone.Name;
    }

    private static EnterZoneLocationHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
