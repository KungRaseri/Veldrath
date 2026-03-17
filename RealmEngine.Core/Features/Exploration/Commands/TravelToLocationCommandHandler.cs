using RealmEngine.Core.Abstractions;

using MediatR;
using Microsoft.Extensions.Logging;

using RealmEngine.Core.Services;
namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for TravelToLocationCommand.
/// </summary>
public class TravelToLocationCommandHandler : IRequestHandler<TravelToLocationCommand, TravelToLocationResult>
{
    private readonly GameStateService _gameState;
    private readonly ILogger<TravelToLocationCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TravelToLocationCommandHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="logger">The logger.</param>
    public TravelToLocationCommandHandler(GameStateService gameState, ILogger<TravelToLocationCommandHandler> logger)
    {
        _gameState = gameState;
        _logger = logger;
    }

    /// <summary>
    /// Handles the travel to location command.
    /// </summary>
    /// <param name="request">The travel command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The travel result.</returns>
    public Task<TravelToLocationResult> Handle(TravelToLocationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Destination))
            {
                return Task.FromResult(new TravelToLocationResult(false, ErrorMessage: "Destination cannot be empty"));
            }

            if (request.Destination == _gameState.CurrentLocation)
            {
                return Task.FromResult(new TravelToLocationResult(false, ErrorMessage: "Already at this location"));
            }

            _gameState.UpdateLocation(request.Destination);

            _logger.LogInformation("Player traveled to {Location}", _gameState.CurrentLocation);

            return Task.FromResult(new TravelToLocationResult(true, NewLocation: _gameState.CurrentLocation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error traveling to location {Destination}", request.Destination);
            return Task.FromResult(new TravelToLocationResult(false, ErrorMessage: ex.Message));
        }
    }
}