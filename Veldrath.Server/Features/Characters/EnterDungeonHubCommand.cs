using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Enters a dungeon by zone slug. This is a thin alias over
/// <see cref="EnterZoneLocationHubCommand"/>: it resolves the dungeon's entrance location and asks the
/// one location-entry path to move the character there, so the arrival, the discovery sweep, the enemy
/// roster and the level warning are identical to walking in.
/// </summary>
/// <param name="CharacterId">The ID of the character entering the dungeon.</param>
/// <param name="DungeonSlug">The slug / zone ID of the dungeon (e.g. <c>"the-halrow"</c>).</param>
public record EnterDungeonHubCommand(Guid CharacterId, string DungeonSlug) : IRequest<EnterDungeonHubResult>;

/// <summary>Result returned by <see cref="EnterDungeonHubCommandHandler"/>.</summary>
public record EnterDungeonHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the dungeon zone ID on success, or <see langword="null"/> on failure.</summary>
    public string? DungeonId { get; init; }

    /// <summary>Gets the entrance location the character arrived at, or <see langword="null"/> on failure.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the level the dungeon expects, or <see langword="null"/> when it sets no expectation.</summary>
    public int? RecommendedLevel { get; init; }

    /// <summary>Gets the arriving character's level.</summary>
    public int YourLevel { get; init; }

    /// <summary>Gets the advisory wording for an under-levelled character, or <see langword="null"/>.</summary>
    public string? LevelWarning { get; init; }
}

/// <summary>
/// Handles <see cref="EnterDungeonHubCommand"/> by resolving the requested dungeon zone and its entrance
/// location, then delegating to <see cref="EnterZoneLocationHubCommand"/>. A character who is not in the
/// zone is told where the way in is rather than being moved.
/// </summary>
public class EnterDungeonHubCommandHandler : IRequestHandler<EnterDungeonHubCommand, EnterDungeonHubResult>
{
    private readonly IZoneRepository _zoneRepo;
    private readonly IZoneLocationRepository _locationRepo;
    private readonly IMediator _mediator;
    private readonly ILogger<EnterDungeonHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EnterDungeonHubCommandHandler"/>.</summary>
    /// <param name="zoneRepo">Repository used to look up zone catalog entries.</param>
    /// <param name="locationRepo">Repository used to resolve the dungeon entrance.</param>
    /// <param name="mediator">Dispatcher for the shared location-entry command.</param>
    /// <param name="logger">Logger instance.</param>
    public EnterDungeonHubCommandHandler(
        IZoneRepository zoneRepo,
        IZoneLocationRepository locationRepo,
        IMediator mediator,
        ILogger<EnterDungeonHubCommandHandler> logger)
    {
        _zoneRepo = zoneRepo;
        _locationRepo = locationRepo;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the dungeon entry outcome.</summary>
    /// <param name="request">The command containing the character ID and dungeon slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EnterDungeonHubResult"/> describing the outcome.</returns>
    public async Task<EnterDungeonHubResult> Handle(
        EnterDungeonHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DungeonSlug))
            return Fail("Dungeon slug cannot be empty");

        var zone = await _zoneRepo.GetByIdAsync(request.DungeonSlug);
        if (zone is null)
            return Fail($"Dungeon '{request.DungeonSlug}' not found");

        if (zone.Type != ZoneType.Dungeon)
            return Fail($"'{zone.Name}' is not a dungeon");

        var entrance = await ResolveEntranceAsync(zone.Id);
        if (entrance is null)
            return Fail($"{zone.Name} has no entrance location");

        var arrival = await _mediator.Send(
            new EnterZoneLocationHubCommand(request.CharacterId, zone.Id, entrance.Slug),
            cancellationToken);

        if (!arrival.Success)
            return Fail(arrival.ErrorMessage ?? $"Failed to enter {zone.Name}");

        var moved = arrival.Arrival!;

        _logger.LogInformation(
            "Character {CharacterIdPrefix} entered dungeon {DungeonId} at {LocationSlug}",
            request.CharacterId.ToString()[..8],
            zone.Id,
            entrance.Slug);

        return new EnterDungeonHubResult
        {
            Success = true,
            DungeonId = zone.Id,
            LocationSlug = entrance.Slug,
            RecommendedLevel = moved.RecommendedLevel,
            YourLevel = moved.YourLevel,
            LevelWarning = moved.LevelWarning,
        };
    }

    /// <summary>
    /// The entrance is the room a dead character resurrects in — the dungeon lobby — falling back to the
    /// first location that is not itself a dungeon room when no lobby has been flagged.
    /// </summary>
    private async Task<RealmEngine.Shared.Models.ZoneLocationEntry?> ResolveEntranceAsync(string zoneId)
    {
        var locations = await _locationRepo.GetByZoneIdAsync(zoneId);

        return locations.FirstOrDefault(location => location.IsSafeRespawn)
            ?? locations.FirstOrDefault(location =>
                !string.Equals(location.TypeKey, "dungeons", StringComparison.OrdinalIgnoreCase))
            ?? locations.FirstOrDefault();
    }

    private static EnterDungeonHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
