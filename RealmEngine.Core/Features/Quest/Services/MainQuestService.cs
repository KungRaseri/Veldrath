namespace RealmEngine.Core.Features.Quests.Services;

using RealmEngine.Shared.Models;

/// <summary>
/// Manages the main quest chain and quest definitions.
/// </summary>
public class MainQuestService
{
    private readonly List<Quest> _allQuests;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainQuestService"/> class for mocking.
    /// </summary>
    /// <param name="initForMocking">Whether to initialize for mocking purposes.</param>
    protected MainQuestService(bool initForMocking)
    {
        _allQuests = new List<Quest>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainQuestService"/> class.
    /// </summary>
    public MainQuestService()
    {
        _allQuests = InitializeQuestDatabase();
    }

    /// <summary>
    /// Gets all main story quests in order.
    /// </summary>
    /// <returns>A list of main quests.</returns>
    public virtual async Task<List<Quest>> GetMainQuestChainAsync()
    {
        return await Task.FromResult(
            _allQuests.Where(q => q.Type == "main")
                      .OrderBy(q => q.Id)
                      .ToList()
        );
    }

    /// <summary>
    /// Gets a quest by its identifier.
    /// </summary>
    /// <param name="questId">The quest identifier.</param>
    /// <returns>The quest if found; otherwise, null.</returns>
    public virtual async Task<Quest?> GetQuestByIdAsync(string questId)
    {
        return await Task.FromResult(_allQuests.FirstOrDefault(q => q.Id == questId));
    }

    private List<Quest> InitializeQuestDatabase()
    {
        return new List<Quest>
        {
            // Main Quest 1: The Awakening
            new Quest
            {
                Id = "main_01_awakening",
                Title = "The Awakening",
                Description = "A mysterious voice calls to you. Investigate the ancient shrine.",
                Type = "main",
                Difficulty = "easy",
                Objectives = new Dictionary<string, int> { { "reach_ancient_shrine", 1 } },
                ObjectiveProgress = new Dictionary<string, int> { { "reach_ancient_shrine", 0 } },
                XpReward = 100,
                GoldReward = 50,
                ApocalypseBonusMinutes = 15,
                Prerequisites = new List<string>()
            },
            
            // Main Quest 2: The First Trial
            new Quest
            {
                Id = "main_02_first_trial",
                Title = "The First Trial",
                Description = "Defeat the guardian of the shrine to prove your worth.",
                Type = "main",
                Difficulty = "medium",
                Objectives = new Dictionary<string, int> { { "defeat_shrine_guardian", 1 } },
                ObjectiveProgress = new Dictionary<string, int> { { "defeat_shrine_guardian", 0 } },
                XpReward = 250,
                GoldReward = 100,
                ApocalypseBonusMinutes = 20,
                Prerequisites = new List<string> { "main_01_awakening" }
            },
            
            // Main Quest 3: Gathering Power
            new Quest
            {
                Id = "main_03_gathering_power",
                Title = "Gathering Power",
                Description = "Collect ancient artifacts to strengthen yourself for the battles ahead.",
                Type = "main",
                Difficulty = "medium",
                Objectives = new Dictionary<string, int> { { "collect_ancient_artifacts", 3 } },
                ObjectiveProgress = new Dictionary<string, int> { { "collect_ancient_artifacts", 0 } },
                XpReward = 400,
                GoldReward = 200,
                ApocalypseBonusMinutes = 25,
                Prerequisites = new List<string> { "main_02_first_trial" }
            },
            
            // Main Quest 4: The Dark Prophecy
            new Quest
            {
                Id = "main_04_dark_prophecy",
                Title = "The Dark Prophecy",
                Description = "Seek out the oracle and learn of the coming apocalypse.",
                Type = "main",
                Difficulty = "hard",
                Objectives = new Dictionary<string, int> { { "talk_to_oracle", 1 } },
                ObjectiveProgress = new Dictionary<string, int> { { "talk_to_oracle", 0 } },
                XpReward = 600,
                GoldReward = 300,
                ApocalypseBonusMinutes = 30,
                Prerequisites = new List<string> { "main_03_gathering_power" }
            },
            
            // Main Quest 5: Into the Abyss
            new Quest
            {
                Id = "main_05_into_abyss",
                Title = "Into the Abyss",
                Description = "Enter the Abyssal Depths and confront the source of evil.",
                Type = "main",
                Difficulty = "hard",
                Objectives = new Dictionary<string, int>
                {
                    { "reach_abyssal_depths", 1 },
                    { "defeat_abyssal_demons", 5 }
                },
                ObjectiveProgress = new Dictionary<string, int>
                {
                    { "reach_abyssal_depths", 0 },
                    { "defeat_abyssal_demons", 0 }
                },
                XpReward = 1000,
                GoldReward = 500,
                ApocalypseBonusMinutes = 40,
                Prerequisites = new List<string> { "main_04_dark_prophecy" }
            },
            
            // Main Quest 6: The Final Confrontation
            new Quest
            {
                Id = "main_06_final_boss",
                Title = "The Final Confrontation",
                Description = "Defeat the Dark Lord and save the world from destruction.",
                Type = "main",
                Difficulty = "epic",
                Objectives = new Dictionary<string, int> { { "defeat_dark_lord", 1 } },
                ObjectiveProgress = new Dictionary<string, int> { { "defeat_dark_lord", 0 } },
                XpReward = 2000,
                GoldReward = 1000,
                ApocalypseBonusMinutes = 60,
                Prerequisites = new List<string> { "main_05_into_abyss" }
            }
        };
    }
}
