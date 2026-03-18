using MediatR;
using RealmEngine.Core.Features.Exploration.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for RestAtInnCommand.
/// </summary>
public class RestAtInnHandler : IRequestHandler<RestAtInnCommand, RestAtInnResult>
{
    private readonly ExplorationService _explorationService;
    private readonly IGameStateService _gameState;
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<RestAtInnHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestAtInnHandler"/> class.
    /// </summary>
    public RestAtInnHandler(
        ExplorationService explorationService,
        IGameStateService gameState,
        ISaveGameService saveGameService,
        ILogger<RestAtInnHandler> logger)
    {
        _explorationService = explorationService;
        _gameState = gameState;
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the RestAtInnCommand.
    /// </summary>
    public async Task<RestAtInnResult> Handle(RestAtInnCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find the location
            var locations = await _explorationService.GetKnownLocationsAsync();
            var location = locations.FirstOrDefault(l => l.Id == request.LocationId);

            if (location == null)
            {
                return new RestAtInnResult
                {
                    Success = false,
                    ErrorMessage = $"Location '{request.LocationId}' not found."
                };
            }

            // Check if location has an inn
            if (!location.HasInn)
            {
                return new RestAtInnResult
                {
                    Success = false,
                    ErrorMessage = $"{location.Name} does not have an inn."
                };
            }

            // Get the character
            var player = _gameState.Player;
            if (player == null)
            {
                return new RestAtInnResult
                {
                    Success = false,
                    ErrorMessage = "No active character found."
                };
            }

            // Check if player has enough gold
            if (player.Gold < request.Cost)
            {
                return new RestAtInnResult
                {
                    Success = false,
                    ErrorMessage = $"Not enough gold. Inn costs {request.Cost} gold."
                };
            }

            // Calculate recovery amounts (full recovery at inn)
            var healthRecovered = player.GetMaxHealth() - player.Health;
            var manaRecovered = player.GetMaxMana() - player.Mana;

            // Restore health and mana
            player.Health = player.GetMaxHealth();
            player.Mana = player.GetMaxMana();

            // Deduct gold
            player.Gold -= request.Cost;

            // Apply inn buffs (well-rested bonus: +5% XP gain for 10 combats)
            var buffs = new List<string> { "Well-Rested (+5% XP for 10 combats)" };

            // Save the game
            bool gameSaved = false;
            try
            {
                var currentSave = _saveGameService.GetCurrentSave();
                if (currentSave != null)
                {
                    _saveGameService.SaveGame(currentSave);
                    gameSaved = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save game after inn rest");
            }

            _logger.LogInformation(
                "Character {CharacterName} rested at inn in {LocationName}. Recovered {Health} HP, {Mana} MP. Paid {Gold} gold.",
                player.Name, location.Name, healthRecovered, manaRecovered, request.Cost);

            return new RestAtInnResult
            {
                Success = true,
                HealthRecovered = healthRecovered,
                ManaRecovered = manaRecovered,
                GoldPaid = request.Cost,
                GameSaved = gameSaved,
                BuffsApplied = buffs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resting at inn in location {LocationId}", request.LocationId);
            return new RestAtInnResult
            {
                Success = false,
                ErrorMessage = $"An error occurred while resting: {ex.Message}"
            };
        }
    }
}
