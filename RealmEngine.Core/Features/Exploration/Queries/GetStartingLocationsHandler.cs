using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for retrieving starting locations with optional background-based filtering
/// </summary>
public class GetStartingLocationsHandler : IRequestHandler<GetStartingLocationsQuery, List<Location>>
{
    private readonly IBackgroundRepository _backgroundRepository;
    private readonly GameDataCache _dataCache;
    private readonly ILogger<GetStartingLocationsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetStartingLocationsHandler
    /// </summary>
    public GetStartingLocationsHandler(
        IBackgroundRepository backgroundRepository,
        GameDataCache dataCache,
        ILogger<GetStartingLocationsHandler> logger)
    {
        _backgroundRepository = backgroundRepository;
        _dataCache = dataCache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<Location>> Handle(GetStartingLocationsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving starting locations (Background: {BgId}, Filter: {Filter})", 
            request.BackgroundId ?? "None", request.FilterByRecommended);

        // Load all starting locations from catalogs
        var startingLocations = LoadStartingLocations();

        _logger.LogInformation("Loaded {Count} starting locations from catalogs", startingLocations.Count);

        // If no background or filtering disabled, return all safe locations
        if (string.IsNullOrWhiteSpace(request.BackgroundId) || !request.FilterByRecommended)
        {
            return startingLocations;
        }

        // Filter by background recommendations
        var background = await _backgroundRepository.GetBackgroundByIdAsync(request.BackgroundId);
        if (background == null)
        {
            _logger.LogWarning("Background not found: {BackgroundId}", request.BackgroundId);
            return startingLocations;
        }

        var recommendedTypes = background.RecommendedLocationTypes;
        var filteredLocations = startingLocations
            .Where(l => l.LocationType != null && 
                       recommendedTypes.Contains(l.LocationType, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Filtered to {Count} recommended locations for background {Background}", 
            filteredLocations.Count, background.Name);

        return filteredLocations;
    }

    /// <summary>
    /// Loads all starting locations from world location catalogs.
    /// </summary>
    private List<Location> LoadStartingLocations()
    {
        var locations = new List<Location>();

        // Load from towns, villages, and wilderness areas
        var catalogPaths = new[]
        {
            "world/locations/towns/catalog.json",
            "world/locations/wilderness/catalog.json",
            "world/locations/dungeons/catalog.json"
        };

        foreach (var catalogPath in catalogPaths)
        {
            try
            {
                var catalogFile = _dataCache.GetFile(catalogPath);
                if (catalogFile == null)
                {
                    _logger.LogWarning("Location catalog not found: {CatalogPath}", catalogPath);
                    continue;
                }

                var catalog = JObject.Parse(catalogFile.JsonData.ToString());
                var loadedLocations = ExtractStartingLocations(catalog, catalogPath);
                locations.AddRange(loadedLocations);

                _logger.LogDebug("Loaded {Count} starting locations from {CatalogPath}", 
                    loadedLocations.Count, catalogPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading location catalog: {CatalogPath}", catalogPath);
            }
        }

        return locations;
    }

    /// <summary>
    /// Extracts starting locations from a catalog JSON structure.
    /// </summary>
    private List<Location> ExtractStartingLocations(JObject catalog, string catalogPath)
    {
        var locations = new List<Location>();

        // Catalogs have hierarchical structure: outposts_types, villages_types, towns_types, etc.
        foreach (var typeCategory in catalog.Properties())
        {
            // Skip metadata
            if (typeCategory.Name == "metadata")
                continue;

            var typeData = typeCategory.Value as JObject;
            if (typeData == null)
                continue;

            // Each type category contains subcategories with items arrays
            foreach (var subcategory in typeData.Properties())
            {
                var subcategoryData = subcategory.Value as JObject;
                var items = subcategoryData?["items"] as JArray;

                if (items == null)
                    continue;

                foreach (var itemToken in items)
                {
                    // Only include locations marked as starting zones
                    var isStartingZone = itemToken["isStartingZone"]?.Value<bool>() ?? false;
                    if (!isStartingZone)
                        continue;

                    var location = ParseLocation(itemToken, catalogPath);
                    if (location != null)
                    {
                        locations.Add(location);
                    }
                }
            }
        }

        return locations;
    }

    /// <summary>
    /// Parses a Location object from a JToken.
    /// </summary>
    private Location? ParseLocation(JToken token, string catalogPath)
    {
        try
        {
            var slug = token["slug"]?.ToString();
            var name = token["name"]?.ToString();
            var description = token["description"]?.ToString();
            var locationType = token["locationType"]?.ToString() ?? "settlement";

            if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Location missing required fields (slug or name) in {CatalogPath}", catalogPath);
                return null;
            }

            // Determine type from catalog path
            var type = catalogPath.Contains("/towns/") ? "town" :
                      catalogPath.Contains("/wilderness/") ? "wilderness" :
                      catalogPath.Contains("/dungeons/") ? "dungeon" : "location";

            var location = new Location
            {
                Id = $"{type}:{slug}",
                Name = name,
                Description = description ?? string.Empty,
                Type = type,
                LocationType = locationType,
                IsStartingZone = true,
                IsSafeZone = type == "town", // Towns are safe zones
                Level = token["level"]?.Value<int>() ?? 1,
                DangerRating = token["dangerRating"]?.Value<int>() ?? 0,
                HasShop = token["services"] != null && 
                         token["services"]?.ToString().Contains("trade", StringComparison.OrdinalIgnoreCase) == true,
                HasInn = token["services"] != null && 
                        token["services"]?.ToString().Contains("rest", StringComparison.OrdinalIgnoreCase) == true
            };

            // Parse notable features
            var features = token["notableFeatures"] as JArray;
            if (features != null)
            {
                location.Features = features.Select(f => f.ToString()).ToList();
            }

            return location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing location from {CatalogPath}", catalogPath);
            return null;
        }
    }
}
