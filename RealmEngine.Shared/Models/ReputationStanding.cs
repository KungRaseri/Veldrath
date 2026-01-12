namespace RealmEngine.Shared.Models;

/// <summary>
/// Tracks a player's reputation with a specific faction.
/// </summary>
public class ReputationStanding
{
    /// <summary>
    /// Gets or sets the faction ID.
    /// </summary>
    public string FactionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current reputation points.
    /// </summary>
    public int ReputationPoints { get; set; } = 0;

    /// <summary>
    /// Gets the current reputation level.
    /// </summary>
    public ReputationLevel Level => GetReputationLevel(ReputationPoints);

    /// <summary>
    /// Gets the next reputation level.
    /// </summary>
    public ReputationLevel? NextLevel => GetNextLevel(Level);

    /// <summary>
    /// Gets points needed for next level.
    /// </summary>
    public int PointsToNextLevel
    {
        get
        {
            var next = NextLevel;
            if (next == null) return 0; // Already Exalted
            return (int)next.Value - ReputationPoints;
        }
    }

    /// <summary>
    /// Gets whether player can accept quests from this faction.
    /// </summary>
    public bool CanAcceptQuests => Level >= ReputationLevel.Neutral;

    /// <summary>
    /// Gets whether player can trade with this faction.
    /// </summary>
    public bool CanTrade => Level >= ReputationLevel.Unfriendly;

    /// <summary>
    /// Gets whether faction will attack on sight.
    /// </summary>
    public bool IsHostile => Level == ReputationLevel.Hostile;

    /// <summary>
    /// Gets price discount multiplier (0.0-0.3 discount).
    /// </summary>
    public double GetPriceDiscount()
    {
        return Level switch
        {
            ReputationLevel.Friendly => 0.05,
            ReputationLevel.Honored => 0.10,
            ReputationLevel.Revered => 0.20,
            ReputationLevel.Exalted => 0.30,
            _ => 0.0
        };
    }

    /// <summary>
    /// Adds reputation points.
    /// </summary>
    public void AddReputation(int amount)
    {
        ReputationPoints = Math.Min(ReputationPoints + amount, 21000); // Cap at Exalted + buffer
    }

    /// <summary>
    /// Removes reputation points.
    /// </summary>
    public void LoseReputation(int amount)
    {
        ReputationPoints = Math.Max(ReputationPoints - amount, -9000); // Floor at deep Hostile
    }

    /// <summary>
    /// Determines reputation level from points.
    /// </summary>
    private static ReputationLevel GetReputationLevel(int points)
    {
        if (points >= 12000) return ReputationLevel.Exalted;
        if (points >= 6000) return ReputationLevel.Revered;
        if (points >= 3000) return ReputationLevel.Honored;
        if (points >= 500) return ReputationLevel.Friendly;
        if (points >= -500) return ReputationLevel.Neutral;
        if (points >= -3000) return ReputationLevel.Unfriendly;
        return ReputationLevel.Hostile;
    }

    /// <summary>
    /// Gets the next reputation level.
    /// </summary>
    private static ReputationLevel? GetNextLevel(ReputationLevel current)
    {
        return current switch
        {
            ReputationLevel.Hostile => ReputationLevel.Unfriendly,
            ReputationLevel.Unfriendly => ReputationLevel.Neutral,
            ReputationLevel.Neutral => ReputationLevel.Friendly,
            ReputationLevel.Friendly => ReputationLevel.Honored,
            ReputationLevel.Honored => ReputationLevel.Revered,
            ReputationLevel.Revered => ReputationLevel.Exalted,
            ReputationLevel.Exalted => null, // Max level
            _ => null
        };
    }
}
