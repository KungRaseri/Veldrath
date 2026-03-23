using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="ActorArchetype"/> rows (enemies and NPCs) into <see cref="ContentDbContext"/>.</summary>
public static class ArchetypeSeeder
{
    /// <summary>Seeds all archetype rows (idempotent, enemies first then NPCs).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedEnemiesAsync(db);
        await SeedNpcsAsync(db);
    }

    // ── Enemies ───────────────────────────────────────────────────────────────

    private static async Task SeedEnemiesAsync(ContentDbContext db)
    {
        if (await db.ActorArchetypes.AnyAsync(a => a.Traits != null && a.Traits.Hostile == true))
            return;

        var now = DateTimeOffset.UtcNow;

        db.ActorArchetypes.AddRange(
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
                    Health          = 30,
                    Mana            = null,
                    Strength        = 6,
                    Agility         = 12,
                    Intelligence    = 4,
                    Constitution    = 8,
                    ArmorClass      = 2,
                    AttackBonus     = 1,
                    Damage          = 4,
                    ExperienceReward = 15,
                    GoldRewardMin   = 1,
                    GoldRewardMax   = 5,
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
            });

        await db.SaveChangesAsync();
    }

    // ── NPCs ──────────────────────────────────────────────────────────────────

    private static async Task SeedNpcsAsync(ContentDbContext db)
    {
        if (await db.ActorArchetypes.AnyAsync(a => a.Traits != null && a.Traits.Shopkeeper == true))
            return;

        var now = DateTimeOffset.UtcNow;

        db.ActorArchetypes.AddRange(
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
                    TradeGold  = 500,
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
            });

        await db.SaveChangesAsync();
    }
}
