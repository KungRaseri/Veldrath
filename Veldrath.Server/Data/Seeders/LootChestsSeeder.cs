using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Seeders;

/// <summary>
/// Seeds the chests content names, so a place that should always hold something does.
/// </summary>
/// <remarks>
/// <para>
/// `forest-chest-loot` had been seeded for a while with nothing to open it: no chest row existed anywhere, so
/// the loot table was unreachable. These rows are the authored half of the chest world — the roaming half is
/// placed by <c>ChestService</c> from the zone's own locations, which is why a chest can be somewhere new each
/// time you look while a quest target stays where its text says.
/// </para>
/// <para>
/// Idempotent by <see cref="LootChest.ChestId"/>: a database that already holds a chest keeps its state
/// (opened, reopen timer), so restarting the server never refills a chest a player emptied a moment ago.
/// </para>
/// </remarks>
public static class LootChestsSeeder
{
    /// <summary>The loot table the authored chests point at, in the engine's reference form.</summary>
    public const string ForestChestLootTableRef = "@loot-tables/chests/forest:forest-chest-loot";

    /// <summary>Seeds the authored chests. Safe to run on every start.</summary>
    /// <param name="db">The application database that holds live world state.</param>
    /// <returns>A task that completes when the rows are stored.</returns>
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var existing = await db.LootChests.Select(chest => chest.ChestId).ToListAsync();
        var known = existing.ToHashSet(StringComparer.Ordinal);
        var missing = Authors().Where(chest => !known.Contains(chest.ChestId)).ToArray();

        if (missing.Length == 0)
            return;

        db.LootChests.AddRange(missing);
        await db.SaveChangesAsync();
    }

    /// <summary>The authored chests. Pure so a test can assert what the seed promises.</summary>
    /// <returns>Every chest the seeder creates on a fresh database.</returns>
    public static LootChest[] Authors() =>
    [
        // Both stand in the Ashlen Wood, where `gather-mushrooms` sends the player and where the forest loot
        // table's contents belong. The strongbox is in the clearing, the cache on the road.
        new LootChest
        {
            ChestId = "old-clearing-strongbox",
            Kind = LootChestKind.Authored,
            ZoneId = "ashlen-wood",
            LocationSlug = "old-clearing",
            DisplayName = "Mossy Strongbox",
            LootTableRefs = $"[\"{ForestChestLootTableRef}\"]",
            MergeStrategy = "addPools",
            SpawnedAt = DateTimeOffset.UtcNow,
        },
        new LootChest
        {
            ChestId = "wood-road-cache",
            Kind = LootChestKind.Authored,
            ZoneId = "ashlen-wood",
            LocationSlug = "wood-road",
            DisplayName = "Traveler's Cache",
            LootTableRefs = $"[\"{ForestChestLootTableRef}\"]",
            MergeStrategy = "addPools",
            SpawnedAt = DateTimeOffset.UtcNow,
        },
    ];
}
