using RealmEngine.Shared.Models;
using QuestModel = RealmEngine.Shared.Models.Quest;

namespace RealmEngine.Core.Features.SaveLoad;

/// <summary>
/// Interface for managing game saves and loads with comprehensive world state tracking.
/// Enables mocking and testing of services that depend on SaveGameService.
/// </summary>
public interface ISaveGameService
{
    /// <summary>
    /// Initialize a new game session with a fresh SaveGame object.
    /// </summary>
    SaveGame CreateNewGame(Character player, DifficultySettings difficulty);

    /// <summary>
    /// Get difficulty settings from current save.
    /// </summary>
    DifficultySettings GetDifficultySettings();

    /// <summary>
    /// Save the current game state with all world data.
    /// </summary>
    void SaveGame(SaveGame saveGame);

    /// <summary>
    /// Save the current game state (legacy compatibility - simplified version).
    /// </summary>
    void SaveGame(Character player, List<Item> inventory, string? saveId = null);

    /// <summary>
    /// Load a game by save ID and restore all state.
    /// </summary>
    SaveGame? LoadGame(string saveId);

    /// <summary>
    /// Get all available save games sorted by most recent.
    /// </summary>
    List<SaveGame> GetAllSaves();

    /// <summary>
    /// Delete a save game.
    /// </summary>
    bool DeleteSave(string saveId);

    /// <summary>
    /// Get the most recent save.
    /// </summary>
    SaveGame? GetMostRecentSave();

    /// <summary>
    /// Check if any saves exist.
    /// </summary>
    bool HasAnySaves();

    /// <summary>
    /// Auto-save the current game (overwrites existing save for this character).
    /// </summary>
    void AutoSave(SaveGame saveGame);

    /// <summary>
    /// Auto-save the current game (legacy compatibility).
    /// </summary>
    void AutoSave(Character player, List<Item> inventory);

    /// <summary>
    /// Get the currently active save game.
    /// </summary>
    SaveGame? GetCurrentSave();

    /// <summary>
    /// Set the current save game (used when loading).
    /// </summary>
    void SetCurrentSave(SaveGame saveGame);

    /// <summary>
    /// Record a player death, incrementing death count.
    /// </summary>
    void RecordDeath(string location, string killedBy);

    /// <summary>
    /// Record a player death (legacy version).
    /// </summary>
    void RecordDeath();

    /// <summary>
    /// Add a QuestModel to available quests (offered but not accepted).
    /// </summary>
    void AddAvailableQuest(QuestModel QuestModel);

    /// <summary>
    /// Accept a QuestModel and move it to active quests.
    /// </summary>
    void AcceptQuest(string questId);

    /// <summary>
    /// Complete a QuestModel and move it to completed quests.
    /// </summary>
    void CompleteQuest(string questId);

    /// <summary>
    /// Fail a QuestModel and move it to failed quests.
    /// </summary>
    void FailQuest(string questId, string reason = "Unknown");

    /// <summary>
    /// Update progress for a specific quest.
    /// </summary>
    void UpdateQuestProgress(string questId, int progress);

    /// <summary>
    /// Record meeting an NPC for the first time.
    /// </summary>
    void MeetNPC(NPC npc);

    /// <summary>
    /// Modify relationship value with an NPC.
    /// </summary>
    void ModifyNPCRelationship(string npcId, int change);

    /// <summary>
    /// Discover a new location (adds to known locations).
    /// </summary>
    void DiscoverLocation(string locationName);

    /// <summary>
    /// Visit a location (marks as visited and updates stats).
    /// </summary>
    void VisitLocation(string locationName);

    /// <summary>
    /// Record defeating an enemy (updates stats and tracked enemies).
    /// </summary>
    void RecordEnemyDefeat(Enemy enemy);

    /// <summary>
    /// Record gold earned for statistics tracking.
    /// </summary>
    void RecordGoldEarned(int amount);

    /// <summary>
    /// Record gold spent for statistics tracking.
    /// </summary>
    void RecordGoldSpent(int amount);
}
