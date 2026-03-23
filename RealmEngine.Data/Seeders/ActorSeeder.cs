using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds actor-related baseline rows (classes, abilities, skills, backgrounds, species, and their relationships) into <see cref="ContentDbContext"/>.</summary>
public static class ActorSeeder
{
    /// <summary>Seeds all actor content rows (idempotent, ordered by dependency).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedActorClassesAsync(db);
        await SeedAbilitiesAsync(db);
        await SeedSkillsAsync(db);
        await SeedBackgroundsAsync(db);
        await SeedSpeciesAsync(db);
        await SeedClassAbilityUnlocksAsync(db);
        await SeedSpeciesAbilityPoolsAsync(db);
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

    // ── Abilities ─────────────────────────────────────────────────────────────

    private static async Task SeedAbilitiesAsync(ContentDbContext db)
    {
        if (await db.Abilities.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Abilities.AddRange(
            // Warrior active — heavy melee strike
            new Ability
            {
                Slug         = "power-strike",
                TypeKey      = "active/offensive",
                AbilityType  = "active",
                DisplayName  = "Power Strike",
                RarityWeight = 50,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new AbilityStats
                {
                    Cooldown   = 6.0f,
                    ManaCost   = 10,
                    CastTime   = 0.5f,
                    Range      = 2,
                    DamageMin  = 15,
                    DamageMax  = 25,
                    MaxTargets = 1,
                },
                Effects = new AbilityEffects
                {
                    DamageType       = "physical",
                    ConditionApplied = "staggered",
                    ConditionChance  = 0.3f,
                },
                Traits = new AbilityTraits
                {
                    RequiresTarget = true,
                    IsAoe          = false,
                    HasCooldown    = true,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = true,
                    IsPassive      = false,
                    RequiresWeapon = true,
                },
            },
            // Warrior passive — flat health bonus
            new Ability
            {
                Slug         = "toughness",
                TypeKey      = "passive/defensive",
                AbilityType  = "passive",
                DisplayName  = "Toughness",
                RarityWeight = 50,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new AbilityStats
                {
                    HealMin = 2,
                    HealMax = 2,
                },
                Effects = new AbilityEffects(),
                Traits = new AbilityTraits
                {
                    IsPassive      = true,
                    RequiresTarget = false,
                    IsAoe          = false,
                    HasCooldown    = false,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = false,
                    RequiresWeapon = false,
                },
            },
            // Mage active — ranged fire AoE
            new Ability
            {
                Slug         = "fireball",
                TypeKey      = "active/offensive",
                AbilityType  = "active",
                DisplayName  = "Fireball",
                RarityWeight = 45,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new AbilityStats
                {
                    Cooldown   = 8.0f,
                    ManaCost   = 30,
                    CastTime   = 1.5f,
                    Range      = 20,
                    DamageMin  = 20,
                    DamageMax  = 35,
                    Radius     = 4,
                    MaxTargets = 6,
                    Duration   = 3.0f,
                },
                Effects = new AbilityEffects
                {
                    DamageType       = "fire",
                    ConditionApplied = "burning",
                    ConditionChance  = 0.5f,
                },
                Traits = new AbilityTraits
                {
                    RequiresTarget = false,
                    IsAoe          = true,
                    HasCooldown    = true,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = true,
                    IsPassive      = false,
                    RequiresWeapon = false,
                },
            },
            // Mage passive — reduces mana cost
            new Ability
            {
                Slug         = "arcane-focus",
                TypeKey      = "passive/utility",
                AbilityType  = "passive",
                DisplayName  = "Arcane Focus",
                RarityWeight = 45,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new AbilityStats
                {
                    ManaCost = -5, // represents the flat mana reduction granted
                },
                Effects = new AbilityEffects(),
                Traits = new AbilityTraits
                {
                    IsPassive      = true,
                    RequiresTarget = false,
                    IsAoe          = false,
                    HasCooldown    = false,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = false,
                    RequiresWeapon = false,
                },
            },
            // Wolf innate — natural bite attack
            new Ability
            {
                Slug         = "bite",
                TypeKey      = "active/offensive",
                AbilityType  = "active",
                DisplayName  = "Bite",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new AbilityStats
                {
                    Cooldown   = 4.0f,
                    ManaCost   = 0,
                    CastTime   = 0.2f,
                    Range      = 1,
                    DamageMin  = 8,
                    DamageMax  = 14,
                    MaxTargets = 1,
                },
                Effects = new AbilityEffects
                {
                    DamageType       = "physical",
                    ConditionApplied = "bleeding",
                    ConditionChance  = 0.25f,
                },
                Traits = new AbilityTraits
                {
                    RequiresTarget = true,
                    IsAoe          = false,
                    HasCooldown    = true,
                    IsInstant      = true,
                    IsChanneled    = false,
                    CanCrit        = true,
                    IsPassive      = false,
                    RequiresWeapon = false,
                },
            }
        );

        await db.SaveChangesAsync();
    }

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

    // ── Class Ability Unlocks ─────────────────────────────────────────────────

    private static async Task SeedClassAbilityUnlocksAsync(ContentDbContext db)
    {
        if (await db.ClassAbilityUnlocks.AnyAsync())
            return;

        var warrior = await db.ActorClasses.FirstOrDefaultAsync(c => c.Slug == "warrior");
        var mage    = await db.ActorClasses.FirstOrDefaultAsync(c => c.Slug == "mage");

        var powerStrike = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "power-strike");
        var toughness   = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "toughness");
        var fireball    = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "fireball");
        var arcaneFocus = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "arcane-focus");

        if (warrior is null || mage is null || powerStrike is null ||
            toughness is null || fireball is null || arcaneFocus is null)
            return;

        db.ClassAbilityUnlocks.AddRange(
            new ClassAbilityUnlock { ClassId = warrior.Id, AbilityId = powerStrike.Id, LevelRequired = 1, Rank = 1 },
            new ClassAbilityUnlock { ClassId = warrior.Id, AbilityId = toughness.Id,   LevelRequired = 1, Rank = 1 },
            new ClassAbilityUnlock { ClassId = mage.Id,    AbilityId = fireball.Id,    LevelRequired = 1, Rank = 1 },
            new ClassAbilityUnlock { ClassId = mage.Id,    AbilityId = arcaneFocus.Id, LevelRequired = 1, Rank = 1 }
        );

        await db.SaveChangesAsync();
    }

    // ── Species Ability Pools ─────────────────────────────────────────────────

    private static async Task SeedSpeciesAbilityPoolsAsync(ContentDbContext db)
    {
        if (await db.SpeciesAbilityPools.AnyAsync())
            return;

        var wolf = await db.Species.FirstOrDefaultAsync(s => s.Slug == "wolf");
        var bite = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "bite");

        if (wolf is null || bite is null)
            return;

        db.SpeciesAbilityPools.Add(new SpeciesAbilityPool { SpeciesId = wolf.Id, AbilityId = bite.Id });

        await db.SaveChangesAsync();
    }
}
