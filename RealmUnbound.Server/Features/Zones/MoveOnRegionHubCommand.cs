using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Contracts.Tilemap;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that moves a character one tile on the region map,
/// validates walkability and rate-limiting, persists the new position, and
/// returns trigger information when the character steps onto a zone entry or region exit tile.
/// </summary>
/// <param name="CharacterId">ID of the character to move.</param>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="Direction">Facing direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, or <c>"right"</c>.</param>
/// <param name="RegionId">The region the character is currently navigating.</param>
public record MoveOnRegionHubCommand(Guid CharacterId, int ToX, int ToY, string Direction, string RegionId)
    : IRequest<MoveOnRegionHubResult>;

/// <summary>Result returned by <see cref="MoveOnRegionHubCommandHandler"/>.</summary>
public record MoveOnRegionHubResult
{
    /// <summary>Gets a value indicating whether the move was accepted.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the tile column the character moved to.</summary>
    public int TileX { get; init; }

    /// <summary>Gets the tile row the character moved to.</summary>
    public int TileY { get; init; }

    /// <summary>Gets the direction the character is now facing.</summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>Gets the zone entry object the character stepped on, or <see langword="null"/> if none.</summary>
    public ZoneObjectDto? ZoneEntryTriggered { get; init; }

    /// <summary>Gets the region exit the character stepped on, or <see langword="null"/> if none.</summary>
    public RegionExitDto? RegionExitTriggered { get; init; }
}

/// <summary>
/// Handles <see cref="MoveOnRegionHubCommand"/> by enforcing:
/// <list type="bullet">
///   <item>Exactly one-tile Manhattan step (|Δx|+|Δy| == 1).</item>
///   <item>100 ms per-character move rate limit.</item>
///   <item>Walkability via the region collision mask.</item>
/// </list>
/// On success, persists the new tile position and checks for zone entry and region exit triggers.
/// </summary>
public class MoveOnRegionHubCommandHandler : IRequestHandler<MoveOnRegionHubCommand, MoveOnRegionHubResult>
{
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(100);
    private static readonly HashSet<string> ValidDirections = ["up", "down", "left", "right"];

    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly ILogger<MoveOnRegionHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="MoveOnRegionHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for collision data.</param>
    /// <param name="sessionRepo">Player session repository for rate-limit tracking and position persistence.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MoveOnRegionHubCommandHandler(
        ITileMapRepository tilemapRepo,
        IPlayerSessionRepository sessionRepo,
        ILogger<MoveOnRegionHubCommandHandler> logger)
    {
        _tilemapRepo = tilemapRepo;
        _sessionRepo = sessionRepo;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<MoveOnRegionHubResult> Handle(MoveOnRegionHubCommand request, CancellationToken cancellationToken)
    {
        var dir = request.Direction.ToLowerInvariant();

        if (!ValidDirections.Contains(dir))
            return Fail("Invalid direction.");

        // ── Rate limit ──────────────────────────────────────────────────────
        var session = await _sessionRepo.GetByCharacterIdAsync(request.CharacterId);
        if (session is not null)
        {
            var elapsed = DateTimeOffset.UtcNow - session.LastMovedAt;
            if (elapsed < MoveCooldown)
                return Fail("Move too fast.");

            var fromX = session.TileX;
            var fromY = session.TileY;
            var dx    = request.ToX - fromX;
            var dy    = request.ToY - fromY;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return Fail("Move must be exactly one tile.");
        }

        // ── Collision check ─────────────────────────────────────────────────
        var map = await _tilemapRepo.GetByRegionIdAsync(request.RegionId);
        if (map is not null && map.IsBlocked(request.ToX, request.ToY))
            return Fail("Tile is blocked.");

        // ── Persist ─────────────────────────────────────────────────────────
        await _sessionRepo.UpdatePositionAsync(request.CharacterId, request.ToX, request.ToY);
        await _sessionRepo.UpdateLastMovedAtAsync(request.CharacterId, DateTimeOffset.UtcNow);

        // ── Trigger checks ──────────────────────────────────────────────────
        ZoneObjectDto? zoneEntry  = null;
        RegionExitDto? regionExit = null;

        if (map is not null)
        {
            var zoneObj = map.GetZoneObjects()
                             .FirstOrDefault(z => z.TileX == request.ToX && z.TileY == request.ToY);
            if (zoneObj is not null)
                zoneEntry = new ZoneObjectDto(zoneObj.ZoneId, zoneObj.DisplayName, zoneObj.MinLevel, zoneObj.MaxLevel, zoneObj.TileX, zoneObj.TileY);

            var exit = map.GetRegionExits()
                          .FirstOrDefault(r => r.TileX == request.ToX && r.TileY == request.ToY);
            if (exit is not null)
                regionExit = new RegionExitDto(exit.ToRegionId, exit.TileX, exit.TileY);
        }

        _logger.LogDebug(
            "Character {Id} moved to ({X},{Y}) facing {Dir} on region {Region}",
            request.CharacterId, request.ToX, request.ToY, dir, request.RegionId);

        return new MoveOnRegionHubResult
        {
            Success              = true,
            TileX                = request.ToX,
            TileY                = request.ToY,
            Direction            = dir,
            ZoneEntryTriggered   = zoneEntry,
            RegionExitTriggered  = regionExit,
        };
    }

    private static MoveOnRegionHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

// ── Hub request DTO ──────────────────────────────────────────────────────────

/// <summary>Payload sent by the client when moving one tile on the region map.</summary>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="Direction">Facing direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, or <c>"right"</c>.</param>
public record MoveOnRegionHubRequest(int ToX, int ToY, string Direction);
