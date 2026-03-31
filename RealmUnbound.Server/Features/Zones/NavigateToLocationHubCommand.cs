using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters.Combat;

namespace RealmUnbound.Server.Features.Zones;

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
    public string? LocationType { get; init; }

    /// <summary>Gets the traversal edges available from the arrived location.</summary>
    public IReadOnlyList<ZoneLocationConnectionEntry> AvailableConnections { get; init; } = [];

    /// <summary>Gets hidden locations newly discovered by the passive skill-check sweep.</summary>
    public IReadOnlyList<ZoneLocationEntry> PassiveDiscoveries { get; init; } = [];

    /// <summary>Gets the live enemies currently present at the arrived location.</summary>
    public IReadOnlyList<SpawnedEnemySummary> SpawnedEnemies { get; init; } = [];
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
/// location's enemy roster is spawned in <see cref="ZoneLocationEnemyStore"/>.
/// </summary>
public class NavigateToLocationHubCommandHandler
    : IRequestHandler<NavigateToLocationHubCommand, NavigateToLocationHubResult>
{
    private const string PassiveCheckType = "skill_check_passive";

    private readonly IZoneLocationRepository _locationRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterUnlockedLocationRepository _unlockedRepo;
    private readonly ActorPoolResolver _actorPoolResolver;
    private readonly ILogger<NavigateToLocationHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="NavigateToLocationHubCommandHandler"/>.</summary>
    /// <param name="locationRepo">Repository used to look up zone location catalog entries.</param>
    /// <param name="characterRepo">Repository used to persist the character's current location.</param>
    /// <param name="unlockedRepo">Repository used to persist passive discovery unlock records.</param>
    /// <param name="actorPoolResolver">Resolver used to spawn the initial enemy roster for a location.</param>
    /// <param name="logger">Logger instance.</param>
    public NavigateToLocationHubCommandHandler(
        IZoneLocationRepository locationRepo,
        ICharacterRepository characterRepo,
        ICharacterUnlockedLocationRepository unlockedRepo,
        ActorPoolResolver actorPoolResolver,
        ILogger<NavigateToLocationHubCommandHandler> logger)
    {
        _locationRepo      = locationRepo;
        _characterRepo     = characterRepo;
        _unlockedRepo      = unlockedRepo;
        _actorPoolResolver = actorPoolResolver;
        _logger            = logger;
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
                Success      = false,
                ErrorMessage = $"Location '{request.LocationSlug}' is not available in zone '{request.ZoneId}'",
            };

        await _characterRepo.UpdateCurrentZoneLocationAsync(request.CharacterId, location.Slug, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} navigated to {LocationSlug} in zone {ZoneId}",
            request.CharacterId.ToString()[..8], location.Slug, request.ZoneId);

        // Load connections from the arrived location.
        var connections = await _locationRepo.GetConnectionsFromAsync(location.Slug);

        // Passive discovery sweep: unlock hidden locations the character is high-level enough to notice.
        var passiveDiscoveries = await RunPassiveDiscoveryAsync(
            request.CharacterId, request.ZoneId, unlockedSlugs, cancellationToken);

        // Ensure the enemy roster for this location is spawned; reuse existing if already present.
        var storeKey = ZoneLocationEnemyStore.MakeKey(request.ZoneGroup, location.Slug);
        if (!string.IsNullOrEmpty(request.ZoneGroup) && !ZoneLocationEnemyStore.HasRoster(storeKey))
        {
            var roster = await _actorPoolResolver.SpawnRosterAsync(location.ActorPool);
            ZoneLocationEnemyStore.TryAddRoster(storeKey, roster);

            _logger.LogInformation(
                "Spawned {Count} enemies at {StoreKey}",
                roster.Count, storeKey);
        }

        var spawnedEnemies = string.IsNullOrEmpty(request.ZoneGroup)
            ? []
            : ZoneLocationEnemyStore.GetAlive(storeKey)
                .Select(e => new SpawnedEnemySummary(e.Id, e.Name, e.Level, e.CurrentHealth, e.MaxHealth))
                .ToList();

        return new NavigateToLocationHubResult
        {
            Success              = true,
            LocationSlug         = location.Slug,
            LocationDisplayName  = location.DisplayName,
            LocationType         = location.LocationType,
            AvailableConnections = connections,
            PassiveDiscoveries   = passiveDiscoveries,
            SpawnedEnemies       = spawnedEnemies,
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

