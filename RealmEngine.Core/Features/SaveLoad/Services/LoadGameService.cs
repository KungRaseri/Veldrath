using Microsoft.Extensions.Logging;
using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
namespace RealmEngine.Core.Features.SaveLoad;

/// <summary>
/// Handles loading saved games.
/// Pure domain logic - UI handled by Godot.
/// </summary>
public class LoadGameService
{
    private readonly ISaveGameService _saveGameService;
    private readonly IApocalypseTimer _apocalypseTimer;
    private readonly ILogger<LoadGameService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadGameService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="apocalypseTimer">The apocalypse timer.</param>
    /// <param name="logger">The logger.</param>
    public LoadGameService(ISaveGameService saveGameService, IApocalypseTimer apocalypseTimer, ILogger<LoadGameService> logger)
    {
        _saveGameService = saveGameService;
        _apocalypseTimer = apocalypseTimer;
        _logger = logger;
    }

    /// <summary>
    /// Loads a saved game by ID and restores apocalypse timer if applicable.
    /// Godot handles save selection UI.
    /// </summary>
    /// <param name="saveId">The save game ID to load.</param>
    /// <returns>Load game result with apocalypse status.</returns>
    public LoadGameResult LoadGame(int saveId)
    {
        try
        {
            var save = _saveGameService.LoadGame(saveId.ToString());
            
            if (save == null)
            {
                return new LoadGameResult
                {
                    Success = false,
                    ErrorMessage = "Save game not found."
                };
            }

            _logger.LogInformation("Game loaded for player {PlayerName}", save.Character.Name);

            // Restore apocalypse timer if applicable
            bool timeExpired = false;
            int? remainingMinutes = null;
            
            if (save.ApocalypseMode && save.ApocalypseStartTime.HasValue)
            {
                _apocalypseTimer.StartFromSave(save.ApocalypseStartTime.Value, save.ApocalypseBonusMinutes);

                // Check if time expired while they were away
                if (_apocalypseTimer.IsExpired())
                {
                    timeExpired = true;
                    _logger.LogWarning("Apocalypse time expired for player {PlayerName}", save.Character.Name);
                }
                else
                {
                    remainingMinutes = _apocalypseTimer.GetRemainingMinutes();
                    _logger.LogInformation("Apocalypse timer restored: {Minutes} minutes remaining", remainingMinutes);
                }
            }

            return new LoadGameResult
            {
                Success = true,
                SaveGame = save,
                ApocalypseMode = save.ApocalypseMode,
                ApocalypseTimeExpired = timeExpired,
                ApocalypseRemainingMinutes = remainingMinutes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load game with ID {SaveId}", saveId);
            return new LoadGameResult
            {
                Success = false,
                ErrorMessage = $"Failed to load game: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets all available save games.
    /// Godot uses this to display save selection menu.
    /// </summary>
    /// <returns>List of all save games.</returns>
    public List<SaveGame> GetAllSaves()
    {
        return _saveGameService.GetAllSaves();
    }
}

/// <summary>
/// Result of a load game operation.
/// Contains save game data and apocalypse timer status.
/// </summary>
public class LoadGameResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the loaded save game.</summary>
    public SaveGame? SaveGame { get; set; }

    /// <summary>Gets or sets a value indicating whether apocalypse mode is active.</summary>
    public bool ApocalypseMode { get; set; }

    /// <summary>Gets or sets a value indicating whether apocalypse time expired.</summary>
    public bool ApocalypseTimeExpired { get; set; }

    /// <summary>Gets or sets the apocalypse time remaining in minutes.</summary>
    public int? ApocalypseRemainingMinutes { get; set; }

    /// <summary>Gets or sets the error message if failed.</summary>
    public string? ErrorMessage { get; set; }
}