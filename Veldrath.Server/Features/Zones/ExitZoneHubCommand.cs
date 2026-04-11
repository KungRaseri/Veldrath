using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Zones;

// ── Request ──────────────────────────────────────────────────────────────────

/// <summary>
/// Hub command that exits a character from their current zone back to the region map,
/// placing them on the zone-entry tile associated with the zone they are leaving.
/// </summary>
/// <param name="CharacterId">ID of the character exiting the zone.</param>
/// <param name="RegionId">The region containing the zone.</param>
/// <param name="CurrentZoneId">The zone slug the character is currently inside.</param>
public record ExitZoneHubCommand(Guid CharacterId, string RegionId, string CurrentZoneId)
    : IRequest<ExitZoneHubResult>;

// ── Result ───────────────────────────────────────────────────────────────────

/// <summary>Result returned by <see cref="ExitZoneHubCommandHandler"/>.</summary>
public record ExitZoneHubResult
{
    /// <summary>Gets a value indicating whether the exit was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the tile column the character spawns at on the region map.</summary>
    public int TileX { get; init; }

    /// <summary>Gets the tile row the character spawns at on the region map.</summary>
    public int TileY { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Handles <see cref="ExitZoneHubCommand"/> by:
/// <list type="number">
///   <item>Loading the region <see cref="RealmEngine.Shared.Models.Tiled.TiledMap"/>.</item>
///   <item>Finding the <see cref="RealmEngine.Shared.Models.TileMapDefinition.ZoneObjectDefinition"/> whose
///         <see cref="RealmEngine.Shared.Models.TileMapDefinition.ZoneObjectDefinition.ZoneSlug"/> matches
///         <see cref="ExitZoneHubCommand.CurrentZoneId"/>.</item>
///   <item>Clearing <see cref="Data.Entities.PlayerSession.ZoneId"/> via
///         <see cref="IPlayerSessionRepository.SetZoneAsync"/>.</item>
///   <item>Setting <see cref="Data.Entities.PlayerSession.TileX"/> /
///         <see cref="Data.Entities.PlayerSession.TileY"/> to the zone-entry tile,
///         or falling back to (1, 1) if the zone is not found on the map.</item>
/// </list>
/// </summary>
public class ExitZoneHubCommandHandler : IRequestHandler<ExitZoneHubCommand, ExitZoneHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly ILogger<ExitZoneHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="ExitZoneHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for region map data.</param>
    /// <param name="sessionRepo">Player session repository for persisting position and zone state.</param>
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
        var (spawnX, spawnY) = (1, 1);

        var map = await _tilemapRepo.GetByRegionIdAsync(request.RegionId);
        if (map is not null)
        {
            var entry = map.GetZoneEntries()
                .FirstOrDefault(e => string.Equals(e.ZoneSlug, request.CurrentZoneId, StringComparison.OrdinalIgnoreCase));

            if (entry is not null)
            {
                spawnX = entry.TileX;
                spawnY = entry.TileY;
            }
            else
            {
                _logger.LogWarning(
                    "Zone entry for '{ZoneId}' not found on region '{RegionId}' — defaulting to (1,1)",
                    request.CurrentZoneId, request.RegionId);
            }
        }
        else
        {
            _logger.LogWarning(
                "No tilemap found for region '{RegionId}' during zone exit — defaulting to (1,1)",
                request.RegionId);
        }

        await _sessionRepo.SetZoneAsync(request.CharacterId, null);
        await _sessionRepo.UpdatePositionAsync(request.CharacterId, spawnX, spawnY);

        _logger.LogDebug(
            "Character {Id} exited zone '{Zone}' and was placed at ({X},{Y}) on region '{Region}'",
            request.CharacterId, request.CurrentZoneId, spawnX, spawnY, request.RegionId);

        return new ExitZoneHubResult { Success = true, TileX = spawnX, TileY = spawnY };
    }
}
