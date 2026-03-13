namespace RealmEngine.Shared.Models;

/// <summary>
/// Records a character's final moments for the Hall of Fame.
/// Used primarily for permadeath mode but can track any character death.
/// </summary>
public class HallOfFameEntry
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>Gets or sets the character name.</summary>
    public string CharacterName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the class name.</summary>
    public string ClassName { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }
    
    /// <summary>Gets or sets the play time in minutes.</summary>
    public int PlayTimeMinutes { get; set; }
    
    /// <summary>Gets or sets the total enemies defeated.</summary>
    public int TotalEnemiesDefeated { get; set; }
    
    /// <summary>Gets or sets the quests completed.</summary>
    public int QuestsCompleted { get; set; }
    
    /// <summary>Gets or sets the death count.</summary>
    public int DeathCount { get; set; }
    
    /// <summary>Gets or sets the death reason.</summary>
    public string DeathReason { get; set; } = "Unknown";
    
    /// <summary>Gets or sets the death location.</summary>
    public string DeathLocation { get; set; } = "Unknown";
    
    /// <summary>Gets or sets the death date.</summary>
    public DateTime DeathDate { get; set; } = DateTime.Now;
    
    /// <summary>Gets or sets the achievements unlocked.</summary>
    public int AchievementsUnlocked { get; set; }
    
    /// <summary>Gets or sets a value indicating whether this is a permadeath run.</summary>
    public bool IsPermadeath { get; set; }
    
    /// <summary>Gets or sets the difficulty level.</summary>
    public string DifficultyLevel { get; set; } = "Normal";

    /// <summary>
    /// Pre-calculated fame score for ranking (persisted for efficient leaderboard queries).
    /// </summary>
    public int FameScore { get; set; }

    /// <summary>
    /// Calculate and update the fame score for ranking.
    /// Call this before saving to database.
    /// </summary>
    public void CalculateFameScore()
    {
        var score = Level * 100;
        score += QuestsCompleted * 50;
        score += TotalEnemiesDefeated * 5;
        score += AchievementsUnlocked * 200;

        if (IsPermadeath)
            score *= 2; // Double points for permadeath runs

        FameScore = score;
    }

    /// <summary>
    /// Calculate a "fame score" for ranking (for backwards compatibility).
    /// </summary>
    public int GetFameScore()
    {
        return FameScore;
    }

    /// <summary>
    /// Get formatted playtime string.
    /// </summary>
    public string GetPlaytimeFormatted()
    {
        var hours = PlayTimeMinutes / 60;
        var minutes = PlayTimeMinutes % 60;
        return $"{hours}h {minutes}m";
    }
}
