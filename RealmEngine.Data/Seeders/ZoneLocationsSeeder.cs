using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds all <see cref="ZoneLocation"/> rows into <see cref="ContentDbContext"/>.
/// Every zone in the world geography has at least two locations so that
/// the zone-level map view has meaningful nodes to display.
/// </summary>
public static class ZoneLocationsSeeder
{
    /// <summary>Seeds all zone location rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedLocationsAsync(db);
    }

    private static async Task SeedLocationsAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ZoneLocations.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllLocations(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;

        // The pools are declared apart from the locations so the whole encounter map reads as one table. A location
        // that is not listed here spawns nothing on purpose: towns, inns, markets and the SafeRespawn lobbies.
        foreach (var location in missing)
            if (SpawnPools.TryGetValue(location.Slug, out var pool))
                location.ActorPool = [.. pool];

        db.ZoneLocations.AddRange(missing);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// What spawns where, keyed by location slug.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every location that is not a settlement or a dungeon lobby appears here, weighted towards the archetype
    /// band its own <c>Stats.MinLevel</c>/<c>MaxLevel</c> covers: the geography runs from level 0 to 30, so a pool
    /// that names a level 3 goblin on a level 26 map is worse than no pool at all.
    /// </para>
    /// <para>
    /// Pools are one of the two placement mechanisms in the game, not the only one: a named <c>ActorInstance</c>
    /// placed at this location is spawned once before any pool draw, so an actor can stand here without being in
    /// the pool. A pool entry still means an attackable enemy, so NPCs are never listed — people are placed by
    /// <c>LocationNpcs</c> or an <c>ActorInstance</c> instead. <c>playground-clearing</c> is the dev-only fixture the
    /// end-to-end specs fight in.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, ActorPoolEntry[]> SpawnPools = new(StringComparer.Ordinal)
    {
        // Varenmark — levels 1–6
        ["drove-road"] = [Rat(60), Goblin(40)],
        ["wood-road"] = [Rat(40), Goblin(40), Wolf(20)],
        ["old-clearing"] = [Wolf(70), Goblin(30)],
        ["mire-crossing"] = [Bandit(60), Wolf(40)],
        ["bone-hollow"] = [Wight(60), Bandit(40)],
        ["sunken-fields"] = [Rat(50), Wolf(50)],
        ["rootbound-cellars"] = [Bandit(70), Wight(30)],
        ["flooded-workings"] = [Bandit(50), Wight(50)],
        ["collapsed-vault"] = [Bandit(70), Wight(30)],
        ["deepest-chamber"] = [Wight(60), Bandit(40)],

        // Greymoor — levels 7–14
        ["ashveil-highlands"] = [Wight(70), Haunt(30)],
        ["shifting-waymark"] = [Haunt(70), Wight(30)],
        ["fenland-crossing"] = [Lurker(70), Haunt(30)],
        ["submerged-ruins"] = [Lurker(60), Revenant(40)],
        ["ancestor-vault"] = [Revenant(60), Haunt(25), Captain(15)],
        ["relic-chamber"] = [Revenant(55), Captain(45)],

        // Saltcliff — levels 13–20
        ["wrack-shore"] = [Brute(70), Revenant(30)],
        ["bone-strand"] = [Brute(55), Harrower(45)],
        ["tidal-grotto"] = [Harrower(60), Brute(40)],
        ["gull-rider-camp"] = [Brute(80), Harrower(20)],
        ["storm-watch-peak"] = [Harrower(100)],
        ["flooded-throne-room"] = [Revenant(60), Harrower(40)],
        ["tidelocked-vault"] = [Harrower(70), Revenant(30)],

        // Cinderplain — levels 20–30
        // The safe node moved from `scorched-battlefield` to `obsidian-grove` (L3, t_6c70dc04): a respawn
        // point on a battlefield reads wrong, and the grove is the node nearest the zone entrance. The
        // battlefield therefore carries the fight and the grove goes quiet, which keeps ashfields at one
        // respawn anchor: the shrine is flagged SafeRespawn too but is hidden, and the respawn resolver
        // reads the hidden-filtered zone query.
        ["scorched-battlefield"] = [Cinderling(70), Pyreling(30)],
        ["vent-fields"] = [Pyreling(70), Cinderling(30)],
        ["pyreling-den"] = [Pyreling(100)],
        ["fire-ancients-chamber"] = [Cinderling(70), Ancient(30)],
        ["kaldreks-heart"] = [Ancient(100)],

        // Dev playground (IsDevOnly zone): the deterministic encounter the e2e specs use. The other fixtures get a
        // token pool so the "every non-settlement location can spawn something" rule holds there too.
        ["playground-clearing"] = [Rat(50), Bandit(50)],
        ["playground-grove"] = [Rat(100)],
        ["playground-dungeon-gate"] = [Bandit(100)],
        ["playground-hidden-skill"] = [Rat(100)],
        ["playground-hidden-active"] = [Rat(100)],
        ["playground-hidden-quest"] = [Rat(100)],
        ["playground-hidden-item"] = [Rat(100)],
        ["playground-hidden-achievement"] = [Rat(100)],
    };

    private static ActorPoolEntry Rat(int weight) => Entry("giant-rat", weight);
    private static ActorPoolEntry Goblin(int weight) => Entry("goblin-scout", weight);
    private static ActorPoolEntry Wolf(int weight) => Entry("forest-wolf", weight);
    private static ActorPoolEntry Bandit(int weight) => Entry("bandit-ruffian", weight);
    private static ActorPoolEntry Captain(int weight) => Entry("bandit-captain", weight);
    private static ActorPoolEntry Wight(int weight) => Entry("moor-wight", weight);
    private static ActorPoolEntry Haunt(int weight) => Entry("cairn-haunt", weight);
    private static ActorPoolEntry Lurker(int weight) => Entry("fen-lurker", weight);
    private static ActorPoolEntry Revenant(int weight) => Entry("drowned-revenant", weight);
    private static ActorPoolEntry Brute(int weight) => Entry("gull-rider-brute", weight);
    private static ActorPoolEntry Harrower(int weight) => Entry("storm-harrower", weight);
    private static ActorPoolEntry Cinderling(int weight) => Entry("cinderling", weight);
    private static ActorPoolEntry Pyreling(int weight) => Entry("pyreling", weight);
    private static ActorPoolEntry Ancient(int weight) => Entry("fire-ancient", weight);

    private static ActorPoolEntry Entry(string archetypeSlug, int weight) =>
        new() { ArchetypeSlug = archetypeSlug, Weight = weight };

    private static ZoneLocation[] GetAllLocations(DateTimeOffset now) =>
    [

            // â”€â”€ Varenmark region â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // crestfall (Town, L0) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "crestfall-square",     DisplayName = "The Crestfall Square",
                ZoneId = "crestfall",          TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 1, Population = 50, MinLevel = 0 },
                Traits = new() { SafeRespawn = true, IsTown = true },
            },
            new ZoneLocation
            {
                Slug = "aurelian-market",      DisplayName = "The Aurelian Market",
                ZoneId = "crestfall",          TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 1, Population = 40, MinLevel = 1 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "hearthbound-inn",      DisplayName = "The Hearthbound Inn",
                ZoneId = "crestfall",          TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 30, MinLevel = 1 },
                Traits = new() { IsTown = true },
            },
            new ZoneLocation
            {
                Slug = "low-quarter",          DisplayName = "The Low Quarter",
                ZoneId = "crestfall",          TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 25, MinLevel = 1 },
                Traits = new() { IsTown = true },
            },

            // the-droveway (Wilderness, L1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "drove-road",           DisplayName = "The Drove Road",
                ZoneId = "the-droveway",       TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 2, Population = 30, MinLevel = 1, MaxLevel = 4 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "weathered-waypost",    DisplayName = "The Weathered Waypost",
                ZoneId = "the-droveway",       TypeKey = "locations",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 2, Population = 5, MinLevel = 1, MaxLevel = 4 },
                Traits = new() { SafeRespawn = true, IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "sunken-fields",        DisplayName = "The Sunken Fields",
                ZoneId = "the-droveway",       TypeKey = "environments",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 3, MinLevel = 2, MaxLevel = 4 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 5 },
            },

            // ashlen-wood (Wilderness, L1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "wood-road",            DisplayName = "The Wood Road",
                ZoneId = "ashlen-wood",        TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 2, Population = 40, MinLevel = 1, MaxLevel = 5 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "old-clearing",         DisplayName = "The Old Clearing",
                ZoneId = "ashlen-wood",        TypeKey = "environments",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 3, Population = 15, MinLevel = 2, MaxLevel = 5 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "strangled-oak",        DisplayName = "The Strangled Oak",
                ZoneId = "ashlen-wood",        TypeKey = "locations",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 4, MinLevel = 2, MaxLevel = 5 },
                Traits = new() { SafeRespawn = true, IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 5 },
            },

            // grevenmire (Wilderness, L3) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "mire-crossing",        DisplayName = "The Mire Crossing",
                ZoneId = "grevenmire",         TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 4, Population = 20, MinLevel = 3, MaxLevel = 6 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "sunken-estate",        DisplayName = "The Sunken Estate",
                ZoneId = "grevenmire",         TypeKey = "locations",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 5, Population = 10, MinLevel = 3, MaxLevel = 6 },
                Traits = new() { SafeRespawn = true, IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "bone-hollow",          DisplayName = "The Bone Hollow",
                ZoneId = "grevenmire",         TypeKey = "environments",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 6, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 8 },
            },

            // the-halrow (Dungeon, L4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "halrow-threshold",     DisplayName = "The Halrow Threshold",
                ZoneId = "the-halrow",         TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 3, Population = 5, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { SafeRespawn = true, IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "rootbound-cellars",    DisplayName = "The Rootbound Cellars",
                ZoneId = "the-halrow",         TypeKey = "dungeons",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 4, Population = 30, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "collapsed-vault",      DisplayName = "The Collapsed Vault",
                ZoneId = "the-halrow",         TypeKey = "dungeons",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 5, Population = 15, MinLevel = 5, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest", UnlockKey = "collapsed-vault" },
            },

            // drowning-pits (Dungeon, L4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "pit-descent",          DisplayName = "The Pit Descent",
                ZoneId = "drowning-pits",      TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 4, Population = 5, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { SafeRespawn = true, IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "flooded-workings",     DisplayName = "The Flooded Workings",
                ZoneId = "drowning-pits",      TypeKey = "dungeons",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 5, Population = 30, MinLevel = 4, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "deepest-chamber",      DisplayName = "The Deepest Chamber",
                ZoneId = "drowning-pits",      TypeKey = "dungeons",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 6, Population = 10, MinLevel = 5, MaxLevel = 6 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest", UnlockKey = "deepest-chamber" },
            },

            // â”€â”€ Greymoor region â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // aldenmere (Town, L5) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "ironhollow-keep",      DisplayName = "Ironhollow Keep",
                ZoneId = "aldenmere",          TypeKey = "locations",
                RarityWeight = 60, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 4, Population = 80, MinLevel = 5, MaxLevel = 12 },
                Traits = new() { SafeRespawn = true, IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "aldenmere-marketplace", DisplayName = "Aldenmere Marketplace",
                ZoneId = "aldenmere",          TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 2, Population = 60, MinLevel = 5 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "grey-cup",             DisplayName = "The Grey Cup",
                ZoneId = "aldenmere",          TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 1, Population = 25, MinLevel = 5 },
                Traits = new() { IsTown = true },
            },

            // pale-moor (Wilderness, L7) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "ashveil-highlands",    DisplayName = "Ashveil Highlands",
                ZoneId = "pale-moor",          TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 7, DangerLevel = 5, Population = 60, MinLevel = 7, MaxLevel = 12 },
                Traits = new() { IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "moorstone-cairns",     DisplayName = "The Moorstone Cairns",
                ZoneId = "pale-moor",          TypeKey = "locations",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 4, Population = 10, MinLevel = 7, MaxLevel = 12 },
                Traits = new() { SafeRespawn = true, IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "shifting-waymark",     DisplayName = "The Shifting Waymark",
                ZoneId = "pale-moor",          TypeKey = "environments",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 6, MinLevel = 8, MaxLevel = 14 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 10 },
            },

            // soddenfen (Wilderness, L9) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "fenland-crossing",     DisplayName = "Fenland Crossing",
                ZoneId = "soddenfen",          TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 5, Population = 30, MinLevel = 9, MaxLevel = 13 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "submerged-ruins",      DisplayName = "The Submerged Ruins",
                ZoneId = "soddenfen",          TypeKey = "dungeons",
                RarityWeight = 50, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 7, Population = 40, MinLevel = 10, MaxLevel = 14 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "skill_check_active", DiscoverThreshold = 12 },
            },

            // barrow-deeps (Dungeon, L11) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "deeps-entrance",       DisplayName = "Deeps Entrance",
                ZoneId = "barrow-deeps",       TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 6, Population = 5, MinLevel = 11, MaxLevel = 14 },
                Traits = new() { SafeRespawn = true, IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "ancestor-vault",       DisplayName = "The Ancestor Vault",
                ZoneId = "barrow-deeps",       TypeKey = "dungeons",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 7, Population = 50, MinLevel = 11, MaxLevel = 14 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "relic-chamber",        DisplayName = "The Relic Chamber",
                ZoneId = "barrow-deeps",       TypeKey = "dungeons",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 8, Population = 20, MinLevel = 12, MaxLevel = 14 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest", UnlockKey = "relic-chamber" },
            },

            // â”€â”€ Saltcliff region â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // tolvaren (Town, L10) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "tolvaren-harbour",     DisplayName = "Tolvaren Harbour",
                ZoneId = "tolvaren",           TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 2, Population = 100, MinLevel = 10 },
                Traits = new() { SafeRespawn = true, IsTown = true },
            },
            new ZoneLocation
            {
                Slug = "cliff-road-market",    DisplayName = "Cliff Road Market",
                ZoneId = "tolvaren",           TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 2, Population = 60, MinLevel = 10 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "saltcrow-inn",         DisplayName = "The Saltcrow Inn",
                ZoneId = "tolvaren",           TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 1, Population = 20, MinLevel = 10 },
                Traits = new() { IsTown = true },
            },

            // tidewrack-flats (Wilderness, L12) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "wrack-shore",          DisplayName = "Wrack Shore",
                ZoneId = "tidewrack-flats",    TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 5, Population = 40, MinLevel = 12, MaxLevel = 18 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "bone-strand",          DisplayName = "The Bone Strand",
                ZoneId = "tidewrack-flats",    TypeKey = "environments",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 6, Population = 35, MinLevel = 13, MaxLevel = 18 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "tidal-grotto",         DisplayName = "The Tidal Grotto",
                ZoneId = "tidewrack-flats",    TypeKey = "dungeons",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 7, Population = 25, MinLevel = 14, MaxLevel = 18 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "skill_check_active", DiscoverThreshold = 14 },
            },

            // saltcliff-heights (Wilderness, L14) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "clifftop-ruins",       DisplayName = "The Clifftop Ruins",
                ZoneId = "saltcliff-heights",  TypeKey = "locations",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 6, Population = 20, MinLevel = 14, MaxLevel = 20 },
                Traits = new() { SafeRespawn = true, IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "gull-rider-camp",      DisplayName = "Gull-Rider Camp",
                ZoneId = "saltcliff-heights",  TypeKey = "environments",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 7, Population = 30, MinLevel = 15, MaxLevel = 20 },
                Traits = new() { IsDiscoverable = true },
            },
            new ZoneLocation
            {
                Slug = "storm-watch-peak",     DisplayName = "Storm-Watch Peak",
                ZoneId = "saltcliff-heights",  TypeKey = "locations",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 8, MinLevel = 16, MaxLevel = 20 },
                Traits = new() { IsHidden = true, UnlockType = "achievement", UnlockKey = "storm-watch-peak" },
            },

            // sunken-name (Dungeon, L16) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "drowned-threshold",    DisplayName = "The Drowned Threshold",
                ZoneId = "sunken-name",        TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 7, Population = 5, MinLevel = 16, MaxLevel = 20 },
                Traits = new() { SafeRespawn = true, IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "flooded-throne-room",  DisplayName = "The Flooded Throne Room",
                ZoneId = "sunken-name",        TypeKey = "dungeons",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 9, Population = 60, MinLevel = 16, MaxLevel = 20 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "tidelocked-vault",     DisplayName = "The Tidelocked Vault",
                ZoneId = "sunken-name",        TypeKey = "dungeons",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 10, Population = 20, MinLevel = 17, MaxLevel = 20 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest", UnlockKey = "tidelocked-vault" },
            },

            // â”€â”€ Cinderplain region â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // skarhold (Town, L18) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "forge-quarter",        DisplayName = "The Forge Quarter",
                ZoneId = "skarhold",           TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 3, Population = 80, MinLevel = 18 },
                Traits = new() { SafeRespawn = true, IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "caldera-market",       DisplayName = "Caldera Market",
                ZoneId = "skarhold",           TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 2, Population = 60, MinLevel = 18 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            new ZoneLocation
            {
                Slug = "ashbrand-lodge",       DisplayName = "The Ashbrand Lodge",
                ZoneId = "skarhold",           TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 30, MinLevel = 18 },
                Traits = new() { IsTown = true },
            },

            // ashfields (Wilderness, L20) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "obsidian-grove",       DisplayName = "The Obsidian Grove",
                ZoneId = "ashfields",          TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 8, Population = 50, MinLevel = 20, MaxLevel = 25 },
                // ashfields' respawn point, moved here from scorched-battlefield by L3 (t_6c70dc04). At
                // (30,20) this is the node nearest the zone's arrival point (32,24), so it is the one a
                // dead character walks out of (MapGraphSeeder.cs:342).
                Traits = new() { SafeRespawn = true, IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "scorched-battlefield", DisplayName = "The Scorched Battlefield",
                ZoneId = "ashfields",          TypeKey = "locations",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 8, DangerLevel = 9, Population = 70, MinLevel = 21, MaxLevel = 25 },
                // Hostile since L3: the respawn flag moved to obsidian-grove and the fight stayed here.
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "ash-shrine",           DisplayName = "The Ash Shrine",
                ZoneId = "ashfields",          TypeKey = "locations",
                RarityWeight = 25, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 7, MinLevel = 20, MaxLevel = 25 },
                // Keeper-only shrine: a rest point, not a spawn (L3, t_6c70dc04), hence SafeRespawn and an
                // empty pool. IsHidden keeps it out of the visible zone query, so it can never be picked as
                // the zone's respawn anchor over the grove.
                Traits = new() { SafeRespawn = true, IsHidden = true, UnlockType = "quest", UnlockKey = "ash-shrine" },
            },

            // smoldering-reach (Wilderness, L23) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "vent-fields",          DisplayName = "The Vent Fields",
                ZoneId = "smoldering-reach",   TypeKey = "environments",
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 7, DangerLevel = 9, Population = 45, MinLevel = 23, MaxLevel = 26 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "pyreling-den",         DisplayName = "The Pyreling Den",
                ZoneId = "smoldering-reach",   TypeKey = "environments",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 10, Population = 35, MinLevel = 24, MaxLevel = 26 },
                Traits = new() { IsIndoor = false },
            },
            new ZoneLocation
            {
                Slug = "lava-bridge",          DisplayName = "The Lava Bridge",
                ZoneId = "smoldering-reach",   TypeKey = "locations",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 1, DangerLevel = 10, MinLevel = 24, MaxLevel = 26 },
                Traits = new() { SafeRespawn = true, IsHidden = true, UnlockType = "skill_check_active", DiscoverThreshold = 18 },
            },

            // kaldrek-maw (Dungeon, L26) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            new ZoneLocation
            {
                Slug = "maw-descent",          DisplayName = "The Maw Descent",
                ZoneId = "kaldrek-maw",        TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 10, Population = 5, MinLevel = 26, MaxLevel = 30 },
                Traits = new() { SafeRespawn = true, IsDungeon = false },
            },
            new ZoneLocation
            {
                Slug = "fire-ancients-chamber", DisplayName = "The Fire-Ancient's Chamber",
                ZoneId = "kaldrek-maw",        TypeKey = "dungeons",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 8, DangerLevel = 10, Population = 30, MinLevel = 26, MaxLevel = 30 },
                Traits = new() { IsIndoor = true, IsDungeon = true },
            },
            new ZoneLocation
            {
                Slug = "kaldreks-heart",       DisplayName = "The Maw's Heart",
                ZoneId = "kaldrek-maw",        TypeKey = "dungeons",
                RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 10, Population = 10, MinLevel = 28, MaxLevel = 30 },
                Traits = new() { IsIndoor = true, IsDungeon = true, IsHidden = true, UnlockType = "quest", UnlockKey = "kaldreks-heart" },
            },

            // ── Dev playground (IsDevOnly zone) ────────────────────────────────
            // One representative location per mechanic. Never returned by public APIs.

            // Town — hub / orientation
            new ZoneLocation
            {
                Slug = "playground-square",    DisplayName = "Dev Square",
                ZoneId = "playground",         TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 0, Population = 10, MinLevel = 0 },
                // The playground's respawn point: without one, a death there falls back to whatever location the
                // repository returns first and logs an error. It is also where the death e2e expects to land.
                Traits = new() { IsTown = true, SafeRespawn = true },
            },
            // Town — inn (rest / health+mana restore)
            new ZoneLocation
            {
                Slug = "playground-tavern",    DisplayName = "Dev Tavern",
                ZoneId = "playground",         TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 0, Population = 8, MinLevel = 0 },
                Traits = new() { IsTown = true, IsIndoor = true },
            },
            // Town — merchant (buy / sell)
            new ZoneLocation
            {
                Slug = "playground-shop",      DisplayName = "Dev Shop",
                ZoneId = "playground",         TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 0, Population = 5, MinLevel = 0 },
                Traits = new() { IsTown = true, HasMerchant = true },
            },
            // Crafting — forge (craft / upgrade items)
            new ZoneLocation
            {
                Slug = "playground-forge",     DisplayName = "Dev Forge",
                ZoneId = "playground",         TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 0, Population = 4, MinLevel = 0 },
                Traits = new() { IsTown = true, IsIndoor = true },
            },
            // Wilderness — combat encounters
            new ZoneLocation
            {
                Slug = "playground-clearing",  DisplayName = "Dev Clearing",
                ZoneId = "playground",         TypeKey = "environments",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 6, DangerLevel = 5, Population = 20, MinLevel = 0, MaxLevel = 30 },
                Traits = new() { IsIndoor = false },
            },
            // Wilderness — harvest nodes / resource gathering
            new ZoneLocation
            {
                Slug = "playground-grove",     DisplayName = "Dev Grove",
                ZoneId = "playground",         TypeKey = "environments",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 4, DangerLevel = 3, Population = 10, MinLevel = 0, MaxLevel = 30 },
                Traits = new() { IsIndoor = false },
            },
            // Dungeon — dungeon enter / boss encounter
            new ZoneLocation
            {
                Slug = "playground-dungeon-gate", DisplayName = "Dev Dungeon Gate",
                ZoneId = "playground",            TypeKey = "dungeons",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 5, DangerLevel = 7, Population = 15, MinLevel = 0, MaxLevel = 30 },
                Traits = new() { IsDungeon = true },
            },
            // NPC dialogue
            new ZoneLocation
            {
                Slug = "playground-npc-hall",  DisplayName = "Dev NPC Hall",
                ZoneId = "playground",         TypeKey = "locations",
                RarityWeight = 100, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 3, DangerLevel = 0, Population = 15, MinLevel = 0 },
                Traits = new() { IsTown = true, IsIndoor = true },
            },
            // Hidden — passive skill check (DC 5)
            new ZoneLocation
            {
                Slug = "playground-hidden-skill", DisplayName = "Hidden: Passive Skill Check",
                ZoneId = "playground",            TypeKey = "locations",
                RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 2, Population = 0, MinLevel = 0 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 5 },
            },
            // Hidden — active skill check (DC 8)
            new ZoneLocation
            {
                Slug = "playground-hidden-active", DisplayName = "Hidden: Active Skill Check",
                ZoneId = "playground",             TypeKey = "locations",
                RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 2, Population = 0, MinLevel = 0 },
                Traits = new() { IsHidden = true, UnlockType = "skill_check_active", UnlockKey = "perception", DiscoverThreshold = 8 },
            },
            // Hidden — quest-gated (placeholder quest slug "dev-quest")
            new ZoneLocation
            {
                Slug = "playground-hidden-quest", DisplayName = "Hidden: Quest Gate",
                ZoneId = "playground",            TypeKey = "locations",
                RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 0, MinLevel = 0 },
                Traits = new() { IsHidden = true, UnlockType = "quest", UnlockKey = "dev-quest" },
            },
            // Hidden — item-gated (placeholder item slug "skeleton-key")
            new ZoneLocation
            {
                Slug = "playground-hidden-item",  DisplayName = "Hidden: Item Check",
                ZoneId = "playground",            TypeKey = "locations",
                RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 0, MinLevel = 0 },
                Traits = new() { IsHidden = true, UnlockType = "item", UnlockKey = "skeleton-key" },
            },
            // Hidden — achievement-gated (placeholder achievement slug "dev-achieve")
            new ZoneLocation
            {
                Slug = "playground-hidden-achievement", DisplayName = "Hidden: Achievement",
                ZoneId = "playground",                  TypeKey = "locations",
                RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now,
                Stats  = new() { Size = 2, DangerLevel = 1, Population = 0, MinLevel = 0 },
                Traits = new() { IsHidden = true, UnlockType = "achievement", UnlockKey = "dev-achieve" },
            },
    ];

}
