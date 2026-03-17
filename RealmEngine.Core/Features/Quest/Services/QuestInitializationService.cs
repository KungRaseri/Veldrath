using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Quests.Services;

/// <summary>
/// Service for initializing quest chains when starting a new game.
/// </summary>
public class QuestInitializationService
{
    private readonly MainQuestService _mainQuestService;
    private readonly ILogger<QuestInitializationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuestInitializationService"/> class.
    /// </summary>
    /// <param name="mainQuestService">The main quest service.</param>
    /// <param name="logger">The logger.</param>
    public QuestInitializationService(MainQuestService mainQuestService, ILogger<QuestInitializationService> logger)
    {
        _mainQuestService = mainQuestService;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the first main quest as available when a new game starts.
    /// </summary>
    public virtual async Task InitializeStartingQuests(SaveGame saveGame)
    {
        // Get the first main quest in the chain
        var firstQuest = await _mainQuestService.GetQuestByIdAsync("main_01_awakening");
        
        if (firstQuest != null)
        {
            // Add to available quests (player can accept it when ready)
            saveGame.AvailableQuests.Add(firstQuest);
            
            _logger.LogInformation("Initialized starting quest: {QuestTitle}", firstQuest.Title);
        }
        else
        {
            _logger.LogWarning("Failed to load starting quest 'main_01_awakening'");
        }
    }

    /// <summary>
    /// Checks if any new quests should become available based on completed prerequisites.
    /// Called after a quest is completed to unlock the next quest in the chain.
    /// </summary>
    public virtual async Task UnlockNextQuestsAsync(SaveGame saveGame, string completedQuestId)
    {
        // Get all main quests
        var allQuests = await _mainQuestService.GetMainQuestChainAsync();

        foreach (var quest in allQuests)
        {
            // Skip if quest is already known (available, active, or completed)
            if (saveGame.AvailableQuests.Any(q => q.Id == quest.Id) ||
                saveGame.ActiveQuests.Any(q => q.Id == quest.Id) ||
                saveGame.CompletedQuests.Any(q => q.Id == quest.Id))
            {
                continue;
            }

            // Check if all prerequisites are met
            if (quest.Prerequisites.Contains(completedQuestId))
            {
                var allPrerequisitesMet = quest.Prerequisites.All(prereqId =>
                    saveGame.CompletedQuests.Any(q => q.Id == prereqId));

                if (allPrerequisitesMet)
                {
                    saveGame.AvailableQuests.Add(quest);
                    _logger.LogInformation("Unlocked new quest: {QuestTitle} (prerequisite {PrereqId} completed)",
                        quest.Title, completedQuestId);
                }
            }
        }
    }
}
