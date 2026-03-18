using MediatR;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.Exploration;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for GenerateEnemyForLocationCommand.
/// Generates enemies appropriate for the player's current location.
/// </summary>
public class GenerateEnemyForLocationHandler : IRequestHandler<GenerateEnemyForLocationCommand, GenerateEnemyForLocationResult>
{
    private readonly IGameStateService _gameState;
    private readonly ExplorationService _explorationService;
    private readonly LocationGenerator _locationGenerator;
    private readonly EnemyGenerator _enemyGenerator;
    private readonly ILogger<GenerateEnemyForLocationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateEnemyForLocationHandler"/> class.
    /// </summary>
    public GenerateEnemyForLocationHandler(
        IGameStateService gameState,
        ExplorationService explorationService,
        LocationGenerator locationGenerator,
        EnemyGenerator enemyGenerator,
        ILogger<GenerateEnemyForLocationHandler> logger)
    {
        _gameState = gameState;
        _explorationService = explorationService;
        _locationGenerator = locationGenerator;
        _enemyGenerator = enemyGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Handles the command to generate a location-appropriate enemy.
    /// </summary>
    public async Task<GenerateEnemyForLocationResult> Handle(
        GenerateEnemyForLocationCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var locationName = request.LocationId ?? _gameState.CurrentLocation;
            
            if (string.IsNullOrEmpty(locationName))
            {
                return new GenerateEnemyForLocationResult(false, ErrorMessage: "No current location set");
            }

            // Try to find the location in known locations
            var locations = await _explorationService.GetKnownLocationsAsync();
            var currentLocation = locations.FirstOrDefault(l => 
                l.Name == locationName || l.Id == locationName);

            if (currentLocation == null)
            {
                // Fallback: Generate a generic location
                _logger.LogWarning("Location {LocationName} not found, generating generic enemy", locationName);
                var fallbackEnemy = await GenerateFallbackEnemyAsync();
                return fallbackEnemy != null
                    ? new GenerateEnemyForLocationResult(true, Enemy: fallbackEnemy)
                    : new GenerateEnemyForLocationResult(false, ErrorMessage: "Failed to generate enemy");
            }

            // Generate location-appropriate enemy
            var enemy = await _locationGenerator.GenerateLocationAppropriateEnemyAsync(
                currentLocation,
                _enemyGenerator);

            if (enemy == null)
            {
                _logger.LogWarning("Failed to generate enemy for location {LocationName}, using fallback", locationName);
                enemy = await GenerateFallbackEnemyAsync();
            }

            return enemy != null
                ? new GenerateEnemyForLocationResult(true, Enemy: enemy)
                : new GenerateEnemyForLocationResult(false, ErrorMessage: "Failed to generate enemy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enemy for location");
            return new GenerateEnemyForLocationResult(false, ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Generates a fallback enemy when location-specific generation fails.
    /// Uses player level to determine appropriate enemy.
    /// </summary>
    private async Task<Shared.Models.Enemy?> GenerateFallbackEnemyAsync()
    {
        try
        {
            var player = _gameState.Player;
            var playerLevel = player.Level;

            // Choose category based on player level
            var category = playerLevel switch
            {
                <= 5 => "beasts",      // Early game
                <= 10 => "humanoids",  // Mid game
                <= 15 => "undead",     // Late mid game
                _ => "demons"          // End game
            };

            var enemies = await _enemyGenerator.GenerateEnemiesAsync(category, count: 5, hydrate: true);
            var appropriateEnemies = enemies
                .Where(e => Math.Abs(e.Level - playerLevel) <= 2)
                .ToList();

            return appropriateEnemies.Any()
                ? appropriateEnemies[Random.Shared.Next(appropriateEnemies.Count)]
                : enemies.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fallback enemy");
            return null;
        }
    }
}
