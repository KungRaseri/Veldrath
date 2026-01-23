using RealmEngine.Shared.Models;
using Serilog;

namespace RealmEngine.Core.Features.Death.Services;

/// <summary>
/// Domain service for respawn-related logic and calculations.
/// </summary>
public class RespawnService
{
    /// <summary>
    /// Calculate the appropriate respawn location based on player progression.
    /// </summary>
    /// <param name="saveGame">The current save game.</param>
    /// <param name="deathLocation">Where the player died.</param>
    /// <returns>The recommended respawn location.</returns>
    public string DetermineRespawnLocation(SaveGame saveGame, string deathLocation)
    {
        // Default to hub town
        var respawnLocation = "Hub Town";

        // If player has discovered other safe zones, respawn at the closest one
        var safeTowns = saveGame.DiscoveredLocations
            .Where(loc => loc.Contains("Town") || loc.Contains("Village") || loc.Contains("Sanctuary"))
            .ToList();

        if (safeTowns.Count > 1)
        {
            // Simple heuristic: if death location contains a region name, respawn in that region's town
            foreach (var town in safeTowns)
            {
                if (deathLocation.Contains(ExtractRegion(town)))
                {
                    respawnLocation = town;
                    break;
                }
            }
        }

        Log.Debug("Player died at {DeathLocation}, respawning at {RespawnLocation}",
            deathLocation, respawnLocation);

        return respawnLocation;
    }

    /// <summary>
    /// Check if a location is a valid respawn point.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="saveGame">The current save game.</param>
    /// <returns>True if the location is a valid respawn point.</returns>
    public bool IsValidRespawnPoint(string location, SaveGame saveGame)
    {
        // Hub Town is always valid
        if (location == "Hub Town")
            return true;

        // Must be discovered
        if (!saveGame.DiscoveredLocations.Contains(location))
            return false;

        // Must be a safe zone (town, village, sanctuary)
        return location.Contains("Town") || 
               location.Contains("Village") || 
               location.Contains("Sanctuary");
    }

    /// <summary>
    /// Get respawn health/mana restoration percentage based on difficulty.
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>The percentage of health/mana to restore (0.0 to 1.0).</returns>
    public double GetRespawnRestorationPercentage(DifficultySettings difficulty)
    {
        // Easy mode: full restoration
        if (difficulty.Name == "Easy")
            return 1.0;

        // Normal/Hard: full restoration
        if (difficulty.Name == "Normal" || difficulty.Name == "Hard")
            return 1.0;

        // Expert/Ironman: 75% restoration
        if (difficulty.Name == "Expert" || difficulty.Name == "Ironman")
            return 0.75;

        // Permadeath: doesn't matter (player is dead)
        return 1.0;
    }

    /// <summary>
    /// Calculate respawn cooldown in seconds (time before player can act).
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <returns>Cooldown in seconds.</returns>
    public int GetRespawnCooldown(DifficultySettings difficulty)
    {
        // Easy: no cooldown
        if (difficulty.Name == "Easy")
            return 0;

        // Normal: 2 seconds
        if (difficulty.Name == "Normal")
            return 2;

        // Hard+: 5 seconds
        return 5;
    }

    /// <summary>
    /// Check if player should receive resurrection sickness debuff.
    /// </summary>
    /// <param name="difficulty">The difficulty settings.</param>
    /// <param name="deathCount">Number of times player has died.</param>
    /// <returns>Duration of debuff in seconds, 0 if no debuff.</returns>
    public int GetResurrectionSicknessDuration(DifficultySettings difficulty, int deathCount)
    {
        // Easy/Normal: no debuff
        if (difficulty.Name == "Easy" || difficulty.Name == "Normal")
            return 0;

        // Hard: 30 seconds
        if (difficulty.Name == "Hard")
            return 30;

        // Expert/Ironman: 60 seconds, stacks with multiple deaths
        if (difficulty.Name == "Expert" || difficulty.Name == "Ironman")
        {
            var baseDuration = 60;
            var stackPenalty = Math.Min(deathCount - 1, 5) * 30; // +30s per death, max 5 stacks
            return baseDuration + stackPenalty;
        }

        return 0;
    }

    /// <summary>
    /// Get list of respawn blessings/buffs available at a location.
    /// </summary>
    /// <param name="location">The respawn location.</param>
    /// <returns>List of available blessings.</returns>
    public List<string> GetAvailableBlessings(string location)
    {
        var blessings = new List<string>();

        // Hub Town always offers basic blessing
        if (location == "Hub Town")
        {
            blessings.Add("Traveler's Grace: +10% movement speed for 5 minutes");
        }

        // Special locations offer unique blessings
        if (location.Contains("Sanctuary"))
        {
            blessings.Add("Divine Protection: +20% damage resistance for 10 minutes");
        }

        if (location.Contains("Mystic"))
        {
            blessings.Add("Arcane Insight: +15% magic damage for 10 minutes");
        }

        return blessings;
    }

    private static string ExtractRegion(string locationName)
    {
        // Extract region from location name (e.g., "Mystic Forest Town" -> "Mystic Forest")
        var parts = locationName.Split(' ');
        if (parts.Length > 1)
        {
            return string.Join(' ', parts.Take(parts.Length - 1));
        }
        return locationName;
    }
}
