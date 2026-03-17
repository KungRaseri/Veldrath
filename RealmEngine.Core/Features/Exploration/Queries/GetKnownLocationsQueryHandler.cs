using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for GetKnownLocationsQuery.
/// </summary>
public class GetKnownLocationsQueryHandler : IRequestHandler<GetKnownLocationsQuery, GetKnownLocationsResult>
{
    private readonly ExplorationService _explorationService;
    private readonly ILogger<GetKnownLocationsQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetKnownLocationsQueryHandler"/> class.
    /// </summary>
    /// <param name="explorationService">The exploration service.</param>
    /// <param name="logger">The logger.</param>
    public GetKnownLocationsQueryHandler(ExplorationService explorationService, ILogger<GetKnownLocationsQueryHandler> logger)
    {
        _explorationService = explorationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query to get known locations.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The known locations result.</returns>
    public async Task<GetKnownLocationsResult> Handle(GetKnownLocationsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var locations = await _explorationService.GetKnownLocationsAsync();
            var locationNames = locations.Select(l => l.Name).ToList();
            return new GetKnownLocationsResult(true, Locations: locationNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting known locations");
            return new GetKnownLocationsResult(false, ErrorMessage: ex.Message);
        }
    }
}