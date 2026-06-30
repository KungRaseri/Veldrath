using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds <see cref="ZoneLocationConnection"/> rows for Crestfall into <see cref="ContentDbContext"/>.
/// Connections define valid traversal paths between locations within Crestfall and exits
/// to adjacent zones and the region map.
/// </summary>
public static class ZoneLocationsConnectionSeeder
{
    /// <summary>Seeds all Crestfall zone location connection rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedCrestfallConnectionsAsync(db);
    }

    private static async Task SeedCrestfallConnectionsAsync(ContentDbContext db)
    {
        var crestfallLocationSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "crestfall-square",
            "aurelian-market",
            "hearthbound-inn",
            "low-quarter",
        };

        // Idempotency: skip if any connections already exist for Crestfall locations.
        var anyExist = await db.ZoneLocationConnections.AsNoTracking()
            .AnyAsync(c => crestfallLocationSlugs.Contains(c.FromLocationSlug));

        if (anyExist) return;

        var connections = GetCrestfallConnections();
        db.ZoneLocationConnections.AddRange(connections);
        await db.SaveChangesAsync();
    }

    private static ZoneLocationConnection[] GetCrestfallConnections() =>
    [
        // ── Intra-zone walks (bidirectional) ──────────────────────────────────

        // crestfall-square ↔ aurelian-market
        new ZoneLocationConnection
        {
            FromLocationSlug = "crestfall-square",
            ToLocationSlug   = "aurelian-market",
            ConnectionType   = "walk",
        },
        new ZoneLocationConnection
        {
            FromLocationSlug = "aurelian-market",
            ToLocationSlug   = "crestfall-square",
            ConnectionType   = "walk",
        },

        // crestfall-square ↔ hearthbound-inn
        new ZoneLocationConnection
        {
            FromLocationSlug = "crestfall-square",
            ToLocationSlug   = "hearthbound-inn",
            ConnectionType   = "walk",
        },
        new ZoneLocationConnection
        {
            FromLocationSlug = "hearthbound-inn",
            ToLocationSlug   = "crestfall-square",
            ConnectionType   = "walk",
        },

        // crestfall-square ↔ low-quarter
        new ZoneLocationConnection
        {
            FromLocationSlug = "crestfall-square",
            ToLocationSlug   = "low-quarter",
            ConnectionType   = "walk",
        },
        new ZoneLocationConnection
        {
            FromLocationSlug = "low-quarter",
            ToLocationSlug   = "crestfall-square",
            ConnectionType   = "walk",
        },

        // aurelian-market ↔ hearthbound-inn (via the square)
        new ZoneLocationConnection
        {
            FromLocationSlug = "aurelian-market",
            ToLocationSlug   = "hearthbound-inn",
            ConnectionType   = "walk",
        },
        new ZoneLocationConnection
        {
            FromLocationSlug = "hearthbound-inn",
            ToLocationSlug   = "aurelian-market",
            ConnectionType   = "walk",
        },

        // ── Cross-zone exits (one-way) ─────────────────────────────────────────

        // crestfall-square → the-droveway (via east road)
        new ZoneLocationConnection
        {
            FromLocationSlug = "crestfall-square",
            ToZoneId         = "the-droveway",
            ConnectionType   = "exit",
        },

        // crestfall-square → ashlen-wood (via west road)
        new ZoneLocationConnection
        {
            FromLocationSlug = "crestfall-square",
            ToZoneId         = "ashlen-wood",
            ConnectionType   = "exit",
        },

        // ── Region map exit (one-way) ─────────────────────────────────────────

        // crestfall-square → varenmark region map (via north road)
        new ZoneLocationConnection
        {
            FromLocationSlug = "crestfall-square",
            ToRegionId       = "varenmark",
            ConnectionType   = "region_exit",
        },
    ];
}
