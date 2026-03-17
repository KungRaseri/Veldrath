using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Victory.Commands;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Victory.Services;

/// <summary>
/// Service for managing victory state and statistics.
/// </summary>
public class VictoryService
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<VictoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VictoryService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public VictoryService(ISaveGameService saveGameService, ILogger<VictoryService> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Calculates victory statistics from the current save game.
    /// </summary>
    /// <returns>Victory statistics if a save game exists; otherwise, null.</returns>
    public virtual async Task<VictoryStatistics?> CalculateVictoryStatisticsAsync()
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return null;

        var statistics = new VictoryStatistics(
            saveGame.Character.Name,
            saveGame.Character.ClassName,
            saveGame.Character.Level,
            saveGame.DifficultyLevel,
            saveGame.PlayTimeMinutes,
            saveGame.QuestsCompleted,
            saveGame.TotalEnemiesDefeated,
            saveGame.DeathCount,
            saveGame.UnlockedAchievements.Count,
            saveGame.TotalGoldEarned
        );

        _logger.LogInformation("Victory statistics calculated for {PlayerName}", saveGame.Character.Name);

        return await Task.FromResult(statistics);
    }

    /// <summary>
    /// Marks the current game as completed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async Task MarkGameCompleteAsync()
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return;

        // Add a game flag for completion
        saveGame.GameFlags["GameCompleted"] = true;
        saveGame.GameFlags["CompletionDate"] = true;

        _saveGameService.SaveGame(saveGame);

        _logger.LogInformation("Game marked as completed for {PlayerName}", saveGame.Character.Name);

        await Task.CompletedTask;
    }
}