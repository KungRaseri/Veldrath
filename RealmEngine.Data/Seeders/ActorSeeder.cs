using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds actor-related baseline rows (classes, skills, backgrounds, species) into <see cref="ContentDbContext"/>.</summary>
public static class ActorSeeder
{
    /// <summary>Seeds all actor content rows (idempotent, ordered by dependency).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedActorClassesAsync(db);
        await SeedSkillsAsync(db);
        await SeedBackgroundsAsync(db);
        await SeedSpeciesAsync(db);
    }

    // ── Actor Classes ─────────────────────────────────────────────────────────

    private static async Task SeedActorClassesAsync(ContentDbContext db)
    {
        if (await db.ActorClasses.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.ActorClasses.AddRange(
            new ActorClass
            {
                Slug         = "warrior",
                TypeKey      = "warriors",
                DisplayName  = "Warrior",
                RarityWeight = 50,
                HitDie       = 10,
                PrimaryStat  = "strength",
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ActorClassStats
                {
                    BaseHealth         = 120,
                    BaseMana           = 20,
                    HealthGrowth       = 12.0f,
                    ManaGrowth         = 2.0f,
                    StrengthGrowth     = 2.5f,
                    DexterityGrowth    = 1.0f,
                    IntelligenceGrowth = 0.5f,
                    ConstitutionGrowth = 2.0f,
                },
                Traits = new ActorClassTraits
                {
                    CanWearHeavy  = true,
                    CanWearShield = true,
                    Melee         = true,
                    Spellcaster   = false,
                    Ranged        = false,
                    Stealth       = false,
                    CanDualWield  = false,
                },
            },
            new ActorClass
            {
                Slug         = "mage",
                TypeKey      = "casters",
                DisplayName  = "Mage",
                RarityWeight = 40,
                HitDie       = 6,
                PrimaryStat  = "intelligence",
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ActorClassStats
                {
                    BaseHealth         = 60,
                    BaseMana           = 100,
                    HealthGrowth       = 6.0f,
                    ManaGrowth         = 10.0f,
                    StrengthGrowth     = 0.5f,
                    DexterityGrowth    = 0.5f,
                    IntelligenceGrowth = 2.5f,
                    ConstitutionGrowth = 1.0f,
                },
                Traits = new ActorClassTraits
                {
                    Spellcaster   = true,
                    CanWearHeavy  = false,
                    CanWearShield = false,
                    Melee         = false,
                    Ranged        = true,
                    Stealth       = false,
                    CanDualWield  = false,
                },
            }
        );

        await db.SaveChangesAsync();
    }

    // ── [Powers/ClassPowerUnlocks/SpeciesPowerPools moved to PowersSeeder] ─────

    // ── Skills ────────────────────────────────────────────────────────────────
    private static async Task SeedSkillsAsync(ContentDbContext db)
    {
        if (await db.Skills.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Skills.AddRange(
            new Skill
            {
                Slug               = "swordsmanship",
                TypeKey            = "combat",
                DisplayName        = "Swordsmanship",
                RarityWeight       = 50,
                MaxRank            = 5,
                GoverningAttribute = "strength",
                IsActive           = true,
                Version            = 1,
                UpdatedAt          = now,
                Stats = new SkillStats
                {
                    XpPerRank    = 100,
                    BonusPerRank = 2.0f,
                    BaseValue    = 5,
                },
                Traits = new SkillTraits
                {
                    Combat      = true,
                    Passive     = false,
                    Crafting    = false,
                    Social      = false,
                    Exploration = false,
                },
            },
            new Skill
            {
                Slug               = "arcanology",
                TypeKey            = "crafting",
                DisplayName        = "Arcanology",
                RarityWeight       = 40,
                MaxRank            = 5,
                GoverningAttribute = "intelligence",
                IsActive           = true,
                Version            = 1,
                UpdatedAt          = now,
                Stats = new SkillStats
                {
                    XpPerRank    = 120,
                    BonusPerRank = 1.5f,
                    BaseValue    = 0,
                },
                Traits = new SkillTraits
                {
                    Crafting    = true,
                    Passive     = false,
                    Combat      = false,
                    Social      = false,
                    Exploration = false,
                },
            }
        );

        await db.SaveChangesAsync();
    }

    // ── Backgrounds ───────────────────────────────────────────────────────────

    private static async Task SeedBackgroundsAsync(ContentDbContext db)
    {
        if (await db.Backgrounds.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Backgrounds.AddRange(
            new Background
            {
                Slug         = "soldier",
                TypeKey      = "common",
                DisplayName  = "Soldier",
                RarityWeight = 55,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new BackgroundStats
                {
                    StartingGold      = 50,
                    BonusStrength     = 2,
                    BonusConstitution = 1,
                },
                Traits = new BackgroundTraits
                {
                    Military  = true,
                    Noble     = false,
                    Criminal  = false,
                    Merchant  = false,
                    Scholar   = false,
                    Religious = false,
                    Regional  = false,
                },
            },
            new Background
            {
                Slug         = "scholar",
                TypeKey      = "scholar",
                DisplayName  = "Scholar",
                RarityWeight = 45,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new BackgroundStats
                {
                    StartingGold      = 80,
                    BonusIntelligence = 2,
                    BonusDexterity    = 1,
                },
                Traits = new BackgroundTraits
                {
                    Scholar   = true,
                    Noble     = false,
                    Criminal  = false,
                    Merchant  = false,
                    Military  = false,
                    Religious = false,
                    Regional  = false,
                },
            }
        );

        await db.SaveChangesAsync();
    }

    // ── Species ───────────────────────────────────────────────────────────────

    private static async Task SeedSpeciesAsync(ContentDbContext db)
    {
        if (await db.Species.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Species.AddRange(
            new Species
            {
                Slug         = "human",
                TypeKey      = "humanoid",
                DisplayName  = "Human",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new SpeciesStats
                {
                    BaseStrength     = 10,
                    BaseAgility      = 10,
                    BaseIntelligence = 10,
                    BaseConstitution = 10,
                    BaseHealth       = 100,
                    NaturalArmor     = 0,
                    MovementSpeed    = 5.0f,
                    SizeCategory     = "medium",
                },
                Traits = new SpeciesTraits
                {
                    Humanoid   = true,
                    Beast      = false,
                    Undead     = false,
                    Demon      = false,
                    Dragon     = false,
                    Elemental  = false,
                    Construct  = false,
                    Darkvision = false,
                    Aquatic    = false,
                    Flying     = false,
                },
            },
            new Species
            {
                Slug         = "wolf",
                TypeKey      = "beast",
                DisplayName  = "Wolf",
                RarityWeight = 55,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new SpeciesStats
                {
                    BaseStrength     = 12,
                    BaseAgility      = 14,
                    BaseIntelligence = 4,
                    BaseConstitution = 10,
                    BaseHealth       = 80,
                    NaturalArmor     = 1,
                    MovementSpeed    = 7.0f,
                    SizeCategory     = "medium",
                },
                Traits = new SpeciesTraits
                {
                    Beast      = true,
                    Humanoid   = false,
                    Undead     = false,
                    Demon      = false,
                    Dragon     = false,
                    Elemental  = false,
                    Construct  = false,
                    Darkvision = false,
                    Aquatic    = false,
                    Flying     = false,
                },
            }
        );

        await db.SaveChangesAsync();
    }
}
