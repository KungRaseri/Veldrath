using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.Death.Commands;

/// <summary>
/// Handler for RespawnCommand.
/// Handles player respawn logic after death.
/// </summary>
public class RespawnCommandHandler : IRequestHandler<RespawnCommand, RespawnResult>
{
    private readonly GameStateService _gameState;
    private readonly DeathService _deathService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RespawnCommandHandler"/> class.
    /// </summary>
    /// <param name="gameState">The game state service.</param>
    /// <param name="deathService">The death service.</param>
    public RespawnCommandHandler(GameStateService gameState, DeathService deathService)
    {
        _gameState = gameState;
        _deathService = deathService;
    }

    /// <summary>
    /// Handles the respawn command.
    /// </summary>
    /// <param name="request">The respawn command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The respawn result.</returns>
    public Task<RespawnResult> Handle(RespawnCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var player = request.Player;
            
            if (player == null)
            {
                return Task.FromResult(new RespawnResult
                {
                    Success = false,
                    ErrorMessage = "No player specified"
                });
            }

            // Restore health and mana to maximum
            player.Health = player.MaxHealth;
            player.Mana = player.MaxMana;

            // Set respawn location
            var respawnLocation = request.RespawnLocation ?? "Hub Town";
            _gameState.UpdateLocation(respawnLocation);

            _logger.LogInformation("Player {PlayerName} respawned at {Location} with {Health}/{MaxHealth} HP",
                player.Name, respawnLocation, player.Health, player.MaxHealth);

            return Task.FromResult(new RespawnResult
            {
                Success = true,
                RespawnLocation = respawnLocation,
                Health = player.Health,
                Mana = player.Mana
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error respawning player");
            return Task.FromResult(new RespawnResult
            {
                Success = false,
                ErrorMessage = $"Failed to respawn: {ex.Message}"
            });
        }
    }
}
