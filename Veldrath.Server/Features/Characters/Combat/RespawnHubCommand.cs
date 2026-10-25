using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data.Repositories;
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
    private readonly IZoneLocationRepository _zoneLocations;
    private readonly IOptions<DeathPenaltyOptions> _penalty;
    private readonly ILogger<RespawnHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="RespawnHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="sessions">Store for per-character combat sessions.</param>
    /// <param name="zoneLocations">Zone location catalog, used to find the zone's safe respawn point.</param>
    /// <param name="penalty">Configured death values, including the sickness window.</param>
    /// <param name="logger">Logger instance.</param>
    public RespawnHubCommandHandler(
        ICharacterRepository characterRepo,
        ICombatSessionStore sessions,
        IZoneLocationRepository zoneLocations,
        IOptions<DeathPenaltyOptions> penalty,
        ILogger<RespawnHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _sessions = sessions;
        _zoneLocations = zoneLocations;
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

        if (locationSlug is not null)
            await _characterRepo.UpdateCurrentZoneLocationAsync(request.CharacterId, locationSlug, cancellationToken);

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
    /// Finds where the character should wake up. The zone's authored safe location wins; anything else is a
    /// degradation, so it is logged as an <c>Error</c> and said out loud to the player.
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

        var locations = await _zoneLocations.GetByZoneIdAsync(entity.CurrentZoneId);
        if (locations.Count == 0)
        {
            _logger.LogError(
                "Zone {ZoneId} has no authored locations; character {CharacterIdPrefix} respawns where they fell",
                entity.CurrentZoneId, entity.Id.ToString()[..8]);
            return (null, null, true, "This zone has no place to wake up; you respawn where you fell.");
        }

        var safe = locations.FirstOrDefault(location => location.IsSafeRespawn);
        if (safe is null)
        {
            _logger.LogError(
                "Zone {ZoneId} has no location flagged SafeRespawn; falling back to {LocationSlug}",
                entity.CurrentZoneId, locations[0].Slug);
            return (locations[0].Slug, locations[0].DisplayName, true,
                "This zone has no safe respawn point; you wake at its entrance instead.");
        }

        return (safe.Slug, safe.DisplayName, false, null);
    }
}
