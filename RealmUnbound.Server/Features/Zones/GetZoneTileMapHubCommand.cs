using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Server.Features.Zones;

/// <summary>Hub command that loads and returns the tilemap for the caller's current zone.</summary>
/// <param name="ZoneId">The zone whose tilemap to load.</param>
public record GetZoneTileMapHubCommand(string ZoneId) : IRequest<GetZoneTileMapHubResult>;

/// <summary>Result returned by <see cref="GetZoneTileMapHubCommandHandler"/>.</summary>
public record GetZoneTileMapHubResult
{
    /// <summary>Gets a value indicating whether the load succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the tilemap DTO to send to the client, or <see langword="null"/> on failure.</summary>
    public TileMapDto? TileMap { get; init; }
}

/// <summary>
/// Handles <see cref="GetZoneTileMapHubCommand"/> by loading the <see cref="TiledMap"/>
/// from the tilemap repository and projecting it into a <see cref="TileMapDto"/> for the client.
/// </summary>
public class GetZoneTileMapHubCommandHandler : IRequestHandler<GetZoneTileMapHubCommand, GetZoneTileMapHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly ILogger<GetZoneTileMapHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetZoneTileMapHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">The tilemap repository used to load zone map definitions.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GetZoneTileMapHubCommandHandler(ITileMapRepository tilemapRepo, ILogger<GetZoneTileMapHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<GetZoneTileMapHubResult> Handle(GetZoneTileMapHubCommand request, CancellationToken cancellationToken)
    {
        var map = await _tilemapRepo.GetByZoneIdAsync(request.ZoneId);
        if (map is null)
        {
            _logger.LogWarning("No tilemap definition found for zone '{ZoneId}'", request.ZoneId);
            return new GetZoneTileMapHubResult { Success = false, ErrorMessage = $"No map found for zone '{request.ZoneId}'" };
        }

        var firstGid = map.GetFirstGid();

        // Convert tilelayers to engine layer DTOs: GIDs → 0-based spritesheet indices
        var layers = map.Layers
            .Where(l => l.Type == "tilelayer" && l.Data is not null)
            .Select((l, i) => new TileLayerDto(
                l.Name,
                TiledMapGameExtensions.ToEngineLayerData(l.Data!, firstGid),
                l.Properties.Find(p => p.Name == "zIndex")?.AsInt(i) ?? i))
            .ToArray();

        var exitTiles   = map.GetExitTiles();
        var spawnPoints = map.GetSpawnPoints();

        var dto = new TileMapDto(
            ZoneId:        map.GetZoneId(),
            TilesetKey:    map.GetTilesetKey(),
            Width:         map.Width,
            Height:        map.Height,
            TileSize:      map.TileWidth,
            Layers:        layers,
            CollisionMask: map.GetCollisionMask(),
            FogMask:       map.GetFogMask(),
            ExitTiles:     exitTiles.Select(e => new ExitTileDto(e.TileX, e.TileY, e.ToZoneId)).ToArray(),
            SpawnPoints:   spawnPoints.Select(s => new SpawnPointDto(s.TileX, s.TileY)).ToArray());

        return new GetZoneTileMapHubResult { Success = true, TileMap = dto };
    }
}
