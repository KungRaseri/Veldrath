using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Reputation.Services;

/// <summary>
/// Service for managing player reputation with factions.
/// </summary>
public class ReputationService
{
    /// <summary>
    /// Gets or creates a reputation standing for the specified faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <returns>The reputation standing.</returns>
    public ReputationStanding GetOrCreateReputation(SaveGame saveGame, string factionId)
    {
        if (saveGame.FactionReputations.TryGetValue(factionId, out var standing))
        {
            return standing;
        }

        // Create new standing at neutral (0 points)
        var newStanding = new ReputationStanding
        {
            FactionId = factionId,
            ReputationPoints = 0
        };

        saveGame.FactionReputations[factionId] = newStanding;
        _logger.LogInformation("Created new reputation standing with faction {FactionId} at Neutral", factionId);

        return newStanding;
    }

    /// <summary>
    /// Adds reputation with a faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <param name="amount">Amount of reputation to add.</param>
    /// <returns>The updated reputation standing.</returns>
    public ReputationStanding GainReputation(SaveGame saveGame, string factionId, int amount)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        var previousLevel = standing.Level;

        standing.AddReputation(amount);

        var newLevel = standing.Level;

        if (newLevel != previousLevel)
        {
            _logger.LogInformation("Reputation with {FactionId} changed from {OldLevel} to {NewLevel}",
                factionId, previousLevel, newLevel);
        }

        return standing;
    }

    /// <summary>
    /// Removes reputation with a faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <param name="amount">Amount of reputation to remove.</param>
    /// <returns>The updated reputation standing.</returns>
    public ReputationStanding LoseReputation(SaveGame saveGame, string factionId, int amount)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        var previousLevel = standing.Level;

        standing.LoseReputation(amount);

        var newLevel = standing.Level;

        if (newLevel != previousLevel)
        {
            _logger.LogWarning("Reputation with {FactionId} decreased from {OldLevel} to {NewLevel}",
                factionId, previousLevel, newLevel);
        }

        return standing;
    }

    /// <summary>
    /// Gets the current reputation level with a faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <returns>The reputation level.</returns>
    public ReputationLevel GetReputationLevel(SaveGame saveGame, string factionId)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        return standing.Level;
    }

    /// <summary>
    /// Checks if the player meets a minimum reputation requirement with a faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <param name="requiredLevel">The minimum required reputation level.</param>
    /// <returns>True if the requirement is met.</returns>
    public bool CheckReputationRequirement(SaveGame saveGame, string factionId, ReputationLevel requiredLevel)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        return standing.Level >= requiredLevel;
    }

    /// <summary>
    /// Gets the price discount percentage for a faction based on reputation.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <returns>Discount percentage (0.0 to 0.30).</returns>
    public double GetPriceDiscount(SaveGame saveGame, string factionId)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        return standing.GetPriceDiscount();
    }

    /// <summary>
    /// Checks if the player can trade with a faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <returns>True if trading is allowed.</returns>
    public bool CanTrade(SaveGame saveGame, string factionId)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        return standing.CanTrade;
    }

    /// <summary>
    /// Checks if the player can accept quests from a faction.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <returns>True if quest acceptance is allowed.</returns>
    public bool CanAcceptQuests(SaveGame saveGame, string factionId)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        return standing.CanAcceptQuests;
    }

    /// <summary>
    /// Checks if a faction is hostile to the player.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <param name="factionId">The faction ID.</param>
    /// <returns>True if the faction is hostile.</returns>
    public bool IsHostile(SaveGame saveGame, string factionId)
    {
        var standing = GetOrCreateReputation(saveGame, factionId);
        return standing.IsHostile;
    }

    /// <summary>
    /// Gets all faction reputations from the save game.
    /// </summary>
    /// <param name="saveGame">The save game.</param>
    /// <returns>Dictionary of faction ID to reputation standing.</returns>
    public Dictionary<string, ReputationStanding> GetAllReputations(SaveGame saveGame)
    {
        return new Dictionary<string, ReputationStanding>(saveGame.FactionReputations);
    }
}
