using RealmEngine.Core.Abstractions;using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Death.Commands;

/// <summary>
/// Handles player death with difficulty-appropriate penalties.
/// </summary>
public class HandlePlayerDeathHandler : IRequestHandler<HandlePlayerDeathCommand, HandlePlayerDeathResult>
{
    private readonly DeathService _deathService;
    private readonly ISaveGameService _saveGameService;
    private readonly IHallOfFameRepository _hallOfFameService;
    private readonly ILogger<HandlePlayerDeathHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlePlayerDeathHandler"/> class.
    /// </summary>
    /// <param name="deathService">The death service.</param>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="hallOfFameRepository">The hall of fame repository.</param>
    /// <param name="logger">The logger.</param>
    public HandlePlayerDeathHandler(
        DeathService deathService,
        ISaveGameService saveGameService,
        IHallOfFameRepository hallOfFameRepository,
        ILogger<HandlePlayerDeathHandler> logger)
    {
        _deathService = deathService;
        _saveGameService = saveGameService;
        _hallOfFameService = hallOfFameRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the player death command and applies appropriate penalties.
    /// </summary>
    /// <param name="request">The death command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The death result including penalties and permadeath status.</returns>
    public async Task<HandlePlayerDeathResult> Handle(HandlePlayerDeathCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var location = request.DeathLocation;
        var killer = request.Killer;
        var saveGame = _saveGameService.GetCurrentSave();

        if (saveGame == null)
        {
            _logger.LogError("No active save game found during death handling");
            return new HandlePlayerDeathResult
            {
                IsPermadeath = false,
                SaveDeleted = false
            };
        }

        var difficulty = _saveGameService.GetDifficultySettings();

        _logger.LogWarning("Player death at {Location}. Difficulty: {Difficulty}, Death count: {DeathCount}",
            location, difficulty.Name, saveGame.DeathCount + 1);

        // Record death in save
        saveGame.DeathCount++;
        saveGame.LastDeathLocation = location;
        saveGame.LastDeathDate = DateTime.Now;

        // Handle based on difficulty
        if (difficulty.IsPermadeath)
        {
            return await HandlePermadeathAsync(player, saveGame, location, killer);
        }
        else
        {
            return await HandleStandardDeathAsync(player, saveGame, location, difficulty);
        }
    }

    private Task<HandlePlayerDeathResult> HandleStandardDeathAsync(
        Character player, SaveGame saveGame, string location, DifficultySettings difficulty)
    {
        // Calculate penalties
        var goldLost = (int)(player.Gold * difficulty.GoldLossPercentage);
        var xpLost = (int)(player.Experience * difficulty.XPLossPercentage);

        // Apply penalties
        player.Gold = Math.Max(0, player.Gold - goldLost);
        player.Experience = Math.Max(0, player.Experience - xpLost);

        // Handle item dropping
        var droppedItems = _deathService.HandleItemDropping(
            player, saveGame, location, difficulty);

        // Respawn
        player.Health = player.MaxHealth;
        player.Mana = player.MaxMana;

        // Auto-save in Ironman mode
        if (difficulty.AutoSaveOnly)
        {
            _saveGameService.SaveGame(saveGame);
        }

        return Task.FromResult(new HandlePlayerDeathResult
        {
            IsPermadeath = false,
            SaveDeleted = false,
            DroppedItems = droppedItems,
            GoldLost = goldLost,
            XPLost = xpLost
        });
    }

    private Task<HandlePlayerDeathResult> HandlePermadeathAsync(
        Character player, SaveGame saveGame, string location, Enemy? killer)
    {
        // Create Hall of Fame entry
        var entry = new HallOfFameEntry
        {
            CharacterName = player.Name,
            ClassName = player.ClassName,
            Level = player.Level,
            PlayTimeMinutes = saveGame.PlayTimeMinutes,
            TotalEnemiesDefeated = saveGame.TotalEnemiesDefeated,
            QuestsCompleted = saveGame.QuestsCompleted,
            DeathCount = saveGame.DeathCount,
            DeathReason = killer != null ? $"Slain by {killer.Name}" : "Unknown cause",
            DeathLocation = location,
            DeathDate = DateTime.Now,
            AchievementsUnlocked = saveGame.UnlockedAchievements.Count,
            IsPermadeath = true,
            DifficultyLevel = saveGame.DifficultyLevel
        };

        _hallOfFameService.AddEntry(entry);

        // Delete save
        _saveGameService.DeleteSave(saveGame.Id);

        return Task.FromResult(new HandlePlayerDeathResult
        {
            IsPermadeath = true,
            SaveDeleted = true,
            HallOfFameId = entry.Id,
            HallOfFameEntry = entry
        });
    }
}