using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Victory.Services;

/// <summary>
/// Service for managing New Game Plus mode.
/// </summary>
public class NewGamePlusService
{
    private readonly ISaveGameService _saveGameService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewGamePlusService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    public NewGamePlusService(ISaveGameService saveGameService)
    {
        _saveGameService = saveGameService;
    }

    /// <summary>
    /// Starts a new game plus session with bonuses from completed save.
    /// </summary>
    /// <returns>A tuple indicating success and the new save game if successful.</returns>
    public virtual async Task<(bool Success, SaveGame? NewSave)> StartNewGamePlusAsync()
    {
        var completedSave = _saveGameService.GetCurrentSave();
        if (completedSave == null)
            return (false, null);

        // Check if game is completed
        var gameCompleted = completedSave.GameFlags.ContainsKey("GameCompleted") && completedSave.GameFlags["GameCompleted"];
        if (!gameCompleted)
            return (false, null);

        // Create New Game+ character with bonuses
        var ngPlusCharacter = new Character
        {
            Name = completedSave.Character.Name,
            ClassName = completedSave.Character.ClassName,
            Level = 1, // Start at level 1
            MaxHealth = completedSave.Character.MaxHealth + 50, // Bonus HP
            Health = completedSave.Character.MaxHealth + 50,
            MaxMana = completedSave.Character.MaxMana + 50, // Bonus Mana
            Mana = completedSave.Character.MaxMana + 50,
            Strength = completedSave.Character.Strength + 5, // Bonus stats
            Intelligence = completedSave.Character.Intelligence + 5,
            Dexterity = completedSave.Character.Dexterity + 5,
            Gold = 500 // Starting gold bonus
        };

        // Create new save game with NG+ character
        var ngPlusSave = new SaveGame
        {
            PlayerName = completedSave.PlayerName + " (NG+)",
            Character = ngPlusCharacter,
            CreationDate = DateTime.Now,
            SaveDate = DateTime.Now,
            DifficultyLevel = completedSave.DifficultyLevel + " NG+",
            UnlockedAchievements = new List<string>(completedSave.UnlockedAchievements) // Carry over achievements
        };

        // Mark as NG+
        ngPlusSave.GameFlags["IsNewGamePlus"] = true;
        ngPlusSave.GameFlags["NewGamePlusGeneration"] = true;

        _saveGameService.SaveGame(ngPlusSave);

        _logger.LogInformation("New Game+ started for {PlayerName}", completedSave.Character.Name);

        return await Task.FromResult((true, ngPlusSave));
    }
}