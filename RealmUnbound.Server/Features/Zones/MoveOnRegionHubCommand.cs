using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Contracts.Tilemap;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

// ── Request ──────────────────────────────────────────────────────────────────

/// <summary>
/// Hub command that moves a character one tile on the region map,
/// validates walkability and rate-limiting, persists the new tile position, and
/// detects any zone-entry or region-exit tiles at the destination.
/// </summary>
/// <param name="CharacterId">ID of the character to move.</param>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="Direction">Facing direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, <c>"right"</c>.</param>
/// <param name="RegionId">The region this character is currently navigating.</param>
public record MoveOnRegionHubCommand(Guid CharacterId, int ToX, int ToY, string Direction, string RegionId)
    : IRequest<MoveOnRegionHubResult>;

// ── Result ───────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Gets the zone-entry object the character stepped on, or <see langword="null"/>
    /// if no zone transition is triggered.
    /// </summary>
    public ZoneObjectDto? ZoneEntryTriggered { get; init; }

    /// <summary>
    /// Gets the region-exit object the character stepped on, or <see langword="null"/>
    /// if no region crossing is triggered.
    /// </summary>
    public RegionExitDto? RegionExitTriggered { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Handles <see cref="MoveOnRegionHubCommand"/> by enforcing:
/// <list type="bullet">
///   <item>Exactly one-tile Manhattan step (|Δx|+|Δy| == 1).</item>
///   <item>100 ms per-character move rate limit (checked via <see cref="IPlayerSessionRepository"/>).</item>
///   <item>Walkability (collision mask loaded from <see cref="ITileMapRepository"/>).</item>
/// </list>
/// On success, updates <see cref="Data.Entities.PlayerSession"/> position and checks for
/// zone-entry and region-exit objects at the destination.
/// </summary>
public class MoveOnRegionHubCommandHandler : IRequestHandler<MoveOnRegionHubCommand, MoveOnRegionHubResult>
{
    private static readonly TimeSpan MoveCooldown     = TimeSpan.FromMilliseconds(100);
    private static readonly HashSet<string> ValidDirs = ["up", "down", "left", "right"];

    private readonly ITileMapRepository _tilemapRepo;
    private readonly IPlayerSessionRepository _sessionRepo;
    private readonly ILogger<MoveOnRegionHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="MoveOnRegionHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for region map data.</param>
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

        if (!ValidDirs.Contains(dir))
            return Fail("Invalid direction.");

        // ── Verify we have an active session and read current position ───────
        var session = await _sessionRepo.GetByCharacterIdAsync(request.CharacterId);
        if (session is null)
            return Fail("No active session found.");

        var fromX = session.TileX;
        var fromY = session.TileY;
        var dx    = request.ToX - fromX;
        var dy    = request.ToY - fromY;

        if (Math.Abs(dx) + Math.Abs(dy) != 1)
            return Fail("Move must be exactly one tile.");

        // ── Rate limit ───────────────────────────────────────────────────────
        var elapsed = DateTimeOffset.UtcNow - session.LastMovedAt;
        if (elapsed < MoveCooldown)
            return Fail("Move too fast.");

        // ── Collision check ──────────────────────────────────────────────────
        var map = await _tilemapRepo.GetByRegionIdAsync(request.RegionId);
        if (map is not null && map.IsBlocked(request.ToX, request.ToY))
            return Fail("Tile is blocked.");

        // ── Persist new position ─────────────────────────────────────────────
        await _sessionRepo.UpdatePositionAsync(request.CharacterId, request.ToX, request.ToY);
        await _sessionRepo.UpdateLastMovedAtAsync(request.CharacterId, DateTimeOffset.UtcNow);

        // ── Zone-entry check ─────────────────────────────────────────────────
        ZoneObjectDto? zoneEntry = null;
        RegionExitDto? regionExit = null;
        if (map is not null)
        {
            var zoneEntries = map.GetZoneEntries();
            var matched     = zoneEntries.FirstOrDefault(e => e.TileX == request.ToX && e.TileY == request.ToY);
            if (matched is not null)
                zoneEntry = new ZoneObjectDto(matched.TileX, matched.TileY, matched.ZoneSlug, matched.DisplayName, matched.MinLevel, matched.MaxLevel);

            if (zoneEntry is null)
            {
                var exits      = map.GetRegionExits();
                var matchedExit = exits.FirstOrDefault(e => e.TileX == request.ToX && e.TileY == request.ToY);
                if (matchedExit is not null)
                    regionExit = new RegionExitDto(matchedExit.TileX, matchedExit.TileY, matchedExit.TargetRegionId);
            }
        }

        _logger.LogDebug(
            "Character {Id} moved on region {Region} to ({X},{Y}) facing {Dir}",
            request.CharacterId, request.RegionId, request.ToX, request.ToY, dir);

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

// ── Hub request DTO ───────────────────────────────────────────────────────────

/// <summary>Request DTO sent by the client when calling the <c>MoveOnRegion</c> hub method.</summary>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="Direction">Cardinal direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, <c>"right"</c>.</param>
public record MoveOnRegionHubRequest(int ToX, int ToY, string Direction);
