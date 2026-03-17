using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Models;
namespace RealmEngine.Core.Features.Achievements.Services;

/// <summary>
/// Provides services for managing achievements, including unlocking, checking criteria, and retrieving achievement data.
/// Pure domain logic - UI notifications handled by Godot.
/// </summary>
public class AchievementService
{
    private readonly ISaveGameService _saveGameService;
    private readonly List<Achievement> _allAchievements;
    private readonly ILogger<AchievementService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AchievementService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public AchievementService(ISaveGameService saveGameService, ILogger<AchievementService> logger)
    {
        _saveGameService = saveGameService;
        _allAchievements = InitializeAchievements();
        _logger = logger;
    }

    /// <summary>
    /// Protected parameterless constructor for Moq proxy creation in tests.
    /// </summary>
    protected AchievementService()
    {
        _saveGameService = null!;
        _allAchievements = new List<Achievement>();
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AchievementService>.Instance;
    }

    /// <summary>
    /// Unlocks an achievement by its ID.
    /// </summary>
    /// <param name="achievementId">The unique identifier of the achievement to unlock.</param>
    /// <returns>A task representing the asynchronous operation, containing the unlocked achievement or null if already unlocked or not found.</returns>
    public virtual async Task<Achievement?> UnlockAchievementAsync(string achievementId)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return null;

        // Check if already unlocked using the existing UnlockedAchievements list
        if (saveGame.UnlockedAchievements.Contains(achievementId))
            return null;

        var achievement = _allAchievements.FirstOrDefault(a => a.Id == achievementId);
        if (achievement == null)
            return null;

        // Unlock achievement
        achievement.IsUnlocked = true;
        achievement.UnlockedAt = DateTime.Now;

        saveGame.UnlockedAchievements.Add(achievementId);
        _saveGameService.SaveGame(saveGame);

        // Note: UI notification handled by Godot via achievement return value
        _logger.LogInformation("Achievement unlocked: {AchievementId} - {Title}", achievementId, achievement.Title);

        return await Task.FromResult(achievement);
    }

    /// <summary>
    /// Checks all achievements to determine if any should be unlocked based on current progress.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing a list of newly unlocked achievements.</returns>
    public virtual async Task<List<Achievement>> CheckAllAchievementsAsync()
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return new List<Achievement>();

        var newlyUnlocked = new List<Achievement>();

        foreach (var achievement in _allAchievements)
        {
            // Skip if already unlocked
            if (saveGame.UnlockedAchievements.Contains(achievement.Id))
                continue;

            // Check criteria
            if (CheckCriteria(achievement, saveGame))
            {
                var unlocked = await UnlockAchievementAsync(achievement.Id);
                if (unlocked != null)
                    newlyUnlocked.Add(unlocked);
            }
        }

        return newlyUnlocked;
    }

    /// <summary>
    /// Retrieves all unlocked achievements for the current save game.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, containing a list of unlocked achievements.</returns>
    public virtual async Task<List<Achievement>> GetUnlockedAchievementsAsync()
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return new List<Achievement>();

        var unlockedIds = saveGame.UnlockedAchievements;
        var unlocked = _allAchievements.Where(a => unlockedIds.Contains(a.Id)).ToList();

        foreach (var achievement in unlocked)
        {
            achievement.IsUnlocked = true;
        }

        return await Task.FromResult(unlocked);
    }

    private bool CheckCriteria(Achievement achievement, SaveGame saveGame)
    {
        return achievement.Criteria.Type switch
        {
            AchievementType.CompleteQuest => saveGame.CompletedQuests.Any(q => q.Id == achievement.Criteria.RequiredId),
            AchievementType.DefeatEnemies => saveGame.TotalEnemiesDefeated >= achievement.Criteria.RequiredValue,
            AchievementType.ReachLevel => saveGame.Character.Level >= achievement.Criteria.RequiredValue,
            AchievementType.CollectGold => saveGame.Character.Gold >= achievement.Criteria.RequiredValue,
            AchievementType.SurviveTime => saveGame.PlayTimeMinutes >= achievement.Criteria.RequiredValue,
            AchievementType.CompleteGame => saveGame.CompletedQuests.Any(q => q.Id == "main_06_final_boss"),
            AchievementType.CompleteDifficulty => saveGame.CompletedQuests.Any(q => q.Id == "main_06_final_boss") &&
                                                   saveGame.DifficultyLevel == achievement.Criteria.RequiredId,
            AchievementType.Deathless => saveGame.DeathCount == 0 && saveGame.CompletedQuests.Any(q => q.Id == "main_06_final_boss"),
            _ => false
        };
    }

    private List<Achievement> InitializeAchievements()
    {
        return new List<Achievement>
        {
            new Achievement
            {
                Id = "first_steps",
                Title = "First Steps",
                Description = "Complete your first quest",
                Icon = "🌟",
                Category = AchievementCategory.Quests,
                Points = 10,
                Criteria = new AchievementCriteria { Type = AchievementType.CompleteQuest, RequiredValue = 1 }
            },
            new Achievement
            {
                Id = "slayer",
                Title = "Slayer",
                Description = "Defeat 100 enemies",
                Icon = "⚔️",
                Category = AchievementCategory.Combat,
                Points = 25,
                Criteria = new AchievementCriteria { Type = AchievementType.DefeatEnemies, RequiredValue = 100 }
            },
            new Achievement
            {
                Id = "master",
                Title = "Master",
                Description = "Reach level 20",
                Icon = "👑",
                Category = AchievementCategory.Mastery,
                Points = 50,
                Criteria = new AchievementCriteria { Type = AchievementType.ReachLevel, RequiredValue = 20 }
            },
            new Achievement
            {
                Id = "savior",
                Title = "Savior of the World",
                Description = "Complete the main quest",
                Icon = "🏆",
                Category = AchievementCategory.Quests,
                Points = 100,
                Criteria = new AchievementCriteria { Type = AchievementType.CompleteGame, RequiredValue = 1 }
            },
            new Achievement
            {
                Id = "apocalypse_survivor",
                Title = "Apocalypse Survivor",
                Description = "Complete the game on Apocalypse difficulty",
                Icon = "💀",
                Category = AchievementCategory.Survival,
                Points = 200,
                IsSecret = true,
                Criteria = new AchievementCriteria { Type = AchievementType.CompleteDifficulty, RequiredId = "Apocalypse" }
            },
            new Achievement
            {
                Id = "deathless",
                Title = "Deathless",
                Description = "Complete the game without dying",
                Icon = "✨",
                Category = AchievementCategory.Mastery,
                Points = 500,
                IsSecret = true,
                Criteria = new AchievementCriteria { Type = AchievementType.Deathless, RequiredValue = 1 }
            }
        };
    }
}
