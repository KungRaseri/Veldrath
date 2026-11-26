using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Zones;

namespace Veldrath.Server.Features.Map;

/// <summary>
/// Asks for the enemy roster at the node the character already stands on.
/// </summary>
/// <remarks>
/// The roster is otherwise only ever delivered as part of a travel result, so a character who logs in or
/// reconnects inside a zone — including inside an instance run — was shown an empty room until their next move.
/// This is a read, not an arrival: it spawns the cohort on first contact through
/// <see cref="LocationRosterReader"/>, exactly as walking in would, and never moves the character.
/// </remarks>
/// <param name="CharacterId">Character whose current position is read.</param>
/// <param name="ZoneGroup">
/// Scope the connection is in — the shared zone, or a run's own group. It comes from the connection, not from
/// the request, so the roster is keyed on the scope the character is actually rendering.
/// </param>
/// <param name="ZoneInstanceId">Run the connection is inside, or <see langword="null"/> for the shared zone.</param>
public record GetLocationRosterHubCommand(
    Guid CharacterId,
    string ZoneGroup,
    Guid? ZoneInstanceId = null)
    : IRequest<GetLocationRosterHubResult>;

/// <summary>Result returned by <see cref="GetLocationRosterHubCommandHandler"/>.</summary>
public record GetLocationRosterHubResult
{
    /// <summary>Gets a value indicating whether the read succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the failure reason when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the location the roster belongs to, or <see langword="null"/> when the character is not standing on
    /// one (an entry point, a zone exit, a region node).
    /// </summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the live enemies present at the location.</summary>
    public IReadOnlyList<SpawnedEnemySummary> SpawnedEnemies { get; init; } = [];

    /// <summary>
    /// Gets content problems found while reading the roster — for example a spawn pool that references no known
    /// actors. Surface them to the player; they mean an operator has content to fix.
    /// </summary>
    public IReadOnlyList<string> ContentWarnings { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetLocationRosterHubCommand"/> by reading the location the character's saved position names
/// and returning the same roster a walk into that location would have shown.
/// </summary>
/// <remarks>
/// The position is read from the character row rather than passed by the caller: after a resume the hub has just
/// written it (<see cref="Veldrath.Server.Hubs.GameHub.EnterZone"/> persists the node it is about to report), and a
/// character whose run was swept is placed in the lobby there — a client that sent its own cached node would ask
/// for the room it logged out in. Standing on a non-location node is not a failure; it is simply a place with no
/// roster, so it answers with an empty list.
/// </remarks>
public class GetLocationRosterHubCommandHandler
    : IRequestHandler<GetLocationRosterHubCommand, GetLocationRosterHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly IZoneLocationRepository _locationRepo;
    private readonly LocationRosterReader _rosters;
    private readonly ILogger<GetLocationRosterHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetLocationRosterHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Character repository, read for the saved position.</param>
    /// <param name="locationRepo">Zone-location catalog, read for the location's pool.</param>
    /// <param name="rosters">Reader that returns the live roster, spawning on first contact.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GetLocationRosterHubCommandHandler(
        ICharacterRepository characterRepo,
        IZoneLocationRepository locationRepo,
        LocationRosterReader rosters,
        ILogger<GetLocationRosterHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _locationRepo = locationRepo;
        _rosters = rosters;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GetLocationRosterHubResult> Handle(
        GetLocationRosterHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CharacterId == Guid.Empty)
            return Fail("Character ID cannot be empty");
        if (string.IsNullOrWhiteSpace(request.ZoneGroup))
            return Fail("The character is not inside a zone");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character '{request.CharacterId}' was not found");

        // Only a location node has a roster. An entry point, a zone exit or a region node is a place to stand,
        // not a room, and answering "nobody is here" is the honest answer rather than a refusal.
        var locationSlug = character.CurrentZoneLocationSlug;
        if (string.IsNullOrWhiteSpace(locationSlug))
            return new GetLocationRosterHubResult { Success = true };

        var location = await _locationRepo.GetBySlugAsync(locationSlug);

        // The stored slug is a zone location only while it still belongs to the zone the character is in. A
        // content change that moved the location elsewhere would otherwise key a roster on the wrong scope, so
        // refuse the read quietly rather than spawn enemies into a group their own zone never loads.
        if (location is not null &&
            !string.IsNullOrWhiteSpace(character.CurrentZoneId) &&
            !string.Equals(location.ZoneId, character.CurrentZoneId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Character {CharacterIdPrefix} stands on {LocationSlug} of zone {LocationZoneId} but is in zone {ZoneId}; no roster was read",
                request.CharacterId.ToString()[..8],
                locationSlug,
                location.ZoneId,
                character.CurrentZoneId);

            return new GetLocationRosterHubResult { Success = true };
        }

        var (enemies, warnings) = await _rosters.ReadAsync(
            request.ZoneGroup,
            locationSlug,
            location,
            request.ZoneInstanceId);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} re-read {Count} enemies at {ZoneGroup}/{LocationSlug}",
            request.CharacterId.ToString()[..8],
            enemies.Count,
            request.ZoneGroup,
            locationSlug);

        return new GetLocationRosterHubResult
        {
            Success = true,
            LocationSlug = location?.Slug ?? locationSlug,
            SpawnedEnemies = enemies,
            ContentWarnings = warnings,
        };
    }

    private static GetLocationRosterHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
