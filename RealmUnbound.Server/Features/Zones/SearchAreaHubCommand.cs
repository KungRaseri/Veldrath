using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

/// <summary>
/// Hub command that performs an active area search, rolling against the
/// <see cref="ZoneLocationEntry.DiscoverThreshold"/> of hidden locations in the zone.
/// Each hidden location with <c>UnlockType = "skill_check_active"</c> is checked individually.
/// </summary>
/// <param name="CharacterId">The character performing the search.</param>
/// <param name="ZoneId">The zone being searched.</param>
public record SearchAreaHubCommand(Guid CharacterId, string ZoneId)
    : IRequest<SearchAreaHubResult>;

/// <summary>A single location discovered during an active area search.</summary>
/// <param name="Slug">Slug of the discovered location.</param>
/// <param name="DisplayName">Display name of the discovered location.</param>
/// <param name="LocationType">Type of the discovered location.</param>
public record DiscoveredLocation(string Slug, string DisplayName, string LocationType);

/// <summary>Result returned by <see cref="SearchAreaHubCommandHandler"/>.</summary>
public record SearchAreaHubResult
{
    /// <summary>Gets a value indicating whether the search concluded without error.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the final roll value checked against each location's discover threshold.</summary>
    public int RollValue { get; init; }

    /// <summary>Gets whether the search turned up at least one new location.</summary>
    public bool AnyFound { get; init; }

    /// <summary>Gets the locations newly discovered by this search.</summary>
    public IReadOnlyList<DiscoveredLocation> Discovered { get; init; } = [];
}

/// <summary>
/// Handles <see cref="SearchAreaHubCommand"/> by rolling against hidden locations with
/// <c>UnlockType = "skill_check_active"</c> and unlocking those whose threshold the roll meets.
/// </summary>
public class SearchAreaHubCommandHandler
    : IRequestHandler<SearchAreaHubCommand, SearchAreaHubResult>
{
    private const string ActiveCheckType = "skill_check_active";

    private readonly IZoneLocationRepository _locationRepo;
    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterUnlockedLocationRepository _unlockedRepo;
    private readonly ILogger<SearchAreaHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="SearchAreaHubCommandHandler"/>.</summary>
    /// <param name="locationRepo">Repository for zone location catalog data.</param>
    /// <param name="characterRepo">Repository for character data (level used as base roll).</param>
    /// <param name="unlockedRepo">Repository for persisting unlock records.</param>
    /// <param name="logger">Logger instance.</param>
    public SearchAreaHubCommandHandler(
        IZoneLocationRepository locationRepo,
        ICharacterRepository characterRepo,
        ICharacterUnlockedLocationRepository unlockedRepo,
        ILogger<SearchAreaHubCommandHandler> logger)
    {
        _locationRepo  = locationRepo;
        _characterRepo = characterRepo;
        _unlockedRepo  = unlockedRepo;
        _logger        = logger;
    }

    /// <summary>Handles the search command, rolling for each active-check hidden location.</summary>
    /// <param name="request">The command containing the character and zone IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SearchAreaHubResult"/> describing what was found.</returns>
    public async Task<SearchAreaHubResult> Handle(
        SearchAreaHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ZoneId))
            return new SearchAreaHubResult { Success = false, ErrorMessage = "Zone ID cannot be empty" };

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new SearchAreaHubResult { Success = false, ErrorMessage = "Character not found" };

        // Roll: character level + random bonus/penalty, range [-5, +9].
        var roll = character.Level + Random.Shared.Next(-5, 10);

        var hiddenLocations = await _locationRepo.GetHiddenByZoneIdAsync(request.ZoneId);
        var candidates = hiddenLocations
            .Where(l => string.Equals(l.UnlockType, ActiveCheckType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var discovered = new List<DiscoveredLocation>();

        foreach (var location in candidates)
        {
            var threshold = location.DiscoverThreshold.GetValueOrDefault(int.MaxValue);
            if (roll < threshold) continue;

            var alreadyUnlocked = await _unlockedRepo.IsUnlockedAsync(
                request.CharacterId, location.Slug, cancellationToken);
            if (alreadyUnlocked) continue;

            await _unlockedRepo.AddUnlockAsync(
                request.CharacterId, location.Slug, ActiveCheckType, cancellationToken);

            discovered.Add(new DiscoveredLocation(location.Slug, location.DisplayName, location.LocationType));

            _logger.LogInformation(
                "Character {CharacterIdPrefix} discovered {LocationSlug} via active search (roll {Roll} vs DC {Threshold})",
                request.CharacterId.ToString()[..8], location.Slug, roll, threshold);
        }

        return new SearchAreaHubResult
        {
            Success   = true,
            RollValue = roll,
            AnyFound  = discovered.Count > 0,
            Discovered = discovered,
        };
    }
}
