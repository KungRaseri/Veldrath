using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// Hub command that moves a character to a specific <see cref="RealmEngine.Data.Entities.ZoneLocation"/>
/// within their current zone, validating that the location exists and belongs to that zone.
/// Also performs a passive skill-check discovery sweep for any hidden locations in the zone.
/// </summary>
/// <param name="CharacterId">The ID of the character navigating.</param>
/// <param name="LocationSlug">The slug of the target zone location.</param>
/// <param name="ZoneId">The ID of the zone the character is currently in.</param>
/// <param name="ZoneGroup">The SignalR group name for the zone, used to key the enemy store.</param>
public record NavigateToLocationHubCommand(Guid CharacterId, string LocationSlug, string ZoneId, string ZoneGroup = "")
    : IRequest<NavigateToLocationHubResult>;

/// <summary>Result returned by <see cref="NavigateToLocationHubCommandHandler"/>.</summary>
public record NavigateToLocationHubResult
{
    /// <summary>Gets a value indicating whether the navigation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the slug of the location navigated to, or <see langword="null"/> on failure.</summary>
    public string? LocationSlug { get; init; }

    /// <summary>Gets the display name of the location, or <see langword="null"/> on failure.</summary>
    public string? LocationDisplayName { get; init; }

    /// <summary>Gets the location type (e.g. "dungeon", "location", "environment"), or <see langword="null"/> on failure.</summary>
    public string? TypeKey { get; init; }

    /// <summary>Gets hidden locations newly discovered by the passive skill-check sweep.</summary>
    public IReadOnlyList<ZoneLocationEntry> PassiveDiscoveries { get; init; } = [];

    /// <summary>Gets the live enemies currently present at the arrived location.</summary>
    public IReadOnlyList<SpawnedEnemySummary> SpawnedEnemies { get; init; } = [];

    /// <summary>
    /// Gets content problems that affected this arrival — for example a spawn pool that references no known actors.
    /// Surface them to the player; they mean an operator has content to fix.
    /// </summary>
    public IReadOnlyList<string> ContentWarnings { get; init; } = [];

    /// <summary>Gets the tile column of the location on the zone tilemap, or <see langword="null"/>.</summary>
    public int? TileX { get; init; }

    /// <summary>Gets the tile row of the location on the zone tilemap, or <see langword="null"/>.</summary>
    public int? TileY { get; init; }
}

/// <summary>A lightweight snapshot of a live enemy visible to all players at a location.</summary>
/// <param name="Id">Unique instance identifier for this spawned enemy.</param>
/// <param name="Name">Display name of the enemy archetype.</param>
/// <param name="Level">Difficulty level of the enemy.</param>
/// <param name="CurrentHealth">Remaining HP at the time of the snapshot.</param>
/// <param name="MaxHealth">Maximum HP of the enemy.</param>
public record SpawnedEnemySummary(Guid Id, string Name, int Level, int CurrentHealth, int MaxHealth);

/// <summary>
/// Handles <see cref="NavigateToLocationHubCommand"/> by verifying the location exists within the
/// character's current zone via <see cref="IZoneLocationRepository"/>, persisting the new current
/// location via <see cref="ICharacterRepository"/>, loading available connections from the arrived
/// location, running a passive discovery sweep for hidden locations in the zone, and ensuring the
/// location's enemy roster is loaded from <see cref="IEnemyInstanceStore"/>.
/// </summary>
public class NavigateToLocationHubCommandHandler
    : IRequestHandler<NavigateToLocationHubCommand, NavigateToLocationHubResult>
{
    private const string PassiveCheckType = "skill_check_passive";

    private readonly IZoneLocationRepository _locationRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterUnlockedLocationRepository _unlockedRepo;
    private readonly ActorPoolResolver _actorPoolResolver;
    private readonly IEnemyInstanceStore _enemyStore;
    private readonly ILogger<NavigateToLocationHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="NavigateToLocationHubCommandHandler"/>.</summary>
    /// <param name="locationRepo">Repository used to look up zone location catalog entries.</param>
    /// <param name="characterRepo">Repository used to persist the character's current location.</param>
    /// <param name="unlockedRepo">Repository used to persist passive discovery unlock records.</param>
    /// <param name="actorPoolResolver">Resolver used to spawn the initial enemy roster for a location.</param>
    /// <param name="enemyStore">Store for the live enemy instances at the location.</param>
    /// <param name="logger">Logger instance.</param>
    public NavigateToLocationHubCommandHandler(
        IZoneLocationRepository locationRepo,
        ICharacterRepository characterRepo,
        ICharacterUnlockedLocationRepository unlockedRepo,
        ActorPoolResolver actorPoolResolver,
        IEnemyInstanceStore enemyStore,
        ILogger<NavigateToLocationHubCommandHandler> logger)
    {
        _locationRepo = locationRepo;
        _characterRepo = characterRepo;
        _unlockedRepo = unlockedRepo;
        _actorPoolResolver = actorPoolResolver;
        _enemyStore = enemyStore;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the navigation outcome.</summary>
    /// <param name="request">The command containing the character ID, location slug, and zone ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="NavigateToLocationHubResult"/> describing the outcome.</returns>
    public async Task<NavigateToLocationHubResult> Handle(
        NavigateToLocationHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LocationSlug))
            return new NavigateToLocationHubResult { Success = false, ErrorMessage = "Location slug cannot be empty" };

        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return new NavigateToLocationHubResult { Success = false, ErrorMessage = "Zone ID cannot be empty" };

        // Determine which locations are visible to this character (non-hidden + unlocked hidden).
        var unlockedSlugs = await _unlockedRepo.GetUnlockedSlugsAsync(request.CharacterId, cancellationToken);
        var locationsInZone = await _locationRepo.GetByZoneIdAsync(request.ZoneId, unlockedSlugs);

        var location = locationsInZone.FirstOrDefault(l =>
            l.Slug.Equals(request.LocationSlug, StringComparison.OrdinalIgnoreCase));

        if (location is null)
            return new NavigateToLocationHubResult
            {
                Success = false,
                ErrorMessage = $"Location '{request.LocationSlug}' is not available in zone '{request.ZoneId}'",
            };

        await _characterRepo.UpdateCurrentZoneLocationAsync(request.CharacterId, location.Slug, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} navigated to {LocationSlug} in zone {ZoneId}",
            request.CharacterId.ToString()[..8], location.Slug, request.ZoneId);

        // Passive discovery sweep: unlock hidden locations the character is high-level enough to notice.
        var passiveDiscoveries = await RunPassiveDiscoveryAsync(
            request.CharacterId, request.ZoneId, unlockedSlugs, cancellationToken);

        // Load the roster, spawning it on first arrival. The store revives dead rows whose respawn time has
        // elapsed, so arriving here is also how a wiped world comes back.
        var spawnedEnemies = new List<SpawnedEnemySummary>();
        var contentWarnings = new List<string>();

        if (!string.IsNullOrEmpty(request.ZoneGroup))
        {
            var pool = location.ActorPool ?? [];

            var alive = await _enemyStore.GetAliveOrSpawnAsync(
                request.ZoneGroup,
                location.Slug,
                _ => _actorPoolResolver.SpawnRosterAsync(pool),
                cancellationToken);

            spawnedEnemies = alive
                .Select(e => new SpawnedEnemySummary(e.Id, e.Name, e.Level, e.CurrentHealth, e.MaxHealth))
                .ToList();

            // Failsafe, not fallback: an authored spawn pool that produced nothing means the content is broken.
            // The player gets a message and the operator gets the resolver's Error log naming the bad slug.
            if (spawnedEnemies.Count == 0 && pool.Count > 0)
            {
                contentWarnings.Add(
                    $"Content error: the spawn pool for '{location.Slug}' references no known actors. Please report this to a staff member.");
            }

            _logger.LogInformation(
                "{Count} enemies present at {ZoneGroup}/{LocationSlug}",
                spawnedEnemies.Count, request.ZoneGroup, location.Slug);
        }

        return new NavigateToLocationHubResult
        {
            Success = true,
            LocationSlug = location.Slug,
            LocationDisplayName = location.DisplayName,
            TypeKey = location.TypeKey,
            PassiveDiscoveries = passiveDiscoveries,
            SpawnedEnemies = spawnedEnemies,
            ContentWarnings = contentWarnings,
            TileX = location.TileX,
            TileY = location.TileY,
        };
    }

    private async Task<IReadOnlyList<ZoneLocationEntry>> RunPassiveDiscoveryAsync(
        Guid characterId,
        string zoneId,
        HashSet<string> alreadyUnlocked,
        CancellationToken cancellationToken)
    {
        var character = await _characterRepo.GetByIdAsync(characterId, cancellationToken);
        if (character is null) return [];

        var hiddenLocations = await _locationRepo.GetHiddenByZoneIdAsync(zoneId);
        var candidates = hiddenLocations
            .Where(l => string.Equals(l.UnlockType, PassiveCheckType, StringComparison.OrdinalIgnoreCase)
                        && !alreadyUnlocked.Contains(l.Slug))
            .ToList();

        var discovered = new List<ZoneLocationEntry>();

        foreach (var loc in candidates)
        {
            var threshold = loc.DiscoverThreshold.GetValueOrDefault(int.MaxValue);
            if (character.Level < threshold) continue;

            await _unlockedRepo.AddUnlockAsync(characterId, loc.Slug, PassiveCheckType, cancellationToken);
            discovered.Add(loc);

            _logger.LogInformation(
                "Character {CharacterIdPrefix} passively discovered {LocationSlug} (level {Level} >= threshold {Threshold})",
                characterId.ToString()[..8], loc.Slug, character.Level, threshold);
        }

        return discovered;
    }
}

