using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using Serilog;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for GetLocationSpawnInfoQuery.
/// Retrieves location-specific enemy spawn rules and loot information.
/// </summary>
public class GetLocationSpawnInfoHandler : IRequestHandler<GetLocationSpawnInfoQuery, LocationSpawnInfoDto>
{
    private readonly GameStateService _gameState;
    private readonly ExplorationService _explorationService;
    private readonly ILogger<GetLocationSpawnInfoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetLocationSpawnInfoHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="explorationService">The exploration service.</param>
    /// <param name="logger">The logger.</param>
    public GetLocationSpawnInfoHandler(
        GameStateService gameState,
        ExplorationService explorationService,
        ILogger<GetLocationSpawnInfoHandler> logger)
    {
        _gameState = gameState;
        _explorationService = explorationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query to get location spawn information.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The location spawn information.</returns>
    public async Task<LocationSpawnInfoDto> Handle(GetLocationSpawnInfoQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var locationName = request.LocationName ?? _gameState.CurrentLocation;
            
            if (string.IsNullOrEmpty(locationName))
            {
                return new LocationSpawnInfoDto
                {
                    Success = false,
                    ErrorMessage = "No location specified and no current location set"
                };
            }

            // Get all known locations
            var locations = await _explorationService.GetKnownLocationsAsync();
            var location = locations.FirstOrDefault(l => l.Name == locationName);

            if (location == null)
            {
                return new LocationSpawnInfoDto
                {
                    Success = false,
                    LocationName = locationName,
                    ErrorMessage = $"Location '{locationName}' not found"
                };
            }

            _logger.LogInformation("Retrieving spawn info for location: {LocationName}", locationName);

            // Extract enemy spawn information
            var enemySpawnWeights = new Dictionary<string, int>();
            var enemyReferences = new List<string>();

            // Parse enemy references from location data
            if (location.Enemies != null && location.Enemies.Any())
            {
                foreach (var enemyRef in location.Enemies)
                {
                    enemyReferences.Add(enemyRef);
                    
                    // Extract category from reference for spawn weighting
                    // Example: "@enemies/beasts/wolves:Wolf" -> category: "beasts/wolves"
                    if (enemyRef.StartsWith("@enemies/"))
                    {
                        var parts = enemyRef.Substring(9).Split(':'); // Remove "@enemies/"
                        if (parts.Length > 0)
                        {
                            var category = parts[0]; // e.g., "beasts/wolves" or "humanoids/bandits"
                            
                            // Increment weight for this category
                            if (enemySpawnWeights.ContainsKey(category))
                                enemySpawnWeights[category] += 10;
                            else
                                enemySpawnWeights[category] = 10;
                        }
                    }
                }
            }

            // Extract loot references
            var lootReferences = new List<string>();
            if (location.Loot != null && location.Loot.Any())
            {
                lootReferences.AddRange(location.Loot);
            }

            // Build NPC lists
            var availableNPCs = location.Npcs?.ToList() ?? new List<string>();
            
            // Note: Merchant filtering handled by Location.HasShop property and NPC.Occupation
            // See VisitShopCommand for merchant NPC resolution
            var availableMerchants = new List<string>();

            // Parse recommended level from metadata
            string? recommendedLevel = null;
            if (location.Metadata.TryGetValue("recommendedLevel", out var levelObj))
            {
                recommendedLevel = levelObj?.ToString();
            }

            var dto = new LocationSpawnInfoDto
            {
                Success = true,
                LocationName = location.Name,
                LocationType = location.Type,
                RecommendedLevel = recommendedLevel,
                DangerRating = location.DangerRating,
                EnemySpawnWeights = enemySpawnWeights,
                EnemyReferences = enemyReferences,
                LootReferences = lootReferences,
                AvailableNPCs = availableNPCs,
                AvailableMerchants = availableMerchants,
                Metadata = new Dictionary<string, object>(location.Metadata)
            };

            _logger.LogInformation(
                "Location spawn info retrieved: {LocationName} - {EnemyCount} enemy types, {LootCount} loot types",
                locationName, enemyReferences.Count, lootReferences.Count);

            return dto;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting location spawn info");
            return new LocationSpawnInfoDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
