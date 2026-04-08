using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds all <see cref="ZoneLocation"/> rows and their <see cref="ZoneLocationConnection"/> edges into
/// <see cref="ContentDbContext"/>.  Every zone in the world geography has at least two locations so that
/// the zone-level map view has meaningful nodes and traversal edges to display.
/// </summary>
public static class ZoneLocationsSeeder
{
    /// <summary>Seeds all zone location rows and their connections (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedLocationsAsync(db);
        await SeedConnectionsAsync(db);
    }

    private static async Task SeedLocationsAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ZoneLocations.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllLocations(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.ZoneLocations.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static ZoneLocation[] GetAllLocations(DateTimeOffset now) =>
    [

            // ── Varenmark region ─────────────────────────────────────────────

            // crestfall (Town, L0) ────────────────────────────────────────────
            new ZoneLocation
            {
                Slug = "crestfall-square",     DisplayName = "The Crestfall Square",
                ZoneId = "crestfall",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 1, Population = 50, MinLevel = 0 },
                Traits = new() { IsTown = true },
            },
            new ZoneLocation
            {
                Slug = "aurelian-market",      DisplayName = "The Aurelian Market",
                ZoneId = "crestfall",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 1, Population = 40, MinLevel = 1 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "hearthbound-inn",      DisplayName = "The Hearthbound Inn",
                ZoneId = "crestfall",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 30, MinLevel = 1 },
                Traits = new() { IsTown = true },
            },
            new ZoneLocation
            {
                Slug = "low-quarter",          DisplayName = "The Low Quarter",
                ZoneId = "crestfall",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 25, MinLevel = 1 },
                Traits = new() { IsTown = true },
            },

            // the-droveway (Wilderness, L1) ───────────────────────────────────
            new ZoneLocation
            {
                Slug = "drove-road",           DisplayName = "The Drove Road",
                ZoneId = "the-droveway",       TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 2, Population = 30, MinLevel = 1, MaxLevel = 4 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "weathered-waypost",    DisplayName = "The Weathered Waypost",
                ZoneId = "the-droveway",       TypeKey = "locations",  LocationType = "location",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 2, Population = 5, MinLevel = 1, MaxLevel = 4 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "sunken-fields",        DisplayName = "The Sunken Fields",
                ZoneId = "the-droveway",       TypeKey = "environments", LocationType = "environment",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 3, MinLevel = 2, MaxLevel = 4 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 5 },
            },

            // ashlen-wood (Wilderness, L1) ────────────────────────────────────
            new ZoneLocation
            {
                Slug = "wood-road",            DisplayName = "The Wood Road",
                ZoneId = "ashlen-wood",        TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 2, Population = 40, MinLevel = 1, MaxLevel = 5 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "old-clearing",         DisplayName = "The Old Clearing",
                ZoneId = "ashlen-wood",        TypeKey = "environments", LocationType = "environment",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 3, Population = 15, MinLevel = 2, MaxLevel = 5 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "strangled-oak",        DisplayName = "The Strangled Oak",
                ZoneId = "ashlen-wood",        TypeKey = "locations",  LocationType = "location",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 4, MinLevel = 2, MaxLevel = 5 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 5 },
            },

            // grevenmire (Wilderness, L3) ─────────────────────────────────────
            new ZoneLocation
            {
                Slug = "mire-crossing",        DisplayName = "The Mire Crossing",
                ZoneId = "grevenmire",         TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 4, Population = 20, MinLevel = 3, MaxLevel = 6 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "sunken-estate",        DisplayName = "The Sunken Estate",
                ZoneId = "grevenmire",         TypeKey = "locations",  LocationType = "location",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 5, Population = 10, MinLevel = 3, MaxLevel = 6 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "bone-hollow",          DisplayName = "The Bone Hollow",
                ZoneId = "grevenmire",         TypeKey = "environments", LocationType = "environment",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 6, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 8 },
            },

            // the-halrow (Dungeon, L4) ─────────────────────────────────────────
            new ZoneLocation
            {
                Slug = "halrow-threshold",     DisplayName = "The Halrow Threshold",
                ZoneId = "the-halrow",         TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 3, Population = 5, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "rootbound-cellars",    DisplayName = "The Rootbound Cellars",
                ZoneId = "the-halrow",         TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 4, Population = 30, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "collapsed-vault",      DisplayName = "The Collapsed Vault",
                ZoneId = "the-halrow",         TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 5, Population = 15, MinLevel = 5, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest" },
            },

            // drowning-pits (Dungeon, L4) ─────────────────────────────────────
            new ZoneLocation
            {
                Slug = "pit-descent",          DisplayName = "The Pit Descent",
                ZoneId = "drowning-pits",      TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 4, Population = 5, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "flooded-workings",     DisplayName = "The Flooded Workings",
                ZoneId = "drowning-pits",      TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 5, Population = 30, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "deepest-chamber",      DisplayName = "The Deepest Chamber",
                ZoneId = "drowning-pits",      TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 6, Population = 10, MinLevel = 5, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest" },
            },

            // ── Greymoor region ─────────────────────────────────────────────

            // aldenmere (Town, L5) ────────────────────────────────────────────
            new ZoneLocation
            {
                Slug = "ironhollow-keep",      DisplayName = "Ironhollow Keep",
                ZoneId = "aldenmere",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 60, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 4, Population = 80, MinLevel = 5, MaxLevel = 12 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "aldenmere-marketplace", DisplayName = "Aldenmere Marketplace",
                ZoneId = "aldenmere",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 2, Population = 60, MinLevel = 5 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "grey-cup",             DisplayName = "The Grey Cup",
                ZoneId = "aldenmere",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 1, Population = 25, MinLevel = 5 },
                Traits = new() { IsTown = true },
            },

            // pale-moor (Wilderness, L7) ──────────────────────────────────────
            new ZoneLocation
            {
                Slug = "ashveil-highlands",    DisplayName = "Ashveil Highlands",
                ZoneId = "pale-moor",          TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 7, DangerLevel = 5, Population = 60, MinLevel = 7, MaxLevel = 12 },
                Traits = new() { IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "moorstone-cairns",     DisplayName = "The Moorstone Cairns",
                ZoneId = "pale-moor",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 4, Population = 10, MinLevel = 7, MaxLevel = 12 },
                Traits = new() { IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "shifting-waymark",     DisplayName = "The Shifting Waymark",
                ZoneId = "pale-moor",          TypeKey = "environments", LocationType = "environment",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 6, MinLevel = 8, MaxLevel = 14 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 10 },
            },

            // soddenfen (Wilderness, L9) ──────────────────────────────────────
            new ZoneLocation
            {
                Slug = "fenland-crossing",     DisplayName = "Fenland Crossing",
                ZoneId = "soddenfen",          TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 5, Population = 30, MinLevel = 9, MaxLevel = 13 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "submerged-ruins",      DisplayName = "The Submerged Ruins",
                ZoneId = "soddenfen",          TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 50, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 7, Population = 40, MinLevel = 10, MaxLevel = 14 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "skill_check_active", DiscoverThreshold = 12 },
            },

            // barrow-deeps (Dungeon, L11) ─────────────────────────────────────
            new ZoneLocation
            {
                Slug = "deeps-entrance",       DisplayName = "Deeps Entrance",
                ZoneId = "barrow-deeps",       TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 6, Population = 5, MinLevel = 11, MaxLevel = 14 },
                Traits = new() { IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "ancestor-vault",       DisplayName = "The Ancestor Vault",
                ZoneId = "barrow-deeps",       TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 7, Population = 50, MinLevel = 11, MaxLevel = 14 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "relic-chamber",        DisplayName = "The Relic Chamber",
                ZoneId = "barrow-deeps",       TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 8, Population = 20, MinLevel = 12, MaxLevel = 14 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest" },
            },

            // ── Saltcliff region ────────────────────────────────────────────

            // tolvaren (Town, L10) ────────────────────────────────────────────
            new ZoneLocation
            {
                Slug = "tolvaren-harbour",     DisplayName = "Tolvaren Harbour",
                ZoneId = "tolvaren",           TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 2, Population = 100, MinLevel = 10 },
                Traits = new() { IsTown = true },
            },
            new ZoneLocation
            {
                Slug = "cliff-road-market",    DisplayName = "Cliff Road Market",
                ZoneId = "tolvaren",           TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 2, Population = 60, MinLevel = 10 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "saltcrow-inn",         DisplayName = "The Saltcrow Inn",
                ZoneId = "tolvaren",           TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 1, Population = 20, MinLevel = 10 },
                Traits = new() { IsTown = true },
            },

            // tidewrack-flats (Wilderness, L12) ──────────────────────────────
            new ZoneLocation
            {
                Slug = "wrack-shore",          DisplayName = "Wrack Shore",
                ZoneId = "tidewrack-flats",    TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 5, Population = 40, MinLevel = 12, MaxLevel = 18 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "bone-strand",          DisplayName = "The Bone Strand",
                ZoneId = "tidewrack-flats",    TypeKey = "environments", LocationType = "environment",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 6, Population = 35, MinLevel = 13, MaxLevel = 18 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "tidal-grotto",         DisplayName = "The Tidal Grotto",
                ZoneId = "tidewrack-flats",    TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 7, Population = 25, MinLevel = 14, MaxLevel = 18 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "skill_check_active", DiscoverThreshold = 14 },
            },

            // saltcliff-heights (Wilderness, L14) ────────────────────────────
            new ZoneLocation
            {
                Slug = "clifftop-ruins",       DisplayName = "The Clifftop Ruins",
                ZoneId = "saltcliff-heights",  TypeKey = "locations",  LocationType = "location",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 6, Population = 20, MinLevel = 14, MaxLevel = 20 },
                Traits = new() { IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "gull-rider-camp",      DisplayName = "Gull-Rider Camp",
                ZoneId = "saltcliff-heights",  TypeKey = "environments", LocationType = "environment",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 7, Population = 30, MinLevel = 15, MaxLevel = 20 },
                Traits = new() { IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "storm-watch-peak",     DisplayName = "Storm-Watch Peak",
                ZoneId = "saltcliff-heights",  TypeKey = "locations",  LocationType = "location",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 8, MinLevel = 16, MaxLevel = 20 },
                Traits = new() { IsHidden = true, UnlockType = "achievement" },
            },

            // sunken-name (Dungeon, L16) ──────────────────────────────────────
            new ZoneLocation
            {
                Slug = "drowned-threshold",    DisplayName = "The Drowned Threshold",
                ZoneId = "sunken-name",        TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 7, Population = 5, MinLevel = 16, MaxLevel = 20 },
                Traits = new() { IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "flooded-throne-room",  DisplayName = "The Flooded Throne Room",
                ZoneId = "sunken-name",        TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 9, Population = 60, MinLevel = 16, MaxLevel = 20 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "tidelocked-vault",     DisplayName = "The Tidelocked Vault",
                ZoneId = "sunken-name",        TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 10, Population = 20, MinLevel = 17, MaxLevel = 20 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest" },
            },

            // ── Cinderplain region ───────────────────────────────────────────

            // skarhold (Town, L18) ────────────────────────────────────────────
            new ZoneLocation
            {
                Slug = "forge-quarter",        DisplayName = "The Forge Quarter",
                ZoneId = "skarhold",           TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 3, Population = 80, MinLevel = 18 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "caldera-market",       DisplayName = "Caldera Market",
                ZoneId = "skarhold",           TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 2, Population = 60, MinLevel = 18 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "ashbrand-lodge",       DisplayName = "The Ashbrand Lodge",
                ZoneId = "skarhold",           TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 30, MinLevel = 18 },
                Traits = new() { IsTown = true },
            },

            // ashfields (Wilderness, L20) ─────────────────────────────────────
            new ZoneLocation
            {
                Slug = "obsidian-grove",       DisplayName = "The Obsidian Grove",
                ZoneId = "ashfields",          TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 8, Population = 50, MinLevel = 20, MaxLevel = 25 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "scorched-battlefield", DisplayName = "The Scorched Battlefield",
                ZoneId = "ashfields",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 8, DangerLevel = 9, Population = 70, MinLevel = 21, MaxLevel = 25 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "ash-shrine",           DisplayName = "The Ash Shrine",
                ZoneId = "ashfields",          TypeKey = "locations",  LocationType = "location",
                RarityWeight = 25, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 7, MinLevel = 20, MaxLevel = 25 },
                Traits = new() { IsHidden = true, UnlockType = "quest" },
            },

            // smoldering-reach (Wilderness, L23) ─────────────────────────────
            new ZoneLocation
            {
                Slug = "vent-fields",          DisplayName = "The Vent Fields",
                ZoneId = "smoldering-reach",   TypeKey = "environments", LocationType = "environment",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 7, DangerLevel = 9, Population = 45, MinLevel = 23, MaxLevel = 26 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "pyreling-den",         DisplayName = "The Pyreling Den",
                ZoneId = "smoldering-reach",   TypeKey = "environments", LocationType = "environment",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 10, Population = 35, MinLevel = 24, MaxLevel = 26 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "lava-bridge",          DisplayName = "The Lava Bridge",
                ZoneId = "smoldering-reach",   TypeKey = "locations",  LocationType = "location",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 10, MinLevel = 24, MaxLevel = 26 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_active", DiscoverThreshold = 18 },
            },

            // kaldrek-maw (Dungeon, L26) ──────────────────────────────────────
            new ZoneLocation
            {
                Slug = "maw-descent",          DisplayName = "The Maw Descent",
                ZoneId = "kaldrek-maw",        TypeKey = "locations",  LocationType = "location",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 10, Population = 5, MinLevel = 26, MaxLevel = 30 },
                Traits = new() { IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "fire-ancients-chamber", DisplayName = "The Fire-Ancient's Chamber",
                ZoneId = "kaldrek-maw",        TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 8, DangerLevel = 10, Population = 30, MinLevel = 26, MaxLevel = 30 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "kaldreks-heart",       DisplayName = "Kaldrek's Heart",
                ZoneId = "kaldrek-maw",        TypeKey = "dungeons",   LocationType = "dungeon",
                RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 10, Population = 10, MinLevel = 28, MaxLevel = 30 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest" },
            }
    ];

    private static async Task SeedConnectionsAsync(ContentDbContext db)
    {
        var existing = (await db.ZoneLocationConnections.AsNoTracking()
                .Select(c => new { c.FromLocationSlug, c.ToLocationSlug })
                .ToListAsync())
            .Select(c => (c.FromLocationSlug, c.ToLocationSlug))
            .ToHashSet();
        var missing = GetAllConnections()
            .Where(c => !existing.Contains((c.FromLocationSlug, c.ToLocationSlug!)))
            .ToList();
        if (missing.Count == 0) return;
        db.ZoneLocationConnections.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static ZoneLocationConnection[] GetAllConnections() =>
    [

            // ── crestfall ────────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "crestfall-square",    ToLocationSlug = "aurelian-market",   ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "aurelian-market",     ToLocationSlug = "crestfall-square",  ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "crestfall-square",    ToLocationSlug = "hearthbound-inn",   ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "hearthbound-inn",     ToLocationSlug = "crestfall-square",  ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "crestfall-square",    ToLocationSlug = "low-quarter",       ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "low-quarter",         ToLocationSlug = "crestfall-square",  ConnectionType = "path", IsTraversable = true },

            // ── the-droveway ─────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "drove-road",          ToLocationSlug = "weathered-waypost", ConnectionType = "path",           IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "weathered-waypost",   ToLocationSlug = "drove-road",        ConnectionType = "path",           IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "weathered-waypost",   ToLocationSlug = "sunken-fields",     ConnectionType = "secret_passage", IsTraversable = true },

            // ── ashlen-wood ──────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "wood-road",           ToLocationSlug = "old-clearing",      ConnectionType = "path",           IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "old-clearing",        ToLocationSlug = "wood-road",         ConnectionType = "path",           IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "old-clearing",        ToLocationSlug = "strangled-oak",     ConnectionType = "secret_passage", IsTraversable = true },

            // ── grevenmire ───────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "mire-crossing",       ToLocationSlug = "sunken-estate",     ConnectionType = "path",           IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "sunken-estate",       ToLocationSlug = "mire-crossing",     ConnectionType = "path",           IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "sunken-estate",       ToLocationSlug = "bone-hollow",       ConnectionType = "secret_passage", IsTraversable = true },

            // ── the-halrow ───────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "halrow-threshold",    ToLocationSlug = "rootbound-cellars", ConnectionType = "dungeon_entrance", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "rootbound-cellars",   ToLocationSlug = "halrow-threshold",  ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "rootbound-cellars",   ToLocationSlug = "collapsed-vault",   ConnectionType = "path",             IsTraversable = true },

            // ── drowning-pits ────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "pit-descent",         ToLocationSlug = "flooded-workings",  ConnectionType = "dungeon_entrance", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "flooded-workings",    ToLocationSlug = "pit-descent",       ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "flooded-workings",    ToLocationSlug = "deepest-chamber",   ConnectionType = "path",             IsTraversable = true },

            // ── aldenmere ───────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "ironhollow-keep",      ToLocationSlug = "aldenmere-marketplace",   ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "aldenmere-marketplace",ToLocationSlug = "ironhollow-keep",         ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "aldenmere-marketplace",ToLocationSlug = "grey-cup",                ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "grey-cup",             ToLocationSlug = "aldenmere-marketplace",   ConnectionType = "path",             IsTraversable = true },

            // ── pale-moor ───────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "ashveil-highlands",    ToLocationSlug = "moorstone-cairns",        ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "moorstone-cairns",     ToLocationSlug = "ashveil-highlands",       ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "moorstone-cairns",     ToLocationSlug = "shifting-waymark",        ConnectionType = "secret_passage",   IsTraversable = true },

            // ── soddenfen ───────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "fenland-crossing",     ToLocationSlug = "submerged-ruins",         ConnectionType = "dungeon_entrance", IsTraversable = true },

            // ── barrow-deeps ────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "deeps-entrance",       ToLocationSlug = "ancestor-vault",          ConnectionType = "dungeon_entrance", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "ancestor-vault",       ToLocationSlug = "deeps-entrance",          ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "ancestor-vault",       ToLocationSlug = "relic-chamber",           ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "deeps-entrance",       ToLocationSlug = "forge-quarter",           ConnectionType = "path",             IsTraversable = true, IsHidden = true },
            new ZoneLocationConnection { FromLocationSlug = "forge-quarter",        ToLocationSlug = "deeps-entrance",          ConnectionType = "path",             IsTraversable = true, IsHidden = true },

            // ── tolvaren ────────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "tolvaren-harbour",     ToLocationSlug = "cliff-road-market",       ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "cliff-road-market",    ToLocationSlug = "tolvaren-harbour",        ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "cliff-road-market",    ToLocationSlug = "saltcrow-inn",            ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "saltcrow-inn",         ToLocationSlug = "cliff-road-market",       ConnectionType = "path",             IsTraversable = true },

            // ── tidewrack-flats ─────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "wrack-shore",          ToLocationSlug = "bone-strand",             ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "bone-strand",          ToLocationSlug = "wrack-shore",             ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "bone-strand",          ToLocationSlug = "tidal-grotto",            ConnectionType = "secret_passage",   IsTraversable = true },

            // ── saltcliff-heights ───────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "clifftop-ruins",       ToLocationSlug = "gull-rider-camp",         ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "gull-rider-camp",      ToLocationSlug = "clifftop-ruins",          ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "gull-rider-camp",      ToLocationSlug = "storm-watch-peak",        ConnectionType = "path",             IsTraversable = true },

            // ── sunken-name ─────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "drowned-threshold",    ToLocationSlug = "flooded-throne-room",     ConnectionType = "dungeon_entrance", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "flooded-throne-room",  ToLocationSlug = "drowned-threshold",       ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "flooded-throne-room",  ToLocationSlug = "tidelocked-vault",        ConnectionType = "path",             IsTraversable = true },

            // ── skarhold ────────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "forge-quarter",        ToLocationSlug = "caldera-market",          ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "caldera-market",       ToLocationSlug = "forge-quarter",           ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "caldera-market",       ToLocationSlug = "ashbrand-lodge",          ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "ashbrand-lodge",       ToLocationSlug = "caldera-market",          ConnectionType = "path",             IsTraversable = true },

            // ── ashfields ───────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "obsidian-grove",       ToLocationSlug = "scorched-battlefield",    ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "scorched-battlefield", ToLocationSlug = "obsidian-grove",          ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "scorched-battlefield", ToLocationSlug = "ash-shrine",              ConnectionType = "secret_passage",   IsTraversable = true },

            // ── smoldering-reach ────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "vent-fields",          ToLocationSlug = "pyreling-den",            ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "pyreling-den",         ToLocationSlug = "vent-fields",             ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "pyreling-den",         ToLocationSlug = "lava-bridge",             ConnectionType = "path",             IsTraversable = true },

            // ── kaldrek-maw ─────────────────────────────────────────────────
            new ZoneLocationConnection { FromLocationSlug = "maw-descent",            ToLocationSlug = "fire-ancients-chamber",   ConnectionType = "dungeon_entrance", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "fire-ancients-chamber",ToLocationSlug = "maw-descent",             ConnectionType = "path",             IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "fire-ancients-chamber",ToLocationSlug = "kaldreks-heart",          ConnectionType = "path",             IsTraversable = true }
        ];
}
