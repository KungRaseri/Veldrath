using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds <see cref="Material"/> and <see cref="MaterialProperty"/> baseline rows into <see cref="ContentDbContext"/>.</summary>
public static class MaterialsSeeder
{
    /// <summary>Seeds all material and material-property rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedMaterialsAsync(db);
        await SeedMaterialPropertiesAsync(db);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    // MaterialStats:         hardness, weight, conductivity, magicAffinity, value
    private static MaterialStats MS(float? h, float? w, float? c, float? m, int? v) =>
        new() { Hardness = h, Weight = w, Conductivity = c, MagicAffinity = m, Value = v };

    // MaterialTraits:        fireResist, flexible, brittle, enchantable, magical, conductive
    private static MaterialTraits MT(bool? fr, bool? fl, bool? br, bool? en, bool? mg, bool? co) =>
        new() { FireResist = fr, Flexible = fl, Brittle = br, Enchantable = en, Magical = mg, Conductive = co };

    // MaterialPropertyStats: hardness, weight, conductivity, magicAffinity, durability, value
    private static MaterialPropertyStats MPS(float? h, float? w, float? c, float? m, float? d, int? v) =>
        new() { Hardness = h, Weight = w, Conductivity = c, MagicAffinity = m, Durability = d, Value = v };

    // MaterialPropertyTraits: conducting, brittle, magical, flexible, transparent, fireproof, enchantable
    private static MaterialPropertyTraits MPT(bool? co, bool? br, bool? mg, bool? fl, bool? tr, bool? fp, bool? en) =>
        new() { Conducting = co, Brittle = br, Magical = mg, Flexible = fl, Transparent = tr, Fireproof = fp, Enchantable = en };

    // ── Materials ─────────────────────────────────────────────────────────────

    private static async Task SeedMaterialsAsync(ContentDbContext db)
    {
        if (await db.Materials.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var materials = new List<Material>
        {
            // Metals
            new() { Slug = "iron",       TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Iron",        RarityWeight = 90, CostScale = 1.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.5f,  8.0f,  0.6f, null, 5),    Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "copper",     TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Copper",      RarityWeight = 85, CostScale = 0.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(4.0f,  9.0f,  0.9f, null, 4),    Traits = MT(false, false, false, true,  false, true)  },
            new() { Slug = "bronze",     TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Bronze",      RarityWeight = 75, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.0f,  8.5f,  0.8f, null, 8),    Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "steel",      TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Steel",       RarityWeight = 60, CostScale = 1.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(8.0f,  7.8f,  0.7f, null, 20),   Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "silver",     TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Silver",      RarityWeight = 40, CostScale = 3.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.5f,  10.5f, 1.0f, 0.4f, 50),   Traits = MT(false, false, false, true,  false, true)  },
            new() { Slug = "gold",       TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Gold",        RarityWeight = 25, CostScale = 5.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(3.0f,  19.3f, 1.0f, 0.3f, 200),  Traits = MT(false, false, false, true,  false, true)  },
            new() { Slug = "mithral",    TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Mithral",     RarityWeight = 10, CostScale = 15.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f,  3.0f,  1.2f, 0.8f, 500),  Traits = MT(true,  false, false, true,  true,  true)  },
            new() { Slug = "adamantine", TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Adamantine",  RarityWeight = 4,  CostScale = 25.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(10.0f, 5.8f,  0.5f, 0.6f, 1200), Traits = MT(true,  false, false, true,  false, false) },

            // Woods
            new() { Slug = "pine",       TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Pine",       RarityWeight = 90, CostScale = 0.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(2.0f, 0.5f, 0.1f, null, 1),   Traits = MT(false, true,  false, false, false, false) },
            new() { Slug = "oak",        TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Oak",        RarityWeight = 80, CostScale = 0.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(4.0f, 0.7f, 0.1f, null, 3),   Traits = MT(false, true,  false, false, false, false) },
            new() { Slug = "birch",      TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Birch",      RarityWeight = 70, CostScale = 0.9f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(3.5f, 0.6f, 0.1f, null, 4),   Traits = MT(false, true,  false, false, false, false) },
            new() { Slug = "ash",        TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Ash",        RarityWeight = 55, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.0f, 0.8f, 0.2f, null, 8),   Traits = MT(false, true,  false, false, false, false) },
            new() { Slug = "ironwood",   TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Ironwood",   RarityWeight = 20, CostScale = 4.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.5f, 1.2f, 0.2f, 0.2f, 60),  Traits = MT(false, true,  false, true,  false, false) },
            new() { Slug = "ebonwood",   TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Ebonwood",   RarityWeight = 8,  CostScale = 12.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(8.5f, 1.5f, 0.4f, 0.5f, 150), Traits = MT(false, false, false, true,  true,  false) },
            new() { Slug = "dragonwood", TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Dragonwood", RarityWeight = 3,  CostScale = 30.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f, 2.0f, 0.8f, 0.9f, 600), Traits = MT(true,  false, false, true,  true,  true)  },

            // Leathers
            new() { Slug = "leather",          TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Leather",          RarityWeight = 90, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(2.0f, 0.5f, 0.1f, null, 3),   Traits = MT(false, true,  false, false, false, false) },
            new() { Slug = "thick-leather",    TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Thick Leather",    RarityWeight = 70, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(3.5f, 0.7f, 0.1f, null, 8),   Traits = MT(false, true,  false, false, false, false) },
            new() { Slug = "hardened-leather", TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Hardened Leather", RarityWeight = 50, CostScale = 1.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.0f, 0.8f, 0.1f, null, 18),  Traits = MT(false, true,  false, true,  false, false) },
            new() { Slug = "scale-leather",    TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Scale Leather",    RarityWeight = 25, CostScale = 4.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(6.0f, 1.0f, 0.2f, 0.2f, 60),  Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "drake-hide",       TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Drake Hide",       RarityWeight = 12, CostScale = 10.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.0f, 1.2f, 0.3f, 0.4f, 200), Traits = MT(true,  false, false, true,  false, false) },
            new() { Slug = "dragon-leather",   TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Dragon Leather",   RarityWeight = 3,  CostScale = 30.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f, 1.8f, 0.5f, 0.8f, 800), Traits = MT(true,  false, false, true,  true,  false) },

            // Gems
            new() { Slug = "quartz",   TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Quartz",   RarityWeight = 80, CostScale = 1.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.0f,  2.65f, 0.3f, 0.2f, 10),   Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "amethyst", TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Amethyst", RarityWeight = 55, CostScale = 2.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.0f,  2.65f, 0.4f, 0.4f, 40),   Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "topaz",    TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Topaz",    RarityWeight = 40, CostScale = 3.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(8.0f,  3.5f,  0.5f, 0.5f, 80),   Traits = MT(false, true,  false, true,  false, false) },
            new() { Slug = "ruby",     TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Ruby",     RarityWeight = 20, CostScale = 7.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f,  4.0f,  0.6f, 0.7f, 300),  Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "sapphire", TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Sapphire", RarityWeight = 18, CostScale = 8.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f,  4.0f,  0.7f, 0.8f, 350),  Traits = MT(false, false, false, true,  false, false) },
            new() { Slug = "emerald",  TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Emerald",  RarityWeight = 12, CostScale = 12.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.5f,  2.7f,  0.5f, 0.9f, 600),  Traits = MT(false, false, true,  true,  false, false) },
            new() { Slug = "diamond",  TypeKey = "gems", MaterialFamily = "gem", DisplayName = "Diamond",  RarityWeight = 5,  CostScale = 25.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(10.0f, 3.5f,  0.2f, 1.0f, 2500), Traits = MT(false, false, false, true,  false, false) },

            // Bones
            new() { Slug = "bone",         TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Bone",         RarityWeight = 85, CostScale = 0.4f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(3.0f, 1.8f, 0.1f, null, 1),   Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "thick-bone",   TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Thick Bone",   RarityWeight = 65, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(4.5f, 2.0f, 0.1f, null, 3),   Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "beast-bone",   TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Beast Bone",   RarityWeight = 45, CostScale = 1.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.5f, 2.2f, 0.2f, 0.1f, 12),  Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "monster-bone", TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Monster Bone", RarityWeight = 20, CostScale = 4.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(6.5f, 2.5f, 0.3f, 0.3f, 50),  Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "dragon-bone",  TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Dragon Bone",  RarityWeight = 4,  CostScale = 20.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.5f, 3.0f, 0.4f, 0.7f, 400), Traits = MT(true,  false, false, true,  false, false) },

            // Fabrics
            new() { Slug = "linen",       TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Linen",       RarityWeight = 90, CostScale = 0.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(1.0f, 0.2f, 0.1f, null, 2),   Traits = MT(false, true, false, false, false, false) },
            new() { Slug = "wool",        TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Wool",        RarityWeight = 80, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(1.5f, 0.3f, 0.1f, null, 4),   Traits = MT(false, true, false, false, false, false) },
            new() { Slug = "silk",        TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Silk",        RarityWeight = 40, CostScale = 3.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(2.5f, 0.1f, 0.2f, 0.1f, 50),  Traits = MT(false, true, false, true,  false, false) },
            new() { Slug = "shadowsilk",  TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Shadowsilk",  RarityWeight = 15, CostScale = 9.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(4.0f, 0.1f, 0.3f, 0.6f, 200), Traits = MT(false, true, true,  true,  false, false) },
            new() { Slug = "spiderweave", TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Spiderweave", RarityWeight = 8,  CostScale = 16.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.5f, 0.2f, 0.4f, 0.5f, 400), Traits = MT(false, true, false, true,  false, false) },
            new() { Slug = "moonweave",   TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Moonweave",   RarityWeight = 3,  CostScale = 35.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(6.0f, 0.1f, 0.5f, 0.9f, 900), Traits = MT(false, true, true,  true,  false, false) },

            // Scales
            new() { Slug = "fish-scale",   TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Fish Scale",   RarityWeight = 80, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(3.0f, 0.8f, 0.2f, null, 3),   Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "snake-scale",  TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Snake Scale",  RarityWeight = 60, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(4.5f, 1.0f, 0.3f, 0.1f, 10),  Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "lizard-scale", TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Lizard Scale", RarityWeight = 45, CostScale = 1.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.5f, 1.2f, 0.3f, 0.2f, 22),  Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "drake-scale",  TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Drake Scale",  RarityWeight = 20, CostScale = 5.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.5f, 1.5f, 0.4f, 0.5f, 100), Traits = MT(true,  false, false, true,  false, false) },
            new() { Slug = "dragon-scale", TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Dragon Scale", RarityWeight = 4,  CostScale = 22.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.5f, 2.0f, 0.6f, 0.9f, 700), Traits = MT(true,  false, false, true,  false, false) },

            // Chitin
            new() { Slug = "insect-chitin",   TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Insect Chitin",   RarityWeight = 80, CostScale = 0.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(4.0f, 0.9f, 0.1f, null, 2),   Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "carapace",        TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Carapace",        RarityWeight = 55, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(5.5f, 1.1f, 0.2f, null, 8),   Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "hardened-chitin", TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Hardened Chitin", RarityWeight = 30, CostScale = 2.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.0f, 1.3f, 0.2f, 0.1f, 30),  Traits = MT(false, false, false, false, false, false) },
            new() { Slug = "venomous-chitin", TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Venomous Chitin", RarityWeight = 15, CostScale = 6.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.5f, 1.4f, 0.3f, 0.3f, 90),  Traits = MT(false, false, true,  true,  false, false) },
            new() { Slug = "ancient-chitin",  TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Ancient Chitin",  RarityWeight = 4,  CostScale = 18.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f, 1.6f, 0.4f, 0.6f, 350), Traits = MT(false, false, true,  true,  false, false) },

            // Crystals
            new() { Slug = "quartz-crystal",    TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Quartz Crystal",    RarityWeight = 75, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(6.0f, 2.6f, 0.5f, 0.3f, 15),   Traits = MT(false, false, false, true, false, false) },
            new() { Slug = "resonance-crystal", TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Resonance Crystal", RarityWeight = 40, CostScale = 3.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(6.5f, 2.8f, 0.8f, 0.6f, 80),   Traits = MT(false, false, true,  true, false, false) },
            new() { Slug = "arcane-crystal",    TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Arcane Crystal",    RarityWeight = 20, CostScale = 7.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(7.5f, 3.0f, 0.9f, 0.9f, 300),  Traits = MT(false, false, true,  true, false, false) },
            new() { Slug = "void-crystal",      TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Void Crystal",      RarityWeight = 8,  CostScale = 18.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(8.0f, 3.2f, 1.0f, 1.0f, 800),  Traits = MT(false, false, true,  true, false, false) },
            new() { Slug = "eternal-crystal",   TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Eternal Crystal",   RarityWeight = 3,  CostScale = 40.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MS(9.0f, 3.5f, 1.0f, 1.0f, 2000), Traits = MT(false, false, true,  true, false, false) },
        };

        db.Materials.AddRange(materials);
        await db.SaveChangesAsync();
    }

    // ── Material Properties ───────────────────────────────────────────────────

    private static async Task SeedMaterialPropertiesAsync(ContentDbContext db)
    {
        if (await db.MaterialProperties.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var props = new List<MaterialProperty>
        {
            // Metals
            new() { Slug = "iron",       TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Iron",       RarityWeight = 90, CostScale = 1.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.5f,  8.0f,  0.6f, null, 0.80f, 5),    Traits = MPT(false, false, false, false, false, false, true)  },
            new() { Slug = "copper",     TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Copper",     RarityWeight = 85, CostScale = 0.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(4.0f,  9.0f,  0.9f, null, 0.60f, 4),    Traits = MPT(true,  false, false, false, false, false, true)  },
            new() { Slug = "bronze",     TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Bronze",     RarityWeight = 75, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.0f,  8.5f,  0.8f, null, 0.70f, 8),    Traits = MPT(true,  false, false, false, false, false, true)  },
            new() { Slug = "steel",      TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Steel",      RarityWeight = 60, CostScale = 1.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(8.0f,  7.8f,  0.7f, null, 0.90f, 20),   Traits = MPT(false, false, false, false, false, false, true)  },
            new() { Slug = "silver",     TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Silver",     RarityWeight = 40, CostScale = 3.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.5f,  10.5f, 1.0f, 0.4f, 0.75f, 50),   Traits = MPT(true,  false, false, false, false, false, true)  },
            new() { Slug = "gold",       TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Gold",       RarityWeight = 25, CostScale = 5.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(3.0f,  19.3f, 1.0f, 0.3f, 0.65f, 200),  Traits = MPT(true,  false, false, false, false, false, true)  },
            new() { Slug = "mithral",    TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Mithral",    RarityWeight = 10, CostScale = 15.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f,  3.0f,  1.2f, 0.8f, 0.95f, 500),  Traits = MPT(true,  false, true,  false, false, true,  true)  },
            new() { Slug = "adamantine", TypeKey = "metals", MaterialFamily = "metal",   DisplayName = "Adamantine", RarityWeight = 4,  CostScale = 25.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(10.0f, 5.8f,  0.5f, 0.6f, 0.99f, 1200), Traits = MPT(false, false, false, false, false, true,  true)  },

            // Woods
            new() { Slug = "pine",       TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Pine",       RarityWeight = 90, CostScale = 0.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(2.0f, 0.5f, 0.1f, null, 0.40f, 1),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "oak",        TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Oak",        RarityWeight = 80, CostScale = 0.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(4.0f, 0.7f, 0.1f, null, 0.65f, 3),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "birch",      TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Birch",      RarityWeight = 70, CostScale = 0.9f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(3.5f, 0.6f, 0.1f, null, 0.60f, 4),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "ash",        TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Ash",        RarityWeight = 55, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.0f, 0.8f, 0.2f, null, 0.72f, 8),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "ironwood",   TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Ironwood",   RarityWeight = 20, CostScale = 4.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.5f, 1.2f, 0.2f, 0.2f, 0.88f, 60), Traits = MPT(false, false, false, false, false, false, true)  },
            new() { Slug = "ebonwood",   TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Ebonwood",   RarityWeight = 8,  CostScale = 12.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(8.5f, 1.5f, 0.4f, 0.5f, 0.93f, 150), Traits = MPT(false, false, true,  false, false, false, true)  },
            new() { Slug = "dragonwood", TypeKey = "woods", MaterialFamily = "wood", DisplayName = "Dragonwood", RarityWeight = 3,  CostScale = 30.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f, 2.0f, 0.8f, 0.9f, 0.98f, 600), Traits = MPT(false, false, true,  false, false, true,  true)  },

            // Leathers
            new() { Slug = "leather",          TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Leather",          RarityWeight = 90, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(2.0f, 0.5f, 0.1f, null, 0.50f, 3),  Traits = MPT(false, false, false, true, false, false, false) },
            new() { Slug = "thick-leather",    TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Thick Leather",    RarityWeight = 70, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(3.5f, 0.7f, 0.1f, null, 0.65f, 8),  Traits = MPT(false, false, false, true, false, false, false) },
            new() { Slug = "hardened-leather", TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Hardened Leather", RarityWeight = 50, CostScale = 1.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.0f, 0.8f, 0.1f, null, 0.78f, 18), Traits = MPT(false, false, false, true, false, false, true)  },
            new() { Slug = "scale-leather",    TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Scale Leather",    RarityWeight = 25, CostScale = 4.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(6.0f, 1.0f, 0.2f, 0.2f, 0.82f, 60), Traits = MPT(false, false, false, true, false, false, true)  },
            new() { Slug = "drake-hide",       TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Drake Hide",       RarityWeight = 12, CostScale = 10.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.0f, 1.2f, 0.3f, 0.4f, 0.90f, 200), Traits = MPT(false, false, false, true, false, true,  true)  },
            new() { Slug = "dragon-leather",   TypeKey = "leathers", MaterialFamily = "leather", DisplayName = "Dragon Leather",   RarityWeight = 3,  CostScale = 30.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f, 1.8f, 0.5f, 0.8f, 0.97f, 800), Traits = MPT(false, false, true,  true, false, true,  true)  },

            // Gemstones
            new() { Slug = "quartz",   TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Quartz",   RarityWeight = 80, CostScale = 1.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.0f,  2.65f, 0.3f, 0.2f, 0.80f, 10),   Traits = MPT(false, false, false, false, true, false, true) },
            new() { Slug = "amethyst", TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Amethyst", RarityWeight = 55, CostScale = 2.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.0f,  2.65f, 0.4f, 0.4f, 0.82f, 40),   Traits = MPT(false, false, false, false, true, false, true) },
            new() { Slug = "topaz",    TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Topaz",    RarityWeight = 40, CostScale = 3.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(8.0f,  3.5f,  0.5f, 0.5f, 0.85f, 80),   Traits = MPT(false, false, false, false, true, false, true) },
            new() { Slug = "ruby",     TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Ruby",     RarityWeight = 20, CostScale = 7.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f,  4.0f,  0.6f, 0.7f, 0.90f, 300),  Traits = MPT(false, false, false, false, true, false, true) },
            new() { Slug = "sapphire", TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Sapphire", RarityWeight = 18, CostScale = 8.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f,  4.0f,  0.7f, 0.8f, 0.90f, 350),  Traits = MPT(false, false, false, false, true, false, true) },
            new() { Slug = "emerald",  TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Emerald",  RarityWeight = 12, CostScale = 12.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.5f,  2.7f,  0.5f, 0.9f, 0.92f, 600),  Traits = MPT(false, false, true,  false, true, false, true) },
            new() { Slug = "diamond",  TypeKey = "gemstones", MaterialFamily = "gem", DisplayName = "Diamond",  RarityWeight = 5,  CostScale = 25.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(10.0f, 3.5f,  0.2f, 1.0f, 0.99f, 2500), Traits = MPT(false, true,  false, false, true, false, true) },

            // Bones
            new() { Slug = "bone",         TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Bone",         RarityWeight = 85, CostScale = 0.4f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(3.0f, 1.8f, 0.1f, null, 0.45f, 1),   Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "thick-bone",   TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Thick Bone",   RarityWeight = 65, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(4.5f, 2.0f, 0.1f, null, 0.60f, 3),   Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "beast-bone",   TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Beast Bone",   RarityWeight = 45, CostScale = 1.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.5f, 2.2f, 0.2f, 0.1f, 0.72f, 12),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "monster-bone", TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Monster Bone", RarityWeight = 20, CostScale = 4.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(6.5f, 2.5f, 0.3f, 0.3f, 0.82f, 50),  Traits = MPT(false, false, false, false, false, false, true)  },
            new() { Slug = "dragon-bone",  TypeKey = "bones", MaterialFamily = "bone", DisplayName = "Dragon Bone",  RarityWeight = 4,  CostScale = 20.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.5f, 3.0f, 0.4f, 0.7f, 0.96f, 400), Traits = MPT(false, false, true,  false, false, true,  true)  },

            // Fabrics
            new() { Slug = "linen",       TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Linen",       RarityWeight = 90, CostScale = 0.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(1.0f, 0.2f, 0.1f, null, 0.30f, 2),   Traits = MPT(false, false, false, true, false, false, false) },
            new() { Slug = "wool",        TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Wool",        RarityWeight = 80, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(1.5f, 0.3f, 0.1f, null, 0.40f, 4),   Traits = MPT(false, false, false, true, false, false, false) },
            new() { Slug = "silk",        TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Silk",        RarityWeight = 40, CostScale = 3.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(2.5f, 0.1f, 0.2f, 0.1f, 0.55f, 50),  Traits = MPT(false, false, false, true, false, false, true)  },
            new() { Slug = "shadowsilk",  TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Shadowsilk",  RarityWeight = 15, CostScale = 9.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(4.0f, 0.1f, 0.3f, 0.6f, 0.75f, 200), Traits = MPT(false, false, true,  true, false, false, true)  },
            new() { Slug = "spiderweave", TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Spiderweave", RarityWeight = 8,  CostScale = 16.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.5f, 0.2f, 0.4f, 0.5f, 0.80f, 400), Traits = MPT(false, false, false, true, false, false, true)  },
            new() { Slug = "moonweave",   TypeKey = "fabrics", MaterialFamily = "fabric", DisplayName = "Moonweave",   RarityWeight = 3,  CostScale = 35.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(6.0f, 0.1f, 0.5f, 0.9f, 0.92f, 900), Traits = MPT(false, false, true,  true, false, false, true)  },

            // Scales
            new() { Slug = "fish-scale",   TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Fish Scale",   RarityWeight = 80, CostScale = 0.7f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(3.0f, 0.8f, 0.2f, null, 0.55f, 3),   Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "snake-scale",  TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Snake Scale",  RarityWeight = 60, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(4.5f, 1.0f, 0.3f, 0.1f, 0.68f, 10),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "lizard-scale", TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Lizard Scale", RarityWeight = 45, CostScale = 1.8f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.5f, 1.2f, 0.3f, 0.2f, 0.75f, 22),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "drake-scale",  TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Drake Scale",  RarityWeight = 20, CostScale = 5.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.5f, 1.5f, 0.4f, 0.5f, 0.88f, 100), Traits = MPT(false, false, false, false, false, true,  true)  },
            new() { Slug = "dragon-scale", TypeKey = "scales", MaterialFamily = "scale", DisplayName = "Dragon Scale", RarityWeight = 4,  CostScale = 22.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.5f, 2.0f, 0.6f, 0.9f, 0.97f, 700), Traits = MPT(false, false, true,  false, false, true,  true)  },

            // Chitin
            new() { Slug = "insect-chitin",   TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Insect Chitin",   RarityWeight = 80, CostScale = 0.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(4.0f, 0.9f, 0.1f, null, 0.50f, 2),   Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "carapace",        TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Carapace",        RarityWeight = 55, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(5.5f, 1.1f, 0.2f, null, 0.65f, 8),   Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "hardened-chitin", TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Hardened Chitin", RarityWeight = 30, CostScale = 2.5f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.0f, 1.3f, 0.2f, 0.1f, 0.78f, 30),  Traits = MPT(false, false, false, false, false, false, false) },
            new() { Slug = "venomous-chitin", TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Venomous Chitin", RarityWeight = 15, CostScale = 6.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.5f, 1.4f, 0.3f, 0.3f, 0.82f, 90),  Traits = MPT(false, false, true,  false, false, false, true)  },
            new() { Slug = "ancient-chitin",  TypeKey = "chitin", MaterialFamily = "chitin", DisplayName = "Ancient Chitin",  RarityWeight = 4,  CostScale = 18.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f, 1.6f, 0.4f, 0.6f, 0.93f, 350), Traits = MPT(false, false, true,  false, false, false, true)  },

            // Crystals
            new() { Slug = "quartz-crystal",    TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Quartz Crystal",    RarityWeight = 75, CostScale = 1.2f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(6.0f, 2.6f, 0.5f, 0.3f, 0.70f, 15),   Traits = MPT(false, false, false, false, true, false, true) },
            new() { Slug = "resonance-crystal", TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Resonance Crystal", RarityWeight = 40, CostScale = 3.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(6.5f, 2.8f, 0.8f, 0.6f, 0.78f, 80),   Traits = MPT(true,  false, true,  false, true, false, true) },
            new() { Slug = "arcane-crystal",    TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Arcane Crystal",    RarityWeight = 20, CostScale = 7.0f,  IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(7.5f, 3.0f, 0.9f, 0.9f, 0.85f, 300),  Traits = MPT(true,  false, true,  false, true, false, true) },
            new() { Slug = "void-crystal",      TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Void Crystal",      RarityWeight = 8,  CostScale = 18.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(8.0f, 3.2f, 1.0f, 1.0f, 0.90f, 800),  Traits = MPT(true,  false, true,  false, true, false, true) },
            new() { Slug = "eternal-crystal",   TypeKey = "crystals", MaterialFamily = "crystal", DisplayName = "Eternal Crystal",   RarityWeight = 3,  CostScale = 40.0f, IsActive = true, Version = 1, UpdatedAt = now, Stats = MPS(9.0f, 3.5f, 1.0f, 1.0f, 0.98f, 2000), Traits = MPT(true,  false, true,  false, true, false, true) },
        };

        db.MaterialProperties.AddRange(props);
        await db.SaveChangesAsync();
    }
}
