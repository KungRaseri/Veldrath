using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Shared.Models;

/// <summary>
/// Defines difficulty settings and modifiers for different game modes.
/// </summary>
public class DifficultySettings : ICombatSettings
{
    /// <summary>Gets or sets the difficulty name.</summary>
    public string Name { get; set; } = "Normal";
    
    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the player damage multiplier.</summary>
    public double PlayerDamageMultiplier { get; set; } = 1.0;
    
    /// <summary>Gets or sets the enemy damage multiplier.</summary>
    public double EnemyDamageMultiplier { get; set; } = 1.0;
    
    /// <summary>Gets or sets the enemy health multiplier.</summary>
    public double EnemyHealthMultiplier { get; set; } = 1.0;
    
    /// <summary>Gets or sets the gold and XP multiplier.</summary>
    public double GoldXPMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets a value indicating whether auto-save only mode is enabled.</summary>
    public bool AutoSaveOnly { get; set; } = false;
    
    /// <summary>Gets or sets a value indicating whether permadeath is enabled.</summary>
    public bool IsPermadeath { get; set; } = false;
    
    /// <summary>Gets or sets a value indicating whether apocalypse mode is enabled.</summary>
    public bool IsApocalypse { get; set; } = false;
    
    /// <summary>Gets or sets the apocalypse time limit in minutes.</summary>
    public int ApocalypseTimeLimitMinutes { get; set; } = 240;

    /// <summary>Gets or sets the gold loss percentage on death.</summary>
    public double GoldLossPercentage { get; set; } = 0.10;
    
    /// <summary>Gets or sets the XP loss percentage on death.</summary>
    public double XPLossPercentage { get; set; } = 0.25;
    
    /// <summary>Gets or sets a value indicating whether all inventory is dropped on death.</summary>
    public bool DropAllInventoryOnDeath { get; set; } = false;
    
    /// <summary>Gets or sets the number of items dropped on death.</summary>
    public int ItemsDroppedOnDeath { get; set; } = 1;

    /// <summary>
    /// Easy mode - Story-focused with reduced challenge.
    /// </summary>
    public static DifficultySettings Easy => new()
    {
        Name = "Easy",
        Description = "Story Mode - Experience the adventure with reduced challenge",
        PlayerDamageMultiplier = 1.5,
        EnemyDamageMultiplier = 0.75,
        EnemyHealthMultiplier = 0.75,
        GoldXPMultiplier = 1.5,
        GoldLossPercentage = 0.05,
        XPLossPercentage = 0.10,
        ItemsDroppedOnDeath = 0
    };

    /// <summary>
    /// Normal mode - Balanced, intended experience (default).
    /// </summary>
    public static DifficultySettings Normal => new()
    {
        Name = "Normal",
        Description = "Balanced Experience - The intended gameplay (Recommended)",
        PlayerDamageMultiplier = 1.0,
        EnemyDamageMultiplier = 1.0,
        EnemyHealthMultiplier = 1.0,
        GoldXPMultiplier = 1.0,
        GoldLossPercentage = 0.10,
        XPLossPercentage = 0.25,
        ItemsDroppedOnDeath = 1
    };

    /// <summary>
    /// Hard mode - For experienced players seeking challenge.
    /// </summary>
    public static DifficultySettings Hard => new()
    {
        Name = "Hard",
        Description = "Experienced Players - Tactical combat, meaningful penalties",
        PlayerDamageMultiplier = 0.75,
        EnemyDamageMultiplier = 1.25,
        EnemyHealthMultiplier = 1.25,
        GoldXPMultiplier = 1.0,
        GoldLossPercentage = 0.20,
        XPLossPercentage = 0.50,
        DropAllInventoryOnDeath = true
    };

    /// <summary>
    /// Expert mode - Brutal challenge for masters.
    /// </summary>
    public static DifficultySettings Expert => new()
    {
        Name = "Expert",
        Description = "Brutal Challenge - For veterans only, very punishing",
        PlayerDamageMultiplier = 0.50,
        EnemyDamageMultiplier = 1.50,
        EnemyHealthMultiplier = 1.50,
        GoldXPMultiplier = 1.0,
        GoldLossPercentage = 0.30,
        XPLossPercentage = 0.75,
        DropAllInventoryOnDeath = true
    };

    /// <summary>
    /// Ironman mode - No save scumming, every choice permanent.
    /// </summary>
    public static DifficultySettings Ironman => new()
    {
        Name = "Ironman",
        Description = "No Reloading Saves - Every choice is permanent",
        PlayerDamageMultiplier = 0.75,
        EnemyDamageMultiplier = 1.25,
        EnemyHealthMultiplier = 1.25,
        GoldXPMultiplier = 1.0,
        AutoSaveOnly = true,
        GoldLossPercentage = 0.25,
        XPLossPercentage = 0.50,
        DropAllInventoryOnDeath = true
    };

    /// <summary>
    /// Permadeath mode - Death deletes save permanently.
    /// </summary>
    public static DifficultySettings Permadeath => new()
    {
        Name = "Permadeath",
        Description = "Death Deletes Save - One life. One chance. Hall of Fame glory.",
        PlayerDamageMultiplier = 0.50,
        EnemyDamageMultiplier = 1.50,
        EnemyHealthMultiplier = 1.50,
        GoldXPMultiplier = 1.0,
        AutoSaveOnly = true,
        IsPermadeath = true,
        // Death penalties don't matter - save is deleted
        GoldLossPercentage = 1.0,
        XPLossPercentage = 1.0,
        DropAllInventoryOnDeath = true
    };

    /// <summary>
    /// Apocalypse mode - 4-hour speed run challenge.
    /// </summary>
    public static DifficultySettings Apocalypse => new()
    {
        Name = "Apocalypse",
        Description = "4-Hour Speed Run - Race against time to save the world",
        PlayerDamageMultiplier = 1.0,
        EnemyDamageMultiplier = 1.0,
        EnemyHealthMultiplier = 1.0,
        GoldXPMultiplier = 1.0,
        IsApocalypse = true,
        ApocalypseTimeLimitMinutes = 240,
        GoldLossPercentage = 0.10,
        XPLossPercentage = 0.25,
        ItemsDroppedOnDeath = 1
    };

    /// <summary>
    /// Get difficulty settings by name.
    /// </summary>
    public static DifficultySettings GetByName(string name)
    {
        return name switch
        {
            "Easy" => Easy,
            "Normal" => Normal,
            "Hard" => Hard,
            "Expert" => Expert,
            "Ironman" => Ironman,
            "Permadeath" => Permadeath,
            "Apocalypse" => Apocalypse,
            _ => Normal
        };
    }

    /// <summary>
    /// Get all available difficulty options.
    /// </summary>
    public static List<DifficultySettings> GetAll()
    {
        return new List<DifficultySettings>
        {
            Easy,
            Normal,
            Hard,
            Expert,
            Ironman,
            Permadeath,
            Apocalypse
        };
    }
}
