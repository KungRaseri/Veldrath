using MediatR;
using Microsoft.Extensions.Logging;

using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
namespace RealmEngine.Core.Features.Exploration.Queries;

/// <summary>
/// Handler for GetCurrentLocationQuery.
/// </summary>
public class GetCurrentLocationQueryHandler : IRequestHandler<GetCurrentLocationQuery, GetCurrentLocationResult>
{
    private readonly IGameStateService _gameState;
    private readonly ILogger<GetCurrentLocationQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCurrentLocationQueryHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="logger">The logger.</param>
    public GetCurrentLocationQueryHandler(IGameStateService gameState, ILogger<GetCurrentLocationQueryHandler> logger)
    {
        _gameState = gameState;
        _logger = logger;
    }

    /// <summary>
    /// Handles the query to get the current location.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current location result.</returns>
    public Task<GetCurrentLocationResult> Handle(GetCurrentLocationQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var currentLocation = _gameState.CurrentLocation;
            return Task.FromResult(new GetCurrentLocationResult(true, CurrentLocation: currentLocation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current location");
            return Task.FromResult(new GetCurrentLocationResult(false, ErrorMessage: ex.Message));
        }
    }
}