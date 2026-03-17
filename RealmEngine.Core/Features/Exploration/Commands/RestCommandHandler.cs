using RealmEngine.Core.Abstractions;

using MediatR;
using Microsoft.Extensions.Logging;

using RealmEngine.Core.Services;
namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for RestCommand.
/// </summary>
public class RestCommandHandler : IRequestHandler<RestCommand, RestResult>
{
    private readonly GameStateService _gameState;
    private readonly ILogger<RestCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestCommandHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="logger">The logger.</param>
    public RestCommandHandler(GameStateService gameState, ILogger<RestCommandHandler> logger)
    {
        _gameState = gameState;
        _logger = logger;
    }

    /// <summary>
    /// Handles the rest command to recover health and mana.
    /// </summary>
    /// <param name="request">The rest command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rest result.</returns>
    public Task<RestResult> Handle(RestCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var player = _gameState.Player;

            if (player == null)
            {
                return Task.FromResult(new RestResult(false, ErrorMessage: "No active player"));
            }

            var healthRecovered = player.MaxHealth - player.Health;
            var manaRecovered = player.MaxMana - player.Mana;

            player.Health = player.MaxHealth;
            player.Mana = player.MaxMana;

            _logger.LogInformation("Player {PlayerName} rested", player.Name);

            return Task.FromResult(new RestResult(
                Success: true,
                HealthRecovered: healthRecovered,
                ManaRecovered: manaRecovered
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rest");
            return Task.FromResult(new RestResult(false, ErrorMessage: ex.Message));
        }
    }
}