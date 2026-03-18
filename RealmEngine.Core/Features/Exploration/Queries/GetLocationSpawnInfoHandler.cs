using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handles queries for enemy and loot spawn information at a location.
/// Returns spawn weights, enemy references, and loot tables for the requested location.
/// </summary>
public class GetLocationSpawnInfoHandler : IRequestHandler<GetLocationSpawnInfoQuery, LocationSpawnInfoDto>
{
    private readonly IGameStateService _gameState;
    private readonly ExplorationService _explorationService;
    private readonly ILogger<GetLocationSpawnInfoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLocationSpawnInfoHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="explorationService">The exploration service.</param>
    /// <param name="logger">The logger.</param>
    public GetLocationSpawnInfoHandler(
        IGameStateService gameState,
        ExplorationService explorationService,
        ILogger<GetLocationSpawnInfoHandler> logger)
    {
        _gameState = gameState;
        _explorationService = explorationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the get location spawn info query.
    /// </summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The location spawn info DTO.</returns>
    public async Task<LocationSpawnInfoDto> Handle(GetLocationSpawnInfoQuery request, CancellationToken cancellationToken)
    {
        var locationName = request.LocationName ?? _gameState.CurrentLocation;

        if (string.IsNullOrWhiteSpace(locationName))
        {
            return new LocationSpawnInfoDto
            {
                Success = false,
                ErrorMessage = "No location specified and no current location active."
            };
        }

        var allLocations = await _explorationService.GetKnownLocationsAsync();
        var location = allLocations.FirstOrDefault(l => l.Name == locationName);

        if (location == null)
        {
            _logger.LogWarning("Location '{LocationName}' not found in known locations", locationName);
            return new LocationSpawnInfoDto
            {
                Success = false,
                LocationName = locationName,
                ErrorMessage = $"Location '{locationName}' not found."
            };
        }

        var enemyReferences = location.Enemies ?? [];
        var lootReferences = location.Loot ?? [];
        var availableNpcs = location.Npcs ?? [];

        var spawnWeights = BuildSpawnWeights(enemyReferences);

        var recommendedLevel = location.Metadata?.TryGetValue("recommendedLevel", out var level) == true
            ? level?.ToString()
            : null;

        _logger.LogInformation(
            "Returning spawn info for '{Location}': {EnemyCount} enemy refs, danger {Danger}",
            locationName, enemyReferences.Count, location.DangerRating);

        return new LocationSpawnInfoDto
        {
            Success = true,
            LocationName = location.Name,
            LocationType = location.Type,
            DangerRating = location.DangerRating,
            RecommendedLevel = recommendedLevel,
            EnemyReferences = [..enemyReferences],
            EnemySpawnWeights = spawnWeights,
            LootReferences = [..lootReferences],
            AvailableNPCs = [..availableNpcs],
            Metadata = location.Metadata ?? new Dictionary<string, object>()
        };
    }

    // Builds a category → weight map from enemy reference strings.
    // Reference format: @enemies/category1/category2:EnemyName
    // Each reference in the same category contributes 10 to the weight.
    private static Dictionary<string, int> BuildSpawnWeights(IEnumerable<string> enemyReferences)
    {
        var weights = new Dictionary<string, int>();

        foreach (var reference in enemyReferences)
        {
            // Strip "@enemies/" prefix and ":Name" suffix to get the category path
            const string prefix = "@enemies/";
            if (!reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var withoutPrefix = reference[prefix.Length..];
            var colonIndex = withoutPrefix.IndexOf(':');
            var categoryPath = colonIndex >= 0 ? withoutPrefix[..colonIndex] : withoutPrefix;

            if (string.IsNullOrWhiteSpace(categoryPath))
                continue;

            weights[categoryPath] = weights.TryGetValue(categoryPath, out var existing)
                ? existing + 10
                : 10;
        }

        return weights;
    }
}
