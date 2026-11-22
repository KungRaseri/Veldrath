using RealmEngine.Data.Entities;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Projects an actor archetype row — with an optional named-instance override composed on top — onto the
/// <see cref="Enemy"/> template a spawn is copied from.
/// </summary>
/// <remarks>
/// <para>
/// The archetype projection is the same one the enemy repository has always made, so a stat the archetype authors is
/// always assigned, and a stat it does not author keeps the model's own documented default instead of a second
/// hardcoded copy of it here.
/// </para>
/// <para>
/// Instance overrides compose <em>on top</em> of that projection, in that order and only after it: a null override
/// inherits the projected value, a non-null override replaces it with an absolute number. The composed template is
/// then snapshotted onto the spawn, which is why the instance slug has to travel with the spawn row — a later reload
/// rebuilds the template, and without the instance it would rebuild the archetype's and silently drop every override.
/// </para>
/// <para>
/// The four authored attributes are projected onto the typed properties —
/// <see cref="Enemy.Strength"/>, <see cref="Enemy.Dexterity"/> (from
/// <see cref="RealmEngine.Data.Entities.ArchetypeStats.Agility"/>, the same attribute under the archetype's
/// name), <see cref="Enemy.Intelligence"/> and <see cref="Enemy.Constitution"/> — because those are the members
/// combat and the AI read. An instance override replaces the typed property, and <see cref="Enemy.Attributes"/> is then
/// filled <em>from</em> those typed values, never the other way round: the dictionary is a compatibility mirror of the
/// attribute surface for consumers that read it by key, and a mirror cannot disagree with what combat reads.
/// </para>
/// <para>
/// <see cref="Enemy.Wisdom"/> and <see cref="Enemy.Charisma"/> are unauthored. No archetype or instance authors them, no
/// consumer needs them and no band publishes them, so they keep the model default.
/// </para>
/// <para>
/// Four authored stats are deliberately reserved and never projected:
/// <see cref="RealmEngine.Data.Entities.ArchetypeStats.ArmorClass"/> and
/// <see cref="RealmEngine.Data.Entities.ArchetypeStats.AttackBonus"/> (the template has no such member — physical
/// mitigation is already Constitution, and neither combat path rolls an attack for a bonus to modify; the matching
/// <see cref="InstanceStatOverrides"/> fields are reserved for the same reason), and
/// <see cref="RealmEngine.Data.Entities.ArchetypeStats.TradeSkill"/> /
/// <see cref="RealmEngine.Data.Entities.ArchetypeStats.TradeGold"/> (shopkeeper fields with no consumer on this
/// path). <c>ArchetypeStatsProjectionClassificationTests</c> pins that split: a new stat that is neither
/// projected nor reserved fails the build.
/// </para>
/// <para>
/// Gold is the one authored value that is not already a single number — the row authors a range and a spawn needs one
/// reward — so it is rolled once, per template, after the archetype range and the instance override have been composed
/// pair-first (a half-authored override pair collapses onto the archetype's other bound). The roll is not repeated
/// later: <c>EfCoreEnemyInstanceStore</c> stores the spawned enemy's gold on the instance row, and every later read
/// takes it from there.
/// </para>
/// </remarks>
internal static class ActorTemplateProjection
{
    /// <summary>Projects one archetype row, composing an optional named-instance override onto it.</summary>
    /// <param name="archetype">The archetype row to project.</param>
    /// <param name="random">Random source for the single gold roll.</param>
    /// <param name="overrides">Instance stat overrides, or <see langword="null"/> for a bare archetype projection.</param>
    /// <param name="levelOverride">Instance level override, or <see langword="null"/> to use the archetype's level.</param>
    /// <param name="displayName">Instance display name, or <see langword="null"/> to use the archetype's.</param>
    /// <param name="lootTableSlug">Instance loot-table slug, or <see langword="null"/> to use the archetype's table.</param>
    /// <returns>The spawned-out enemy template.</returns>
    public static Enemy Project(
        ActorArchetype archetype,
        IRandomSource random,
        InstanceStatOverrides? overrides = null,
        int? levelOverride = null,
        string? displayName = null,
        string? lootTableSlug = null)
    {
        var stats = archetype.Stats;

        // The Enemy model owns its attribute defaults; reading them from a fresh template keeps this projection from
        // carrying a second copy of the number that could drift away from the model.
        var defaults = new Enemy();
        var name = displayName ?? archetype.DisplayName ?? archetype.Slug;

        var enemy = new Enemy
        {
            Id = archetype.Slug,
            Slug = archetype.Slug,
            Name = name,
            BaseName = name,
            // An instance's level override is absolute; a level below 1 would make the difficulty scaling nonsense.
            Level = levelOverride is { } level ? Math.Max(1, level) : archetype.MinLevel,
            RarityWeight = archetype.RarityWeight,
            // The kill path rolls this table. Carried on the model because the spawned enemy is a copy of it, and the
            // archetype row's loot id is otherwise unreachable from combat. An instance override names its own table.
            LootTableIds = LootIds(lootTableSlug ?? archetype.LootTable?.Slug),
            // Hostility has to survive the projection: a spawn pool is allowed to name any archetype, and a pool that
            // names a shopkeeper must not put them in the roster as something to hit. Unset means hostile, which keeps
            // every monster archetype behaving as it always has.
            Traits =
            {
                ["hostile"] = new TraitValue(archetype.Traits.Hostile ?? true, TraitType.Boolean),
                // The portrait frame is drawn by the client from these two inert labels. They are carried here
                // as well as on a bare archetype projection so a placed instance keeps them: only the UI reads
                // them, and a re-band must never force art to be redrawn.
                ["boss"] = new TraitValue(archetype.Traits.Boss ?? false, TraitType.Boolean),
                ["elite"] = new TraitValue(archetype.Traits.Elite ?? false, TraitType.Boolean),
                // The content taxonomy field behind the fallback glyph and the townsfolk plate. A string, not a
                // drawing instruction: the client maps it (and one recorded slug exception) onto a silhouette class.
                ["typeKey"] = new TraitValue(archetype.TypeKey, TraitType.String),
            },
            Strength = overrides?.Strength ?? stats.Strength ?? defaults.Strength,
            Dexterity = overrides?.Agility ?? stats.Agility ?? defaults.Dexterity,
            Intelligence = overrides?.Intelligence ?? stats.Intelligence ?? defaults.Intelligence,
            Constitution = overrides?.Constitution ?? stats.Constitution ?? defaults.Constitution,
        };

        // `Attributes` mirrors the typed properties assigned above rather than re-reading the row: one source, so the
        // dictionary cannot disagree with the value combat actually reads. The lowercase keys are the existing
        // consumer-facing contract and do not change.
        enemy.Attributes["strength"] = enemy.Strength;
        enemy.Attributes["dexterity"] = enemy.Dexterity;
        enemy.Attributes["intelligence"] = enemy.Intelligence;
        enemy.Attributes["constitution"] = enemy.Constitution;

        // One authored health value, and a spawn arrives at full health — so it drives both current and maximum.
        if ((overrides?.Health ?? stats.Health) is { } health)
        {
            enemy.Health = health;
            enemy.MaxHealth = health;
        }

        if ((overrides?.Damage ?? stats.Damage) is { } damage)
        {
            enemy.BasePhysicalDamage = damage;
        }

        if ((overrides?.ExperienceReward ?? stats.ExperienceReward) is { } experience)
        {
            enemy.XP = experience;
        }

        // A caster's pool is authored as one number too; a spawn arrives with it full.
        if ((overrides?.Mana ?? stats.Mana) is { } mana)
        {
            enemy.Mana = mana;
            enemy.MaxMana = mana;
        }

        // The pair is composed before the roll, so an instance that overrides one bound only narrows or widens the
        // archetype's other bound rather than falling back to the model default.
        if (RollGold(
                overrides?.GoldRewardMin ?? stats.GoldRewardMin,
                overrides?.GoldRewardMax ?? stats.GoldRewardMax,
                random) is { } gold)
        {
            enemy.GoldReward = gold;
        }

        return enemy;
    }

    /// <summary>Builds the template's loot-table list from the one slug that applies, if any.</summary>
    /// <param name="slug">The composed loot-table slug.</param>
    /// <returns>An empty list, or a single-entry list.</returns>
    private static List<string> LootIds(string? slug) => slug is null ? [] : [slug];

    /// <summary>
    /// Rolls the gold one spawn drops from the composed reward range.
    /// </summary>
    /// <remarks>
    /// The range is inclusive at both ends, which is why the exclusive bound handed to <see cref="IRandomSource"/>
    /// is <c>max + 1</c>: the authored maximum has to be reachable, not merely the boundary the framework refuses.
    /// A half-authored range collapses onto the authored bound, and a row that authors no range at all has
    /// nothing to project, so the template keeps its own default and no draw is spent.
    /// </remarks>
    /// <param name="min">Composed minimum gold, if any.</param>
    /// <param name="max">Composed maximum gold, if any.</param>
    /// <param name="random">Random source to draw from.</param>
    /// <returns>The rolled reward, or <see langword="null"/> when neither bound is authored.</returns>
    private static int? RollGold(int? min, int? max, IRandomSource random)
    {
        if (min is null && max is null)
        {
            return null;
        }

        var low = min ?? max!.Value;
        var high = max ?? min!.Value;

        return high > low ? random.Next(low, high + 1) : low;
    }
}
