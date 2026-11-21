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
/// The second block is D3's approved town roster (<c>docs/design/content/towns.md</c> §4.2–4.4): one person at every
/// settlement location of aldenmere, tolvaren and skarhold, plus the low-quarter carter the crestfall table already
/// named. It is not decoration. The shop is a person — `ShopMerchantResolver` requires a placement at the character's
/// exact zone and location whose archetype carries `Traits.Shopkeeper` — so a merchant location with nobody standing
/// at it refuses every buy, sell, catalog and visit with `ShopRefusal.NoMerchant` while the client still offers a shop
/// button (`ZoneLocation.HasMerchant`). Every merchant location outside the dev sandbox now has one of these people.
/// </para>
/// <para>
/// <c>playground</c> is deliberately still empty: it is the dev sandbox (`IsDevOnly = true`), its locations are
/// fixtures rather than a settlement, and <c>ZoneStockSeeder</c> authors no shelf for it — its shop refuses because
/// there is nobody there and nothing to sell, which is the documented decision rather than an omission.
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

        // Aldenmere (L5). `ironhollow-keep` and `aldenmere-marketplace` are both merchant locations, so each gets the
        // person who serves it: the castellan trades coin for the moor crossings he knows, the mead-seller holds the
        // market. `grey-cup` carries no merchant flag, but the resolver gates on the zone's, so its keeper still
        // answers a trade the way crestfall's tavern keeper does.
        new LocationNpc
        {
            NpcId = "aldenmere-keep-castellan",
            ZoneId = "aldenmere",
            LocationSlug = "ironhollow-keep",
            ArchetypeSlug = "keep-castellan",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "aldenmere-marketplace-mead",
            ZoneId = "aldenmere",
            LocationSlug = "aldenmere-marketplace",
            ArchetypeSlug = "mead-seller",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "aldenmere-greycup-keeper",
            ZoneId = "aldenmere",
            LocationSlug = "grey-cup",
            ArchetypeSlug = "cellar-keeper",
            SortOrder = 10,
        },

        // Tolvaren (L10). `cliff-road-market` is the town's only merchant location and the ship-chandler stands there;
        // the harbour-master takes no coin at all (he teaches and refuses berth), while the port innkeeper keeps the
        // Saltcrow and trades board.
        new LocationNpc
        {
            NpcId = "tolvaren-harbour-master",
            ZoneId = "tolvaren",
            LocationSlug = "tolvaren-harbour",
            ArchetypeSlug = "harbour-master",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "tolvaren-cliffroad-chandler",
            ZoneId = "tolvaren",
            LocationSlug = "cliff-road-market",
            ArchetypeSlug = "ship-chandler",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "tolvaren-saltcrow-keeper",
            ZoneId = "tolvaren",
            LocationSlug = "saltcrow-inn",
            ArchetypeSlug = "port-innkeeper",
            SortOrder = 10,
        },

        // Skarhold (L18). `forge-quarter` and `caldera-market` are both merchant locations: the forge-master takes
        // commissions where the work is done, the ash-glass trader holds the market. The lodge keeper keeps the
        // Ashbrand, which is an inn rather than a stall.
        new LocationNpc
        {
            NpcId = "skarhold-forge-master",
            ZoneId = "skarhold",
            LocationSlug = "forge-quarter",
            ArchetypeSlug = "forge-master",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "skarhold-caldera-glass",
            ZoneId = "skarhold",
            LocationSlug = "caldera-market",
            ArchetypeSlug = "ash-glass-seller",
            SortOrder = 10,
        },
        new LocationNpc
        {
            NpcId = "skarhold-ashbrand-keeper",
            ZoneId = "skarhold",
            LocationSlug = "ashbrand-lodge",
            ArchetypeSlug = "lodge-keeper",
            SortOrder = 10,
        },

        // Crestfall's fourth row from the same table — the carter in the low quarter. He carries a purse like the rest of
        // the roster, so the starter town's shelf is reachable from the low quarter too; the shop is per zone, not per
        // location (towns.md C6).
        new LocationNpc
        {
            NpcId = "crestfall-lowquarter-carter",
            ZoneId = "crestfall",
            LocationSlug = "low-quarter",
            ArchetypeSlug = "road-carter",
            SortOrder = 10,
        },
    ];
}
