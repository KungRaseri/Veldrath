using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds the unified power catalog (abilities, spells, talents, cantrips, passives) into <see cref="ContentDbContext"/>.</summary>
public static class PowersSeeder
{
    /// <summary>Seeds all power rows and their junction relationships (idempotent, ordered by dependency).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedPowersAsync(db);
        await SeedClassPowerUnlocksAsync(db);
        await SeedSpeciesPowerPoolsAsync(db);
    }

    // Powers
    private static async Task SeedPowersAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.Powers.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllPowers(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.Powers.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static Power[] GetAllPowers(DateTimeOffset now) =>
    [
            // Warrior active — heavy melee strike
            new Power
            {
                Slug        = "power-strike",
                TypeKey     = "active/offensive",
                PowerType   = "talent",
                DisplayName = "Power Strike",
                RarityWeight = 50,
                IsActive    = true,
                Version     = 1,
                UpdatedAt   = now,
                Stats = new PowerStats
                {
                    Cooldown   = 6.0f,
                    ManaCost   = 10,
                    CastTime   = 0.5f,
                    Range      = 2,
                    DamageMin  = 15,
                    DamageMax  = 25,
                    MaxTargets = 1,
                },
                Effects = new PowerEffects
                {
                    DamageType       = "physical",
                    ConditionApplied = "staggered",
                    ConditionChance  = 0.3f,
                },
                Traits = new PowerTraits
                {
                    RequiresTarget = true,
                    IsAoe          = false,
                    HasCooldown    = true,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = true,
                    IsPassive      = false,
                },
            },
            // Warrior passive — flat health bonus
            new Power
            {
                Slug         = "toughness",
                TypeKey      = "passive/defensive",
                PowerType    = "passive",
                DisplayName  = "Toughness",
                RarityWeight = 50,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new PowerStats
                {
                    HealMin = 2,
                    HealMax = 2,
                },
                Effects = new PowerEffects(),
                Traits = new PowerTraits
                {
                    IsPassive      = true,
                    RequiresTarget = false,
                    IsAoe          = false,
                    HasCooldown    = false,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = false,
                },
            },
            // Mage active — ranged fire AoE spell
            new Power
            {
                Slug         = "fireball",
                TypeKey      = "active/offensive",
                PowerType    = "spell",
                School       = "fire",
                DisplayName  = "Fireball",
                RarityWeight = 45,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new PowerStats
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
                Effects = new PowerEffects
                {
                    DamageType       = "fire",
                    ConditionApplied = "burning",
                    ConditionChance  = 0.5f,
                },
                Traits = new PowerTraits
                {
                    RequiresTarget = false,
                    IsAoe          = true,
                    HasCooldown    = true,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = true,
                    IsPassive      = false,
                },
            },
            // Mage passive — reduces mana cost
            new Power
            {
                Slug         = "arcane-focus",
                TypeKey      = "passive/utility",
                PowerType    = "passive",
                School       = "arcane",
                DisplayName  = "Arcane Focus",
                RarityWeight = 45,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new PowerStats
                {
                    ManaCost = -5, // represents the flat mana reduction granted
                },
                Effects = new PowerEffects(),
                Traits = new PowerTraits
                {
                    IsPassive      = true,
                    RequiresTarget = false,
                    IsAoe          = false,
                    HasCooldown    = false,
                    IsInstant      = false,
                    IsChanneled    = false,
                    CanCrit        = false,
                },
            },
            // Wolf innate — natural bite attack
            new Power
            {
                Slug         = "bite",
                TypeKey      = "active/offensive",
                PowerType    = "innate",
                DisplayName  = "Bite",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new PowerStats
                {
                    Cooldown   = 4.0f,
                    ManaCost   = 0,
                    CastTime   = 0.2f,
                    Range      = 1,
                    DamageMin  = 8,
                    DamageMax  = 14,
                    MaxTargets = 1,
                },
                Effects = new PowerEffects
                {
                    DamageType       = "physical",
                    ConditionApplied = "bleeding",
                    ConditionChance  = 0.25f,
                },
                Traits = new PowerTraits
                {
                    RequiresTarget = true,
                    IsAoe          = false,
                    HasCooldown    = true,
                    IsInstant      = true,
                    IsChanneled    = false,
                    CanCrit        = true,
                    IsPassive      = false,
                },
            }
    ];

    // Class Power Unlocks
    private static async Task SeedClassPowerUnlocksAsync(ContentDbContext db)
    {
        var warrior = await db.ActorClasses.FirstOrDefaultAsync(c => c.Slug == "warrior");
        var mage    = await db.ActorClasses.FirstOrDefaultAsync(c => c.Slug == "mage");

        var powerStrike = await db.Powers.FirstOrDefaultAsync(p => p.Slug == "power-strike");
        var toughness   = await db.Powers.FirstOrDefaultAsync(p => p.Slug == "toughness");
        var fireball    = await db.Powers.FirstOrDefaultAsync(p => p.Slug == "fireball");
        var arcaneFocus = await db.Powers.FirstOrDefaultAsync(p => p.Slug == "arcane-focus");

        if (warrior is null || mage is null || powerStrike is null ||
            toughness is null || fireball is null || arcaneFocus is null)
            return;

        var existingUnlocks = (await db.ClassPowerUnlocks.AsNoTracking()
                .Select(u => new { u.ClassId, u.PowerId }).ToListAsync())
            .Select(u => (u.ClassId, u.PowerId)).ToHashSet();
        var missingUnlocks = GetAllClassPowerUnlocks(warrior.Id, mage.Id, powerStrike.Id, toughness.Id, fireball.Id, arcaneFocus.Id)
            .Where(u => !existingUnlocks.Contains((u.ClassId, u.PowerId))).ToList();
        if (missingUnlocks.Count == 0) return;
        db.ClassPowerUnlocks.AddRange(missingUnlocks);
        await db.SaveChangesAsync();
    }

    private static ClassPowerUnlock[] GetAllClassPowerUnlocks(
        Guid warriorId, Guid mageId, Guid powerStrikeId, Guid toughnessId, Guid fireballId, Guid arcaneFocusId) =>
    [
        new ClassPowerUnlock { ClassId = warriorId, PowerId = powerStrikeId, LevelRequired = 1, Rank = 1 },
        new ClassPowerUnlock { ClassId = warriorId, PowerId = toughnessId,   LevelRequired = 1, Rank = 1 },
        new ClassPowerUnlock { ClassId = mageId,    PowerId = fireballId,    LevelRequired = 1, Rank = 1 },
        new ClassPowerUnlock { ClassId = mageId,    PowerId = arcaneFocusId, LevelRequired = 1, Rank = 1 },
    ];

    // Species Power Pools
    private static async Task SeedSpeciesPowerPoolsAsync(ContentDbContext db)
    {
        var wolf = await db.Species.FirstOrDefaultAsync(s => s.Slug == "wolf");
        var bite = await db.Powers.FirstOrDefaultAsync(p => p.Slug == "bite");

        if (wolf is null || bite is null)
            return;

        var existingPools = (await db.SpeciesPowerPools.AsNoTracking()
                .Select(u => new { u.SpeciesId, u.PowerId }).ToListAsync())
            .Select(u => (u.SpeciesId, u.PowerId)).ToHashSet();
        var missingPools = GetAllSpeciesPowerPools(wolf.Id, bite.Id)
            .Where(u => !existingPools.Contains((u.SpeciesId, u.PowerId))).ToList();
        if (missingPools.Count == 0) return;
        db.SpeciesPowerPools.AddRange(missingPools);
        await db.SaveChangesAsync();
    }

    private static SpeciesPowerPool[] GetAllSpeciesPowerPools(Guid wolfId, Guid biteId) =>
    [
        new SpeciesPowerPool { SpeciesId = wolfId, PowerId = biteId },
    ];
}
