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
    /// The loot table each archetype drops, by loot slug. Kept apart from the archetypes because the table ids are
    /// database-generated: the seeder resolves them once the loot tables exist.
    /// </summary>
    private static readonly Dictionary<string, string> LootTableByArchetype = new(StringComparer.Ordinal)
    {
        ["goblin-scout"] = "goblin-common-drops",
        ["forest-wolf"] = "wolf-common-drops",
        ["bandit-captain"] = "bandit-boss-drops",
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
            }
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
