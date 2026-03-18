using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Abstractions;

/// <summary>
/// Provides access to the current game state: active save, player character, location, and difficulty.
/// Abstracts <see cref="RealmEngine.Core.Services.GameStateService"/> for handler injection and test isolation.
/// </summary>
public interface IGameStateService
{
    /// <summary>
    /// Gets or sets the player's current location in the game world.
    /// </summary>
    string CurrentLocation { get; set; }

    /// <summary>
    /// Gets the currently active save game.
    /// Throws <see cref="InvalidOperationException"/> if no save is active.
    /// </summary>
    SaveGame CurrentSave { get; }

    /// <summary>
    /// Gets the player character from the current save.
    /// Throws <see cref="InvalidOperationException"/> if no save is active.
    /// </summary>
    Character Player { get; }

    /// <summary>
    /// Gets the difficulty level string (Easy, Normal, Hard, Expert) for the current save.
    /// </summary>
    string DifficultyLevel { get; }

    /// <summary>
    /// Gets a value indicating whether Ironman mode is enabled for the current save.
    /// </summary>
    bool IsIronmanMode { get; }

    /// <summary>
    /// Updates the current location and records it as visited in the active save.
    /// </summary>
    /// <param name="location">The new location name.</param>
    void UpdateLocation(string location);

    /// <summary>
    /// Records a player death in the current save, incrementing the death counter.
    /// </summary>
    /// <param name="killedBy">Description of what killed the player.</param>
    void RecordDeath(string killedBy);
}
