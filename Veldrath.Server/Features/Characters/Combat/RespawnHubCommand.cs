using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that handles a player choosing to respawn after death in normal mode.
/// Verifies the character is dead (HP at or below zero) and resets them to a safe state.
/// </summary>
/// <param name="CharacterId">The character requesting respawn.</param>
public record RespawnHubCommand(Guid CharacterId)
    : IRequest<RespawnHubResult>;

/// <summary>Result returned by <see cref="RespawnHubCommandHandler"/>.</summary>
public record RespawnHubResult
{
    /// <summary>Gets a value indicating whether the respawn succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's HP after respawning.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>Gets the character's mana after respawning.</summary>
    public int CurrentMana { get; init; }

    /// <summary>Gets the slug of the location the character woke up at, when the zone has one.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the display name of that location.</summary>
    public string? LocationDisplayName { get; init; }

    /// <summary>Gets whether the respawn point was a degraded fallback rather than an authored safe location.</summary>
    public bool UsedFallbackRespawn { get; init; }

    /// <summary>Gets the client-facing explanation when <see cref="UsedFallbackRespawn"/> is <see langword="true"/>.</summary>
    public string? Warning { get; init; }

    /// <summary>Gets when death sickness wears off, or <see langword="null"/> when none was applied.</summary>
    public DateTimeOffset? SicknessExpiresAt { get; init; }
}

/// <summary>
/// Handles <see cref="RespawnHubCommand"/> by verifying the character was killed (HP ≤ 0),
/// removing any lingering combat session, and restoring the character to minimum viable health.
/// </summary>
public class RespawnHubCommandHandler
    : IRequestHandler<RespawnHubCommand, RespawnHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ICombatSessionStore _sessions;
    private readonly IZoneLobbyResolver _lobbies;
    private readonly IMapGraphRepository _maps;
    private readonly IOptions<DeathPenaltyOptions> _penalty;
    private readonly ILogger<RespawnHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="RespawnHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="sessions">Store for per-character combat sessions.</param>
    /// <param name="lobbies">Zone lobby resolver, used to find the safe respawn point the character may use.</param>
    /// <param name="maps">Map graphs, used to name the node that represents the safe location.</param>
    /// <param name="penalty">Configured death values, including the sickness window.</param>
    /// <param name="logger">Logger instance.</param>
    public RespawnHubCommandHandler(
        ICharacterRepository characterRepo,
        ICombatSessionStore sessions,
        IZoneLobbyResolver lobbies,
        IMapGraphRepository maps,
        IOptions<DeathPenaltyOptions> penalty,
        ILogger<RespawnHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _sessions = sessions;
        _lobbies = lobbies;
        _maps = maps;
        _penalty = penalty;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the respawn outcome.</summary>
    /// <param name="request">The command containing the character's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="RespawnHubResult"/> describing the outcome.</returns>
    public async Task<RespawnHubResult> Handle(
        RespawnHubCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new RespawnHubResult { Success = false, ErrorMessage = "Character not found" };

        if (entity.DeletedAt.HasValue)
            return new RespawnHubResult { Success = false, ErrorMessage = "Hardcore character cannot respawn" };

        var attrs = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        attrs.TryGetValue("CurrentHealth", out int currentHp);

        if (currentHp > 0)
            return new RespawnHubResult { Success = false, ErrorMessage = "Character is not dead" };

        int maxHealth = attrs.TryGetValue("MaxHealth", out var mh) ? mh : entity.Level * 10;
        int maxMana = attrs.TryGetValue("MaxMana", out var mm) ? mm : entity.Level * 5;

        // Restore to 25% of max HP and full mana.
        int respawnHp = Math.Max(1, maxHealth / 4);
        int respawnMana = maxMana;

        attrs["CurrentHealth"] = respawnHp;
        attrs["CurrentMana"] = respawnMana;

        var (locationSlug, locationName, usedFallback, warning) = await ResolveRespawnLocationAsync(entity, cancellationToken);

        var options = _penalty.Value;
        DateTimeOffset? sicknessExpiresAt = null;
        if (options.DeathSicknessMinutes > 0 && options.DeathSicknessDamagePenaltyPercent > 0)
        {
            sicknessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(options.DeathSicknessMinutes);
            attrs[DeathPenalties.SicknessUntilKey] = DeathPenalties.ToSicknessStamp(sicknessExpiresAt.Value);
            attrs[DeathPenalties.SicknessPenaltyKey] = options.DeathSicknessDamagePenaltyPercent;
        }
        else
        {
            // A disabled sickness must not leave a stale window behind from an earlier death.
            attrs.Remove(DeathPenalties.SicknessUntilKey);
            attrs.Remove(DeathPenalties.SicknessPenaltyKey);
        }

        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        if (locationSlug is not null && !string.IsNullOrWhiteSpace(entity.CurrentZoneId))
            await MoveToLocationAsync(entity, entity.CurrentZoneId, locationSlug, cancellationToken);

        await _sessions.RemoveAsync(request.CharacterId, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} respawned at {LocationSlug} with {Hp}/{MaxHp} HP",
            request.CharacterId.ToString()[..8], locationSlug ?? "(unchanged)", respawnHp, maxHealth);

        return new RespawnHubResult
        {
            Success = true,
            CurrentHealth = respawnHp,
            CurrentMana = respawnMana,
            LocationSlug = locationSlug,
            LocationDisplayName = locationName,
            UsedFallbackRespawn = usedFallback,
            Warning = warning,
            SicknessExpiresAt = sicknessExpiresAt,
        };
    }

    /// <summary>
    /// Moves the character onto the node that represents <paramref name="locationSlug"/> inside their zone.
    /// Writing the location slug alone is not enough: a client that logs in again resumes from the map node, so a
    /// respawn that moved only the slug put the player back at the zone's arrival node on the next reload.
    /// </summary>
    /// <param name="entity">The character that died.</param>
    /// <param name="zoneId">The zone the character died in; the map that names the location's node.</param>
    /// <param name="locationSlug">The zone location the character wakes up at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task MoveToLocationAsync(
        Data.Entities.Character entity,
        string zoneId,
        string locationSlug,
        CancellationToken cancellationToken)
    {
        var zoneMap = await _maps.GetMapForZoneAsync(zoneId, cancellationToken);
        var locationNode = zoneMap?.Nodes.FirstOrDefault(node =>
            node.NodeType == MapNodeType.Location &&
            string.Equals(node.RefId, locationSlug, StringComparison.OrdinalIgnoreCase));

        if (zoneMap is null || locationNode is null)
        {
            // No map to name the node — a zone without a graph, or a location it never placed. The player still has
            // to move, so the slug is written on its own; the missing node is content debt and is reported as such.
            _logger.LogError(
                "Zone {ZoneId} has no map node for respawn location {LocationSlug}; character {CharacterIdPrefix} keeps their old node",
                zoneId, locationSlug, entity.Id.ToString()[..8]);
            await _characterRepo.UpdateCurrentZoneLocationAsync(entity.Id, locationSlug, cancellationToken);
            return;
        }

        await _characterRepo.UpdateMapPositionAsync(
            entity.Id,
            zoneMap.Id,
            locationNode.Id,
            zoneId,
            locationSlug,
            cancellationToken);
    }

    /// <summary>
    /// Finds where the character should wake up. The zone's authored safe location wins, read through the
    /// character's own unlocks so a hidden lobby they have opened is found; anything else is a degradation, so it
    /// is logged as an <c>Error</c> and said out loud to the player.
    /// </summary>
    /// <param name="entity">Character that died.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The location to move to (null to stay put), and how degraded that choice was.</returns>
    private async Task<(string? Slug, string? Name, bool UsedFallback, string? Warning)> ResolveRespawnLocationAsync(
        Data.Entities.Character entity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.CurrentZoneId))
            return (null, null, false, null);

        var lobby = await _lobbies.ResolveAsync(entity.CurrentZoneId, entity.Id, cancellationToken);

        switch (lobby.Status)
        {
            case ZoneLobbyStatus.Resolved when lobby.Lobby is { } safe:
                return (safe.Slug, safe.DisplayName, false, null);

            case ZoneLobbyStatus.NotUnlocked when lobby.LockedLobby is { } locked:
                // The zone has a lobby, but it is hidden from this character. Say that rather than blaming the zone
                // for having none: the fallback is still the entrance, and the wording is the difference between
                // "broken content" and "not open to you yet".
                _logger.LogError(
                    "Zone {ZoneId} has a hidden safe respawn location {LocationSlug} that character {CharacterIdPrefix} has not unlocked; falling back to {FallbackSlug}",
                    entity.CurrentZoneId, locked.Slug, entity.Id.ToString()[..8],
                    lobby.Fallback?.Slug ?? "where they fell");
                return lobby.Fallback is { } fallback
                    ? (fallback.Slug, fallback.DisplayName, true,
                        "This zone's safe haven is not yet open to you; you wake at its entrance instead.")
                    : (null, null, true,
                        "This zone's safe haven is not yet open to you; you respawn where you fell.");

            case ZoneLobbyStatus.NoSafeLocation when lobby.Fallback is { } entrance:
                _logger.LogError(
                    "Zone {ZoneId} has no location flagged SafeRespawn; falling back to {LocationSlug}",
                    entity.CurrentZoneId, entrance.Slug);
                return (entrance.Slug, entrance.DisplayName, true,
                    "This zone has no safe respawn point; you wake at its entrance instead.");

            default:
                _logger.LogError(
                    "Zone {ZoneId} has no location a respawn can use for character {CharacterIdPrefix} ({Status}); they respawn where they fell",
                    entity.CurrentZoneId, entity.Id.ToString()[..8], lobby.Status);
                return (null, null, true, "This zone has no place to wake up; you respawn where you fell.");
        }
    }
}
