using LiteDB;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a saved game state with all player progress and game world state.
/// </summary>
public class SaveGame
{
    /// <summary>Gets or sets the unique identifier.</summary>
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Gets or sets the player name.</summary>
    public string PlayerName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the save date.</summary>
    public DateTime SaveDate { get; set; } = DateTime.Now;
    
    /// <summary>Gets or sets the creation date.</summary>
    public DateTime CreationDate { get; set; } = DateTime.Now;
    
    /// <summary>Gets or sets the play time in minutes.</summary>
    public int PlayTimeMinutes { get; set; }
    
    /// <summary>Gets or sets the game version.</summary>
    public string GameVersion { get; set; } = "1.0.0";

    /// <summary>Gets or sets the character data.</summary>
    /// <summary>Gets or sets the character data.</summary>
    public Character Character { get; set; } = new();

    // === Party System ===
    /// <summary>Gets or sets the player's party (leader + recruited NPCs).</summary>
    public Party? Party { get; set; }

    // === Reputation & Factions ===
    /// <summary>Gets or sets reputation standings with all factions.</summary>
    public Dictionary<string, ReputationStanding> FactionReputations { get; set; } = new();

    /// <summary>Gets or sets the active quests.</summary>
    public List<Quest> ActiveQuests { get; set; } = new();
    
    /// <summary>Gets or sets the completed quests.</summary>
    public List<Quest> CompletedQuests { get; set; } = new();
    
    /// <summary>Gets or sets the failed quests.</summary>
    public List<Quest> FailedQuests { get; set; } = new();
    
    /// <summary>Gets or sets the available quests (offered but not yet accepted).</summary>
    public List<Quest> AvailableQuests { get; set; } = new();

    // === World State ===
    /// <summary>Gets or sets the known NPCs (NPCs the player has met).</summary>
    public List<NPC> KnownNPCs { get; set; } = new();
    /// <summary>Gets or sets the NPC relationships (NPC ID -> relationship value from -100 to 100).</summary>
    public Dictionary<string, int> NPCRelationships { get; set; } = new();
    /// <summary>Gets or sets the visited locations.</summary>
    public List<string> VisitedLocations { get; set; } = new();
    /// <summary>Gets or sets the discovered locations (on map but not visited).</summary>
    public List<string> DiscoveredLocations { get; set; } = new();

    /// <summary>Gets or sets the total enemies defeated.</summary>
    public int TotalEnemiesDefeated { get; set; }
    
    /// <summary>Gets or sets the enemies defeated by type.</summary>
    public Dictionary<string, int> EnemiesDefeatedByType { get; set; } = new();
    
    /// <summary>Gets or sets the legendary enemies defeated.</summary>
    public List<Enemy> LegendaryEnemiesDefeated { get; set; } = new();

    /// <summary>Gets or sets the total gold earned.</summary>
    public int TotalGoldEarned { get; set; }
    
    /// <summary>Gets or sets the total gold spent.</summary>
    public int TotalGoldSpent { get; set; }
    
    /// <summary>Gets or sets the quests completed.</summary>
    public int QuestsCompleted { get; set; }
    
    /// <summary>Gets or sets the quests failed.</summary>
    public int QuestsFailed { get; set; }
    
    /// <summary>Gets or sets the items crafted.</summary>
    public int ItemsCrafted { get; set; }
    
    /// <summary>Gets or sets the items sold.</summary>
    public int ItemsSold { get; set; }
    
    /// <summary>Gets or sets the death count.</summary>
    public int DeathCount { get; set; }
    
    /// <summary>Gets or sets the last death location.</summary>
    public string LastDeathLocation { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the last death date.</summary>
    public DateTime? LastDeathDate { get; set; }

    /// <summary>Gets or sets the unlocked achievements.</summary>
    public List<string> UnlockedAchievements { get; set; } = new();
    
    /// <summary>Gets or sets the game flags (story flags, events, etc.).</summary>
    public Dictionary<string, bool> GameFlags { get; set; } = new();

    /// <summary>Gets or sets the difficulty level.</summary>
    public string DifficultyLevel { get; set; } = "Normal";
    
    /// <summary>Gets or sets a value indicating whether ironman mode is enabled (no reload, single save file).</summary>
    public bool IronmanMode { get; set; } = false;

    /// <summary>Gets or sets a value indicating whether permadeath mode is enabled.</summary>
    public bool PermadeathMode { get; set; } = false;
    
    /// <summary>Gets or sets a value indicating whether apocalypse mode is enabled.</summary>
    public bool ApocalypseMode { get; set; } = false;
    
    /// <summary>Gets or sets the apocalypse start time.</summary>
    public DateTime? ApocalypseStartTime { get; set; }
    
    /// <summary>Gets or sets the apocalypse bonus minutes.</summary>
    public int ApocalypseBonusMinutes { get; set; } = 0;

    /// <summary>Gets or sets the dropped items at locations (for tracking items dropped on death).</summary>
    public Dictionary<string, List<Item>> DroppedItemsAtLocations { get; set; } = new();

    /// <summary>
    /// Get a summary of the save game for display in load menu.
    /// </summary>
    public string GetSummary()
    {
        return $"{PlayerName} - Level {Character.Level} {Character.ClassName} - {PlayTimeMinutes / 60}h {PlayTimeMinutes % 60}m - {ActiveQuests.Count} active quests";
    }

    /// <summary>
    /// Calculate completion percentage based on various metrics.
    /// </summary>
    public double GetCompletionPercentage()
    {
        double total = 0;
        double completed = 0;

        // Quest completion (40% weight)
        if (CompletedQuests.Count + ActiveQuests.Count + FailedQuests.Count > 0)
        {
            total += 40;
            completed += (CompletedQuests.Count / (double)(CompletedQuests.Count + ActiveQuests.Count + FailedQuests.Count)) * 40;
        }

        // Location discovery (30% weight)
        if (DiscoveredLocations.Count + VisitedLocations.Count > 0)
        {
            total += 30;
            completed += (VisitedLocations.Count / (double)(DiscoveredLocations.Count + VisitedLocations.Count)) * 30;
        }

        // Achievement completion (30% weight)
        // Assuming 50 total achievements for now
        total += 30;
        completed += (UnlockedAchievements.Count / 50.0) * 30;

        return total > 0 ? (completed / total) * 100 : 0;
    }
}
