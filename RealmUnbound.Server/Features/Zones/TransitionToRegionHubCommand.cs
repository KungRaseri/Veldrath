using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Contracts.Tilemap;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that moves a character from their current region to an adjacent region.
/// Updates <see cref="Data.Entities.PlayerSession"/> with the new region, clears the zone,
/// and places the character at the region's first spawn point (or tile (0,0) if none defined).
/// </summary>
/// <param name="CharacterId">ID of the character transitioning.</param>
/// <param name="TargetRegionId">Slug of the region to move to.</param>
public record TransitionToRegionHubCommand(Guid CharacterId, string TargetRegionId)
    : IRequest<TransitionToRegionHubResult>;

/// <summary>Result returned by <see cref="TransitionToRegionHubCommandHandler"/>.</summary>
public record TransitionToRegionHubResult
{
    /// <summary>Gets a value indicating whether the transition succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the region map DTO for the destination region, or <see langword="null"/> on failure.</summary>
    public RegionMapDto? Map { get; init; }

    /// <summary>Gets the tile column the character spawned at in the new region.</summary>
    public int SpawnTileX { get; init; }

    /// <summary>Gets the tile row the character spawned at in the new region.</summary>
    public int SpawnTileY { get; init; }
}

/// <summary>
/// Handles <see cref="TransitionToRegionHubCommand"/> by:
/// <list type="bullet">
///   <item>Validating the target region exists.</item>
///   <item>Loading the target region tilemap and resolving the spawn position.</item>
///   <item>Updating <see cref="IPlayerSessionRepository.UpdateRegionAsync"/> with the new region and spawn tile.</item>
///   <item>Returning the <see cref="RegionMapDto"/> and spawn coordinates to the caller.</item>
/// </list>
/// </summary>
public class TransitionToRegionHubCommandHandler : IRequestHandler<TransitionToRegionHubCommand, TransitionToRegionHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly IRegionRepository _regionRepo;
    private readonly ILogger<TransitionToRegionHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="TransitionToRegionHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for loading region map definitions.</param>
    /// <param name="sessionRepo">Player session repository for persisting the region transition.</param>
    /// <param name="regionRepo">Region repository for validating the target region exists.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public TransitionToRegionHubCommandHandler(
        ITileMapRepository tilemapRepo,
        IPlayerSessionRepository sessionRepo,
        IRegionRepository regionRepo,
        ILogger<TransitionToRegionHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _sessionRepo = sessionRepo;
        _regionRepo  = regionRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<TransitionToRegionHubResult> Handle(TransitionToRegionHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetRegionId))
            return Fail("Target region ID cannot be empty.");

        var region = await _regionRepo.GetByIdAsync(request.TargetRegionId);
        if (region is null)
            return Fail($"Region '{request.TargetRegionId}' not found.");

        // Determine spawn position from region map spawn points; default to (0,0)
        var spawnX = 0;
        var spawnY = 0;

        var map = await _tilemapRepo.GetByRegionIdAsync(request.TargetRegionId);
        if (map is not null)
        {
            var spawn = map.GetSpawnPoints().FirstOrDefault();
            if (spawn is not null)
            {
                spawnX = spawn.TileX;
                spawnY = spawn.TileY;
            }
        }

        await _sessionRepo.UpdateRegionAsync(request.CharacterId, request.TargetRegionId, spawnX, spawnY);

        _logger.LogDebug(
            "Character {Id} transitioned to region '{Region}', placed at ({X},{Y})",
            request.CharacterId, request.TargetRegionId, spawnX, spawnY);

        RegionMapDto? dto = null;
        if (map is not null)
        {
            var firstGid = map.GetFirstGid();
            var layers   = map.Layers
                .Where(l => l.Type == "tilelayer" && l.Data is not null)
                .Select((l, i) => new TileLayerDto(
                    l.Name,
                    TiledMapGameExtensions.ToEngineLayerData(l.Data!, firstGid),
                    l.Properties.Find(p => p.Name == "zIndex")?.AsInt(i) ?? i))
                .ToArray();

            var zoneObjs    = map.GetZoneObjects();
            var regionExits = map.GetRegionExits();
            var tilesetKey  = map.GetStringProperty("tilesetKey") ?? "onebit_packed";

            dto = new RegionMapDto(
                RegionId:      request.TargetRegionId,
                TilesetKey:    tilesetKey,
                Width:         map.Width,
                Height:        map.Height,
                TileSize:      map.TileWidth,
                Layers:        layers,
                CollisionMask: map.GetCollisionMask(),
                Zones:         zoneObjs.Select(z => new ZoneObjectDto(z.ZoneId, z.DisplayName, z.MinLevel, z.MaxLevel, z.TileX, z.TileY)).ToArray(),
                RegionExits:   regionExits.Select(r => new RegionExitDto(r.ToRegionId, r.TileX, r.TileY)).ToArray());
        }

        return new TransitionToRegionHubResult
        {
            Success    = true,
            Map        = dto,
            SpawnTileX = spawnX,
            SpawnTileY = spawnY,
        };
    }

    private static TransitionToRegionHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

// ── Hub request DTO ──────────────────────────────────────────────────────────

/// <summary>Payload sent by the client when transitioning to an adjacent region.</summary>
/// <param name="TargetRegionId">Slug of the region to travel to.</param>
public record TransitionToRegionHubRequest(string TargetRegionId);
