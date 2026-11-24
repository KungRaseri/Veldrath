using RealmEngine.Shared.Models;
using EngineCharacter = RealmEngine.Shared.Models.Character;
using ServerCharacter = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Features.LevelUp;

/// <summary>
/// The server-side place a stored character gains experience: the rolling <c>Level × 100</c> formula plus the derived
/// pools a level-up has to rewrite in the attributes blob.
/// </summary>
/// <remarks>
/// <see cref="GainExperienceHubCommandHandler"/> is its only caller today. The combat reward path
/// (<c>CombatHelpers.DistributeRewardsAsync</c>) still writes <c>Experience</c> directly and never applies a level;
/// that gap is tracked separately, and this helper is the shape that path should adopt once the combat payload can
/// carry the level and the pools.
/// The engine's own <see cref="RealmEngine.Shared.Models.Character.GainExperience(int)"/> recomputes health and mana
/// on a level-up; the server blob carries the authoritative numbers, and when only <c>Level</c>/<c>Experience</c>
/// were written every level-up was cosmetic — a level-6 character kept the level-1 120 HP bar.
/// <para>
/// A level-up also banks the points the engine's own <see cref="RealmEngine.Shared.Models.Character"/> hands the
/// player — 3 attribute points per level plus 2 more on every fifth — under <c>UnspentAttributePoints</c>, the key
/// <see cref="Veldrath.Server.Features.Characters.AllocateAttributePointsHubCommandHandler"/> spends from. Before
/// this, nothing in
/// <c>Veldrath.Server</c> ever incremented that key: it could only be decremented, so every character carried zero
/// unspent points for life and the attribute-allocation command always answered "not enough points". Skill points
/// are still not persisted — the server has no path that spends them.
/// </para>
/// </remarks>
internal static class CharacterProgression
{
    internal const string KeyConstitution = "Constitution";
    internal const string KeyWisdom = "Wisdom";
    internal const string KeyCurrentHealth = "CurrentHealth";
    internal const string KeyMaxHealth = "MaxHealth";
    internal const string KeyCurrentMana = "CurrentMana";
    internal const string KeyMaxMana = "MaxMana";

    /// <summary>
    /// The attributes-blob key that holds the points a player has banked but not yet allocated. Written by a
    /// level-up here and spent by <c>AllocateAttributePointsHubCommandHandler</c>, which requires the exact spelling.
    /// </summary>
    internal const string KeyUnspentAttributePoints = "UnspentAttributePoints";

    /// <summary>Attribute points the engine awards for <em>reaching</em> <paramref name="level"/>.</summary>
    /// <remarks>
    /// Mirrors <c>RealmEngine.Shared.Models.Character.LevelUp()</c>: 3 points per level plus 2 more on every fifth
    /// (5, 10, 15, …), which is the same rule <c>RealmEngine.Shared.Tests</c> pins. The award is reproduced here
    /// rather than delegated to the engine object because that object's <c>LevelUp()</c> also rewrites the pools
    /// from its <c>Constitution × 10</c> formulas, which this path deliberately does not adopt; a server test pins
    /// the two rules together so they cannot drift.
    /// </remarks>
    /// <param name="level">The level reached.</param>
    /// <returns>The attribute points that level is worth.</returns>
    internal static int AttributePointsForLevel(int level) => 3 + (level % 5 == 0 ? 2 : 0);

    /// <summary>The points a <paramref name="oldLevel"/> → <paramref name="newLevel"/> crossing awards.</summary>
    /// <remarks>
    /// Every level crossed is awarded on its own, exactly as the engine's per-level <c>LevelUp()</c> loop does: a
    /// 3 → 7 award pays levels 4, 5, 6 and 7 (3 + 5 + 3 + 3 = 14), not a flat multiple of the starting level's rate.
    /// </remarks>
    /// <param name="oldLevel">Level before the award.</param>
    /// <param name="newLevel">Level after the award.</param>
    /// <returns>The total points awarded, or 0 when no level was crossed.</returns>
    internal static int AttributePointsForLevels(int oldLevel, int newLevel)
    {
        var total = 0;
        for (var level = oldLevel + 1; level <= newLevel; level++)
            total += AttributePointsForLevel(level);
        return total;
    }

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
    /// <paramref name="attributes"/> from the character's class curve and its new level, and banks the attribute
    /// points those levels are worth.
    /// </summary>
    /// <param name="character">Character row to advance.</param>
    /// <param name="amount">Positive experience to award.</param>
    /// <param name="attributes">The character's parsed attributes blob; rewritten in place on a level-up.</param>
    /// <param name="characterClass">
    /// The catalogue row the character was created from, or <see langword="null"/> when it cannot be resolved. The
    /// row supplies the level-1 pool and the per-level growth creation used; without it the engine formulas apply.
    /// </param>
    /// <returns>The resulting level progress.</returns>
    internal static Progress AwardExperience(
        ServerCharacter character,
        int amount,
        Dictionary<string, int> attributes,
        CharacterClass? characterClass = null)
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
        {
            // The engine's own level-up hands the player points to spend. Nothing else in the server ever increments
            // the key, so without this every character carries zero unspent points for life and the allocation
            // command can never succeed. Awarded before the pools are rebuilt so both edits land in one blob write.
            attributes[KeyUnspentAttributePoints] =
                attributes.GetValueOrDefault(KeyUnspentAttributePoints)
                + AttributePointsForLevels(oldLevel, level);

            RecomputeDerivedStats(attributes, level, characterClass);
        }

        return new Progress(oldLevel, level, xp);
    }

    /// <summary>
    /// Rewrites <c>MaxHealth</c>/<c>MaxMana</c> along the character's class curve and clamps the current pools to the
    /// new ceilings. A pool is never set below the value the blob already carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creation does not use the engine's <c>Constitution × 10 + level × 5</c> / <c>Wisdom × 5 + level × 3</c>
    /// formulas: <c>CreateCharacterHandler</c> writes the class's <c>StartingHealth</c>/<c>StartingMana</c> straight
    /// into the blob (warrior 120/20, mage 60/100), while <c>POST /api/characters</c> writes the engine formulas. A
    /// level-up derived from either one alone would lower the other writer's characters, so the class curve
    /// (<c>StartingHealth + HealthGrowth × (level − 1)</c>, same for mana) is the primary source, the engine formula
    /// is the fallback when no class row resolves, and the stored pool is the floor.
    /// </para>
    /// <para>
    /// A bigger bar does not heal the character: the current pools keep the value they had, clamped down only when
    /// they exceed the ceiling. A level gained mid-fight raises the ceiling without quietly restoring hit points the
    /// fight had already taken away.
    /// </para>
    /// <para>
    /// The mana curve deliberately carries no skill multiplier. The blob does record learned skills
    /// (<c>Skill_{id}_XP</c> / <c>Skill_{id}_Rank</c>), but the engine object built for the fallback has no
    /// <c>Skills</c> collection and the server has never applied <c>SkillEffectCalculator.GetMaxManaMultiplier</c> to
    /// a stored pool, so the multiplier stays at its empty-skill value (1.0) here as well.
    /// </para>
    /// </remarks>
    /// <param name="attributes">The character's parsed attributes blob, rewritten in place.</param>
    /// <param name="level">The character's level after the award.</param>
    /// <param name="characterClass">The class catalogue row, or <see langword="null"/> when it could not be resolved.</param>
    private static void RecomputeDerivedStats(
        Dictionary<string, int> attributes,
        int level,
        CharacterClass? characterClass)
    {
        int computedHealth;
        int computedMana;

        if (characterClass is not null)
        {
            var growth = characterClass.Progression?.StatGrowth;
            computedHealth = LevelPool(characterClass.StartingHealth, growth?.HealthPerLevel ?? 0, level);
            computedMana = LevelPool(characterClass.StartingMana, growth?.ManaPerLevel ?? 0, level);
        }
        else
        {
            // No catalogue row (legacy data, a partial content catalog, or a stub in a unit test): the engine
            // formulas are the only derivation left.
            var engine = new EngineCharacter
            {
                Level = level,
                Constitution = attributes.GetValueOrDefault(KeyConstitution, 10),
                Wisdom = attributes.GetValueOrDefault(KeyWisdom, 10),
            };

            computedHealth = engine.GetMaxHealth();
            computedMana = engine.GetMaxMana();
        }

        // Never take a ceiling away. The blob is the authority on what the character currently has, and a writer this
        // curve disagrees with must not clamp a living character's bar down on a level-up.
        var maxHealth = Math.Max(attributes.GetValueOrDefault(KeyMaxHealth), computedHealth);
        var maxMana = Math.Max(attributes.GetValueOrDefault(KeyMaxMana), computedMana);

        attributes[KeyMaxHealth] = maxHealth;
        attributes[KeyMaxMana] = maxMana;

        if (attributes.TryGetValue(KeyCurrentHealth, out var health))
            attributes[KeyCurrentHealth] = Math.Min(health, maxHealth);
        if (attributes.TryGetValue(KeyCurrentMana, out var mana))
            attributes[KeyCurrentMana] = Math.Min(mana, maxMana);
    }

    /// <summary>One pool on the class curve: the level-1 pool plus its per-level growth, rounded to a point.</summary>
    /// <param name="basePool">The class's pool at level 1.</param>
    /// <param name="growthPerLevel">The class's growth per level; 0 when the content does not carry one.</param>
    /// <param name="level">The character's level after the award.</param>
    /// <returns>The pool at <paramref name="level"/>.</returns>
    private static int LevelPool(int basePool, double growthPerLevel, int level) =>
        (int)Math.Round(basePool + (growthPerLevel * (level - 1)), MidpointRounding.AwayFromZero);
}
