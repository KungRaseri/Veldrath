using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

// ── Request ──────────────────────────────────────────────────────────────────

/// <summary>
/// Hub command that moves a character across a region boundary, placing them on the
/// matching entry tile in the target region's tilemap.
/// </summary>
/// <param name="CharacterId">ID of the character crossing the boundary.</param>
/// <param name="CurrentRegionId">The region the character is currently in.</param>
/// <param name="TargetRegionId">The adjacent region the character is entering.</param>
public record ChangeRegionHubCommand(Guid CharacterId, string CurrentRegionId, string TargetRegionId)
    : IRequest<ChangeRegionHubResult>;

// ── Result ───────────────────────────────────────────────────────────────────

/// <summary>Result returned by <see cref="ChangeRegionHubCommandHandler"/>.</summary>
public record ChangeRegionHubResult
{
    /// <summary>Gets a value indicating whether the region change was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the region the character has entered.</summary>
    public string NewRegionId { get; init; } = string.Empty;

    /// <summary>Gets the tile column the character spawns at on the new region map.</summary>
    public int TileX { get; init; }

    /// <summary>Gets the tile row the character spawns at on the new region map.</summary>
    public int TileY { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Handles <see cref="ChangeRegionHubCommand"/> by:
/// <list type="number">
///   <item>Loading the target region's <see cref="TiledMap"/>.</item>
///   <item>Finding the <see cref="RealmEngine.Shared.Models.TileMapDefinition.RegionExitDefinition"/>
///         in the target map whose <c>TargetRegionId</c> matches <see cref="ChangeRegionHubCommand.CurrentRegionId"/>
///         (i.e. the "door back" from the current region's perspective).</item>
///   <item>Placing the character one tile inward from that exit, or falling back to (1,1).</item>
///   <item>Persisting the new region and tile position via <see cref="IPlayerSessionRepository"/>.</item>
/// </list>
/// </summary>
public class ChangeRegionHubCommandHandler : IRequestHandler<ChangeRegionHubCommand, ChangeRegionHubResult>
{
    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly ILogger<ChangeRegionHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="ChangeRegionHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for region map data.</param>
    /// <param name="sessionRepo">Player session repository for persisting position and region state.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ChangeRegionHubCommandHandler(
        ITileMapRepository tilemapRepo,
        IPlayerSessionRepository sessionRepo,
        ILogger<ChangeRegionHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _sessionRepo = sessionRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<ChangeRegionHubResult> Handle(ChangeRegionHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetRegionId))
            return Fail("Target region ID cannot be empty.");

        var map = await _tilemapRepo.GetByRegionIdAsync(request.TargetRegionId);
        if (map is null)
        {
            _logger.LogWarning(
                "No tilemap found for target region '{TargetRegionId}' during region change",
                request.TargetRegionId);
            return Fail($"No map found for region '{request.TargetRegionId}'.");
        }

        // Find the region_exit in the target map that points back to our origin region.
        // That exit represents the "entrance" from the character's perspective.
        var entryExit = map.GetRegionExits()
            .FirstOrDefault(e => string.Equals(
                e.TargetRegionId, request.CurrentRegionId, StringComparison.OrdinalIgnoreCase));

        // Spawn one tile inward from the border exit marker, falling back to (1,1).
        var (spawnX, spawnY) = entryExit is not null
            ? SpawnInward(entryExit.TileX, entryExit.TileY, map.Width, map.Height)
            : (1, 1);

        if (entryExit is null)
            _logger.LogWarning(
                "No region_exit pointing back to '{CurrentRegionId}' in map '{TargetRegionId}' — defaulting to (1,1)",
                request.CurrentRegionId, request.TargetRegionId);

        await _sessionRepo.SetRegionAsync(request.CharacterId, request.TargetRegionId);
        await _sessionRepo.UpdatePositionAsync(request.CharacterId, spawnX, spawnY);

        _logger.LogDebug(
            "Character {Id} crossed region boundary '{From}' → '{To}' and was placed at ({X},{Y})",
            request.CharacterId, request.CurrentRegionId, request.TargetRegionId, spawnX, spawnY);

        return new ChangeRegionHubResult
        {
            Success     = true,
            NewRegionId = request.TargetRegionId,
            TileX       = spawnX,
            TileY       = spawnY,
        };
    }

    private static ChangeRegionHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    /// <summary>
    /// Given an exit tile on the border, returns a spawn coordinate one step towards
    /// the interior of the map (so the player is not immediately standing on the exit again).
    /// </summary>
    private static (int X, int Y) SpawnInward(int exitX, int exitY, int mapWidth, int mapHeight)
    {
        // Push inward based on which edge the exit is closest to.
        if (exitX == 0)               return (1, exitY);
        if (exitX == mapWidth - 1)    return (mapWidth - 2, exitY);
        if (exitY == 0)               return (exitX, 1);
        if (exitY == mapHeight - 1)   return (exitX, mapHeight - 2);
        // Exit is not on a border; just use it directly.
        return (exitX, exitY);
    }
}
