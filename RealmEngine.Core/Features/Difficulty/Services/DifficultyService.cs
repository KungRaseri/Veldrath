using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Difficulty.Services;

/// <summary>
/// Domain service for difficulty-related calculations and logic.
/// </summary>
public class DifficultyService
{
    /// <summary>
    /// Calculate player damage with difficulty multiplier.
    /// </summary>
    /// <param name="baseDamage">The base damage before difficulty adjustment.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The adjusted damage value.</returns>
    public int CalculatePlayerDamage(int baseDamage, DifficultySettings difficulty)
    {
        var adjusted = baseDamage * difficulty.PlayerDamageMultiplier;
        return (int)Math.Round(adjusted);
    }

    /// <summary>
    /// Calculate enemy damage with difficulty multiplier.
    /// </summary>
    /// <param name="baseDamage">The base damage before difficulty adjustment.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The adjusted damage value.</returns>
    public int CalculateEnemyDamage(int baseDamage, DifficultySettings difficulty)
    {
        var adjusted = baseDamage * difficulty.EnemyDamageMultiplier;
        return (int)Math.Round(adjusted);
    }

    /// <summary>
    /// Calculate enemy health with difficulty multiplier.
    /// </summary>
    /// <param name="baseHealth">The base health before difficulty adjustment.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The adjusted health value.</returns>
    public int CalculateEnemyHealth(int baseHealth, DifficultySettings difficulty)
    {
        var adjusted = baseHealth * difficulty.EnemyHealthMultiplier;
        return (int)Math.Round(adjusted);
    }

    /// <summary>
    /// Calculate gold reward with difficulty multiplier.
    /// </summary>
    /// <param name="baseGold">The base gold before difficulty adjustment.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The adjusted gold value.</returns>
    public int CalculateGoldReward(int baseGold, DifficultySettings difficulty)
    {
        var adjusted = baseGold * difficulty.GoldXPMultiplier;
        return (int)Math.Round(adjusted);
    }

    /// <summary>
    /// Calculate XP reward with difficulty multiplier.
    /// </summary>
    /// <param name="baseXP">The base XP before difficulty adjustment.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The adjusted XP value.</returns>
    public int CalculateXPReward(int baseXP, DifficultySettings difficulty)
    {
        var adjusted = baseXP * difficulty.GoldXPMultiplier;
        return (int)Math.Round(adjusted);
    }

    /// <summary>
    /// Calculate gold lost on death based on difficulty.
    /// </summary>
    /// <param name="currentGold">The player's current gold.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The amount of gold to lose.</returns>
    public int CalculateGoldLoss(int currentGold, DifficultySettings difficulty)
    {
        var loss = currentGold * difficulty.GoldLossPercentage;
        return (int)Math.Round(loss);
    }

    /// <summary>
    /// Calculate XP lost on death based on difficulty.
    /// </summary>
    /// <param name="currentXP">The player's current XP.</param>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The amount of XP to lose.</returns>
    public int CalculateXPLoss(int currentXP, DifficultySettings difficulty)
    {
        var loss = currentXP * difficulty.XPLossPercentage;
        return (int)Math.Round(loss);
    }

    /// <summary>
    /// Check if difficulty allows manual saving.
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>True if manual saving is allowed, false if auto-save only.</returns>
    public bool CanManualSave(DifficultySettings difficulty)
    {
        return !difficulty.AutoSaveOnly;
    }

    /// <summary>
    /// Check if difficulty has permadeath enabled.
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>True if permadeath is enabled.</returns>
    public bool IsPermadeath(DifficultySettings difficulty)
    {
        return difficulty.IsPermadeath;
    }

    /// <summary>
    /// Check if difficulty has apocalypse mode enabled.
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>True if apocalypse mode is enabled.</returns>
    public bool IsApocalypseMode(DifficultySettings difficulty)
    {
        return difficulty.IsApocalypse;
    }

    /// <summary>
    /// Get a summary of difficulty modifiers for display.
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>A dictionary of modifier descriptions.</returns>
    public Dictionary<string, string> GetDifficultySummary(DifficultySettings difficulty)
    {
        var summary = new Dictionary<string, string>
        {
            ["Name"] = difficulty.Name,
            ["Description"] = difficulty.Description,
            ["Player Damage"] = FormatMultiplier(difficulty.PlayerDamageMultiplier),
            ["Enemy Damage"] = FormatMultiplier(difficulty.EnemyDamageMultiplier),
            ["Enemy Health"] = FormatMultiplier(difficulty.EnemyHealthMultiplier),
            ["Gold/XP Gain"] = FormatMultiplier(difficulty.GoldXPMultiplier),
            ["Gold Loss on Death"] = FormatPercentage(difficulty.GoldLossPercentage),
            ["XP Loss on Death"] = FormatPercentage(difficulty.XPLossPercentage)
        };

        if (difficulty.AutoSaveOnly)
        {
            summary["Save Mode"] = "Auto-save only (Ironman)";
        }

        if (difficulty.IsPermadeath)
        {
            summary["Permadeath"] = "Enabled - Death deletes save";
        }

        if (difficulty.IsApocalypse)
        {
            summary["Time Limit"] = $"{difficulty.ApocalypseTimeLimitMinutes} minutes";
        }

        if (difficulty.DropAllInventoryOnDeath)
        {
            summary["Item Loss"] = "Drop ALL items on death";
        }
        else if (difficulty.ItemsDroppedOnDeath > 0)
        {
            summary["Item Loss"] = $"Drop {difficulty.ItemsDroppedOnDeath} item(s) on death";
        }
        else
        {
            summary["Item Loss"] = "No items dropped";
        }

        return summary;
    }

    private static string FormatMultiplier(double multiplier)
    {
        if (multiplier == 1.0)
            return "Normal (×1.0)";
        if (multiplier > 1.0)
            return $"Increased (×{multiplier:F1})";
        return $"Decreased (×{multiplier:F1})";
    }

    private static string FormatPercentage(double percentage)
    {
        var percent = percentage * 100;
        return $"{percent:F0}%";
    }
}
