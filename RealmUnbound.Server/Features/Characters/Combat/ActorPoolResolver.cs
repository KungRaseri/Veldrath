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
    private readonly Random _random = new();

    /// <summary>Initializes a new instance of <see cref="ActorPoolResolver"/>.</summary>
    /// <param name="enemyGenerator">Generator used to look up enemy archetypes by slug.</param>
    public ActorPoolResolver(EnemyGenerator enemyGenerator)
    {
        _enemyGenerator = enemyGenerator;
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
        int count   = _random.Next(MinCount, MaxCount + 1);

        if (pool is null || pool.Count == 0)
        {
            var fallback = await _enemyGenerator.GenerateEnemiesAsync("common", 1);
            foreach (var e in fallback)
                spawned.Add(ToSpawnedEnemy(e));
            return spawned;
        }

        for (int i = 0; i < count; i++)
        {
            var entry = SelectWeighted(pool);
            var enemy = await _enemyGenerator.GenerateEnemyByNameAsync(string.Empty, entry.ArchetypeSlug);
            if (enemy is not null)
                spawned.Add(ToSpawnedEnemy(enemy));
        }

        return spawned;
    }

    private static SpawnedEnemy ToSpawnedEnemy(Enemy enemy) =>
        new()
        {
            ArchetypeSlug = enemy.Slug,
            Name          = string.IsNullOrWhiteSpace(enemy.Name) ? enemy.Slug : enemy.Name,
            Level         = Math.Max(1, enemy.Level),
            CurrentHealth = Math.Max(1, enemy.MaxHealth),
            MaxHealth     = Math.Max(1, enemy.MaxHealth),
            BaseXp        = Math.Max(1, enemy.XP),
            GoldReward    = Math.Max(0, enemy.GoldReward),
            Template      = enemy,
        };

    private ActorPoolEntry SelectWeighted(IReadOnlyList<ActorPoolEntry> pool)
    {
        int total = pool.Sum(e => e.Weight > 0 ? e.Weight : 1);
        int roll  = _random.Next(total);
        int cum   = 0;
        foreach (var entry in pool)
        {
            cum += entry.Weight > 0 ? entry.Weight : 1;
            if (roll < cum) return entry;
        }
        return pool[^1];
    }
}
