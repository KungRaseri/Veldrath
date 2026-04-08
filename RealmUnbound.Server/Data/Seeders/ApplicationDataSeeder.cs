using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Seeders;

/// <summary>Seeds world-geography rows (World, Regions, Zones, and their connections) into <see cref="ApplicationDbContext"/>.</summary>
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
            Id          = "draveth",
            Name        = "Draveth",
            Description = "A world of scattered kingdoms, ancient ruins, and contested wilds, shaped by centuries of war and forgotten magic.",
            Era         = "The Age of Embers",
        });

        await db.SaveChangesAsync();
    }

    // Regions
    private static async Task SeedRegionsAsync(ApplicationDbContext db)
    {
        if (await db.Regions.AnyAsync())
            return;

        db.Regions.AddRange(
            new Region { Id = "varenmark",   Name = "Varenmark",   Description = "The oldest settled march in the lowlands. Farmsteads, drove roads and market crossings gave way to wilder land. Whatever was here before the settlers is still here.", Type = RegionType.Countryside, MinLevel = 0, MaxLevel = 6, IsStarter = true, IsDiscoverable = true, WorldId = "draveth" },
            new Region { Id = "greymoor",    Name = "Greymoor",    Description = "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.",    Type = RegionType.Highland, MinLevel = 5,  MaxLevel = 14, IsStarter = false, IsDiscoverable = true, WorldId = "draveth" },
            new Region { Id = "saltcliff",   Name = "Saltcliff",   Description = "Thysmara sea cliffs battered by storm winds, home to sailors, smugglers, and the drowned remnants of a sunken empire.",  Type = RegionType.Coastal,  MinLevel = 10, MaxLevel = 20, IsStarter = false, IsDiscoverable = true, WorldId = "draveth" },
            new Region { Id = "cinderplain", Name = "Cinderplain", Description = "Kaldrek's scorched expanse of ash and cooled lava, where forge-tempered warbands and fire-touched creatures stake their claim.", Type = RegionType.Volcanic, MinLevel = 18, MaxLevel = 30, IsStarter = false, IsDiscoverable = true, WorldId = "draveth" }
        );

        await db.SaveChangesAsync();
    }

    // Region Connections
    private static async Task SeedRegionConnectionsAsync(ApplicationDbContext db)
    {
        if (await db.RegionConnections.AnyAsync())
            return;

        db.RegionConnections.AddRange(
            new RegionConnection { FromRegionId = "varenmark",   ToRegionId = "greymoor"    },
            new RegionConnection { FromRegionId = "greymoor",    ToRegionId = "varenmark"   },
            new RegionConnection { FromRegionId = "greymoor",    ToRegionId = "saltcliff"   },
            new RegionConnection { FromRegionId = "saltcliff",   ToRegionId = "greymoor"    },
            new RegionConnection { FromRegionId = "greymoor",    ToRegionId = "cinderplain" },
            new RegionConnection { FromRegionId = "cinderplain", ToRegionId = "greymoor"    }
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
            new Zone { Id = "crestfall",     Name = "Crestfall",         Description = "A prosperous crossroads market town where three old drove roads meet. Warm lanterns, merchant stalls, and the last comfortable beds for many miles.",    Type = ZoneType.Town,       MinLevel = 0, MaxPlayers = 0, IsStarter = true,  HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "the-droveway",  Name = "The Droveway",      Description = "An old cattle track heading north from Crestfall. Dry-stone walls, pastures gone to seed, and a quiet that feels like it is waiting for something.",   Type = ZoneType.Wilderness, MinLevel = 1, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "ashlen-wood",   Name = "The Ashlen Wood",   Description = "Old-growth forest south of Crestfall. The light is wrong here. Animals behave strangely and old clearings refuse to grow back.",                         Type = ZoneType.Wilderness, MinLevel = 1, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "grevenmire",    Name = "Grevenmire",        Description = "Deep boggy country beyond the Ashlen Wood. Half-sunken ruins, fog that does not lift, and a hostility that feels personal.",                              Type = ZoneType.Wilderness, MinLevel = 3, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "the-halrow",    Name = "The Halrow",        Description = "A buried estate at the end of the Droveway, walls still standing but roof long collapsed. Something has lived in the cellars for a very long time.",      Type = ZoneType.Dungeon,    MinLevel = 4, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            new Zone { Id = "drowning-pits", Name = "The Drowning Pits", Description = "Subterranean chambers beneath Grevenmire, half-flooded and ancient. Whatever built this was not building for humans.",                                    Type = ZoneType.Dungeon,    MinLevel = 4, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "varenmark" },
            // Greymoor
            new Zone { Id = "aldenmere",         Name = "Aldenmere",             Description = "A grey-stoned Dravan waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets.",             Type = ZoneType.Town,       MinLevel = 5,  MaxPlayers = 0, IsStarter = false, HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            new Zone { Id = "pale-moor",         Name = "The Pale Moor",         Description = "Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides.",                   Type = ZoneType.Wilderness, MinLevel = 7,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            new Zone { Id = "soddenfen",         Name = "Soddenfen",             Description = "A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense.",                     Type = ZoneType.Wilderness, MinLevel = 9,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            new Zone { Id = "barrow-deeps",      Name = "The Barrow Deeps",      Description = "A sprawling underground burial network carved through the moorland, where ancestor-beasts guard ancient Dravan relics.",           Type = ZoneType.Dungeon,    MinLevel = 11, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            // Saltcliff
            new Zone { Id = "tolvaren",          Name = "Tolvaren",              Description = "A salt-caked Thysmara port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders.",         Type = ZoneType.Town,       MinLevel = 10, MaxPlayers = 0, IsStarter = false, HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            new Zone { Id = "tidewrack-flats",   Name = "The Tidewrack Flats",   Description = "Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators.",          Type = ZoneType.Wilderness, MinLevel = 12, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            new Zone { Id = "saltcliff-heights", Name = "The Saltcliff Heights", Description = "Wind-blasted clifftops above the Thysmara sea, dotted with lighthouse ruins and contested by rival gull-rider clans.",            Type = ZoneType.Wilderness, MinLevel = 14, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            new Zone { Id = "sunken-name",       Name = "The Sunken Name",       Description = "The drowned heart of a once-great Thysmara empire, now a flooded ruin accessible only to the brave and the waterproofed.",         Type = ZoneType.Dungeon,    MinLevel = 16, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            // Cinderplain
            new Zone { Id = "skarhold",          Name = "Skarhold",              Description = "A Kaldrek forge-city built inside a dormant caldera, where the best smiths in Draveth ply their trade under sulphur skies.",      Type = ZoneType.Town,       MinLevel = 18, MaxPlayers = 0, IsStarter = false, HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "ashfields",         Name = "The Ashfields",         Description = "Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies.",            Type = ZoneType.Wilderness, MinLevel = 20, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "smoldering-reach",  Name = "The Smoldering Reach",  Description = "A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk.",               Type = ZoneType.Wilderness, MinLevel = 23, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "kaldrek-maw",       Name = "Kaldrek's Maw",         Description = "A vast volcanic vent complex known as the Maw, lair of Kaldrek's fire-bound ancient and final test of the worthy.",               Type = ZoneType.Dungeon,    MinLevel = 26, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" }
        );

        await db.SaveChangesAsync();
    }

}
