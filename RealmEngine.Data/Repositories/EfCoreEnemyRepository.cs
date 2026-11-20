using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Services;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for enemy catalog data. The archetype row is projected onto the <see cref="Enemy"/>
/// template a spawn is copied from, so every authored stat has to survive the projection: combat reads the
/// template, never the content row.
/// </summary>
/// <param name="db">Content database context.</param>
/// <param name="logger">Logger instance.</param>
/// <param name="randomSource">
/// Source for the per-spawn gold roll; defaults to <see cref="SystemRandomSource.Shared"/>. Register or pass a
/// seeded source to make a spawn reproducible.
/// </param>
public class EfCoreEnemyRepository(
    ContentDbContext db,
    ILogger<EfCoreEnemyRepository> logger,
    IRandomSource? randomSource = null)
    : IEnemyRepository
{
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Shared;

    /// <inheritdoc />
    public async Task<List<Enemy>> GetAllAsync()
    {
        var entities = await db.ActorArchetypes.AsNoTracking()
            .Where(e => e.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} actor archetypes from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Enemy?> GetBySlugAsync(string slug)
    {
        var entity = await db.ActorArchetypes.AsNoTracking()
            .Include(e => e.LootTable)
            .Where(e => e.IsActive && e.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Enemy>> GetByFamilyAsync(string family)
    {
        var entities = await db.ActorArchetypes.AsNoTracking()
            .Where(e => e.IsActive && e.TypeKey == family)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <summary>
    /// Projects one archetype row onto the template a spawn is copied from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stat the archetype authors is always assigned; a stat it does not author keeps the model's own documented
    /// default instead of a second hardcoded copy of it here.
    /// </para>
    /// <para>
    /// The four authored attributes are projected onto the typed properties —
    /// <see cref="Enemy.Strength"/>, <see cref="Enemy.Dexterity"/> (from
    /// <see cref="RealmEngine.Data.Entities.ArchetypeStats.Agility"/>, the same attribute under the archetype's
    /// name), <see cref="Enemy.Intelligence"/> and <see cref="Enemy.Constitution"/> — because those are the members
    /// combat and the AI read. <see cref="Enemy.Attributes"/> is then filled <em>from</em> those typed values, never
    /// the other way round: the dictionary is a compatibility mirror of the attribute surface for consumers that read
    /// it by key, and a mirror cannot disagree with what combat reads.
    /// </para>
    /// <para>
    /// <see cref="Enemy.Wisdom"/> and <see cref="Enemy.Charisma"/> are unauthored. No archetype authors them, no
    /// consumer needs them and no band publishes them, so they keep the model default.
    /// </para>
    /// <para>
    /// Four authored stats are deliberately reserved and never projected:
    /// <see cref="RealmEngine.Data.Entities.ArchetypeStats.ArmorClass"/> and
    /// <see cref="RealmEngine.Data.Entities.ArchetypeStats.AttackBonus"/> (the template has no such member — physical
    /// mitigation is already Constitution, and neither combat path rolls an attack for a bonus to modify), and
    /// <see cref="RealmEngine.Data.Entities.ArchetypeStats.TradeSkill"/> /
    /// <see cref="RealmEngine.Data.Entities.ArchetypeStats.TradeGold"/> (shopkeeper fields with no consumer on this
    /// path). <c>ArchetypeStatsProjectionClassificationTests</c> pins that split: a new stat that is neither
    /// projected nor reserved fails the build.
    /// </para>
    /// <para>
    /// Gold is the one authored value that is not already a single number — the row authors a range and a spawn needs
    /// one reward — so it is rolled once, per template, from the repository's random source. The roll is not repeated
    /// later: <c>EfCoreEnemyInstanceStore</c> stores the spawned enemy's gold on the instance row, and every later read
    /// takes it from there.
    /// </para>
    /// </remarks>
    /// <param name="e">The archetype row.</param>
    /// <returns>The spawned-out enemy template.</returns>
    private Enemy MapToModel(Entities.ActorArchetype e)
    {
        // The Enemy model owns its attribute defaults; reading them from a fresh template keeps this projection from
        // carrying a second copy of the number that could drift away from the model.
        var defaults = new Enemy();

        var enemy = new Enemy
        {
            Id = e.Slug,
            Slug = e.Slug,
            Name = e.DisplayName ?? e.Slug,
            BaseName = e.DisplayName ?? e.Slug,
            Level = e.MinLevel,
            RarityWeight = e.RarityWeight,
            // The kill path rolls this table. Carried on the model because the spawned enemy is a copy of it, and the
            // archetype row's loot id is otherwise unreachable from combat.
            LootTableIds = e.LootTable is null ? [] : [e.LootTable.Slug],
            // Hostility has to survive the projection: a spawn pool is allowed to name any archetype, and a pool that
            // names a shopkeeper must not put them in the roster as something to hit. Unset means hostile, which keeps
            // every monster archetype behaving as it always has.
            Traits =
            {
                ["hostile"] = new TraitValue(e.Traits.Hostile ?? true, TraitType.Boolean),
                // The portrait frame is drawn by the client from these two inert labels. They are carried
                // so a re-band never has to redraw art: only the UI reads them.
                ["boss"] = new TraitValue(e.Traits.Boss ?? false, TraitType.Boolean),
                ["elite"] = new TraitValue(e.Traits.Elite ?? false, TraitType.Boolean),
            },
            Strength = e.Stats.Strength ?? defaults.Strength,
            Dexterity = e.Stats.Agility ?? defaults.Dexterity,
            Intelligence = e.Stats.Intelligence ?? defaults.Intelligence,
            Constitution = e.Stats.Constitution ?? defaults.Constitution,
        };

        // `Attributes` mirrors the typed properties assigned above rather than re-reading the row: one source, so the
        // dictionary cannot disagree with the value combat actually reads. The lowercase keys are the existing
        // consumer-facing contract and do not change.
        enemy.Attributes["strength"] = enemy.Strength;
        enemy.Attributes["dexterity"] = enemy.Dexterity;
        enemy.Attributes["intelligence"] = enemy.Intelligence;
        enemy.Attributes["constitution"] = enemy.Constitution;

        // One authored health value, and a spawn arrives at full health — so it drives both current and maximum.
        if (e.Stats.Health is { } health)
        {
            enemy.Health = health;
            enemy.MaxHealth = health;
        }

        if (e.Stats.Damage is { } damage)
        {
            enemy.BasePhysicalDamage = damage;
        }

        if (e.Stats.ExperienceReward is { } experience)
        {
            enemy.XP = experience;
        }

        // A caster's pool is authored as one number too; a spawn arrives with it full.
        if (e.Stats.Mana is { } mana)
        {
            enemy.Mana = mana;
            enemy.MaxMana = mana;
        }

        if (RollGold(e.Stats.GoldRewardMin, e.Stats.GoldRewardMax) is { } gold)
        {
            enemy.GoldReward = gold;
        }

        return enemy;
    }

    /// <summary>
    /// Rolls the gold one spawn drops from the range the archetype authors.
    /// </summary>
    /// <remarks>
    /// The range is inclusive at both ends, which is why the exclusive bound handed to <see cref="IRandomSource"/>
    /// is <c>max + 1</c>: the authored maximum has to be reachable, not merely the boundary the framework refuses.
    /// A half-authored range collapses onto the authored bound, and an archetype that authors no range at all has
    /// nothing to project, so the template keeps its own default and no draw is spent.
    /// </remarks>
    /// <param name="min">Authored minimum gold, if any.</param>
    /// <param name="max">Authored maximum gold, if any.</param>
    /// <returns>The rolled reward, or <see langword="null"/> when the archetype authors no range.</returns>
    private int? RollGold(int? min, int? max)
    {
        if (min is null && max is null)
        {
            return null;
        }

        var low = min ?? max!.Value;
        var high = max ?? min!.Value;

        return high > low ? _random.Next(low, high + 1) : low;
    }
}
