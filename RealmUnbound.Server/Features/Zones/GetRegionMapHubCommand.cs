using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Contracts.Tilemap;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>Hub command that loads and returns the region tilemap for the caller's current region.</summary>
/// <param name="RegionId">The region whose tilemap to load.</param>
public record GetRegionMapHubCommand(string RegionId) : IRequest<GetRegionMapHubResult>;

/// <summary>Result returned by <see cref="GetRegionMapHubCommandHandler"/>.</summary>
public record GetRegionMapHubResult
{
    /// <summary>Gets a value indicating whether the load succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the region map DTO to send to the client, or <see langword="null"/> on failure.</summary>
    public RegionMapDto? Map { get; init; }

    /// <summary>Gets the snapshot of players currently on the region map (not inside a zone).</summary>
    public IReadOnlyList<TileEntityDto> Players { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetRegionMapHubCommand"/> by loading the region <see cref="TiledMap"/>
/// and projecting it into a <see cref="RegionMapDto"/> for the client.
/// Also queries all player sessions on the region map to populate the live entity snapshot.
/// </summary>
public class GetRegionMapHubCommandHandler : IRequestHandler<GetRegionMapHubCommand, GetRegionMapHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly ILogger<GetRegionMapHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetRegionMapHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for loading region map definitions.</param>
    /// <param name="sessionRepo">Player session repository for live entity snapshots.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GetRegionMapHubCommandHandler(
        ITileMapRepository tilemapRepo,
        IPlayerSessionRepository sessionRepo,
        ILogger<GetRegionMapHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _sessionRepo = sessionRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<GetRegionMapHubResult> Handle(GetRegionMapHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RegionId))
            return Fail("Region ID cannot be empty.");

        var map = await _tilemapRepo.GetByRegionIdAsync(request.RegionId);
        if (map is null)
        {
            _logger.LogWarning("No tilemap definition found for region '{RegionId}'", request.RegionId);
            return Fail($"No map found for region '{request.RegionId}'");
        }

        var firstGid = map.GetFirstGid();

        var layers = map.Layers
            .Where(l => l.Type == "tilelayer" && l.Data is not null)
            .Select((l, i) => new TileLayerDto(
                l.Name,
                TiledMapGameExtensions.ToEngineLayerData(l.Data!, firstGid),
                l.Properties.Find(p => p.Name == "zIndex")?.AsInt(i) ?? i))
            .ToArray();

        var zoneObjs    = map.GetZoneObjects();
        var regionExits = map.GetRegionExits();
        var tilesetKey  = map.GetStringProperty("tilesetKey") ?? "onebit_packed";

        var dto = new RegionMapDto(
            RegionId:      request.RegionId,
            TilesetKey:    tilesetKey,
            Width:         map.Width,
            Height:        map.Height,
            TileSize:      map.TileWidth,
            Layers:        layers,
            CollisionMask: map.GetCollisionMask(),
            Zones:         zoneObjs.Select(z => new ZoneObjectDto(z.ZoneId, z.DisplayName, z.MinLevel, z.MaxLevel, z.TileX, z.TileY)).ToArray(),
            RegionExits:   regionExits.Select(r => new RegionExitDto(r.ToRegionId, r.TileX, r.TileY)).ToArray());

        // Build live entity snapshot for players currently on the region map
        var sessions = await _sessionRepo.GetOnRegionMapAsync(request.RegionId);
        var players  = sessions
            .Select(s => new TileEntityDto(s.CharacterId, "player", "player_default", s.TileX, s.TileY, "S"))
            .ToArray();

        return new GetRegionMapHubResult { Success = true, Map = dto, Players = players };
    }

    private static GetRegionMapHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
