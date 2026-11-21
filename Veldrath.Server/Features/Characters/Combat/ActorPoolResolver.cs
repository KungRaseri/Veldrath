using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Services;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Generates a <see cref="SpawnedEnemy"/> roster for a zone location from its placed actor instances and its actor
/// pool. Weighted random selection ensures variety while respecting archetype rarity.
/// </summary>
/// <remarks>
/// <para>
/// A roster is the placed instances of the location — each exactly once — plus pool draws filling it out to the rolled
/// size of 2–4. A location with placements and no pool therefore spawns exactly its placements, and a location with
/// neither spawns nothing: that is a safe town, authored content rather than an error.
/// </para>
/// <para>
/// The resolver's draws (roster size and the weighted pick) and the gold roll the archetype projection makes both
/// come from <see cref="IRandomSource"/>, which the composition root registers once — so seeding that one binding
/// makes an entire spawn, rewards included, reproducible.
/// </para>
/// </remarks>
public class ActorPoolResolver
{
    private const int MinCount = 2;
    private const int MaxCount = 4;

    private readonly EnemyGenerator _enemyGenerator;
    private readonly ILogger<ActorPoolResolver> _logger;
    private readonly IRandomSource _random;
    private readonly IActorInstanceRepository? _instances;

    /// <summary>Initializes a new instance of <see cref="ActorPoolResolver"/>.</summary>
    /// <param name="enemyGenerator">Generator used to look up enemy archetypes by slug.</param>
    /// <param name="logger">Logger for missing-content reports; defaults to a null logger in tests.</param>
    /// <param name="randomSource">
    /// Source for roster size and selection; defaults to <see cref="SystemRandomSource.Shared"/>, the binding the
    /// composition root registers for every other consumer, the archetype projection included.
    /// </param>
    /// <param name="instances">
    /// Repository used to rebuild a placed instance's composed template on reload; defaults to
    /// <see langword="null"/>, which means a store reading a row with an instance slug cannot rebuild it and reports
    /// the missing content instead of guessing.
    /// </param>
    public ActorPoolResolver(
        EnemyGenerator enemyGenerator,
        ILogger<ActorPoolResolver>? logger = null,
        IRandomSource? randomSource = null,
        IActorInstanceRepository? instances = null)
    {
        _enemyGenerator = enemyGenerator;
        _logger = logger ?? NullLogger<ActorPoolResolver>.Instance;
        _random = randomSource ?? SystemRandomSource.Shared;
        _instances = instances;
    }

    /// <summary>
    /// Spawns a roster for a location: every placed instance once, then pool draws up to the rolled 2–4. An empty or
    /// <see langword="null"/> pool with nothing placed means "nothing spawns here" — authored content, as in a safe
    /// town, not an error — so the method returns an empty roster and spawns nothing instead of substituting a default
    /// archetype. Pool entries whose archetype is unknown or not hostile are logged and skipped, never replaced.
    /// </summary>
    /// <param name="pool">Weighted archetype entries for this location, or <see langword="null"/>.</param>
    /// <param name="placements">
    /// Named instances placed at this location, or <see langword="null"/>/empty. Optional and last, so every existing
    /// call site keeps its meaning.
    /// </param>
    /// <returns>A list of ready-to-use <see cref="SpawnedEnemy"/> instances.</returns>
    public async Task<List<SpawnedEnemy>> SpawnRosterAsync(
        IReadOnlyList<ActorPoolEntry>? pool,
        IReadOnlyList<PlacedActorInstance>? placements = null)
    {
        var spawned = new List<SpawnedEnemy>();

        var hasPool = pool is { Count: > 0 };
        var hasPlacements = placements is { Count: > 0 };

        if (!hasPool && !hasPlacements)
        {
            // Neither a pool nor a placement means "nothing spawns here" — a safe town, for example. That is authored
            // content, not an error, so the roster is simply empty. Substituting a default archetype would be the
            // silent fallback this milestone forbids, so the old "common" fallback is gone.
            return spawned;
        }

        int count = _random.Next(MinCount, MaxCount + 1);

        foreach (var placement in placements ?? [])
        {
            // A placement is a named actor standing somewhere, not necessarily something to hit. A shopkeeper placed
            // by mistake is content debt; warning and leaving them out beats turning them into a target.
            if (!IsHostile(placement.Template))
            {
                _logger.LogWarning(
                    "Placed actor instance '{InstanceSlug}' is not hostile; leaving them out of the combat roster",
                    placement.Slug);
                continue;
            }

            spawned.Add(ToSpawnedEnemy(placement.Template, placement.Slug));
        }

        if (!hasPool)
        {
            // Placements alone: exactly what was placed, never topped up from nothing.
            return spawned;
        }

        for (int i = spawned.Count; i < count; i++)
        {
            var entry = SelectWeighted(pool!);
            var enemy = await _enemyGenerator.GenerateEnemyByNameAsync(string.Empty, entry.ArchetypeSlug);
            if (enemy is null)
            {
                // Failsafe, not fallback: name the missing slug loudly and spawn nothing for it.
                _logger.LogError(
                    "Spawn pool references unknown actor archetype '{ArchetypeSlug}'; no enemy spawned for it",
                    entry.ArchetypeSlug);
                continue;
            }

            // A pool is "what may stand here", not "what may be attacked": a shopkeeper or a quest giver named in
            // a pool is content debt, and turning them into a target would be the silent punishment for it. They are
            // placed by `LocationNpcs`, which is what makes them meetable instead of hittable.
            if (!IsHostile(enemy))
            {
                _logger.LogWarning(
                    "Spawn pool entry '{ArchetypeSlug}' is not hostile; leaving them out of the combat roster",
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

    /// <summary>
    /// Resolves a placed instance's composed template by slug without spawning it. Used to rebuild the template of an
    /// enemy that was spawned from an instance, so a reload keeps the authored damage as well as the stored health.
    /// </summary>
    /// <param name="instanceSlug">Actor instance slug stored on the spawn row.</param>
    /// <returns>The composed template, or <see langword="null"/> when the content or the repository is missing.</returns>
    public async Task<Enemy?> ResolveInstanceTemplateAsync(string instanceSlug)
    {
        if (_instances is null)
        {
            _logger.LogError(
                "Cannot rebuild placed actor instance '{InstanceSlug}': no actor instance repository is configured",
                instanceSlug);
            return null;
        }

        var placed = await _instances.GetTemplateBySlugAsync(instanceSlug);
        return placed?.Template;
    }

    private static bool IsHostile(Enemy enemy) =>
        !enemy.Traits.TryGetValue("hostile", out var hostile) || hostile.AsBool();

    private static SpawnedEnemy ToSpawnedEnemy(Enemy enemy, string? instanceSlug = null) =>
        new()
        {
            ArchetypeSlug = enemy.Slug,
            InstanceSlug = instanceSlug,
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
