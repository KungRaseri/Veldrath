using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that moves a character from their current zone back to the region map.
/// Clears <c>PlayerSession.ZoneId</c> and places the character one tile below the zone's
/// entry object on the region map.
/// </summary>
/// <param name="CharacterId">ID of the character exiting the zone.</param>
public record ExitZoneHubCommand(Guid CharacterId) : IRequest<ExitZoneHubResult>;

/// <summary>Result returned by <see cref="ExitZoneHubCommandHandler"/>.</summary>
public record ExitZoneHubResult
{
    /// <summary>Gets a value indicating whether the exit succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the region ID the character has returned to.</summary>
    public string RegionId { get; init; } = string.Empty;

    /// <summary>Gets the tile column the character spawned at on the region map.</summary>
    public int SpawnTileX { get; init; }

    /// <summary>Gets the tile row the character spawned at on the region map.</summary>
    public int SpawnTileY { get; init; }
}

/// <summary>
/// Handles <see cref="ExitZoneHubCommand"/> by:
/// <list type="bullet">
///   <item>Verifying the character is currently inside a zone.</item>
///   <item>Finding the zone's entry object on the region map and placing the character one tile below it.</item>
///   <item>Calling <see cref="IPlayerSessionRepository.SetZoneAsync"/> with <see langword="null"/> to return to region map.</item>
///   <item>Persisting the spawn tile position.</item>
/// </list>
/// </summary>
public class ExitZoneHubCommandHandler : IRequestHandler<ExitZoneHubCommand, ExitZoneHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly ILogger<ExitZoneHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="ExitZoneHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository to find the zone object on the region map.</param>
    /// <param name="sessionRepo">Player session repository to update position and zone state.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ExitZoneHubCommandHandler(
        ITileMapRepository tilemapRepo,
        IPlayerSessionRepository sessionRepo,
        ILogger<ExitZoneHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _sessionRepo = sessionRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<ExitZoneHubResult> Handle(ExitZoneHubCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepo.GetByCharacterIdAsync(request.CharacterId);
        if (session is null)
            return Fail("No active session found.");

        if (session.ZoneId is null)
            return Fail("Character is not currently inside a zone.");

        var currentZoneId = session.ZoneId;
        var regionId      = session.RegionId;

        // Find the zone entry tile on the region map to determine spawn position
        var spawnX = 0;
        var spawnY = 0;

        var regionMap = await _tilemapRepo.GetByRegionIdAsync(regionId);
        if (regionMap is not null)
        {
            var zoneObj = regionMap.GetZoneObjects()
                                   .FirstOrDefault(z => z.ZoneId.Equals(currentZoneId, StringComparison.OrdinalIgnoreCase));
            if (zoneObj is not null)
            {
                // Spawn one tile below the zone entry object
                spawnX = zoneObj.TileX;
                spawnY = zoneObj.TileY + 1;
            }
        }

        // Exit zone (back to region map) and persist spawn position
        await _sessionRepo.SetZoneAsync(request.CharacterId, null);
        await _sessionRepo.UpdatePositionAsync(request.CharacterId, spawnX, spawnY);

        _logger.LogDebug(
            "Character {Id} exited zone '{ZoneId}', placed at ({X},{Y}) on region '{Region}'",
            request.CharacterId, currentZoneId, spawnX, spawnY, regionId);

        return new ExitZoneHubResult
        {
            Success    = true,
            RegionId   = regionId,
            SpawnTileX = spawnX,
            SpawnTileY = spawnY,
        };
    }

    private static ExitZoneHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
