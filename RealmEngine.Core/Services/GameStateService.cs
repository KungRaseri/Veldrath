using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Services;

/// <summary>
/// Centralized service for accessing game state and context.
/// Provides clean access to difficulty settings, current save, player character, and location.
/// </summary>
public class GameStateService : IGameStateService
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GameStateService> _logger;

    /// <summary>
    /// Current location in the game world.
    /// </summary>
    public virtual string CurrentLocation { get; set; } = "Hub Town";

    /// <summary>
    /// Initializes a new instance of the <see cref="GameStateService"/> class.
    /// Protected parameterless constructor for mocking and testing.
    /// </summary>
    protected GameStateService() { _saveGameService = null!; _logger = null!; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameStateService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GameStateService(ISaveGameService saveGameService, ILogger<GameStateService> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Get the current active save game.
    /// Throws InvalidOperationException if no save is active.
    /// </summary>
    public SaveGame CurrentSave =>
        _saveGameService.GetCurrentSave()
        ?? throw new InvalidOperationException("No active save game");

    /// <summary>
    /// Get the player character from the current save.
    /// Throws InvalidOperationException if no save is active.
    /// </summary>
    public virtual Character Player => CurrentSave.Character;

    /// <summary>
    /// Get the difficulty level string (Easy, Normal, Hard, Expert).
    /// </summary>
    public string DifficultyLevel => CurrentSave.DifficultyLevel;

    /// <summary>
    /// Check if Ironman mode is enabled for the current save.
    /// </summary>
    public bool IsIronmanMode => CurrentSave.IronmanMode;

    /// <summary>
    /// Update the current location and track it in the save game.
    /// </summary>
    /// <param name="location">The new location.</param>
    public virtual void UpdateLocation(string location)
    {
        CurrentLocation = location;

        var save = _saveGameService.GetCurrentSave();
        if (save != null && !save.VisitedLocations.Contains(location))
        {
            save.VisitedLocations.Add(location);
            _logger.LogInformation("Player visited {Location} for the first time", location);
        }
    }

    /// <summary>
    /// Record a player death in the current save.
    /// </summary>
    /// <param name="killedBy">What killed the player.</param>
    public void RecordDeath(string killedBy)
    {
        _saveGameService.RecordDeath(CurrentLocation, killedBy);
    }
}