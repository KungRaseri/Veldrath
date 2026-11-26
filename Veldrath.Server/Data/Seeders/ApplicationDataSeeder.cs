using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Seeders;

/// <summary>Seeds world-geography rows (World, Regions, Zones, and their connections) into <see cref="ApplicationDbContext"/>.</summary>
/// <remarks>
/// Names and descriptions come from <see cref="WorldCanonText"/>, which <see cref="WorldCanonSweep"/> reads as
/// well, so the text a fresh database gets and the text an existing one is corrected to cannot drift apart.
/// Each section is skipped once its table has rows, which is why a text change on its own never reaches a
/// database that is already seeded — that is the sweep's job.
/// </remarks>
public static class ApplicationDataSeeder
{
    /// <summary>Seeds all world-geography rows (idempotent, ordered by dependency).</summary>
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        await SeedWorldAsync(db);
        await SeedRegionsAsync(db);
        await SeedRegionConnectionsAsync(db);
        await SeedZonesAsync(db);
    }

    // World
    private static async Task SeedWorldAsync(ApplicationDbContext db)
    {
        if (await db.Worlds.AnyAsync())
            return;

        db.Worlds.Add(new World
        {
            Id = WorldCanonText.WorldId,
            Name = WorldCanonText.World.Name,
            Description = WorldCanonText.World.Description,
            Era = WorldCanonText.World.Era,
        });

        await db.SaveChangesAsync();
    }

    // Regions
    private static async Task SeedRegionsAsync(ApplicationDbContext db)
    {
        if (await db.Regions.AnyAsync())
            return;

        db.Regions.AddRange(
            new Region { Id = "varenmark", Name = WorldCanonText.Regions["varenmark"].Name, Description = WorldCanonText.Regions["varenmark"].Description, Type = RegionType.Countryside, MinLevel = 0, MaxLevel = 6, IsStarter = true, IsDiscoverable = true, WorldId = "veldrath" },
            new Region { Id = "greymoor", Name = WorldCanonText.Regions["greymoor"].Name, Description = WorldCanonText.Regions["greymoor"].Description, Type = RegionType.Highland, MinLevel = 5, MaxLevel = 14, IsStarter = false, IsDiscoverable = true, WorldId = "veldrath" },
            new Region { Id = "saltcliff", Name = WorldCanonText.Regions["saltcliff"].Name, Description = WorldCanonText.Regions["saltcliff"].Description, Type = RegionType.Coastal, MinLevel = 10, MaxLevel = 20, IsStarter = false, IsDiscoverable = true, WorldId = "veldrath" },
            new Region { Id = "cinderplain", Name = WorldCanonText.Regions["cinderplain"].Name, Description = WorldCanonText.Regions["cinderplain"].Description, Type = RegionType.Volcanic, MinLevel = 18, MaxLevel = 30, IsStarter = false, IsDiscoverable = true, WorldId = "veldrath" }
        );

        await db.SaveChangesAsync();
    }

    // Region Connections
    private static async Task SeedRegionConnectionsAsync(ApplicationDbContext db)
    {
        if (await db.RegionConnections.AnyAsync())
            return;

        db.RegionConnections.AddRange(
            new RegionConnection { FromRegionId = "varenmark", ToRegionId = "greymoor" },
            new RegionConnection { FromRegionId = "greymoor", ToRegionId = "varenmark" },
            new RegionConnection { FromRegionId = "greymoor", ToRegionId = "saltcliff" },
            new RegionConnection { FromRegionId = "saltcliff", ToRegionId = "greymoor" },
            new RegionConnection { FromRegionId = "greymoor", ToRegionId = "cinderplain" },
            new RegionConnection { FromRegionId = "cinderplain", ToRegionId = "greymoor" }
        );

        await db.SaveChangesAsync();
    }

    // Zones
    private static async Task SeedZonesAsync(ApplicationDbContext db)
    {
        if (await db.Zones.AnyAsync())
            return;

        db.Zones.AddRange(
            // Varenmark
            new Zone { Id = "crestfall", Name = WorldCanonText.Zones["crestfall"].Name, Description = WorldCanonText.Zones["crestfall"].Description, Type = ZoneType.Town, MinLevel = 0, MaxPlayers = 0, IsStarter = true, HasInn = true, HasMerchant = true, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "the-droveway", Name = WorldCanonText.Zones["the-droveway"].Name, Description = WorldCanonText.Zones["the-droveway"].Description, Type = ZoneType.Wilderness, MinLevel = 1, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "ashlen-wood", Name = WorldCanonText.Zones["ashlen-wood"].Name, Description = WorldCanonText.Zones["ashlen-wood"].Description, Type = ZoneType.Wilderness, MinLevel = 1, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "grevenmire", Name = WorldCanonText.Zones["grevenmire"].Name, Description = WorldCanonText.Zones["grevenmire"].Description, Type = ZoneType.Wilderness, MinLevel = 3, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "the-halrow", Name = WorldCanonText.Zones["the-halrow"].Name, Description = WorldCanonText.Zones["the-halrow"].Description, Type = ZoneType.Dungeon, MinLevel = 4, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "drowning-pits", Name = WorldCanonText.Zones["drowning-pits"].Name, Description = WorldCanonText.Zones["drowning-pits"].Description, Type = ZoneType.Dungeon, MinLevel = 4, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            // Greymoor
            new Zone { Id = "aldenmere", Name = WorldCanonText.Zones["aldenmere"].Name, Description = WorldCanonText.Zones["aldenmere"].Description, Type = ZoneType.Town, MinLevel = 5, MaxPlayers = 0, IsStarter = false, HasInn = true, HasMerchant = true, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor" },
            new Zone { Id = "pale-moor", Name = WorldCanonText.Zones["pale-moor"].Name, Description = WorldCanonText.Zones["pale-moor"].Description, Type = ZoneType.Wilderness, MinLevel = 7, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor" },
            new Zone { Id = "soddenfen", Name = WorldCanonText.Zones["soddenfen"].Name, Description = WorldCanonText.Zones["soddenfen"].Description, Type = ZoneType.Wilderness, MinLevel = 9, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor" },
            new Zone { Id = "barrow-deeps", Name = WorldCanonText.Zones["barrow-deeps"].Name, Description = WorldCanonText.Zones["barrow-deeps"].Description, Type = ZoneType.Dungeon, MinLevel = 11, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor" },
            // Saltcliff
            new Zone { Id = "tolvaren", Name = WorldCanonText.Zones["tolvaren"].Name, Description = WorldCanonText.Zones["tolvaren"].Description, Type = ZoneType.Town, MinLevel = 10, MaxPlayers = 0, IsStarter = false, HasInn = true, HasMerchant = true, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff" },
            new Zone { Id = "tidewrack-flats", Name = WorldCanonText.Zones["tidewrack-flats"].Name, Description = WorldCanonText.Zones["tidewrack-flats"].Description, Type = ZoneType.Wilderness, MinLevel = 12, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff" },
            new Zone { Id = "saltcliff-heights", Name = WorldCanonText.Zones["saltcliff-heights"].Name, Description = WorldCanonText.Zones["saltcliff-heights"].Description, Type = ZoneType.Wilderness, MinLevel = 14, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff" },
            new Zone { Id = "sunken-name", Name = WorldCanonText.Zones["sunken-name"].Name, Description = WorldCanonText.Zones["sunken-name"].Description, Type = ZoneType.Dungeon, MinLevel = 16, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff" },
            // Cinderplain
            new Zone { Id = "skarhold", Name = WorldCanonText.Zones["skarhold"].Name, Description = WorldCanonText.Zones["skarhold"].Description, Type = ZoneType.Town, MinLevel = 18, MaxPlayers = 0, IsStarter = false, HasInn = true, HasMerchant = true, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "ashfields", Name = WorldCanonText.Zones["ashfields"].Name, Description = WorldCanonText.Zones["ashfields"].Description, Type = ZoneType.Wilderness, MinLevel = 20, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "smoldering-reach", Name = WorldCanonText.Zones["smoldering-reach"].Name, Description = WorldCanonText.Zones["smoldering-reach"].Description, Type = ZoneType.Wilderness, MinLevel = 23, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "kaldrek-maw", Name = WorldCanonText.Zones["kaldrek-maw"].Name, Description = WorldCanonText.Zones["kaldrek-maw"].Description, Type = ZoneType.Dungeon, MinLevel = 26, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            // Dev playground — not visible in public APIs (IsDevOnly = true, IsDiscoverable = false)
            new Zone { Id = "playground", Name = WorldCanonText.Zones["playground"].Name, Description = WorldCanonText.Zones["playground"].Description, Type = ZoneType.Town, MinLevel = 0, MaxPlayers = 0, IsStarter = false, HasInn = true, HasMerchant = true, IsPvpEnabled = false, IsDiscoverable = false, IsDevOnly = true, RegionId = "varenmark" }
        );

        await db.SaveChangesAsync();
    }

}
