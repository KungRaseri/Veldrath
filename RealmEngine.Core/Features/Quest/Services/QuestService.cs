using RealmEngine.Core.Features.SaveLoad;
using Microsoft.Extensions.Logging;

using RealmEngine.Shared.Models;
namespace RealmEngine.Core.Features.Quests.Services;

/// <summary>
/// Service for managing quest lifecycle including starting, completing, and tracking quests.
/// </summary>
public class QuestService
{
    private readonly ISaveGameService _saveGameService;
    private readonly MainQuestService _mainQuestService;
    private readonly QuestInitializationService _initService;
    private readonly ILogger<QuestService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="mainQuestService">The main quest service.</param>
    /// <param name="initService">The quest initialization service.</param>
    /// <param name="logger">The logger.</param>
    public QuestService(
        ISaveGameService saveGameService, 
        MainQuestService mainQuestService,
        QuestInitializationService initService,
        ILogger<QuestService> logger)
    {
        _saveGameService = saveGameService;
        _mainQuestService = mainQuestService;
        _initService = initService;
        _logger = logger;
    }

    /// <summary>
    /// Starts a quest by adding it to the active quests list.
    /// </summary>
    /// <param name="questId">The quest identifier.</param>
    /// <returns>A tuple containing success status, quest object, and error message if any.</returns>
    public virtual async Task<(bool Success, Quest? Quest, string ErrorMessage)> StartQuestAsync(string questId)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return (false, null, "No active save game");

        // Check if quest is already active or completed
        if (saveGame.ActiveQuests.Any(q => q.Id == questId))
            return (false, null, "Quest is already active");

        if (saveGame.CompletedQuests.Any(q => q.Id == questId))
            return (false, null, "Quest is already completed");

        // Get quest definition
        var quest = await GetQuestDefinitionAsync(questId);
        if (quest == null)
            return (false, null, "Quest not found");

        // Check prerequisites
        if (!CheckPrerequisites(quest, saveGame))
            return (false, null, "Prerequisites not met");

        // Check level requirement (use existing logic or default to level 1)
        // For now, we'll skip level requirements since the existing Quest model doesn't have RequiredLevel

        // Start quest
        quest.IsActive = true;
        quest.StartTime = DateTime.Now;
        saveGame.ActiveQuests.Add(quest);

        _saveGameService.SaveGame(saveGame);

        _logger.LogInformation("Quest started: {QuestId} - {QuestTitle}", questId, quest.Title);

        return await Task.FromResult((true, quest, string.Empty));
    }

    /// <summary>
    /// Completes a quest after verifying all objectives are met.
    /// </summary>
    /// <param name="questId">The quest identifier.</param>
    /// <returns>A tuple containing success status, completed quest object, and error message if any.</returns>
    public virtual async Task<(bool Success, Quest? Quest, string ErrorMessage)> CompleteQuestAsync(string questId)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return (false, null, "No active save game");

        var quest = saveGame.ActiveQuests.FirstOrDefault(q => q.Id == questId);
        if (quest == null)
            return (false, null, "Quest is not active");

        // Check if all objectives are complete
        var allObjectivesComplete = quest.Objectives.All(kvp =>
            quest.ObjectiveProgress.ContainsKey(kvp.Key) && quest.ObjectiveProgress[kvp.Key] >= kvp.Value);

        if (!allObjectivesComplete)
            return (false, null, "Not all objectives complete");

        // Complete quest
        quest.IsCompleted = true;
        quest.IsActive = false;

        saveGame.ActiveQuests.Remove(quest);
        saveGame.CompletedQuests.Add(quest);
        saveGame.QuestsCompleted++;

        _saveGameService.SaveGame(saveGame);

        _logger.LogInformation("Quest completed: {QuestId} - {QuestTitle}", questId, quest.Title);

        // Unlock next quests in the chain
        await _initService.UnlockNextQuestsAsync(saveGame, questId);

        return await Task.FromResult((true, quest, string.Empty));
    }

    /// <summary>
    /// Gets all active quests from the current save game.
    /// </summary>
    /// <returns>A list of active quests.</returns>
    public virtual async Task<List<Quest>> GetActiveQuestsAsync()
    {
        var saveGame = _saveGameService.GetCurrentSave();
        return await Task.FromResult(saveGame?.ActiveQuests ?? new List<Quest>());
    }

    private async Task<Quest?> GetQuestDefinitionAsync(string questId)
    {
        // Get from main quest service or quest database
        return await _mainQuestService.GetQuestByIdAsync(questId);
    }

    private bool CheckPrerequisites(Quest quest, SaveGame saveGame)
    {
        return quest.Prerequisites.All(prereqId =>
            saveGame.CompletedQuests.Any(q => q.Id == prereqId));
    }
}
