using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for GetLocationDetailQuery.
/// Provides comprehensive location information including spawn rules for Godot UI.
/// </summary>
public class GetLocationDetailHandler : IRequestHandler<GetLocationDetailQuery, LocationDetailDto>
{
    private readonly IGameStateService _gameState;
    private readonly ExplorationService _explorationService;
    private readonly ILogger<GetLocationDetailHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLocationDetailHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="explorationService">The exploration service.</param>
    /// <param name="logger">The logger.</param>
    public GetLocationDetailHandler(
        IGameStateService gameState,
        ExplorationService explorationService,
        ILogger<GetLocationDetailHandler> logger)
    {
        _gameState = gameState;
        _explorationService = explorationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the GetLocationDetailQuery request.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Detailed location information including spawn rules.</returns>
    public async Task<LocationDetailDto> Handle(GetLocationDetailQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Determine which location to query
            string locationName = request.LocationName ?? _gameState.CurrentLocation;
            
            if (string.IsNullOrEmpty(locationName))
            {
                return new LocationDetailDto
                {
                    Success = false,
                    ErrorMessage = "No location specified and no current location set."
                };
            }

            // Get the location from ExplorationService
            var knownLocations = await _explorationService.GetKnownLocationsAsync();
            var location = knownLocations.FirstOrDefault(l => 
                l.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase));

            if (location == null)
            {
                _logger.LogWarning("Location not found: {LocationName}", locationName);
                return new LocationDetailDto
                {
                    Success = false,
                    ErrorMessage = $"Location '{locationName}' not found."
                };
            }

            _logger.LogInformation("Building detailed info for location: {LocationName}", location.Name);

            // Build enemy spawn weights from enemy references
            var enemySpawnWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var enemyRef in location.Enemies)
            {
                if (enemyRef.StartsWith("@enemies/", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract category from reference (e.g., "@enemies/beasts/wolves:Wolf" -> "beasts/wolves")
                    var refPart = enemyRef.Substring(9); // Remove "@enemies/"
                    var colonIndex = refPart.IndexOf(':');
                    var category = colonIndex > 0 ? refPart.Substring(0, colonIndex) : refPart;
                    
                    // Each reference adds 10 weight to its category
                    enemySpawnWeights[category] = enemySpawnWeights.GetValueOrDefault(category, 0) + 10;
                }
            }

            // Build loot spawn weights from loot references
            var lootSpawnWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var lootRef in location.Loot)
            {
                if (lootRef.StartsWith("@items/", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract category from reference (e.g., "@items/weapons/swords:*" -> "weapons/swords")
                    var refPart = lootRef.Substring(7); // Remove "@items/"
                    var colonIndex = refPart.IndexOf(':');
                    var category = colonIndex > 0 ? refPart.Substring(0, colonIndex) : refPart;
                    
                    // Each reference adds 10 weight to its category
                    lootSpawnWeights[category] = lootSpawnWeights.GetValueOrDefault(category, 0) + 10;
                }
            }

            // Separate NPCs into regular NPCs and merchants based on IDs or metadata
            var npcs = location.Npcs.Where(n => !n.Contains("merchant", StringComparison.OrdinalIgnoreCase)).ToList();
            var merchants = location.Npcs.Where(n => n.Contains("merchant", StringComparison.OrdinalIgnoreCase)).ToList();

            return new LocationDetailDto
            {
                Success = true,
                LocationId = location.Id,
                LocationName = location.Name,
                LocationType = location.Type,
                Description = location.Description,
                RecommendedLevel = location.Level,
                DangerRating = location.DangerRating,
                EnemySpawnWeights = enemySpawnWeights,
                EnemyReferences = location.Enemies.ToList(),
                LootReferences = location.Loot.ToList(),
                LootSpawnWeights = lootSpawnWeights,
                AvailableNPCs = npcs,
                AvailableMerchants = merchants,
                Features = location.Features.ToList(),
                ParentRegion = location.ParentRegion,
                Metadata = new Dictionary<string, object>(location.Metadata)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location detail for: {LocationName}", request.LocationName);
            return new LocationDetailDto
            {
                Success = false,
                ErrorMessage = $"Error retrieving location details: {ex.Message}"
            };
        }
    }
}
