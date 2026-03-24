using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="WorldLocation"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class WorldLocationsSeeder
{
    /// <summary>Seeds all world location rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.WorldLocations.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.WorldLocations.AddRange(
            // ── Settlements ───────────────────────────────────────────────────
            new WorldLocation
            {
                Slug         = "thornveil-village",
                TypeKey      = "locations",
                DisplayName  = "Thornveil Village",
                LocationType = "location",
                RarityWeight = 100,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new WorldLocationStats
                {
                    Size        = 3,
                    DangerLevel = 1,
                    Population  = 40,
                    MinLevel    = 1,
                    MaxLevel    = null,
                },
                Traits = new WorldLocationTraits
                {
                    IsTown        = true,
                    IsIndoor      = false,
                    HasMerchant   = true,
                    PvpEnabled    = false,
                    IsDiscoverable = false,
                    IsDungeon     = false,
                },
            },

            new WorldLocation
            {
                Slug         = "ironhollow-keep",
                TypeKey      = "locations",
                DisplayName  = "Ironhollow Keep",
                LocationType = "location",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new WorldLocationStats
                {
                    Size        = 5,
                    DangerLevel = 4,
                    Population  = 80,
                    MinLevel    = 5,
                    MaxLevel    = 12,
                },
                Traits = new WorldLocationTraits
                {
                    IsTown        = true,
                    IsIndoor      = false,
                    HasMerchant   = true,
                    PvpEnabled    = false,
                    IsDiscoverable = false,
                    IsDungeon     = false,
                },
            },

            // ── Wilderness environments ───────────────────────────────────────
            new WorldLocation
            {
                Slug         = "darkwood-forest",
                TypeKey      = "environments",
                DisplayName  = "Darkwood Forest",
                LocationType = "environment",
                RarityWeight = 80,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new WorldLocationStats
                {
                    Size        = 8,
                    DangerLevel = 3,
                    Population  = 120,
                    MinLevel    = 1,
                    MaxLevel    = 6,
                },
                Traits = new WorldLocationTraits
                {
                    IsTown        = false,
                    IsIndoor      = false,
                    HasMerchant   = false,
                    PvpEnabled    = false,
                    IsDiscoverable = false,
                    IsDungeon     = false,
                },
            },

            new WorldLocation
            {
                Slug         = "ashveil-highlands",
                TypeKey      = "environments",
                DisplayName  = "Ashveil Highlands",
                LocationType = "environment",
                RarityWeight = 50,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new WorldLocationStats
                {
                    Size        = 7,
                    DangerLevel = 5,
                    Population  = 60,
                    MinLevel    = 8,
                    MaxLevel    = 15,
                },
                Traits = new WorldLocationTraits
                {
                    IsTown        = false,
                    IsIndoor      = false,
                    HasMerchant   = false,
                    PvpEnabled    = true,
                    IsDiscoverable = true,
                    IsDungeon     = false,
                },
            },

            // ── Dungeons ──────────────────────────────────────────────────────
            new WorldLocation
            {
                Slug         = "goblin-warrens",
                TypeKey      = "dungeons",
                DisplayName  = "Goblin Warrens",
                LocationType = "region",
                RarityWeight = 70,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new WorldLocationStats
                {
                    Size        = 4,
                    DangerLevel = 3,
                    Population  = 50,
                    MinLevel    = 2,
                    MaxLevel    = 5,
                },
                Traits = new WorldLocationTraits
                {
                    IsTown        = false,
                    IsIndoor      = true,
                    HasMerchant   = false,
                    PvpEnabled    = false,
                    IsDiscoverable = true,
                    IsDungeon     = true,
                },
            }
        );

        await db.SaveChangesAsync();
    }
}
