using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;
using RealmUnbound.Contracts.Tilemap;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

// ── Request ─────────────────────────────────────────────────────────────────

/// <summary>
/// Hub command that moves a character one tile in the given direction,
/// validates walkability and rate-limiting, persists the new position, and
/// returns a <see cref="MoveCharacterHubResult"/> so the hub can broadcast
/// <see cref="CharacterMovedPayload"/> and/or <see cref="ExitTileDto"/> events.
/// </summary>
/// <param name="CharacterId">ID of the character to move.</param>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="Direction">Facing direction (e.g. <c>"down"</c>, <c>"up"</c>, <c>"left"</c>, <c>"right"</c>).</param>
/// <param name="ZoneId">The zone the character is currently in.</param>
public record MoveCharacterHubCommand(Guid CharacterId, int ToX, int ToY, string Direction, string ZoneId)
    : IRequest<MoveCharacterHubResult>;

// ── Result ───────────────────────────────────────────────────────────────────

/// <summary>Result returned by <see cref="MoveCharacterHubCommandHandler"/>.</summary>
public record MoveCharacterHubResult
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

    /// <summary>Gets the exit tile the character stepped on, or <see langword="null"/> if no transition is triggered.</summary>
    public ExitTileDto? ExitTriggered { get; init; }
}

// ── Handler ──────────────────────────────────────────────────────────────────

/// <summary>
/// Handles <see cref="MoveCharacterHubCommand"/> by enforcing:
/// <list type="bullet">
///   <item>Exactly one-tile Manhattan step (|Δx|+|Δy| == 1).</item>
///   <item>100 ms per-character move rate limit (checked via <see cref="IZoneSessionRepository"/>).</item>
///   <item>Walkability (collision mask loaded from <see cref="ITileMapRepository"/>).</item>
/// </list>
/// On success, persists the new tile position via <see cref="ICharacterRepository"/> and updates
/// <see cref="Data.Entities.ZoneSession.LastMovedAt"/> via <see cref="IZoneSessionRepository"/>.
/// </summary>
public class MoveCharacterHubCommandHandler : IRequestHandler<MoveCharacterHubCommand, MoveCharacterHubResult>
{
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(100);
    private static readonly HashSet<string> ValidDirections = ["up", "down", "left", "right"];

    private readonly ITileMapRepository _tilemapRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly IZoneSessionRepository _sessionRepo;
    private readonly ILogger<MoveCharacterHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="MoveCharacterHubCommandHandler"/>.</summary>
    /// <param name="tilemapRepo">Tilemap repository for collision data.</param>
    /// <param name="characterRepo">Character repository to persist the new tile position.</param>
    /// <param name="sessionRepo">Zone session repository for rate-limit tracking.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MoveCharacterHubCommandHandler(
        ITileMapRepository tilemapRepo,
        ICharacterRepository characterRepo,
        IZoneSessionRepository sessionRepo,
        ILogger<MoveCharacterHubCommandHandler> logger)
    {
        _tilemapRepo    = tilemapRepo;
        _characterRepo  = characterRepo;
        _sessionRepo    = sessionRepo;
        _logger         = logger;
    }

    /// <inheritdoc/>
    public async Task<MoveCharacterHubResult> Handle(MoveCharacterHubCommand request, CancellationToken cancellationToken)
    {
        var dir = request.Direction.ToLowerInvariant();

        // ── Input sanity ────────────────────────────────────────────────────
        if (!ValidDirections.Contains(dir))
            return Fail("Invalid direction.");

        // ── Load character current position ─────────────────────────────────
        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail("Character not found.");

        var fromX = character.TileX;
        var fromY = character.TileY;
        var dx    = request.ToX - fromX;
        var dy    = request.ToY - fromY;

        // Exactly one cardinal step
        if (Math.Abs(dx) + Math.Abs(dy) != 1)
            return Fail("Move must be exactly one tile.");

        // ── Rate limit ──────────────────────────────────────────────────────
        var session = await _sessionRepo.GetByCharacterIdAsync(request.CharacterId);
        if (session is not null)
        {
            var elapsed = DateTimeOffset.UtcNow - session.LastMovedAt;
            if (elapsed < MoveCooldown)
                return Fail("Move too fast.");
        }

        // ── Collision check ─────────────────────────────────────────────────
        var map = await _tilemapRepo.GetByZoneIdAsync(request.ZoneId);
        if (map is not null && map.IsBlocked(request.ToX, request.ToY))
            return Fail("Tile is blocked.");

        // ── Persist ─────────────────────────────────────────────────────────
        await _characterRepo.UpdateTilePositionAsync(
            request.CharacterId, request.ToX, request.ToY, request.ZoneId, cancellationToken);

        await _sessionRepo.UpdateLastMovedAtAsync(request.CharacterId, DateTimeOffset.UtcNow);

        // ── Exit tile check ─────────────────────────────────────────────────
        ExitTileDto? exitTriggered = null;
        if (map is not null)
        {
            var exit = map.GetExitTiles().FirstOrDefault(e => e.TileX == request.ToX && e.TileY == request.ToY);
            if (exit is not null)
                exitTriggered = new ExitTileDto(exit.TileX, exit.TileY, exit.ToZoneId);
        }

        _logger.LogDebug(
            "Character {Id} moved to ({X},{Y}) facing {Dir} in zone {Zone}",
            request.CharacterId, request.ToX, request.ToY, dir, request.ZoneId);

        return new MoveCharacterHubResult
        {
            Success        = true,
            TileX          = request.ToX,
            TileY          = request.ToY,
            Direction      = dir,
            ExitTriggered  = exitTriggered,
        };
    }

    private static MoveCharacterHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

// ── Hub request DTO ──────────────────────────────────────────────────────────

/// <summary>Request DTO sent by the client when calling <see cref="GameHub.MoveCharacter"/>.</summary>
/// <param name="ToX">Target tile column.</param>
/// <param name="ToY">Target tile row.</param>
/// <param name="Direction">Cardinal direction: <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, <c>"right"</c>.</param>
public record MoveCharacterHubRequest(int ToX, int ToY, string Direction);
