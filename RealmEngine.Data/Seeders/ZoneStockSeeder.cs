using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds each merchant town's shop stock into <see cref="ContentDbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// The shop used to hand every zone the whole item catalog — twenty-four rows, identical everywhere — which made a
/// merchant a vending machine at the edge of the world. This seeder authors what each town actually offers: between
/// <c>8</c> and <c>16</c> of the twenty-four purchasable items, per the approved band in
/// <c>game-context/budget.yaml</c> (<c>merchants.stock_size</c>). The lists are the town, not the shopkeeper: two
/// merchant locations in one town draw on the same rows (<c>docs/design/content/towns.md</c> §C6).
/// </para>
/// <para>
/// Only the four settlements a player can reach are authored. <c>playground</c> is the dev sandbox
/// (<c>IsDevOnly = true</c>) and its shop refuses like any other zone with no authored stock; inventing a roster for
/// it would be inventing a settlement.
/// </para>
/// <para>
/// An item is only listed if the item catalog can sell it — a row with a price of zero is not purchasable, and the
/// list is checked against the seeded catalog by <c>ZoneStockSeederTests</c>. Like every other content seeder this
/// one inserts only what is missing: an edited row belongs to a data migration (<c>docs/content-packs.md</c>), not to
/// a restart.
/// </para>
/// </remarks>
public static class ZoneStockSeeder
{
    /// <summary>The lowest number of distinct items a merchant town may offer.</summary>
    public const int MinimumStockSize = 8;

    /// <summary>The highest number of distinct items a merchant town may offer.</summary>
    public const int MaximumStockSize = 16;

    /// <summary>Seeds the authored per-zone stock. Safe to run on every start.</summary>
    /// <param name="db">The content database that holds the item catalog.</param>
    /// <returns>A task that completes when the missing rows are stored.</returns>
    public static async Task SeedAsync(ContentDbContext db)
    {
        var existing = await db.ZoneStockItems
            .AsNoTracking()
            .Select(row => new { row.ZoneId, row.ItemSlug })
            .ToListAsync();

        var known = existing
            .Select(row => (row.ZoneId, row.ItemSlug))
            .ToHashSet();

        var missing = Authors()
            .Where(row => !known.Contains((row.ZoneId, row.ItemSlug)))
            .ToArray();

        if (missing.Length == 0)
            return;

        db.ZoneStockItems.AddRange(missing);
        await db.SaveChangesAsync();
    }

    /// <summary>The authored stock lists, one row per item per zone. Pure, so a test can assert what the seed promises.</summary>
    /// <returns>Every row the seeder creates on a fresh database.</returns>
    public static ZoneStockItem[] Authors()
    {
        // Crestfall (L0) is the crossroads market: staples, a starter blade and the two reagents a new character
        // can use. Ten rows — a third of the catalog, the low end of the approved band.
        string[] crestfall =
        [
            "health-potion",
            "mana-potion",
            "antidote",
            "iron-ingot",
            "glowcap-mushroom",
            "rune-of-fire",
            "essence-of-fire",
            "iron-sword",
            "leather-cap",
            "hunters-bow",
        ];

        // Aldenmere (L5) sells mead-town practicality plus the first real elixir: contracts, cures and the two
        // defences a moor traveller needs.
        string[] aldenmere =
        [
            "health-potion",
            "mana-potion",
            "antidote",
            "elixir-of-strength",
            "soul-crystal",
            "rune-of-frost",
            "rune-of-power",
            "essence-of-shadow",
            "essence-of-nature",
            "scroll-of-fireball",
            "hunters-bow",
            "iron-chestplate",
        ];

        // Tolvaren (L10) is a port: rope, pitch and magic bought by the crate, so the reagents and gems dominate.
        string[] tolvaren =
        [
            "mana-potion",
            "antidote",
            "elixir-of-strength",
            "soul-crystal",
            "void-shard",
            "storm-topaz",
            "rune-of-power",
            "essence-of-shadow",
            "scroll-of-fireball",
            "orb-of-force",
            "iron-chestplate",
        ];

        // Skarhold (L18) is the forge-city at the top of the ladder: the dearest gear, the fire stones, and the
        // two orbs no lower town will handle.
        string[] skarhold =
        [
            "health-potion",
            "elixir-of-strength",
            "fire-ruby",
            "frost-sapphire",
            "storm-topaz",
            "rune-of-fire",
            "rune-of-power",
            "essence-of-fire",
            "orb-of-force",
            "orb-of-decay",
            "iron-sword",
            "iron-chestplate",
        ];

        return
        [
            .. Rows("crestfall", crestfall),
            .. Rows("aldenmere", aldenmere),
            .. Rows("tolvaren", tolvaren),
            .. Rows("skarhold", skarhold),
        ];
    }

    private static IEnumerable<ZoneStockItem> Rows(string zoneId, IReadOnlyList<string> itemSlugs) =>
        itemSlugs.Select((itemSlug, index) => new ZoneStockItem
        {
            ZoneId = zoneId,
            ItemSlug = itemSlug,
            SortOrder = (index + 1) * 10,
        });
}
