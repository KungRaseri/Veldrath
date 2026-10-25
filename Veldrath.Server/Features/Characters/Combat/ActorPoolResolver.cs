using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Models;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Generates a <see cref="SpawnedEnemy"/> roster for a zone location from its actor pool.
/// Weighted random selection ensures variety while respecting archetype rarity.
/// </summary>
public class ActorPoolResolver
{
    private const int MinCount = 2;
    private const int MaxCount = 4;

    private readonly EnemyGenerator _enemyGenerator;
    private readonly ILogger<ActorPoolResolver> _logger;
    private readonly Random _random = new();

    /// <summary>Initializes a new instance of <see cref="ActorPoolResolver"/>.</summary>
    /// <param name="enemyGenerator">Generator used to look up enemy archetypes by slug.</param>
    /// <param name="logger">Logger for missing-content reports; defaults to a null logger in tests.</param>
    public ActorPoolResolver(EnemyGenerator enemyGenerator, ILogger<ActorPoolResolver>? logger = null)
    {
        _enemyGenerator = enemyGenerator;
        _logger = logger ?? NullLogger<ActorPoolResolver>.Instance;
    }

    /// <summary>
    /// Spawns a roster of 2–4 enemies from an actor pool. Falls back to one random
    /// enemy from the <c>"common"</c> category when the pool is empty.
    /// </summary>
    /// <param name="pool">Weighted archetype entries for this location, or <see langword="null"/>.</param>
    /// <returns>A list of ready-to-use <see cref="SpawnedEnemy"/> instances.</returns>
    public async Task<List<SpawnedEnemy>> SpawnRosterAsync(IReadOnlyList<ActorPoolEntry>? pool)
    {
        var spawned = new List<SpawnedEnemy>();

        if (pool is null || pool.Count == 0)
        {
            // No pool means "nothing spawns here" — a safe town, for example. That is authored content, not an error, so
            // the roster is simply empty. Substituting a default archetype would be the silent fallback this milestone
            // forbids, so the old "common" fallback is gone.
            return spawned;
        }

        int count = _random.Next(MinCount, MaxCount + 1);

        for (int i = 0; i < count; i++)
        {
            var entry = SelectWeighted(pool);
            var enemy = await _enemyGenerator.GenerateEnemyByNameAsync(string.Empty, entry.ArchetypeSlug);
            if (enemy is null)
            {
                // Failsafe, not fallback: name the missing slug loudly and spawn nothing for it.
                _logger.LogError(
                    "Spawn pool references unknown actor archetype '{ArchetypeSlug}'; no enemy spawned for it",
                    entry.ArchetypeSlug);
                continue;
            }

            spawned.Add(ToSpawnedEnemy(enemy));
        }

        return spawned;
    }

    /// <summary>
    /// Resolves a single archetype by slug without spawning it. Used to rebuild the template of an enemy whose
    /// health and position already exist in the database.
    /// </summary>
    /// <param name="archetypeSlug">Actor archetype slug.</param>
    /// <returns>The archetype, or <see langword="null"/> when the content no longer exists.</returns>
    public Task<Enemy?> ResolveArchetypeAsync(string archetypeSlug) =>
        _enemyGenerator.GenerateEnemyByNameAsync(string.Empty, archetypeSlug);

    private static SpawnedEnemy ToSpawnedEnemy(Enemy enemy) =>
        new()
        {
            ArchetypeSlug = enemy.Slug,
            Name = string.IsNullOrWhiteSpace(enemy.Name) ? enemy.Slug : enemy.Name,
            Level = Math.Max(1, enemy.Level),
            CurrentHealth = Math.Max(1, enemy.MaxHealth),
            MaxHealth = Math.Max(1, enemy.MaxHealth),
            BaseXp = Math.Max(1, enemy.XP),
            GoldReward = Math.Max(0, enemy.GoldReward),
            Template = enemy,
        };

    private ActorPoolEntry SelectWeighted(IReadOnlyList<ActorPoolEntry> pool)
    {
        int total = pool.Sum(e => e.Weight > 0 ? e.Weight : 1);
        int roll = _random.Next(total);
        int cum = 0;
        foreach (var entry in pool)
        {
            cum += entry.Weight > 0 ? entry.Weight : 1;
            if (roll < cum) return entry;
        }
        return pool[^1];
    }
}
