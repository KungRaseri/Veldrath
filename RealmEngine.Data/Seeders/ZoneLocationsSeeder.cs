using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="ZoneLocation"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class ZoneLocationsSeeder
{
    /// <summary>Seeds all zone location rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.ZoneLocations.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.ZoneLocations.AddRange(
            // â”€â”€ Settlements â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug         = "thornveil-village",
                TypeKey      = "locations",
                DisplayName  = "Thornveil Village",
                ZoneId       = "fenwick-crossing",
                LocationType = "location",
                RarityWeight = 100,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ZoneLocationStats
                {
                    Size        = 3,
                    DangerLevel = 1,
                    Population  = 40,
                    MinLevel    = 1,
                    MaxLevel    = null,
                },
                Traits = new ZoneLocationTraits
                {
                    IsTown        = true,
                    IsIndoor      = false,
                    HasMerchant   = true,
                    IsDiscoverable = false,
                    IsDungeon     = false,
                },
            },

            new ZoneLocation
            {
                Slug         = "ironhollow-keep",
                TypeKey      = "locations",
                DisplayName  = "Ironhollow Keep",
                ZoneId       = "aldenmere",
                LocationType = "location",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ZoneLocationStats
                {
                    Size        = 5,
                    DangerLevel = 4,
                    Population  = 80,
                    MinLevel    = 5,
                    MaxLevel    = 12,
                },
                Traits = new ZoneLocationTraits
                {
                    IsTown        = true,
                    IsIndoor      = false,
                    HasMerchant   = true,
                    IsDiscoverable = false,
                    IsDungeon     = false,
                },
            },

            // â”€â”€ Wilderness environments â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug         = "darkwood-forest",
                TypeKey      = "environments",
                DisplayName  = "Darkwood Forest",
                ZoneId       = "greenveil-paths",
                LocationType = "environment",
                RarityWeight = 80,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ZoneLocationStats
                {
                    Size        = 8,
                    DangerLevel = 3,
                    Population  = 120,
                    MinLevel    = 1,
                    MaxLevel    = 6,
                },
                Traits = new ZoneLocationTraits
                {
                    IsTown        = false,
                    IsIndoor      = false,
                    HasMerchant   = false,
                    IsDiscoverable = false,
                    IsDungeon     = false,
                },
            },

            new ZoneLocation
            {
                Slug         = "ashveil-highlands",
                TypeKey      = "environments",
                DisplayName  = "Ashveil Highlands",
                ZoneId       = "pale-moor",
                LocationType = "environment",
                RarityWeight = 50,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ZoneLocationStats
                {
                    Size        = 7,
                    DangerLevel = 5,
                    Population  = 60,
                    MinLevel    = 8,
                    MaxLevel    = 15,
                },
                Traits = new ZoneLocationTraits
                {
                    IsTown        = false,
                    IsIndoor      = false,
                    HasMerchant   = false,
                    IsDiscoverable = true,
                    IsDungeon     = false,
                },
            },

            // â”€â”€ Dungeons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug         = "goblin-warrens",
                TypeKey      = "dungeons",
                DisplayName  = "Goblin Warrens",
                ZoneId       = "thornveil-hollow",
                LocationType = "dungeon",
                RarityWeight = 70,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ZoneLocationStats
                {
                    Size        = 4,
                    DangerLevel = 3,
                    Population  = 50,
                    MinLevel    = 2,
                    MaxLevel    = 5,
                },
                Traits = new ZoneLocationTraits
                {
                    IsTown        = false,
                    IsIndoor      = true,
                    HasMerchant   = false,
                    IsDiscoverable = true,
                    IsDungeon     = true,
                },
            }
        );

        await db.SaveChangesAsync();
    }
}
