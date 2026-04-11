using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Server.Features.Zones;

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
    public RegionMapDto? RegionMap { get; init; }
}

/// <summary>
/// Handles <see cref="GetRegionMapHubCommand"/> by loading the <see cref="TiledMap"/>
/// from the tilemap repository using <see cref="ITileMapRepository.GetByRegionIdAsync"/>
/// and projecting it into a <see cref="RegionMapDto"/> for the client.
/// If no TMX file is found for the region, <see cref="GetRegionMapHubResult.Success"/> is
/// <see langword="false"/> and <see cref="GetRegionMapHubResult.ErrorMessage"/> contains a
/// descriptive message indicating which region ID was not found.
/// </summary>
public class GetRegionMapHubCommandHandler : IRequestHandler<GetRegionMapHubCommand, GetRegionMapHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly ILogger<GetRegionMapHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetRegionMapHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">The tilemap repository used to load region map definitions.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GetRegionMapHubCommandHandler(ITileMapRepository tilemapRepo, ILogger<GetRegionMapHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<GetRegionMapHubResult> Handle(GetRegionMapHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RegionId))
            return new GetRegionMapHubResult { Success = false, ErrorMessage = "Region ID cannot be empty" };

        var map = await _tilemapRepo.GetByRegionIdAsync(request.RegionId);
        if (map is null)
        {
            _logger.LogWarning("No tilemap definition found for region '{RegionId}'", request.RegionId);
            return new GetRegionMapHubResult
            {
                Success      = false,
                ErrorMessage = $"No map found for region '{request.RegionId}'",
            };
        }

        var firstGid = map.GetFirstGid();

        var layers = map.Layers
            .Where(l => l.Type == "tilelayer" && l.Data is not null)
            .Select((l, i) => new TileLayerDto(
                l.Name,
                TiledMapGameExtensions.ToEngineLayerData(l.Data!, firstGid),
                l.Properties.Find(p => p.Name == "zIndex")?.AsInt(i) ?? i))
            .ToArray();

        var zoneEntries = map.GetZoneEntries()
            .Select(e => new ZoneObjectDto(e.TileX, e.TileY, e.ZoneSlug, e.DisplayName, e.MinLevel, e.MaxLevel))
            .ToArray();

        var regionExits = map.GetRegionExits()
            .Select(e => new RegionExitDto(e.TileX, e.TileY, e.TargetRegionId))
            .ToArray();

        var labels = map.GetZoneLabels()
            .Select(l => new ZoneLabelDto(l.TileX, l.TileY, l.Text, l.ZoneSlug, l.IsHidden))
            .ToArray();

        var paths = map.GetRegionPaths()
            .Select(p => new RegionPathDto(
                p.Name,
                p.Points.Select(pt => new RegionPathPointDto(pt.TileX, pt.TileY)).ToArray()))
            .ToArray();

        var dto = new RegionMapDto(
            RegionId:        map.GetRegionId().Length > 0 ? map.GetRegionId() : request.RegionId,
            TilesetKey:      map.GetTilesetKey(),
            Width:           map.Width,
            Height:          map.Height,
            TileSize:        map.TileWidth,
            Layers:          layers,
            CollisionMask:   map.GetCollisionMask(),
            ZoneEntries:     zoneEntries,
            RegionExits:     regionExits,
            Labels:          labels,
            Paths:           paths);

        return new GetRegionMapHubResult { Success = true, RegionMap = dto };
    }
}
