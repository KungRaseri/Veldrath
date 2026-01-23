using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using Serilog;

namespace RealmEngine.Core.Features.Exploration;

/// <summary>
/// Handles in-game operations like saving and resting.
/// Pure domain logic - UI feedback handled by Godot.
/// </summary>
public class GameplayService
{
    private readonly SaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameplayService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public GameplayService(SaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Rest and recover health and mana to maximum.
    /// </summary>
    /// <param name="player">The player character.</param>
    /// <returns>Result info for UI display.</returns>
    public RestResult Rest(Character player)
    {
        if (player == null)
        {
            return new RestResult { Success = false, ErrorMessage = "No player character" };
        }

        var healthRecovered = player.MaxHealth - player.Health;
        var manaRecovered = player.MaxMana - player.Mana;

        player.Health = player.MaxHealth;
        player.Mana = player.MaxMana;

        Log.Information("Player {PlayerName} rested. HP: +{HP}, Mana: +{Mana}",
            player.Name, healthRecovered, manaRecovered);

        return new RestResult
        {
            Success = true,
            HealthRecovered = healthRecovered,
            ManaRecovered = manaRecovered
        };
    }

    /// <summary>
    /// Save the current game state.
    /// </summary>
    /// <param name="player">The player character.</param>
    /// <param name="inventory">The player's inventory.</param>
    /// <param name="currentSaveId">The current save ID.</param>
    /// <returns>Result indicating success or failure.</returns>
    public SaveResult SaveGame(Character player, List<Item> inventory, string? currentSaveId)
    {
        if (player == null)
        {
            return new SaveResult { Success = false, ErrorMessage = "No active game to save" };
        }

        try
        {
            _saveGameService.SaveGame(player, inventory, currentSaveId);
            Log.Information("Game saved for player {PlayerName}", player.Name);
            
            return new SaveResult { Success = true };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save game for {PlayerName}", player.Name);
            return new SaveResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}

/// <summary>
/// Result of a rest operation.
/// </summary>
public class RestResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the amount of health recovered.</summary>
    public int HealthRecovered { get; set; }
    
    /// <summary>Gets or sets the amount of mana recovered.</summary>
    public int ManaRecovered { get; set; }
    
    /// <summary>Gets or sets the error message if failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a save operation.
/// </summary>
public class SaveResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; set; }
    
    /// <summary>Gets or sets the error message if failed.</summary>
    public string? ErrorMessage { get; set; }
}