using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Seeders;

/// <summary>
/// Seeds the people a player can meet, so the world contains the speakers its own content names.
/// </summary>
/// <remarks>
/// <para>
/// Three townspeople were seeded as actor archetypes (`village-blacksmith`, `tavern-keeper`, `wayfarer-elder`) with
/// dialogue lines keyed to their slugs, and nothing placed any of them — so `talk:` objectives, which the main-story
/// quest in <c>QuestsSeeder</c> uses for "Return the cargo to the Village Blacksmith", could not be completed by
/// any route. These rows are the placement half of that content.
/// </para>
/// <para>
/// Idempotent by <see cref="LocationNpc.NpcId"/>, so a restart neither duplicates a person nor moves one.
/// </para>
/// </remarks>
public static class LocationNpcsSeeder
{
    /// <summary>Seeds the authored placements. Safe to run on every start.</summary>
    /// <param name="db">The application database that holds world state.</param>
    /// <returns>A task that completes when the rows are stored.</returns>
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var existing = await db.LocationNpcs.Select(npc => npc.NpcId).ToListAsync();
        var known = existing.ToHashSet(StringComparer.Ordinal);
        var missing = Authors().Where(npc => !known.Contains(npc.NpcId)).ToArray();

        if (missing.Length == 0)
            return;

        db.LocationNpcs.AddRange(missing);
        await db.SaveChangesAsync();
    }

    /// <summary>The authored placements. Pure so a test can assert what the seed promises.</summary>
    /// <returns>Every person the seeder creates on a fresh database.</returns>
    public static LocationNpc[] Authors() =>
    [
        // The smith works the market, the keeper keeps the inn the quest mentions, the elder holds the square.
        new LocationNpc
        {
            NpcId = "crestfall-aurelian-blacksmith",
            ZoneId = "crestfall",
            LocationSlug = "aurelian-market",
            ArchetypeSlug = "village-blacksmith",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "crestfall-hearthbound-keeper",
            ZoneId = "crestfall",
            LocationSlug = "hearthbound-inn",
            ArchetypeSlug = "tavern-keeper",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "crestfall-square-elder",
            ZoneId = "crestfall",
            LocationSlug = "crestfall-square",
            ArchetypeSlug = "wayfarer-elder",
            SortOrder = 10,
        },
    ];
}
