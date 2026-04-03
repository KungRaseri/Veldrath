using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for retrieving starting locations with optional background-based filtering.
/// </summary>
public class GetStartingLocationsHandler : IRequestHandler<GetStartingLocationsQuery, List<Location>>
{
    private readonly IBackgroundRepository _backgroundRepository;
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;
    private readonly ILogger<GetStartingLocationsHandler> _logger;

    public GetStartingLocationsHandler(
        IBackgroundRepository backgroundRepository,
        IDbContextFactory<ContentDbContext> dbFactory,
        ILogger<GetStartingLocationsHandler> logger)
    {
        _backgroundRepository = backgroundRepository;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<Location>> Handle(GetStartingLocationsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving starting locations (Background: {BgId}, Filter: {Filter})",
            request.BackgroundId ?? "None", request.FilterByRecommended);

        using var db = _dbFactory.CreateDbContext();

        // Load all active locations first, then filter for towns in memory.
        // Filtering on l.Traits.IsTown inside a DB Where() on a ToJson()-owned entity generates
        // "Traits"['IsTown']::boolean which Postgres rejects ("cannot cast type jsonb to boolean").
        // Client-side filtering avoids that translation entirely.
        var all = await db.ZoneLocations
            .AsNoTracking()
            .Where(l => l.IsActive)
            .ToListAsync(cancellationToken);

        var raw = all.Where(l => l.Traits.IsTown == true).ToList();
        if (raw.Count == 0)
            raw = all;

        var locations = raw
            .Select(l => new Location
            {
                Id = $"{l.TypeKey}:{l.Slug}",
                Name = l.DisplayName ?? l.TypeKey,
                Description = l.DisplayName ?? l.TypeKey,
                Type = l.TypeKey,
                LocationType = l.LocationType,
                IsStartingZone = true,
                IsSafeZone = l.Traits.IsTown == true,
                Level = l.Stats.MinLevel ?? 1,
                DangerRating = l.Stats.DangerLevel ?? 0,
                HasShop = l.Traits.HasMerchant == true,
            })
            .ToList();

        _logger.LogInformation("Loaded {Count} starting locations", locations.Count);

        if (string.IsNullOrWhiteSpace(request.BackgroundId) || !request.FilterByRecommended)
            return locations;

        var background = await _backgroundRepository.GetBackgroundByIdAsync(request.BackgroundId);
        if (background == null)
        {
            _logger.LogWarning("Background not found: {BackgroundId}", request.BackgroundId);
            return locations;
        }

        var recommendedTypes = background.RecommendedLocationTypes;
        var filtered = locations
            .Where(l => l.LocationType != null &&
                        recommendedTypes.Contains(l.LocationType, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Filtered to {Count} recommended locations for background {Background}",
            filtered.Count, background.Name);

        return filtered;
    }
}
