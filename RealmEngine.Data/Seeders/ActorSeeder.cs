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

    // Actor Classes
    private static async Task SeedActorClassesAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.ActorClasses.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllActorClasses(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.ActorClasses.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static ActorClass[] GetAllActorClasses(DateTimeOffset now) =>
    [
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
    ];

    // Skills
    private static async Task SeedSkillsAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.Skills.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllSkills(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.Skills.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static Skill[] GetAllSkills(DateTimeOffset now) =>
    [
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
    ];

    // Backgrounds
    private static async Task SeedBackgroundsAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.Backgrounds.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var allDefs = GetAllBackgrounds(now);
        var missing = allDefs.Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count > 0)
        {
            db.Backgrounds.AddRange(missing);
            await db.SaveChangesAsync();
        }
        // Patch descriptions on existing rows that were seeded before this field existed.
        var needsDescription = await db.Backgrounds.Where(b => b.Description == null).ToListAsync();
        if (needsDescription.Count > 0)
        {
            var bySlug = allDefs.ToDictionary(d => d.Slug, d => d.Description);
            foreach (var b in needsDescription)
            {
                if (bySlug.TryGetValue(b.Slug, out var desc))
                    b.Description = desc;
            }
            await db.SaveChangesAsync();
        }
    }

    private static Background[] GetAllBackgrounds(DateTimeOffset now) =>
    [
            new Background
            {
                Slug         = "soldier",
                TypeKey      = "common",
                DisplayName  = "Soldier",
                Description  = "You served in the military, learning discipline and the art of combat. Time on the battlefield hardened your body and sharpened your instincts. You begin with +2 Strength and +1 Constitution.",
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
                Description  = "You spent years in academic study, poring over tomes and experimenting with arcane theory. The pursuit of knowledge defines you. You begin with +2 Intelligence and +1 Dexterity.",
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
    ];

    // Species
    private static async Task SeedSpeciesAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.Species.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var allDefs = GetAllSpecies(now);
        var missing = allDefs.Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count > 0)
        {
            db.Species.AddRange(missing);
            await db.SaveChangesAsync();
        }
        // Patch descriptions on existing rows that were seeded before this field existed.
        var needsDescription = await db.Species.Where(s => s.Description == null).ToListAsync();
        if (needsDescription.Count > 0)
        {
            var bySlug = allDefs.ToDictionary(d => d.Slug, d => d.Description);
            foreach (var s in needsDescription)
            {
                if (bySlug.TryGetValue(s.Slug, out var desc))
                    s.Description = desc;
            }
            await db.SaveChangesAsync();
        }
    }

    private static Species[] GetAllSpecies(DateTimeOffset now) =>
    [
            new Species
            {
                Slug              = "human",
                TypeKey           = "humanoid",
                DisplayName       = "Human",
                Description       = "Versatile and adaptable, humans are found across all corners of the realm, thriving through determination and ingenuity. You gain +2 Constitution and +1 Charisma.",
                RarityWeight      = 60,
                IsActive          = true,
                IsPlayerSelectable = true,
                Version           = 1,
                UpdatedAt         = now,
                Stats = new SpeciesStats
                {
                    BaseStrength          = 10,
                    BaseAgility           = 10,
                    BaseIntelligence      = 10,
                    BaseConstitution      = 10,
                    BaseHealth            = 100,
                    NaturalArmor          = 0,
                    MovementSpeed         = 5.0f,
                    SizeCategory          = "medium",
                    PlayerBonusConstitution = 2,
                    PlayerBonusCharisma     = 1,
                },
                Traits = new SpeciesTraits
                {
                    Humanoid = true, Beast = false, Undead = false, Demon = false,
                    Dragon = false, Elemental = false, Construct = false,
                    Darkvision = false, Aquatic = false, Flying = false,
                },
            },
            new Species
            {
                Slug              = "elf",
                TypeKey           = "humanoid",
                DisplayName       = "Elf",
                Description       = "Ancient and graceful, elves have walked the forests of the world since before memory. Their keen senses and natural agility set them apart. You gain +2 Dexterity and +1 Wisdom.",
                RarityWeight      = 50,
                IsActive          = true,
                IsPlayerSelectable = true,
                Version           = 1,
                UpdatedAt         = now,
                Stats = new SpeciesStats
                {
                    BaseStrength     = 8,
                    BaseAgility      = 12,
                    BaseIntelligence = 10,
                    BaseConstitution = 8,
                    BaseHealth       = 90,
                    NaturalArmor     = 0,
                    MovementSpeed    = 5.5f,
                    SizeCategory     = "medium",
                    PlayerBonusDexterity = 2,
                    PlayerBonusWisdom    = 1,
                },
                Traits = new SpeciesTraits
                {
                    Humanoid = true, Beast = false, Undead = false, Demon = false,
                    Dragon = false, Elemental = false, Construct = false,
                    Darkvision = true, Aquatic = false, Flying = false,
                },
            },
            new Species
            {
                Slug              = "dwarf",
                TypeKey           = "humanoid",
                DisplayName       = "Dwarf",
                Description       = "Hardy and resilient, dwarves are master craftspeople of the mountain halls. Their stout frames endure where others would falter. You gain +2 Constitution and +1 Strength.",
                RarityWeight      = 50,
                IsActive          = true,
                IsPlayerSelectable = true,
                Version           = 1,
                UpdatedAt         = now,
                Stats = new SpeciesStats
                {
                    BaseStrength     = 10,
                    BaseAgility      = 8,
                    BaseIntelligence = 10,
                    BaseConstitution = 12,
                    BaseHealth       = 110,
                    NaturalArmor     = 1,
                    MovementSpeed    = 4.5f,
                    SizeCategory     = "medium",
                    PlayerBonusConstitution = 2,
                    PlayerBonusStrength     = 1,
                },
                Traits = new SpeciesTraits
                {
                    Humanoid = true, Beast = false, Undead = false, Demon = false,
                    Dragon = false, Elemental = false, Construct = false,
                    Darkvision = true, Aquatic = false, Flying = false,
                },
            },
            new Species
            {
                Slug              = "halfling",
                TypeKey           = "humanoid",
                DisplayName       = "Halfling",
                Description       = "Small in stature but bold in spirit, halflings are nimble wanderers with surprising luck. Their cheerful resilience carries them through the most perilous adventures. You gain +2 Dexterity and +1 Charisma.",
                RarityWeight      = 45,
                IsActive          = true,
                IsPlayerSelectable = true,
                Version           = 1,
                UpdatedAt         = now,
                Stats = new SpeciesStats
                {
                    BaseStrength     = 7,
                    BaseAgility      = 12,
                    BaseIntelligence = 10,
                    BaseConstitution = 9,
                    BaseHealth       = 85,
                    NaturalArmor     = 0,
                    MovementSpeed    = 4.0f,
                    SizeCategory     = "small",
                    PlayerBonusDexterity = 2,
                    PlayerBonusCharisma  = 1,
                },
                Traits = new SpeciesTraits
                {
                    Humanoid = true, Beast = false, Undead = false, Demon = false,
                    Dragon = false, Elemental = false, Construct = false,
                    Darkvision = false, Aquatic = false, Flying = false,
                },
            },
            new Species
            {
                Slug              = "gnome",
                TypeKey           = "humanoid",
                DisplayName       = "Gnome",
                Description       = "Curious and inventive, gnomes approach every problem with boundless enthusiasm. Their natural affinity for the arcane arts makes them formidable scholars and tinkerers. You gain +2 Intelligence and +1 Dexterity.",
                RarityWeight      = 40,
                IsActive          = true,
                IsPlayerSelectable = true,
                Version           = 1,
                UpdatedAt         = now,
                Stats = new SpeciesStats
                {
                    BaseStrength     = 6,
                    BaseAgility      = 10,
                    BaseIntelligence = 13,
                    BaseConstitution = 9,
                    BaseHealth       = 80,
                    NaturalArmor     = 0,
                    MovementSpeed    = 4.0f,
                    SizeCategory     = "small",
                    PlayerBonusIntelligence = 2,
                    PlayerBonusDexterity    = 1,
                },
                Traits = new SpeciesTraits
                {
                    Humanoid = true, Beast = false, Undead = false, Demon = false,
                    Dragon = false, Elemental = false, Construct = false,
                    Darkvision = true, Aquatic = false, Flying = false,
                },
            },
            new Species
            {
                Slug         = "wolf",
                TypeKey      = "beast",
                DisplayName  = "Wolf",
                RarityWeight = 55,
                IsActive     = true,
                IsPlayerSelectable = false,
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
                    Beast = true, Humanoid = false, Undead = false, Demon = false,
                    Dragon = false, Elemental = false, Construct = false,
                    Darkvision = false, Aquatic = false, Flying = false,
                },
            }
    ];
}
