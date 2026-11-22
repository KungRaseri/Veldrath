using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="ActorArchetype"/> rows (enemies and NPCs) into <see cref="ContentDbContext"/>.</summary>
/// <remarks>
/// <para>
/// The roster is banded by level so that every zone in the world has an enemy that belongs there: the geography
/// runs from level 1 to 30, and a single level 1–3 goblin would leave everything above level 6 empty. A location's
/// spawn pool (<see cref="ZoneLocationsSeeder"/>) names these slugs, and the pool is only as good as the band it
/// points at.
/// </para>
/// <para>
/// Two of these archetypes are NPCs (<see cref="ActorArchetype.Traits"/>.<c>Hostile</c> is false). They are not
/// placed in spawn pools — a pool turns every entry into an enemy — and exist so trade, quest and dialogue content
/// has an actor to hang off.
/// </para>
/// </remarks>
public static class ArchetypeSeeder
{
    /// <summary>Seeds all archetype rows (idempotent, enemies first then NPCs).</summary>
    /// <param name="db">Content database context.</param>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedEnemiesAsync(db);
        await SeedNpcsAsync(db);
    }

    /// <summary>
    /// The loot table each hostile archetype drops, by loot slug. Kept apart from the archetypes because the table
    /// ids are database-generated: the seeder resolves them once the loot tables exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two kinds of slug appear here. Four tables are seeded (<see cref="LootTablesSeeder"/>): the two common enemy
    /// tables and one per boss, so this map is what gives a boss its table on a fresh catalogue — the seed guard
    /// (<c>SeededEncounterCoverageTests</c>) refuses a boss without one.
    /// </para>
    /// <para>
    /// The other thirty are authored in <c>content/loottables/</c> and imported after the catalogue is seeded. A slug
    /// the catalogue does not have yet is skipped, so the row keeps a null <c>LootTableId</c> — this seeder is
    /// insert-only and never revisits a seeded row. The archetype pack (<c>content/archetypes/</c>) is what binds them
    /// on an existing catalogue: it names the same slug in <c>lootTableId</c> and the importer resolves it. Import the
    /// loot tables first, then the archetypes (<c>docs/content-packs.md</c>).
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> LootTableByArchetype = new(StringComparer.Ordinal)
    {
        ["giant-rat"] = "giant-rat-drops",
        ["goblin-scout"] = "goblin-common-drops",
        ["drove-cur"] = "drove-cur-drops",
        ["peat-adder"] = "peat-adder-drops",
        ["forest-wolf"] = "wolf-common-drops",
        ["bandit-ruffian"] = "bandit-ruffian-drops",
        ["sump-crawler"] = "sump-crawler-drops",
        ["mire-boar"] = "mire-boar-drops",
        ["bandit-captain"] = "bandit-boss-drops",
        ["moor-wight"] = "moor-wight-drops",
        ["barrow-bear"] = "barrow-bear-drops",
        ["moor-tollman"] = "moor-tollman-drops",
        ["cairn-haunt"] = "cairn-haunt-drops",
        ["fen-lurker"] = "fen-lurker-drops",
        ["burrow-worm"] = "burrow-worm-drops",
        ["bog-lantern"] = "bog-lantern-drops",
        ["drowned-revenant"] = "drowned-revenant-drops",
        ["tide-strider"] = "tide-strider-drops",
        ["gull-rider-skirmisher"] = "gull-rider-skirmisher-drops",
        ["gull-rider-brute"] = "gull-rider-brute-drops",
        ["storm-harrower"] = "storm-harrower-drops",
        ["cliff-breaker"] = "cliff-breaker-drops",
        ["undertow"] = "undertow-drops",
        ["cinderling"] = "cinderling-drops",
        ["drowned-cantor"] = "drowned-cantor-drops",
        ["ash-jack"] = "ash-jack-drops",
        ["forge-deserter"] = "forge-deserter-drops",
        ["pyreling"] = "pyreling-drops",
        ["vent-stalker"] = "vent-stalker-drops",
        ["crust-crawler"] = "crust-crawler-drops",
        ["ash-picker"] = "ash-picker-drops",
        ["ash-eater"] = "ash-eater-drops",
        ["maw-serpent"] = "maw-serpent-drops",
        ["fire-ancient"] = "fire-ancient-drops",
    };

    private static async Task SeedEnemiesAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ActorArchetypes.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllEnemyArchetypes(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;

        await AttachLootTablesAsync(db, missing);
        db.ActorArchetypes.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task AttachLootTablesAsync(ContentDbContext db, IEnumerable<ActorArchetype> archetypes)
    {
        var wanted = archetypes.Select(archetype => archetype.Slug).Where(LootTableByArchetype.ContainsKey).ToList();
        if (wanted.Count == 0) return;

        var idsBySlug = await db.LootTables
            .AsNoTracking()
            .Select(table => new { table.Slug, table.Id })
            .ToDictionaryAsync(table => table.Slug, table => table.Id);

        foreach (var archetype in archetypes)
        {
            if (!LootTableByArchetype.TryGetValue(archetype.Slug, out var lootSlug)) continue;
            if (idsBySlug.TryGetValue(lootSlug, out var lootTableId)) archetype.LootTableId = lootTableId;
        }
    }

    // Enemies, in level bands. The band each archetype covers is the MinLevel/MaxLevel pair; a location's own
    // MinLevel/MaxLevel decides which of these can appear there.
    private static ActorArchetype[] GetAllEnemyArchetypes(DateTimeOffset now) =>
    [
            new ActorArchetype
            {
                Slug         = "giant-rat",
                TypeKey      = "beasts",
                DisplayName  = "Giant Rat",
                RarityWeight = 90,
                MinLevel     = 1,
                MaxLevel     = 3,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 20,
                    Mana             = null,
                    Strength         = 4,
                    Agility          = 10,
                    Intelligence     = 2,
                    Constitution     = 6,
                    ArmorClass       = 1,
                    AttackBonus      = 0,
                    Damage           = 3,
                    ExperienceReward = 8,
                    GoldRewardMin    = 1,
                    GoldRewardMax    = 3,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "goblin-scout",
                TypeKey      = "beasts",
                DisplayName  = "Goblin Scout",
                RarityWeight = 80,
                MinLevel     = 1,
                MaxLevel     = 3,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 30,
                    Mana             = null,
                    Strength         = 6,
                    Agility          = 12,
                    Intelligence     = 4,
                    Constitution     = 8,
                    ArmorClass       = 2,
                    AttackBonus      = 1,
                    Damage           = 4,
                    ExperienceReward = 15,
                    GoldRewardMin    = 1,
                    GoldRewardMax    = 5,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "drove-cur",
                TypeKey      = "beasts",
                DisplayName  = "Drove Cur",
                RarityWeight = 85,
                MinLevel     = 1,
                MaxLevel     = 3,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 26,
                    Mana             = null,
                    Strength         = 6,
                    Agility          = 12,
                    Intelligence     = 3,
                    Constitution     = 7,
                    ArmorClass       = 1,
                    AttackBonus      = 1,
                    Damage           = 3,
                    ExperienceReward = 12,
                    GoldRewardMin    = 1,
                    GoldRewardMax    = 4,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "peat-adder",
                TypeKey      = "beasts",
                DisplayName  = "Peat Adder",
                RarityWeight = 75,
                MinLevel     = 1,
                MaxLevel     = 3,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 18,
                    Mana             = null,
                    Strength         = 7,
                    Agility          = 10,
                    Intelligence     = 2,
                    Constitution     = 5,
                    ArmorClass       = 2,
                    AttackBonus      = 2,
                    Damage           = 5,
                    ExperienceReward = 14,
                    GoldRewardMin    = 1,
                    GoldRewardMax    = 4,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = true,
                },
            },
            new ActorArchetype
            {
                Slug         = "forest-wolf",
                TypeKey      = "beasts",
                DisplayName  = "Forest Wolf",
                RarityWeight = 75,
                MinLevel     = 3,
                MaxLevel     = 6,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 45,
                    Mana             = null,
                    Strength         = 8,
                    Agility          = 14,
                    Intelligence     = 3,
                    Constitution     = 9,
                    ArmorClass       = 3,
                    AttackBonus      = 2,
                    Damage           = 6,
                    ExperienceReward = 28,
                    GoldRewardMin    = 2,
                    GoldRewardMax    = 8,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "bandit-ruffian",
                TypeKey      = "humanoids",
                DisplayName  = "Bandit Ruffian",
                RarityWeight = 70,
                MinLevel     = 3,
                MaxLevel     = 6,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 55,
                    Mana             = null,
                    Strength         = 10,
                    Agility          = 8,
                    Intelligence     = 6,
                    Constitution     = 10,
                    ArmorClass       = 4,
                    AttackBonus      = 3,
                    Damage           = 7,
                    ExperienceReward = 40,
                    GoldRewardMin    = 5,
                    GoldRewardMax    = 15,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "sump-crawler",
                TypeKey      = "beasts",
                DisplayName  = "Sump Crawler",
                RarityWeight = 65,
                MinLevel     = 4,
                MaxLevel     = 6,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 65,
                    Mana             = null,
                    Strength         = 12,
                    Agility          = 9,
                    Intelligence     = 3,
                    Constitution     = 11,
                    ArmorClass       = 4,
                    AttackBonus      = 3,
                    Damage           = 8,
                    ExperienceReward = 34,
                    GoldRewardMin    = 5,
                    GoldRewardMax    = 12,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "mire-boar",
                TypeKey      = "beasts",
                DisplayName  = "Mire Boar",
                RarityWeight = 55,
                MinLevel     = 4,
                MaxLevel     = 6,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 95,
                    Mana             = null,
                    Strength         = 14,
                    Agility          = 7,
                    Intelligence     = 2,
                    Constitution     = 13,
                    ArmorClass       = 5,
                    AttackBonus      = 3,
                    Damage           = 12,
                    ExperienceReward = 55,
                    GoldRewardMin    = 6,
                    GoldRewardMax    = 14,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "bandit-captain",
                TypeKey      = "humanoids",
                DisplayName  = "Bandit Captain",
                RarityWeight = 25,
                MinLevel     = 6,
                MaxLevel     = 12,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 220,
                    Mana             = null,
                    Strength         = 15,
                    Agility          = 12,
                    Intelligence     = 10,
                    Constitution     = 14,
                    ArmorClass       = 8,
                    AttackBonus      = 6,
                    Damage           = 14,
                    ExperienceReward = 180,
                    GoldRewardMin    = 40,
                    GoldRewardMax    = 80,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = true,
                    Elite        = true,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "moor-wight",
                TypeKey      = "undead",
                DisplayName  = "Moor Wight",
                RarityWeight = 60,
                MinLevel     = 6,
                MaxLevel     = 10,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 90,
                    Mana             = 30,
                    Strength         = 12,
                    Agility          = 8,
                    Intelligence     = 12,
                    Constitution     = 12,
                    ArmorClass       = 5,
                    AttackBonus      = 4,
                    Damage           = 10,
                    ExperienceReward = 70,
                    GoldRewardMin    = 8,
                    GoldRewardMax    = 20,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = true,
                    FireImmune   = false,
                    ColdImmune   = true,
                    PoisonImmune = true,
                },
            },
            new ActorArchetype
            {
                Slug         = "barrow-bear",
                TypeKey      = "beasts",
                DisplayName  = "Barrow Bear",
                RarityWeight = 50,
                MinLevel     = 7,
                MaxLevel     = 9,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 150,
                    Mana             = null,
                    Strength         = 17,
                    Agility          = 7,
                    Intelligence     = 3,
                    Constitution     = 16,
                    ArmorClass       = 6,
                    AttackBonus      = 5,
                    Damage           = 16,
                    ExperienceReward = 110,
                    GoldRewardMin    = 12,
                    GoldRewardMax    = 28,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "moor-tollman",
                TypeKey      = "humanoids",
                DisplayName  = "Moor Tollman",
                RarityWeight = 60,
                MinLevel     = 7,
                MaxLevel     = 9,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 100,
                    Mana             = null,
                    Strength         = 11,
                    Agility          = 11,
                    Intelligence     = 8,
                    Constitution     = 11,
                    ArmorClass       = 5,
                    AttackBonus      = 6,
                    Damage           = 12,
                    ExperienceReward = 80,
                    GoldRewardMin    = 10,
                    GoldRewardMax    = 25,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = true,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "cairn-haunt",
                TypeKey      = "undead",
                DisplayName  = "Cairn Haunt",
                RarityWeight = 45,
                MinLevel     = 8,
                MaxLevel     = 14,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 110,
                    Mana             = 45,
                    Strength         = 14,
                    Agility          = 10,
                    Intelligence     = 14,
                    Constitution     = 12,
                    ArmorClass       = 6,
                    AttackBonus      = 5,
                    Damage           = 12,
                    ExperienceReward = 95,
                    GoldRewardMin    = 10,
                    GoldRewardMax    = 24,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = true,
                    FireImmune   = false,
                    ColdImmune   = true,
                    PoisonImmune = true,
                },
            },
            new ActorArchetype
            {
                Slug         = "fen-lurker",
                TypeKey      = "beasts",
                DisplayName  = "Fen Lurker",
                RarityWeight = 55,
                MinLevel     = 9,
                MaxLevel     = 14,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 140,
                    Mana             = null,
                    Strength         = 18,
                    Agility          = 12,
                    Intelligence     = 4,
                    Constitution     = 16,
                    ArmorClass       = 7,
                    AttackBonus      = 6,
                    Damage           = 15,
                    ExperienceReward = 120,
                    GoldRewardMin    = 12,
                    GoldRewardMax    = 30,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = true,
                },
            },
            new ActorArchetype
            {
                Slug         = "burrow-worm",
                TypeKey      = "beasts",
                DisplayName  = "Burrow Worm",
                RarityWeight = 45,
                MinLevel     = 10,
                MaxLevel     = 12,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 135,
                    Mana             = null,
                    Strength         = 16,
                    Agility          = 8,
                    Intelligence     = 2,
                    Constitution     = 15,
                    ArmorClass       = 6,
                    AttackBonus      = 6,
                    Damage           = 15,
                    ExperienceReward = 118,
                    GoldRewardMin    = 12,
                    GoldRewardMax    = 28,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "bog-lantern",
                TypeKey      = "spirits",
                DisplayName  = "Bog Lantern",
                RarityWeight = 40,
                MinLevel     = 10,
                MaxLevel     = 12,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 95,
                    Mana             = 40,
                    Strength         = 9,
                    Agility          = 12,
                    Intelligence     = 16,
                    Constitution     = 10,
                    ArmorClass       = 6,
                    AttackBonus      = 6,
                    Damage           = 11,
                    ExperienceReward = 100,
                    GoldRewardMin    = 10,
                    GoldRewardMax    = 24,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = true,
                    Caster       = true,
                    FireImmune   = false,
                    ColdImmune   = true,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "drowned-revenant",
                TypeKey      = "undead",
                DisplayName  = "Drowned Revenant",
                RarityWeight = 40,
                MinLevel     = 11,
                MaxLevel     = 18,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 170,
                    Mana             = 20,
                    Strength         = 20,
                    Agility          = 10,
                    Intelligence     = 10,
                    Constitution     = 18,
                    ArmorClass       = 9,
                    AttackBonus      = 8,
                    Damage           = 18,
                    ExperienceReward = 150,
                    GoldRewardMin    = 15,
                    GoldRewardMax    = 35,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = true,
                    PoisonImmune = true,
                },
            },
            new ActorArchetype
            {
                Slug         = "tide-strider",
                TypeKey      = "beasts",
                DisplayName  = "Tide Strider",
                RarityWeight = 45,
                MinLevel     = 13,
                MaxLevel     = 15,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 175,
                    Mana             = null,
                    Strength         = 19,
                    Agility          = 14,
                    Intelligence     = 4,
                    Constitution     = 16,
                    ArmorClass       = 8,
                    AttackBonus      = 8,
                    Damage           = 19,
                    ExperienceReward = 165,
                    GoldRewardMin    = 16,
                    GoldRewardMax    = 38,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "gull-rider-skirmisher",
                TypeKey      = "humanoids",
                DisplayName  = "Gull-Rider Skirmisher",
                RarityWeight = 50,
                MinLevel     = 13,
                MaxLevel     = 15,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 150,
                    Mana             = null,
                    Strength         = 17,
                    Agility          = 17,
                    Intelligence     = 9,
                    Constitution     = 14,
                    ArmorClass       = 8,
                    AttackBonus      = 8,
                    Damage           = 17,
                    ExperienceReward = 140,
                    GoldRewardMin    = 15,
                    GoldRewardMax    = 35,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = true,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "gull-rider-brute",
                TypeKey      = "humanoids",
                DisplayName  = "Gull-Rider Brute",
                RarityWeight = 40,
                MinLevel     = 13,
                MaxLevel     = 18,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 210,
                    Mana             = null,
                    Strength         = 22,
                    Agility          = 16,
                    Intelligence     = 8,
                    Constitution     = 18,
                    ArmorClass       = 10,
                    AttackBonus      = 9,
                    Damage           = 22,
                    ExperienceReward = 200,
                    GoldRewardMin    = 20,
                    GoldRewardMax    = 45,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = true,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "storm-harrower",
                TypeKey      = "spirits",
                DisplayName  = "Storm Harrower",
                RarityWeight = 30,
                MinLevel     = 15,
                MaxLevel     = 20,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 240,
                    Mana             = 80,
                    Strength         = 18,
                    Agility          = 18,
                    Intelligence     = 20,
                    Constitution     = 16,
                    ArmorClass       = 11,
                    AttackBonus      = 10,
                    Damage           = 25,
                    ExperienceReward = 240,
                    GoldRewardMin    = 25,
                    GoldRewardMax    = 55,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = true,
                    Ranged       = true,
                    Caster       = true,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "cliff-breaker",
                TypeKey      = "beasts",
                DisplayName  = "Cliff Breaker",
                RarityWeight = 35,
                MinLevel     = 16,
                MaxLevel     = 18,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 265,
                    Mana             = null,
                    Strength         = 21,
                    Agility          = 11,
                    Intelligence     = 4,
                    Constitution     = 19,
                    ArmorClass       = 9,
                    AttackBonus      = 9,
                    Damage           = 26,
                    ExperienceReward = 210,
                    GoldRewardMin    = 22,
                    GoldRewardMax    = 48,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = true,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "undertow",
                TypeKey      = "spirits",
                DisplayName  = "Undertow",
                RarityWeight = 30,
                MinLevel     = 16,
                MaxLevel     = 18,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 215,
                    Mana             = 60,
                    Strength         = 16,
                    Agility          = 14,
                    Intelligence     = 18,
                    Constitution     = 16,
                    ArmorClass       = 9,
                    AttackBonus      = 9,
                    Damage           = 22,
                    ExperienceReward = 175,
                    GoldRewardMin    = 20,
                    GoldRewardMax    = 44,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = true,
                    FireImmune   = false,
                    ColdImmune   = true,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "cinderling",
                TypeKey      = "elementals",
                DisplayName  = "Cinderling",
                RarityWeight = 35,
                MinLevel     = 20,
                MaxLevel     = 26,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 280,
                    Mana             = null,
                    Strength         = 24,
                    Agility          = 14,
                    Intelligence     = 12,
                    Constitution     = 20,
                    ArmorClass       = 12,
                    AttackBonus      = 11,
                    Damage           = 28,
                    ExperienceReward = 300,
                    GoldRewardMin    = 30,
                    GoldRewardMax    = 60,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "drowned-cantor",
                TypeKey      = "undead",
                DisplayName  = "Drowned Cantor",
                RarityWeight = 30,
                MinLevel     = 19,
                MaxLevel     = 20,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 265,
                    Mana             = 70,
                    Strength         = 18,
                    Agility          = 12,
                    Intelligence     = 20,
                    Constitution     = 18,
                    ArmorClass       = 10,
                    AttackBonus      = 10,
                    Damage           = 27,
                    ExperienceReward = 230,
                    GoldRewardMin    = 25,
                    GoldRewardMax    = 50,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = true,
                    FireImmune   = false,
                    ColdImmune   = true,
                    PoisonImmune = true,
                },
            },
            new ActorArchetype
            {
                Slug         = "ash-jack",
                TypeKey      = "beasts",
                DisplayName  = "Ash Jack",
                RarityWeight = 40,
                // 19-22 rather than the roster band's 19-20: scorched-battlefield starts at 21 and the pool
                // overlap guard needs MaxLevel to reach it (pool-matrix.md:57, SeededEncounterCoverageTests).
                MinLevel     = 19,
                MaxLevel     = 22,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 270,
                    Mana             = null,
                    Strength         = 20,
                    Agility          = 20,
                    Intelligence     = 9,
                    Constitution     = 18,
                    ArmorClass       = 10,
                    AttackBonus      = 10,
                    Damage           = 26,
                    ExperienceReward = 200,
                    GoldRewardMin    = 25,
                    GoldRewardMax    = 48,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = true,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "forge-deserter",
                TypeKey      = "humanoids",
                DisplayName  = "Forge Deserter",
                RarityWeight = 35,
                // Same widened range as ash-jack, and for the same reason: the only room that pools either
                // is scorched-battlefield (L21-25), so a 19-20 seed would place them nowhere.
                MinLevel     = 19,
                MaxLevel     = 22,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 315,
                    Mana             = null,
                    Strength         = 24,
                    Agility          = 10,
                    Intelligence     = 10,
                    Constitution     = 22,
                    ArmorClass       = 13,
                    AttackBonus      = 10,
                    Damage           = 30,
                    ExperienceReward = 260,
                    GoldRewardMin    = 30,
                    GoldRewardMax    = 58,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "pyreling",
                TypeKey      = "elementals",
                DisplayName  = "Pyreling",
                RarityWeight = 30,
                MinLevel     = 23,
                MaxLevel     = 28,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 320,
                    Mana             = 60,
                    Strength         = 26,
                    Agility          = 16,
                    Intelligence     = 14,
                    Constitution     = 22,
                    ArmorClass       = 13,
                    AttackBonus      = 12,
                    Damage           = 32,
                    ExperienceReward = 360,
                    GoldRewardMin    = 35,
                    GoldRewardMax    = 70,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = true,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "vent-stalker",
                TypeKey      = "beasts",
                DisplayName  = "Vent Stalker",
                RarityWeight = 35,
                MinLevel     = 23,
                MaxLevel     = 26,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 330,
                    Mana             = null,
                    Strength         = 27,
                    Agility          = 18,
                    Intelligence     = 5,
                    Constitution     = 22,
                    ArmorClass       = 12,
                    AttackBonus      = 12,
                    Damage           = 34,
                    ExperienceReward = 300,
                    GoldRewardMin    = 32,
                    GoldRewardMax    = 66,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = true,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "crust-crawler",
                TypeKey      = "beasts",
                DisplayName  = "Crust Crawler",
                RarityWeight = 30,
                MinLevel     = 23,
                MaxLevel     = 26,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 350,
                    Mana             = null,
                    Strength         = 28,
                    Agility          = 12,
                    Intelligence     = 3,
                    Constitution     = 24,
                    ArmorClass       = 14,
                    AttackBonus      = 11,
                    Damage           = 36,
                    ExperienceReward = 320,
                    GoldRewardMin    = 34,
                    GoldRewardMax    = 70,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "ash-picker",
                TypeKey      = "humanoids",
                DisplayName  = "Ash Picker",
                RarityWeight = 40,
                MinLevel     = 23,
                MaxLevel     = 26,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 310,
                    Mana             = null,
                    Strength         = 26,
                    Agility          = 15,
                    Intelligence     = 9,
                    Constitution     = 21,
                    ArmorClass       = 12,
                    AttackBonus      = 11,
                    Damage           = 32,
                    ExperienceReward = 280,
                    GoldRewardMin    = 30,
                    GoldRewardMax    = 62,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = false,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "ash-eater",
                TypeKey      = "beasts",
                DisplayName  = "Ash Eater",
                RarityWeight = 20,
                MinLevel     = 26,
                MaxLevel     = 30,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 380,
                    Mana             = null,
                    Strength         = 30,
                    Agility          = 10,
                    Intelligence     = 4,
                    Constitution     = 28,
                    ArmorClass       = 14,
                    AttackBonus      = 13,
                    Damage           = 42,
                    ExperienceReward = 375,
                    GoldRewardMin    = 40,
                    GoldRewardMax    = 75,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "maw-serpent",
                TypeKey      = "beasts",
                DisplayName  = "Maw Serpent",
                RarityWeight = 15,
                MinLevel     = 28,
                MaxLevel     = 30,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 430,
                    Mana             = null,
                    Strength         = 30,
                    Agility          = 14,
                    Intelligence     = 6,
                    Constitution     = 26,
                    ArmorClass       = 15,
                    AttackBonus      = 14,
                    Damage           = 46,
                    ExperienceReward = 450,
                    GoldRewardMin    = 45,
                    GoldRewardMax    = 88,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = true,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = false,
                    Elite        = false,
                    Ranged       = false,
                    Caster       = false,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "fire-ancient",
                TypeKey      = "elementals",
                DisplayName  = "Fire-Ancient",
                RarityWeight = 12,
                MinLevel     = 26,
                MaxLevel     = 30,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health           = 900,
                    Mana             = 200,
                    Strength         = 30,
                    Agility          = 12,
                    Intelligence     = 22,
                    Constitution     = 26,
                    ArmorClass       = 16,
                    AttackBonus      = 16,
                    Damage           = 42,
                    ExperienceReward = 1200,
                    GoldRewardMin    = 150,
                    GoldRewardMax    = 300,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile      = true,
                    Aggressive   = false,
                    PackHunter   = false,
                    Shopkeeper   = false,
                    QuestGiver   = false,
                    HasDialogue  = false,
                    Immortal     = false,
                    Wanderer     = false,
                    Boss         = true,
                    Elite        = true,
                    Ranged       = false,
                    Caster       = true,
                    FireImmune   = true,
                    ColdImmune   = false,
                    PoisonImmune = false,
                },
            },
    ];

    // NPCs. Not placed in any spawn pool: a pool entry becomes an enemy, so an NPC in a pool is an NPC you can
    // attack. They exist for trade, quest and dialogue content.
    private static async Task SeedNpcsAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ActorArchetypes.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllNpcArchetypes(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.ActorArchetypes.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static ActorArchetype[] GetAllNpcArchetypes(DateTimeOffset now) =>
    [
            new ActorArchetype
            {
                Slug         = "village-blacksmith",
                TypeKey      = "townspeople",
                DisplayName  = "Village Blacksmith",
                RarityWeight = 60,
                MinLevel     = 1,
                MaxLevel     = 1,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health     = 80,
                    TradeSkill = 70,
                    // The L0 band (game-context/budget.yaml merchants.gold_by_town_level: [100, 250]) tops out at 250,
                    // and the shop reads this as the purse a single buyback may draw on.
                    TradeGold  = 250,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile     = false,
                    Aggressive  = false,
                    PackHunter  = false,
                    Shopkeeper  = true,
                    QuestGiver  = false,
                    HasDialogue = true,
                    Immortal    = true,
                    Wanderer    = false,
                    Boss        = false,
                    Elite       = false,
                    Ranged      = false,
                    Caster      = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "tavern-keeper",
                TypeKey      = "townspeople",
                DisplayName  = "Tavern Keeper",
                RarityWeight = 65,
                MinLevel     = 1,
                MaxLevel     = 1,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health     = 60,
                    TradeSkill = 50,
                    TradeGold  = 200,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile     = false,
                    Aggressive  = false,
                    PackHunter  = false,
                    Shopkeeper  = true,
                    QuestGiver  = true,
                    HasDialogue = true,
                    Immortal    = true,
                    Wanderer    = false,
                    Boss        = false,
                    Elite       = false,
                    Ranged      = false,
                    Caster      = false,
                },
            },
            new ActorArchetype
            {
                Slug         = "wayfarer-elder",
                TypeKey      = "townspeople",
                DisplayName  = "Wayfarer Elder",
                RarityWeight = 55,
                MinLevel     = 1,
                MaxLevel     = 1,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArchetypeStats
                {
                    Health     = 50,
                    TradeSkill = 0,
                    TradeGold  = 0,
                },
                Traits = new ArchetypeTraits
                {
                    Hostile     = false,
                    Aggressive  = false,
                    PackHunter  = false,
                    Shopkeeper  = false,
                    QuestGiver  = true,
                    HasDialogue = true,
                    Immortal    = true,
                    Wanderer    = false,
                    Boss        = false,
                    Elite       = false,
                    Ranged      = false,
                    Caster      = false,
                },
            },

            // D3's town rosters (docs/design/content/towns.md §4.2–4.4). Every placement in LocationNpcsSeeder needs
            // its archetype here, because the shop is a person: ShopMerchantResolver only trades when the archetype
            // standing at the character's location carries Traits.Shopkeeper. Each purse is the band of the town that
            // person stands in — game-context/budget.yaml merchants.gold_by_town_level — and
            // MerchantBandTests.Every_Seeded_Shopkeeper_Purse_Is_Inside_Its_Towns_Band fails the build when one leaves
            // it, so these numbers are chosen inside their town's range, never beside it.

            // crestfall (L0) — purse band [100, 250].
            Townsperson(now, "road-carter", "Korrern", health: 50, tradeSkill: 30, tradeGold: 150),

            // aldenmere (L5) — purse band [250, 650].
            Townsperson(now, "keep-castellan", "Kaenvren", health: 95, tradeSkill: 55, tradeGold: 300),
            Townsperson(now, "mead-seller", "Raekmeld", health: 55, tradeSkill: 45, tradeGold: 400, rarityWeight: 70),
            Townsperson(now, "cellar-keeper", "Dornrek", health: 60, tradeSkill: 40, tradeGold: 500),

            // tolvaren (L10) — purse band [650, 1600]. The harbour-master takes no coin at the roster's own telling
            // ("teaches the tide line ... refuses berth"), so he is the one townsperson here who is not a shopkeeper.
            Townsperson(now, "harbour-master", "Gaelrek", health: 70, tradeSkill: 0, tradeGold: 0, trades: false),
            Townsperson(now, "ship-chandler", "Laeldern", health: 55, tradeSkill: 65, tradeGold: 900, rarityWeight: 70),
            Townsperson(now, "port-innkeeper", "Thaeldren", health: 60, tradeSkill: 50, tradeGold: 800),

            // skarhold (L18) — purse band [1300, 3200].
            Townsperson(now, "forge-master", "Daeldvaen", health: 100, tradeSkill: 90, tradeGold: 1600),
            Townsperson(now, "ash-glass-seller", "Raekdaeld", health: 60, tradeSkill: 75, tradeGold: 2400, rarityWeight: 70),
            Townsperson(now, "lodge-keeper", "Kaendaeld", health: 55, tradeSkill: 45, tradeGold: 1800),
    ];

    /// <summary>
    /// Builds one placed townsperson: the shared shape of every D3 roster row.
    /// </summary>
    /// <remarks>
    /// The traits are what make a person meetable rather than fightable, so they are stated once here instead of ten
    /// times in the list: non-hostile, immortal, no pack behaviour, no boss/elite tier, and <c>HasDialogue</c> so the
    /// towns.md dialogue register resolves the slug. <c>trades</c> is the row's own <c>Traits.Shopkeeper</c> — false
    /// only for someone the roster says takes no coin, and only such a person may carry a zero purse.
    /// </remarks>
    /// <param name="now">Timestamp stamped on the row, so a seed run is one consistent moment.</param>
    /// <param name="slug">Archetype slug — also the dialogue speaker key and the placement's target.</param>
    /// <param name="displayName">The person's name; each new archetype is exactly one person.</param>
    /// <param name="health">Authored health, flavour only: the person is immortal.</param>
    /// <param name="tradeSkill">Authored trade skill, read when the archetype is a shopkeeper.</param>
    /// <param name="tradeGold">Purse a single buyback may draw on; must sit inside the town's band.</param>
    /// <param name="trades">True when the roster has this person sell or teach for coin.</param>
    /// <param name="rarityWeight">Spawn weight, kept inside the documented 1–100 range; townspeople are never pooled.</param>
    /// <returns>The archetype row to seed.</returns>
    private static ActorArchetype Townsperson(
        DateTimeOffset now,
        string slug,
        string displayName,
        int health,
        int tradeSkill,
        int tradeGold,
        bool trades = true,
        int rarityWeight = 60) =>
        new()
        {
            Slug = slug,
            TypeKey = "townspeople",
            DisplayName = displayName,
            RarityWeight = rarityWeight,
            MinLevel = 1,
            MaxLevel = 1,
            IsActive = true,
            Version = 1,
            UpdatedAt = now,
            Stats = new ArchetypeStats
            {
                Health = health,
                TradeSkill = tradeSkill,
                TradeGold = tradeGold,
            },
            Traits = new ArchetypeTraits
            {
                Hostile = false,
                Aggressive = false,
                PackHunter = false,
                Shopkeeper = trades,
                QuestGiver = false,
                HasDialogue = true,
                Immortal = true,
                Wanderer = false,
                Boss = false,
                Elite = false,
                Ranged = false,
                Caster = false,
            },
        };
}
