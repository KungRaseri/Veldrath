using EngineCharacter = RealmEngine.Shared.Models.Character;
using ServerCharacter = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Features.LevelUp;

/// <summary>
/// The one place a stored character gains experience: the rolling <c>Level × 100</c> formula plus the derived
/// stats a level-up has to rewrite in the attributes blob.
/// </summary>
/// <remarks>
/// Both the quest/experience command and the combat reward path call this. The engine's own
/// <see cref="RealmEngine.Shared.Models.Character.GainExperience(int)"/> recomputes health and mana on a level-up;
/// the server blob carries the same authoritative numbers, and when only <c>Level</c>/<c>Experience</c> were written
/// every level-up was cosmetic — a level-6 character kept the level-1 120 HP bar.
/// </remarks>
internal static class CharacterProgression
{
    internal const string KeyConstitution = "Constitution";
    internal const string KeyWisdom = "Wisdom";
    internal const string KeyCurrentHealth = "CurrentHealth";
    internal const string KeyMaxHealth = "MaxHealth";
    internal const string KeyCurrentMana = "CurrentMana";
    internal const string KeyMaxMana = "MaxMana";

    /// <summary>What an award did to the character's level and experience.</summary>
    /// <param name="OldLevel">Level before the award.</param>
    /// <param name="NewLevel">Level after the award.</param>
    /// <param name="RemainingExperience">Experience still banked toward the next level.</param>
    internal readonly record struct Progress(int OldLevel, int NewLevel, long RemainingExperience)
    {
        /// <summary>Gets a value indicating whether the award crossed at least one level.</summary>
        internal bool LevelledUp => NewLevel > OldLevel;
    }

    /// <summary>
    /// Applies an experience award and, when it crosses a level, rebuilds the derived pools in
    /// <paramref name="attributes"/> from the character's base attributes and its new level.
    /// </summary>
    /// <param name="character">Character row to advance.</param>
    /// <param name="amount">Positive experience to award.</param>
    /// <param name="attributes">The character's parsed attributes blob; rewritten in place on a level-up.</param>
    /// <returns>The resulting level progress.</returns>
    internal static Progress AwardExperience(
        ServerCharacter character,
        int amount,
        Dictionary<string, int> attributes)
    {
        var oldLevel = character.Level;

        // Same rolling formula as RealmEngine.Shared.Models.Character.GainExperience():
        //   while (xp >= level * 100) { xp -= level * 100; level++; }
        long xp = character.Experience + amount;
        var level = character.Level;
        while (xp >= (long)level * 100)
        {
            xp -= (long)level * 100;
            level++;
        }

        character.Experience = xp;
        character.Level = level;

        if (level > oldLevel)
            RecomputeDerivedStats(attributes, level);

        return new Progress(oldLevel, level, xp);
    }

    /// <summary>
    /// Rewrites <c>MaxHealth</c>/<c>MaxMana</c> with the engine formulas the creation wizard uses, and clamps the
    /// current pools to the new ceilings.
    /// </summary>
    /// <remarks>
    /// A bigger bar does not heal the character: the current pools keep the value they had. Only the maximums are
    /// derived, so a level gained mid-fight raises the ceiling without quietly restoring hit points the fight had
    /// already taken away.
    /// </remarks>
    /// <param name="attributes">The character's parsed attributes blob, rewritten in place.</param>
    /// <param name="level">The character's level after the award.</param>
    private static void RecomputeDerivedStats(Dictionary<string, int> attributes, int level)
    {
        // The blob does not carry learned skills, so the skill multiplier stays at its empty-skill value (1.0) —
        // exactly what the creation wizard wrote for a fresh character.
        var engine = new EngineCharacter
        {
            Level = level,
            Constitution = attributes.GetValueOrDefault(KeyConstitution, 10),
            Wisdom = attributes.GetValueOrDefault(KeyWisdom, 10),
        };

        var maxHealth = engine.GetMaxHealth();
        var maxMana = engine.GetMaxMana();

        attributes[KeyMaxHealth] = maxHealth;
        attributes[KeyMaxMana] = maxMana;

        if (attributes.TryGetValue(KeyCurrentHealth, out var health))
            attributes[KeyCurrentHealth] = Math.Min(health, maxHealth);
        if (attributes.TryGetValue(KeyCurrentMana, out var mana))
            attributes[KeyCurrentMana] = Math.Min(mana, maxMana);
    }
}
