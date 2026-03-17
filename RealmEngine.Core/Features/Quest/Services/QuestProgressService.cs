using RealmEngine.Core.Features.SaveLoad;
using Microsoft.Extensions.Logging;
namespace RealmEngine.Core.Features.Quests.Services;

/// <summary>
/// Service for tracking and updating quest objective progress.
/// </summary>
public class QuestProgressService
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<QuestProgressService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestProgressService"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public QuestProgressService(ISaveGameService saveGameService, ILogger<QuestProgressService> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Updates progress for a specific quest objective.
    /// </summary>
    /// <param name="questId">The quest identifier.</param>
    /// <param name="objectiveId">The objective identifier.</param>
    /// <param name="amount">The amount to increment progress by.</param>
    /// <returns>A tuple indicating success, objective completion status, and overall quest completion status.</returns>
    public virtual async Task<(bool Success, bool ObjectiveCompleted, bool QuestCompleted)> UpdateProgressAsync(
        string questId, string objectiveId, int amount)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame == null)
            return (false, false, false);

        var quest = saveGame.ActiveQuests.FirstOrDefault(q => q.Id == questId);
        if (quest == null)
            return (false, false, false);

        if (!quest.Objectives.ContainsKey(objectiveId))
            return (false, false, false);

        // Update progress
        var current = quest.ObjectiveProgress.ContainsKey(objectiveId) ? quest.ObjectiveProgress[objectiveId] : 0;
        var required = quest.Objectives[objectiveId];

        quest.ObjectiveProgress[objectiveId] = Math.Min(current + amount, required);

        var objectiveCompleted = quest.ObjectiveProgress[objectiveId] >= required;
        var questCompleted = quest.Objectives.All(kvp =>
            quest.ObjectiveProgress.ContainsKey(kvp.Key) && quest.ObjectiveProgress[kvp.Key] >= kvp.Value);

        _saveGameService.SaveGame(saveGame);

        _logger.LogDebug("Quest progress updated: {QuestId}/{ObjectiveId} = {Current}/{Required}",
            questId, objectiveId, quest.ObjectiveProgress[objectiveId], required);

        return await Task.FromResult((true, objectiveCompleted, questCompleted));
    }
}
