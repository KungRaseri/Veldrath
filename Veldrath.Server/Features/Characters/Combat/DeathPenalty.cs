using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>What a death actually took from a character, so the client can be told exactly that.</summary>
/// <param name="GoldLost">Gold removed from the character.</param>
/// <param name="XpLost">Experience removed from the character.</param>
public sealed record DeathPenalty(int GoldLost, int XpLost)
{
    /// <summary>Gets a value indicating whether nothing was taken at all.</summary>
    public bool IsEmpty => GoldLost == 0 && XpLost == 0;
}

/// <summary>The outcome of a death check: whether the character died, and what it cost.</summary>
/// <param name="IsDead">Whether the character died.</param>
/// <param name="IsHardcore">Whether the death was permanent (hardcore soft delete) rather than a respawn.</param>
/// <param name="Penalty">What the death cost, if anything.</param>
public sealed record DeathOutcome(bool IsDead, bool IsHardcore, DeathPenalty Penalty)
{
    /// <summary>A character that survived the turn — no death, no cost.</summary>
    public static DeathOutcome Alive { get; } = new(false, false, new DeathPenalty(0, 0));

    /// <summary>Gets the gold the death took.</summary>
    public int GoldLost => Penalty.GoldLost;

    /// <summary>Gets the experience the death took.</summary>
    public int XpLost => Penalty.XpLost;
}

/// <summary>
/// Applies the agreed death rule: a small share of carried gold, and only when the character carries essentially
/// none, a small share of experience instead. Never both, and nothing at all for a character holding neither.
/// </summary>
public static class DeathPenalties
{
    /// <summary>Attributes-blob key holding the unix-minute stamp at which death sickness wears off.</summary>
    public const string SicknessUntilKey = "DeathSicknessUntil";

    /// <summary>Attributes-blob key holding the damage penalty death sickness applies, in percent.</summary>
    public const string SicknessPenaltyKey = "DeathSicknessPenalty";

    /// <summary>Stores an expiry as whole unix minutes, which fits the integer attribute blob past 2038.</summary>
    /// <param name="until">Moment the sickness wears off.</param>
    /// <returns>The stamp to persist.</returns>
    public static int ToSicknessStamp(DateTimeOffset until) => (int)(until.ToUnixTimeSeconds() / 60);

    /// <summary>Reads whether death sickness is still active for a character.</summary>
    /// <param name="attrs">Character attributes blob.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns><see langword="true"/> while the sickness window is open.</returns>
    public static bool IsSicknessActive(IReadOnlyDictionary<string, int> attrs, DateTimeOffset now)
    {
        if (!attrs.TryGetValue(SicknessUntilKey, out var until)) return false;
        return until > ToSicknessStamp(now);
    }

    /// <summary>
    /// Applies death sickness to an outgoing damage figure. Kept pure so the combat helper has nothing to decide.
    /// </summary>
    /// <param name="damage">Damage the character would deal while healthy.</param>
    /// <param name="attrs">Character attributes blob.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The reduced damage, never below 1.</returns>
    public static int ApplySicknessToDamage(int damage, IReadOnlyDictionary<string, int> attrs, DateTimeOffset now)
    {
        if (!IsSicknessActive(attrs, now)) return damage;
        if (!attrs.TryGetValue(SicknessPenaltyKey, out var penaltyPercent) || penaltyPercent <= 0) return damage;

        var clamped = Math.Min(90, penaltyPercent);
        return Math.Max(1, damage * (100 - clamped) / 100);
    }

    /// <summary>Calculates what a death costs, without touching any stored state.</summary>
    /// <param name="carriedGold">Gold the character is carrying.</param>
    /// <param name="experience">Total experience the character has (stored as a <see cref="long"/>).</param>
    /// <param name="options">Configured penalty values.</param>
    /// <returns>The penalty to apply; empty when the character has nothing worth taking.</returns>
    public static DeathPenalty Calculate(int carriedGold, long experience, DeathPenaltyOptions options)
    {
        var gold = Math.Max(0, carriedGold);
        var xp = Math.Min(Math.Max(0L, experience), int.MaxValue);

        if (gold >= options.GoldFloor && gold > 0 && options.GoldLossPercent > 0)
        {
            var capped = Math.Min(Share(gold, options.GoldLossPercent), Math.Max(0, options.GoldLossCap));
            if (capped > 0) return new DeathPenalty(capped, 0);
        }

        if (xp > 0 && options.XpLossPercent > 0)
            return new DeathPenalty(0, Share((int)xp, options.XpLossPercent));

        return new DeathPenalty(0, 0);
    }

    /// <summary>
    /// A share of a pool, rounded up so a small balance still loses something noticeable, and never more than the
    /// pool itself.
    /// </summary>
    /// <param name="pool">Amount the share is taken from.</param>
    /// <param name="percent">Share to take, in percent.</param>
    /// <returns>The amount to take.</returns>
    private static int Share(int pool, int percent) =>
        Math.Clamp((int)Math.Ceiling(pool * percent / 100.0), 1, pool);
}
