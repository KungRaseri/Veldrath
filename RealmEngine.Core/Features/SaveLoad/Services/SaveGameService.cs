using QuestModel = RealmEngine.Shared.Models.Quest;
using RealmEngine.Shared.Models;
using Serilog;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Core.Abstractions;

namespace RealmEngine.Core.Features.SaveLoad;

/// <summary>
/// Service for managing game saves and loads with comprehensive world state tracking.
/// </summary>
public class SaveGameService : ISaveGameService, IDisposable
{
    private readonly ISaveGameRepository _repository;
    private readonly IApocalypseTimer _apocalypseTimer;
    private DateTime _gameStartTime;
    private SaveGame? _currentSave;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveGameService"/> class.
    /// </summary>
    /// <param name="repository">The save game repository.</param>
    /// <param name="apocalypseTimer">The apocalypse timer.</param>
    public SaveGameService(ISaveGameRepository repository, IApocalypseTimer apocalypseTimer)
    {
        _repository = repository;
        _apocalypseTimer = apocalypseTimer;
        _gameStartTime = DateTime.Now;
    }

    /// <summary>
    /// Parameterless constructor for testing/mocking purposes.
    /// </summary>
    protected SaveGameService()
    {
        _repository = null!;
        _apocalypseTimer = null!;
        _gameStartTime = DateTime.Now;
    }

    /// <summary>
    /// Initialize a new game session with a fresh SaveGame object.
    /// </summary>
    public SaveGame CreateNewGame(Character player, DifficultySettings difficulty)
    {
        _currentSave = new SaveGame
        {
            PlayerName = player.Name,
            Character = player,
            CreationDate = DateTime.Now,
            SaveDate = DateTime.Now,
            DifficultyLevel = difficulty.Name,
            IronmanMode = difficulty.AutoSaveOnly,
            PermadeathMode = difficulty.IsPermadeath,
            ApocalypseMode = difficulty.IsApocalypse,
            ApocalypseStartTime = difficulty.IsApocalypse ? DateTime.Now : null,
            ApocalypseBonusMinutes = 0,
            PlayTimeMinutes = 0
        };

        _gameStartTime = DateTime.Now;
        Log.Information("New game created for player {PlayerName} (Difficulty: {Difficulty}, Ironman: {Ironman}, Permadeath: {Permadeath}, Apocalypse: {Apocalypse})",
            player.Name, difficulty.Name, difficulty.AutoSaveOnly, difficulty.IsPermadeath, difficulty.IsApocalypse);

        return _currentSave;
    }

    /// <summary>
    /// Get difficulty settings from current save.
    /// </summary>
    public virtual DifficultySettings GetDifficultySettings()
    {
        if (_currentSave == null)
            return DifficultySettings.Normal;

        return DifficultySettings.GetByName(_currentSave.DifficultyLevel);
    }

    /// <summary>
    /// Save the current game state with all world data.
    /// </summary>
    public virtual void SaveGame(SaveGame saveGame)
    {
        try
        {
            // Update play time
            if (_currentSave?.Id == saveGame.Id)
            {
                saveGame.PlayTimeMinutes = (int)(DateTime.Now - _gameStartTime).TotalMinutes;
            }

            // Update apocalypse timer state if applicable
            if (saveGame.ApocalypseMode)
            {
                saveGame.ApocalypseBonusMinutes = _apocalypseTimer.GetBonusMinutes();
                // ApocalypseStartTime is already set during game creation
            }

            // Clone all collections to avoid modification during serialization
            saveGame.Character.Inventory = saveGame.Character.Inventory.ToList();
            saveGame.Character.PendingLevelUps = saveGame.Character.PendingLevelUps.ToList();
            // Skills is a Dictionary, not a List - no need to clone
            // saveGame.Character.LearnedSkills = saveGame.Character.LearnedSkills.ToList();

            saveGame.ActiveQuests = saveGame.ActiveQuests.ToList();
            saveGame.CompletedQuests = saveGame.CompletedQuests.ToList();
            saveGame.FailedQuests = saveGame.FailedQuests.ToList();
            saveGame.AvailableQuests = saveGame.AvailableQuests.ToList();
            saveGame.KnownNPCs = saveGame.KnownNPCs.ToList();
            saveGame.VisitedLocations = saveGame.VisitedLocations.ToList();
            saveGame.DiscoveredLocations = saveGame.DiscoveredLocations.ToList();
            saveGame.LegendaryEnemiesDefeated = saveGame.LegendaryEnemiesDefeated.ToList();
            saveGame.UnlockedAchievements = saveGame.UnlockedAchievements.ToList();

            saveGame.NPCRelationships = new Dictionary<string, int>(saveGame.NPCRelationships);
            saveGame.EnemiesDefeatedByType = new Dictionary<string, int>(saveGame.EnemiesDefeatedByType);
            saveGame.GameFlags = new Dictionary<string, bool>(saveGame.GameFlags);
            saveGame.DroppedItemsAtLocations = new Dictionary<string, List<Item>>(
                saveGame.DroppedItemsAtLocations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList())
            );

            saveGame.SaveDate = DateTime.Now;
            _repository.Save(saveGame);
            _currentSave = saveGame;

            Log.Information("Game saved for player {PlayerName} (Level {Level}, {QuestCount} active quests, {PlayTime}m playtime)",
                saveGame.PlayerName, saveGame.Character.Level, saveGame.ActiveQuests.Count, saveGame.PlayTimeMinutes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save game for player {PlayerName}", saveGame.PlayerName);
            throw;
        }
    }

    /// <summary>
    /// Save the current game state (legacy compatibility - simplified version).
    /// </summary>
    public virtual void SaveGame(Character player, List<Item> inventory, string? saveId = null)
    {
        // For legacy compatibility, always create a new SaveGame unless saveId is provided
        var saveGame = saveId != null && _currentSave?.Id == saveId
            ? _currentSave
            : new SaveGame
            {
                Id = saveId ?? Guid.NewGuid().ToString(),
                PlayerName = player.Name,
                CreationDate = DateTime.Now
            };

        // Transfer legacy inventory parameter to Character.Inventory for backwards compatibility
        if (inventory != null && inventory.Any())
        {
            player.Inventory = inventory.ToList();
        }

        saveGame.Character = player;
        saveGame.PlayerName = player.Name; // Update in case it changed

        SaveGame(saveGame);
    }

    /// <summary>
    /// Load a game by save ID and restore all state.
    /// </summary>
    public virtual SaveGame? LoadGame(string saveId)
    {
        try
        {
            var save = _repository.GetById(saveId);
            if (save != null)
            {
                _currentSave = save;
                _gameStartTime = DateTime.Now.AddMinutes(-save.PlayTimeMinutes);

                Log.Information("Game loaded for player {PlayerName} (Level {Level}, {CompletionPercent:F1}% complete)",
                    save.PlayerName, save.Character.Level, save.GetCompletionPercentage());
            }
            return save;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load game with ID {SaveId}", saveId);
            throw;
        }
    }

    /// <summary>
    /// Get all available save games sorted by most recent.
    /// </summary>
    public virtual List<SaveGame> GetAllSaves()
    {
        try
        {
            return _repository.GetAll()
                .OrderByDescending(s => s.SaveDate)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve save games");
            throw;
        }
    }

    /// <summary>
    /// Delete a save game.
    /// </summary>
    public virtual bool DeleteSave(string saveId)
    {
        try
        {
            var result = _repository.Delete(saveId);
            if (result)
            {
                Log.Information("Save game deleted (ID: {SaveId})", saveId);
                if (_currentSave?.Id == saveId)
                {
                    _currentSave = null;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete save with ID {SaveId}", saveId);
            throw;
        }
    }

    /// <summary>
    /// Get the most recent save.
    /// </summary>
    public SaveGame? GetMostRecentSave()
    {
        try
        {
            return _repository.GetMostRecent();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to retrieve most recent save");
            throw;
        }
    }

    /// <summary>
    /// Check if any saves exist.
    /// </summary>
    public bool HasAnySaves()
    {
        try
        {
            return _repository.GetAll().Any();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for existing saves");
            return false;
        }
    }

    /// <summary>
    /// Auto-save the current game (overwrites existing save for this character).
    /// </summary>
    public void AutoSave(SaveGame saveGame)
    {
        try
        {
            SaveGame(saveGame);
            Log.Information("Auto-save completed for player {PlayerName}", saveGame.PlayerName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Auto-save failed for player {PlayerName}", saveGame.PlayerName);
            // Don't throw on auto-save failure, just log it
        }
    }

    /// <summary>
    /// Auto-save the current game (legacy compatibility).
    /// </summary>
    public void AutoSave(Character player, List<Item> inventory)
    {
        if (_currentSave != null)
        {
            _currentSave.Character = player;
            AutoSave(_currentSave);
        }
        else
        {
            // Create a new save if none exists
            var existingSaves = _repository.GetByPlayerName(player.Name);
            var saveId = existingSaves.FirstOrDefault()?.Id ?? Guid.NewGuid().ToString();
            SaveGame(player, inventory, saveId);
        }
    }

    /// <summary>
    /// Record a player death, incrementing death count and handling auto-save for Ironman mode.
    /// </summary>
    public void RecordDeath(string location, string killedBy)
    {
        if (_currentSave == null)
        {
            Log.Warning("Attempted to record death with no active save game");
            return;
        }

        _currentSave.DeathCount++;

        // Track death in game flags for historical reference
        if (!_currentSave.GameFlags.ContainsKey("deaths"))
        {
            _currentSave.GameFlags["deaths"] = true;
        }

        Log.Warning("Player death #{Count} at {Location} by {Killer}",
            _currentSave.DeathCount, location, killedBy);

        // Auto-save in Ironman mode
        if (_currentSave.IronmanMode)
        {
            SaveGame(_currentSave);
            Log.Information("Ironman auto-save triggered by death");
        }
    }

    // === QuestModel Management ===

    /// <summary>
    /// Add a QuestModel to available quests (offered but not accepted).
    /// </summary>
    public void AddAvailableQuest(QuestModel quest)
    {
        if (_currentSave != null && !_currentSave.AvailableQuests.Any(q => q.Id == quest.Id))
        {
            _currentSave.AvailableQuests.Add(quest);
            Log.Information("QuestModel '{QuestTitle}' added to available quests", quest.Title);
        }
    }

    /// <summary>
    /// Accept a QuestModel (move from available to active).
    /// </summary>
    public void AcceptQuest(string questId)
    {
        if (_currentSave == null) return;

        var quest = _currentSave.AvailableQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            _currentSave.AvailableQuests.Remove(quest);
            quest.IsActive = true;
            quest.StartTime = DateTime.Now;
            _currentSave.ActiveQuests.Add(quest);
            Log.Information("QuestModel '{QuestTitle}' accepted and started", quest.Title);
        }
    }

    /// <summary>
    /// Complete a QuestModel (move from active to completed).
    /// </summary>
    public void CompleteQuest(string questId)
    {
        if (_currentSave == null) return;

        var quest = _currentSave.ActiveQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            _currentSave.ActiveQuests.Remove(quest);
            quest.IsCompleted = true;
            quest.IsActive = false;
            _currentSave.CompletedQuests.Add(quest);
            _currentSave.QuestsCompleted++;
            Log.Information("QuestModel '{QuestTitle}' completed!", quest.Title);

            // Award bonus time in Apocalypse mode
            if (_currentSave.ApocalypseMode)
            {
                var bonusMinutes = quest.Difficulty.ToLower() switch
                {
                    "easy" => 10,
                    "medium" => 20,
                    "hard" => 30,
                    _ => 15
                };

                _apocalypseTimer.AddBonusTime(bonusMinutes, $"Completed quest: {quest.Title}");

                // Update save with new bonus time
                _currentSave.ApocalypseBonusMinutes = _apocalypseTimer.GetBonusMinutes();
                SaveGame(_currentSave);
            }
        }
    }

    /// <summary>
    /// Fail a QuestModel (move from active to failed).
    /// </summary>
    public void FailQuest(string questId, string reason = "Unknown")
    {
        if (_currentSave == null) return;

        var quest = _currentSave.ActiveQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            _currentSave.ActiveQuests.Remove(quest);
            quest.IsActive = false;
            _currentSave.FailedQuests.Add(quest);
            _currentSave.QuestsFailed++;
            Log.Warning("QuestModel '{QuestTitle}' failed: {Reason}", quest.Title, reason);
        }
    }

    /// <summary>
    /// Update QuestModel progress.
    /// </summary>
    public void UpdateQuestProgress(string questId, int progress)
    {
        if (_currentSave == null) return;

        var quest = _currentSave.ActiveQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            quest.Progress = progress;

            // Auto-complete if progress meets quantity
            if (progress >= quest.Quantity)
            {
                CompleteQuest(questId);
            }
        }
    }

    // === NPC Management ===

    /// <summary>
    /// Record meeting an NPC for the first time.
    /// </summary>
    public void MeetNPC(NPC npc)
    {
        if (_currentSave != null && !_currentSave.KnownNPCs.Any(n => n.Id == npc.Id))
        {
            _currentSave.KnownNPCs.Add(npc);
            _currentSave.NPCRelationships[npc.Id] = 0; // Neutral starting relationship
            Log.Information("Met NPC: {NpcName} ({Occupation})", npc.Name, npc.Occupation);
        }
    }

    /// <summary>
    /// Modify relationship with an NPC.
    /// </summary>
    public void ModifyNPCRelationship(string npcId, int change)
    {
        if (_currentSave == null) return;

        if (_currentSave.NPCRelationships.ContainsKey(npcId))
        {
            _currentSave.NPCRelationships[npcId] = Math.Clamp(
                _currentSave.NPCRelationships[npcId] + change,
                -100,
                100
            );
            Log.Debug("NPC relationship changed: {NpcId} {Change:+0;-0}", npcId, change);
        }
    }

    // === Location Management ===

    /// <summary>
    /// Discover a new location (visible on map but not visited).
    /// </summary>
    public void DiscoverLocation(string locationName)
    {
        if (_currentSave != null && !_currentSave.DiscoveredLocations.Contains(locationName)
            && !_currentSave.VisitedLocations.Contains(locationName))
        {
            _currentSave.DiscoveredLocations.Add(locationName);
            Log.Information("Location discovered: {LocationName}", locationName);
        }
    }

    /// <summary>
    /// Visit a location for the first time.
    /// </summary>
    public void VisitLocation(string locationName)
    {
        if (_currentSave == null) return;

        if (!_currentSave.VisitedLocations.Contains(locationName))
        {
            _currentSave.VisitedLocations.Add(locationName);
            _currentSave.DiscoveredLocations.Remove(locationName); // Move from discovered to visited
            Log.Information("Location visited: {LocationName}", locationName);
        }
    }

    // === Combat Tracking ===

    /// <summary>
    /// Record an enemy defeat.
    /// </summary>
    public void RecordEnemyDefeat(Enemy enemy)
    {
        if (_currentSave == null) return;

        _currentSave.TotalEnemiesDefeated++;

        // Track by type
        var enemyType = enemy.Type.ToString().ToLower();
        if (_currentSave.EnemiesDefeatedByType.ContainsKey(enemyType))
        {
            _currentSave.EnemiesDefeatedByType[enemyType]++;
        }
        else
        {
            _currentSave.EnemiesDefeatedByType[enemyType] = 1;
        }

        // Track legendary enemies
        if (enemy.Traits.ContainsKey("legendary") && enemy.Traits["legendary"].AsBool())
        {
            _currentSave.LegendaryEnemiesDefeated.Add(enemy);
            Log.Information("Legendary enemy defeated: {EnemyName} (Level {Level})", enemy.Name, enemy.Level);
        }
    }

    // === Statistics ===

    /// <summary>
    /// Record gold earned.
    /// </summary>
    public void RecordGoldEarned(int amount)
    {
        if (_currentSave != null)
        {
            _currentSave.TotalGoldEarned += amount;
        }
    }

    /// <summary>
    /// Record gold spent.
    /// </summary>
    public void RecordGoldSpent(int amount)
    {
        if (_currentSave != null)
        {
            _currentSave.TotalGoldSpent += amount;
        }
    }

    /// <summary>
    /// Record character death.
    /// </summary>
    public void RecordDeath()
    {
        if (_currentSave != null)
        {
            _currentSave.DeathCount++;
            Log.Warning("Player death recorded. Total deaths: {DeathCount}", _currentSave.DeathCount);

            // Auto-delete save in Ironman mode
            if (_currentSave.IronmanMode)
            {
                Log.Warning("Ironman mode: Save will be deleted");
                DeleteSave(_currentSave.Id);
            }
        }
    }

    /// <summary>
    /// Unlock an achievement.
    /// </summary>
    public void UnlockAchievement(string achievementId)
    {
        if (_currentSave != null && !_currentSave.UnlockedAchievements.Contains(achievementId))
        {
            _currentSave.UnlockedAchievements.Add(achievementId);
            Log.Information("Achievement unlocked: {AchievementId}", achievementId);
        }
    }

    /// <summary>
    /// Set a game flag (story events, choices, etc.).
    /// </summary>
    public void SetGameFlag(string flagName, bool value)
    {
        if (_currentSave != null)
        {
            _currentSave.GameFlags[flagName] = value;
            Log.Debug("Game flag set: {FlagName} = {Value}", flagName, value);
        }
    }

    /// <summary>
    /// Get a game flag value.
    /// </summary>
    public bool GetGameFlag(string flagName)
    {
        return _currentSave?.GameFlags.TryGetValue(flagName, out var value) == true && value;
    }

    /// <summary>
    /// Get the current active save.
    /// </summary>
    public virtual SaveGame? GetCurrentSave()
    {
        return _currentSave;
    }

    /// <summary>
    /// Set the current save game (used when loading or testing).
    /// </summary>
    public void SetCurrentSave(SaveGame saveGame)
    {
        _currentSave = saveGame;
        _gameStartTime = DateTime.Now.AddMinutes(-saveGame.PlayTimeMinutes);
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        _repository?.Dispose();
    }
}