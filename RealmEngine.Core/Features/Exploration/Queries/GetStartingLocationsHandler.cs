using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for retrieving starting locations with optional background-based filtering
/// </summary>
public class GetStartingLocationsHandler : IRequestHandler<GetStartingLocationsQuery, List<Location>>
{
    private readonly IBackgroundRepository _backgroundRepository;
    private readonly ILogger<GetStartingLocationsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GetStartingLocationsHandler
    /// </summary>
    public GetStartingLocationsHandler(
        IBackgroundRepository backgroundRepository,
        ILogger<GetStartingLocationsHandler> logger)
    {
        _backgroundRepository = backgroundRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<Location>> Handle(GetStartingLocationsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving starting locations (Background: {BgId}, Filter: {Filter})", 
            request.BackgroundId ?? "None", request.FilterByRecommended);

        // TODO: Implement location loading from catalog
        // For now, return empty list - will be implemented when LocationRepository is available
        var startingLocations = new List<Location>();

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
}
