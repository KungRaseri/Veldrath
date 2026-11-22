using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="ActorInstance"/> rows into <see cref="ContentDbContext"/>.</summary>
/// <remarks>
/// <para>
/// Depends on <see cref="ArchetypeSeeder"/> having run first — actor instances require a valid
/// <see cref="ActorArchetype"/> foreign key.
/// </para>
/// <para>
/// Seeding is insert-only by slug, with one deliberate exception: an authored placement is written onto a stored row
/// whose location is still null. That is a narrow backfill, not a reconcile — a row that already has a location, or
/// any other field an author has edited in Foundry, is left exactly as it is. Overwriting authored edits on every
/// server start would be a worse bug than a row the seed never revisits.
/// </para>
/// </remarks>
public static class ActorInstancesSeeder
{
    /// <summary>Seeds all actor instance rows (idempotent).</summary>
    /// <param name="db">Content catalog database to seed.</param>
    /// <returns>A task that completes when the rows are stored.</returns>
    public static async Task SeedAsync(ContentDbContext db)
    {
        var goblinScout = await db.ActorArchetypes.FirstOrDefaultAsync(a => a.Slug == "goblin-scout");
        var banditRuffian = await db.ActorArchetypes.FirstOrDefaultAsync(a => a.Slug == "bandit-ruffian");

        // Skip seeding if archetypes have not been seeded yet.
        if (goblinScout is null || banditRuffian is null)
            return;

        var now = DateTimeOffset.UtcNow;
        var authored = Authors(now, goblinScout.Id, banditRuffian.Id);

        // Tracked, because the placement backfill below writes to existing rows.
        var existing = await db.ActorInstances.ToListAsync();
        var stored = existing.ToDictionary(x => x.Slug, StringComparer.Ordinal);

        var missing = authored.Where(x => !stored.ContainsKey(x.Slug)).ToList();
        if (missing.Count > 0)
            db.ActorInstances.AddRange(missing);

        var placed = BackfillPlacements(authored, stored, now);

        if (missing.Count == 0 && placed == 0)
            return;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Writes an authored placement onto a stored row that has none yet, and nothing else.
    /// </summary>
    /// <param name="authored">The rows this seeder authors.</param>
    /// <param name="stored">Stored rows by slug.</param>
    /// <param name="now">Timestamp to stamp on a row the backfill changes.</param>
    /// <returns>How many rows the backfill moved.</returns>
    private static int BackfillPlacements(
        IEnumerable<ActorInstance> authored,
        IReadOnlyDictionary<string, ActorInstance> stored,
        DateTimeOffset now)
    {
        var placed = 0;

        foreach (var canonical in authored)
        {
            if (canonical.LocationSlug is null)
                continue;

            if (!stored.TryGetValue(canonical.Slug, out var row))
                continue;

            // Only a row the world has never placed is touched. A row with a location was authored somewhere on
            // purpose — possibly in Foundry — and re-pointing it here would silently undo that.
            if (row.LocationSlug is not null)
                continue;

            row.LocationSlug = canonical.LocationSlug;
            row.UpdatedAt = now;
            placed++;
        }

        return placed;
    }

    /// <summary>The authored instances. Pure, so tests assert against these values instead of copying the numbers.</summary>
    /// <param name="now">Timestamp to stamp on the rows.</param>
    /// <param name="goblinScoutId">Id of the <c>goblin-scout</c> archetype.</param>
    /// <param name="banditRuffianId">Id of the <c>bandit-ruffian</c> archetype.</param>
    /// <returns>Every instance row a fresh database receives.</returns>
    public static ActorInstance[] Authors(DateTimeOffset now, Guid goblinScoutId, Guid banditRuffianId) =>
    [
            // Boss instances
            new ActorInstance
            {
                Slug            = "elder-goblin-chief",
                TypeKey         = "boss",
                DisplayName     = "Elder Goblin Chief",
                RarityWeight    = 20,
                ArchetypeId     = goblinScoutId,
                LocationSlug    = "old-clearing",
                LevelOverride   = 5,
                FactionOverride = "goblin-warband",
                IsActive        = true,
                Version         = 1,
                UpdatedAt       = now,
                StatOverrides = new InstanceStatOverrides
                {
                    Health           = 120,
                    Strength         = 14,
                    Agility          = 10,
                    Damage           = 12,
                    ExperienceReward = 80,
                    GoldRewardMin    = 10,
                    GoldRewardMax    = 30,
                },
            },

            // Story instances
            new ActorInstance
            {
                Slug            = "captain-blackthorn",
                TypeKey         = "story",
                DisplayName     = "Captain Blackthorn",
                RarityWeight    = 10,
                ArchetypeId     = banditRuffianId,
                LocationSlug    = "collapsed-vault",
                LevelOverride   = 8,
                FactionOverride = "shadowhand-syndicate",
                IsActive        = true,
                Version         = 1,
                UpdatedAt       = now,
                StatOverrides = new InstanceStatOverrides
                {
                    Health           = 200,
                    Strength         = 16,
                    Agility          = 12,
                    Intelligence     = 10,
                    Damage           = 18,
                    ExperienceReward = 200,
                    GoldRewardMin    = 30,
                    GoldRewardMax    = 80,
                },
            }
    ];
}
