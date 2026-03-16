using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmUnbound.Server.Data;

/// <summary>
/// Seeds the content database with baseline game data on first startup.
/// Each section is idempotent — it checks for existing rows before inserting.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        await SeedMaterialsAsync(db);
        await SeedMaterialPropertiesAsync(db);
        await SeedActorClassesAsync(db);
        await SeedAbilitiesAsync(db);
        await SeedSkillsAsync(db);
        await SeedBackgroundsAsync(db);
        await SeedSpeciesAsync(db);
    }

    // Stat/trait factory helpers — keeps data rows concise.
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
            M("iron",        "metals", "metal",   "Iron",        90, 1.0f, MS(7.5f, 8.0f, 0.6f, null, 5),   MT(false, false, false, true,  false, false), now),
            M("copper",     "metals", "metal", "Copper",     85, 0.8f, MS(4.0f, 9.0f,  0.9f, null, 4),    MT(false, false, false, true,  false, true),  now),
            M("bronze",     "metals", "metal", "Bronze",     75, 1.2f, MS(5.0f, 8.5f,  0.8f, null, 8),    MT(false, false, false, true,  false, false), now),
            M("steel",      "metals", "metal", "Steel",      60, 1.8f, MS(8.0f, 7.8f,  0.7f, null, 20),   MT(false, false, false, true,  false, false), now),
            M("silver",     "metals", "metal", "Silver",     40, 3.0f, MS(5.5f, 10.5f, 1.0f, 0.4f, 50),   MT(false, false, false, true,  false, true),  now),
            M("gold",       "metals", "metal", "Gold",       25, 5.0f, MS(3.0f, 19.3f, 1.0f, 0.3f, 200),  MT(false, false, false, true,  false, true),  now),
            M("mithral",    "metals", "metal", "Mithral",    10, 15.0f,MS(9.0f, 3.0f,  1.2f, 0.8f, 500),  MT(true,  false, false, true,  true,  true),  now),
            M("adamantine", "metals", "metal", "Adamantine", 4,  25.0f,MS(10.0f,5.8f,  0.5f, 0.6f, 1200), MT(true,  false, false, true,  false, false), now),

            // Woods
            M("pine",       "woods", "wood", "Pine",      90, 0.5f, MS(2.0f, 0.5f, 0.1f, null, 1),   MT(false, true,  false, false, false, false), now),
            M("oak",        "woods", "wood", "Oak",       80, 0.8f, MS(4.0f, 0.7f, 0.1f, null, 3),   MT(false, true,  false, false, false, false), now),
            M("birch",      "woods", "wood", "Birch",     70, 0.9f, MS(3.5f, 0.6f, 0.1f, null, 4),   MT(false, true,  false, false, false, false), now),
            M("ash",        "woods", "wood", "Ash",       55, 1.2f, MS(5.0f, 0.8f, 0.2f, null, 8),   MT(false, true,  false, false, false, false), now),
            M("ironwood",   "woods", "wood", "Ironwood",  20, 4.0f, MS(7.5f, 1.2f, 0.2f, 0.2f, 60),  MT(false, true,  false, true,  false, false), now),
            M("ebonwood",   "woods", "wood", "Ebonwood",  8,  12.0f,MS(8.5f, 1.5f, 0.4f, 0.5f, 150), MT(false, false, false, true,  true,  false), now),
            M("dragonwood", "woods", "wood", "Dragonwood",3,  30.0f,MS(9.0f, 2.0f, 0.8f, 0.9f, 600), MT(true,  false, false, true,  true,  true),  now),

            // Leathers
            M("leather",          "leathers", "leather", "Leather",          90, 0.7f, MS(2.0f, 0.5f, 0.1f, null, 3),    MT(false, true,  false, false, false, false), now),
            M("thick-leather",    "leathers", "leather", "Thick Leather",    70, 1.2f, MS(3.5f, 0.7f, 0.1f, null, 8),    MT(false, true,  false, false, false, false), now),
            M("hardened-leather", "leathers", "leather", "Hardened Leather", 50, 1.8f, MS(5.0f, 0.8f, 0.1f, null, 18),   MT(false, true,  false, true,  false, false), now),
            M("scale-leather",    "leathers", "leather", "Scale Leather",    25, 4.0f, MS(6.0f, 1.0f, 0.2f, 0.2f, 60),   MT(false, false, false, true,  false, false), now),
            M("drake-hide",       "leathers", "leather", "Drake Hide",       12, 10.0f,MS(7.0f, 1.2f, 0.3f, 0.4f, 200),  MT(true,  false, false, true,  false, false), now),
            M("dragon-leather",   "leathers", "leather", "Dragon Leather",   3,  30.0f,MS(9.0f, 1.8f, 0.5f, 0.8f, 800),  MT(true,  false, false, true,  true,  false), now),

            // Gems
            M("quartz",   "gems", "gem", "Quartz",   80, 1.0f, MS(7.0f,  2.65f, 0.3f, 0.2f, 10),   MT(false, false, false, true,  false, false), now),
            M("amethyst", "gems", "gem", "Amethyst", 55, 2.0f, MS(7.0f,  2.65f, 0.4f, 0.4f, 40),   MT(false, false, false, true,  false, false), now),
            M("topaz",    "gems", "gem", "Topaz",    40, 3.5f, MS(8.0f,  3.5f,  0.5f, 0.5f, 80),   MT(false, true,  false, true,  false, false), now),
            M("ruby",     "gems", "gem", "Ruby",     20, 7.0f, MS(9.0f,  4.0f,  0.6f, 0.7f, 300),  MT(false, false, false, true,  false, false), now),
            M("sapphire", "gems", "gem", "Sapphire", 18, 8.0f, MS(9.0f,  4.0f,  0.7f, 0.8f, 350),  MT(false, false, false, true,  false, false), now),
            M("emerald",  "gems", "gem", "Emerald",  12, 12.0f,MS(7.5f,  2.7f,  0.5f, 0.9f, 600),  MT(false, false, true,  true,  false, false), now),
            M("diamond",  "gems", "gem", "Diamond",  5,  25.0f,MS(10.0f, 3.5f,  0.2f, 1.0f, 2500), MT(false, false, false, true,  false, false), now),

            // Bones
            M("bone",         "bones", "bone", "Bone",         85, 0.4f, MS(3.0f, 1.8f, 0.1f, null, 1),   MT(false, false, false, false, false, false), now),
            M("thick-bone",   "bones", "bone", "Thick Bone",   65, 0.7f, MS(4.5f, 2.0f, 0.1f, null, 3),   MT(false, false, false, false, false, false), now),
            M("beast-bone",   "bones", "bone", "Beast Bone",   45, 1.5f, MS(5.5f, 2.2f, 0.2f, 0.1f, 12),  MT(false, false, false, false, false, false), now),
            M("monster-bone", "bones", "bone", "Monster Bone", 20, 4.0f, MS(6.5f, 2.5f, 0.3f, 0.3f, 50),  MT(false, false, false, false, false, false), now),
            M("dragon-bone",  "bones", "bone", "Dragon Bone",  4,  20.0f,MS(9.5f, 3.0f, 0.4f, 0.7f, 400), MT(true,  false, false, true,  false, false), now),

            // Fabrics
            M("linen",       "fabrics", "fabric", "Linen",       90, 0.5f, MS(1.0f, 0.2f, 0.1f, null, 2),   MT(false, true, false, false, false, false), now),
            M("wool",        "fabrics", "fabric", "Wool",        80, 0.7f, MS(1.5f, 0.3f, 0.1f, null, 4),   MT(false, true, false, false, false, false), now),
            M("silk",        "fabrics", "fabric", "Silk",        40, 3.0f, MS(2.5f, 0.1f, 0.2f, 0.1f, 50),  MT(false, true, false, true,  false, false), now),
            M("shadowsilk",  "fabrics", "fabric", "Shadowsilk",  15, 9.0f, MS(4.0f, 0.1f, 0.3f, 0.6f, 200), MT(false, true, true,  true,  false, false), now),
            M("spiderweave", "fabrics", "fabric", "Spiderweave", 8,  16.0f,MS(5.5f, 0.2f, 0.4f, 0.5f, 400), MT(false, true, false, true,  false, false), now),
            M("moonweave",   "fabrics", "fabric", "Moonweave",   3,  35.0f,MS(6.0f, 0.1f, 0.5f, 0.9f, 900), MT(false, true, true,  true,  false, false), now),

            // Scales
            M("fish-scale",   "scales", "scale", "Fish Scale",   80, 0.7f, MS(3.0f, 0.8f, 0.2f, null, 3),   MT(false, false, false, false, false, false), now),
            M("snake-scale",  "scales", "scale", "Snake Scale",  60, 1.2f, MS(4.5f, 1.0f, 0.3f, 0.1f, 10),  MT(false, false, false, false, false, false), now),
            M("lizard-scale", "scales", "scale", "Lizard Scale", 45, 1.8f, MS(5.5f, 1.2f, 0.3f, 0.2f, 22),  MT(false, false, false, false, false, false), now),
            M("drake-scale",  "scales", "scale", "Drake Scale",  20, 5.0f, MS(7.5f, 1.5f, 0.4f, 0.5f, 100), MT(true,  false, false, true,  false, false), now),
            M("dragon-scale", "scales", "scale", "Dragon Scale", 4,  22.0f,MS(9.5f, 2.0f, 0.6f, 0.9f, 700), MT(true,  false, false, true,  false, false), now),

            // Chitin
            M("insect-chitin",   "chitin", "chitin", "Insect Chitin",   80, 0.5f, MS(4.0f, 0.9f, 0.1f, null, 2),   MT(false, false, false, false, false, false), now),
            M("carapace",        "chitin", "chitin", "Carapace",        55, 1.2f, MS(5.5f, 1.1f, 0.2f, null, 8),   MT(false, false, false, false, false, false), now),
            M("hardened-chitin", "chitin", "chitin", "Hardened Chitin", 30, 2.5f, MS(7.0f, 1.3f, 0.2f, 0.1f, 30),  MT(false, false, false, false, false, false), now),
            M("venomous-chitin", "chitin", "chitin", "Venomous Chitin", 15, 6.0f, MS(7.5f, 1.4f, 0.3f, 0.3f, 90),  MT(false, false, true,  true,  false, false), now),
            M("ancient-chitin",  "chitin", "chitin", "Ancient Chitin",  4,  18.0f,MS(9.0f, 1.6f, 0.4f, 0.6f, 350), MT(false, false, true,  true,  false, false), now),

            // Crystals
            M("quartz-crystal",    "crystals", "crystal", "Quartz Crystal",    75, 1.2f, MS(6.0f, 2.6f, 0.5f, 0.3f, 15),    MT(false, false, false, true, false, false), now),
            M("resonance-crystal", "crystals", "crystal", "Resonance Crystal", 40, 3.0f, MS(6.5f, 2.8f, 0.8f, 0.6f, 80),    MT(false, false, true,  true, false, false), now),
            M("arcane-crystal",    "crystals", "crystal", "Arcane Crystal",    20, 7.0f, MS(7.5f, 3.0f, 0.9f, 0.9f, 300),   MT(false, false, true,  true, false, false), now),
            M("void-crystal",      "crystals", "crystal", "Void Crystal",      8,  18.0f,MS(8.0f, 3.2f, 1.0f, 1.0f, 800),   MT(false, false, true,  true, false, false), now),
            M("eternal-crystal",   "crystals", "crystal", "Eternal Crystal",   3,  40.0f,MS(9.0f, 3.5f, 1.0f, 1.0f, 2000),  MT(false, false, true,  true, false, false), now),
        };

        db.Materials.AddRange(materials);
        await db.SaveChangesAsync();
    }

    private static Material M(
        string slug, string typeKey, string family, string displayName,
        int rarityWeight, float costScale,
        MaterialStats stats, MaterialTraits traits,
        DateTimeOffset now) => new()
    {
        Slug           = slug,
        TypeKey        = typeKey,
        MaterialFamily = family,
        DisplayName    = displayName,
        RarityWeight   = rarityWeight,
        CostScale      = costScale,
        IsActive       = true,
        Version        = 1,
        UpdatedAt      = now,
        Stats          = stats,
        Traits         = traits,
    };

    // ── Material Properties ───────────────────────────────────────────────────

    private static async Task SeedMaterialPropertiesAsync(ContentDbContext db)
    {
        if (await db.MaterialProperties.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var props = new List<MaterialProperty>
        {
            // Metals
            MP("iron",       "metals", "metal", "Iron",       90, 1.0f, MPS(7.5f, 8.0f,  0.6f, null, 0.80f, 5),    MPT(false, false, false, false, false, false, true), now),
            MP("copper",     "metals", "metal", "Copper",     85, 0.8f, MPS(4.0f, 9.0f,  0.9f, null, 0.60f, 4),    MPT(true,  false, false, false, false, false, true), now),
            MP("bronze",     "metals", "metal", "Bronze",     75, 1.2f, MPS(5.0f, 8.5f,  0.8f, null, 0.70f, 8),    MPT(true,  false, false, false, false, false, true), now),
            MP("steel",      "metals", "metal", "Steel",      60, 1.8f, MPS(8.0f, 7.8f,  0.7f, null, 0.90f, 20),   MPT(false, false, false, false, false, false, true), now),
            MP("silver",     "metals", "metal", "Silver",     40, 3.0f, MPS(5.5f, 10.5f, 1.0f, 0.4f, 0.75f, 50),   MPT(true,  false, false, false, false, false, true), now),
            MP("gold",       "metals", "metal", "Gold",       25, 5.0f, MPS(3.0f, 19.3f, 1.0f, 0.3f, 0.65f, 200),  MPT(true,  false, false, false, false, false, true), now),
            MP("mithral",    "metals", "metal", "Mithral",    10, 15.0f,MPS(9.0f, 3.0f,  1.2f, 0.8f, 0.95f, 500),  MPT(true,  false, true,  false, false, true,  true), now),
            MP("adamantine", "metals", "metal", "Adamantine", 4,  25.0f,MPS(10.0f,5.8f,  0.5f, 0.6f, 0.99f, 1200), MPT(false, false, false, false, false, true,  true), now),

            // Woods
            MP("pine",       "woods", "wood", "Pine",      90, 0.5f, MPS(2.0f, 0.5f, 0.1f, null, 0.40f, 1),  MPT(false, false, false, false, false, false, false), now),
            MP("oak",        "woods", "wood", "Oak",       80, 0.8f, MPS(4.0f, 0.7f, 0.1f, null, 0.65f, 3),  MPT(false, false, false, false, false, false, false), now),
            MP("birch",      "woods", "wood", "Birch",     70, 0.9f, MPS(3.5f, 0.6f, 0.1f, null, 0.60f, 4),  MPT(false, false, false, false, false, false, false), now),
            MP("ash",        "woods", "wood", "Ash",       55, 1.2f, MPS(5.0f, 0.8f, 0.2f, null, 0.72f, 8),  MPT(false, false, false, false, false, false, false), now),
            MP("ironwood",   "woods", "wood", "Ironwood",  20, 4.0f, MPS(7.5f, 1.2f, 0.2f, 0.2f, 0.88f, 60), MPT(false, false, false, false, false, false, true),  now),
            MP("ebonwood",   "woods", "wood", "Ebonwood",  8,  12.0f,MPS(8.5f, 1.5f, 0.4f, 0.5f, 0.93f, 150),MPT(false, false, true,  false, false, false, true),  now),
            MP("dragonwood", "woods", "wood", "Dragonwood",3,  30.0f,MPS(9.0f, 2.0f, 0.8f, 0.9f, 0.98f, 600),MPT(false, false, true,  false, false, true,  true),  now),

            // Leathers
            MP("leather",          "leathers", "leather", "Leather",          90, 0.7f, MPS(2.0f, 0.5f, 0.1f, null, 0.50f, 3),  MPT(false, false, false, true, false, false, false), now),
            MP("thick-leather",    "leathers", "leather", "Thick Leather",    70, 1.2f, MPS(3.5f, 0.7f, 0.1f, null, 0.65f, 8),  MPT(false, false, false, true, false, false, false), now),
            MP("hardened-leather", "leathers", "leather", "Hardened Leather", 50, 1.8f, MPS(5.0f, 0.8f, 0.1f, null, 0.78f, 18), MPT(false, false, false, true, false, false, true),  now),
            MP("scale-leather",    "leathers", "leather", "Scale Leather",    25, 4.0f, MPS(6.0f, 1.0f, 0.2f, 0.2f, 0.82f, 60), MPT(false, false, false, true, false, false, true),  now),
            MP("drake-hide",       "leathers", "leather", "Drake Hide",       12, 10.0f,MPS(7.0f, 1.2f, 0.3f, 0.4f, 0.90f, 200),MPT(false, false, false, true, false, true,  true),  now),
            MP("dragon-leather",   "leathers", "leather", "Dragon Leather",   3,  30.0f,MPS(9.0f, 1.8f, 0.5f, 0.8f, 0.97f, 800),MPT(false, false, true,  true, false, true,  true),  now),

            // Gemstones
            MP("quartz",   "gemstones", "gem", "Quartz",   80, 1.0f, MPS(7.0f,  2.65f, 0.3f, 0.2f, 0.80f, 10),   MPT(false, false, false, false, true, false, true), now),
            MP("amethyst", "gemstones", "gem", "Amethyst", 55, 2.0f, MPS(7.0f,  2.65f, 0.4f, 0.4f, 0.82f, 40),   MPT(false, false, false, false, true, false, true), now),
            MP("topaz",    "gemstones", "gem", "Topaz",    40, 3.5f, MPS(8.0f,  3.5f,  0.5f, 0.5f, 0.85f, 80),   MPT(false, false, false, false, true, false, true), now),
            MP("ruby",     "gemstones", "gem", "Ruby",     20, 7.0f, MPS(9.0f,  4.0f,  0.6f, 0.7f, 0.90f, 300),  MPT(false, false, false, false, true, false, true), now),
            MP("sapphire", "gemstones", "gem", "Sapphire", 18, 8.0f, MPS(9.0f,  4.0f,  0.7f, 0.8f, 0.90f, 350),  MPT(false, false, false, false, true, false, true), now),
            MP("emerald",  "gemstones", "gem", "Emerald",  12, 12.0f,MPS(7.5f,  2.7f,  0.5f, 0.9f, 0.92f, 600),  MPT(false, false, true,  false, true, false, true), now),
            MP("diamond",  "gemstones", "gem", "Diamond",  5,  25.0f,MPS(10.0f, 3.5f,  0.2f, 1.0f, 0.99f, 2500), MPT(false, true,  false, false, true, false, true), now),

            // Bones
            MP("bone",         "bones", "bone", "Bone",         85, 0.4f, MPS(3.0f, 1.8f, 0.1f, null, 0.45f, 1),   MPT(false, false, false, false, false, false, false), now),
            MP("thick-bone",   "bones", "bone", "Thick Bone",   65, 0.7f, MPS(4.5f, 2.0f, 0.1f, null, 0.60f, 3),   MPT(false, false, false, false, false, false, false), now),
            MP("beast-bone",   "bones", "bone", "Beast Bone",   45, 1.5f, MPS(5.5f, 2.2f, 0.2f, 0.1f, 0.72f, 12),  MPT(false, false, false, false, false, false, false), now),
            MP("monster-bone", "bones", "bone", "Monster Bone", 20, 4.0f, MPS(6.5f, 2.5f, 0.3f, 0.3f, 0.82f, 50),  MPT(false, false, false, false, false, false, true),  now),
            MP("dragon-bone",  "bones", "bone", "Dragon Bone",  4,  20.0f,MPS(9.5f, 3.0f, 0.4f, 0.7f, 0.96f, 400), MPT(false, false, true,  false, false, true,  true),  now),

            // Fabrics
            MP("linen",       "fabrics", "fabric", "Linen",       90, 0.5f, MPS(1.0f, 0.2f, 0.1f, null, 0.30f, 2),   MPT(false, false, false, true, false, false, false), now),
            MP("wool",        "fabrics", "fabric", "Wool",        80, 0.7f, MPS(1.5f, 0.3f, 0.1f, null, 0.40f, 4),   MPT(false, false, false, true, false, false, false), now),
            MP("silk",        "fabrics", "fabric", "Silk",        40, 3.0f, MPS(2.5f, 0.1f, 0.2f, 0.1f, 0.55f, 50),  MPT(false, false, false, true, false, false, true),  now),
            MP("shadowsilk",  "fabrics", "fabric", "Shadowsilk",  15, 9.0f, MPS(4.0f, 0.1f, 0.3f, 0.6f, 0.75f, 200), MPT(false, false, true,  true, false, false, true),  now),
            MP("spiderweave", "fabrics", "fabric", "Spiderweave", 8,  16.0f,MPS(5.5f, 0.2f, 0.4f, 0.5f, 0.80f, 400), MPT(false, false, false, true, false, false, true),  now),
            MP("moonweave",   "fabrics", "fabric", "Moonweave",   3,  35.0f,MPS(6.0f, 0.1f, 0.5f, 0.9f, 0.92f, 900), MPT(false, false, true,  true, false, false, true),  now),

            // Scales
            MP("fish-scale",   "scales", "scale", "Fish Scale",   80, 0.7f, MPS(3.0f, 0.8f, 0.2f, null, 0.55f, 3),   MPT(false, false, false, false, false, false, false), now),
            MP("snake-scale",  "scales", "scale", "Snake Scale",  60, 1.2f, MPS(4.5f, 1.0f, 0.3f, 0.1f, 0.68f, 10),  MPT(false, false, false, false, false, false, false), now),
            MP("lizard-scale", "scales", "scale", "Lizard Scale", 45, 1.8f, MPS(5.5f, 1.2f, 0.3f, 0.2f, 0.75f, 22),  MPT(false, false, false, false, false, false, false), now),
            MP("drake-scale",  "scales", "scale", "Drake Scale",  20, 5.0f, MPS(7.5f, 1.5f, 0.4f, 0.5f, 0.88f, 100), MPT(false, false, false, false, false, true,  true),  now),
            MP("dragon-scale", "scales", "scale", "Dragon Scale", 4,  22.0f,MPS(9.5f, 2.0f, 0.6f, 0.9f, 0.97f, 700), MPT(false, false, true,  false, false, true,  true),  now),

            // Chitin
            MP("insect-chitin",   "chitin", "chitin", "Insect Chitin",   80, 0.5f, MPS(4.0f, 0.9f, 0.1f, null, 0.50f, 2),   MPT(false, false, false, false, false, false, false), now),
            MP("carapace",        "chitin", "chitin", "Carapace",        55, 1.2f, MPS(5.5f, 1.1f, 0.2f, null, 0.65f, 8),   MPT(false, false, false, false, false, false, false), now),
            MP("hardened-chitin", "chitin", "chitin", "Hardened Chitin", 30, 2.5f, MPS(7.0f, 1.3f, 0.2f, 0.1f, 0.78f, 30),  MPT(false, false, false, false, false, false, false), now),
            MP("venomous-chitin", "chitin", "chitin", "Venomous Chitin", 15, 6.0f, MPS(7.5f, 1.4f, 0.3f, 0.3f, 0.82f, 90),  MPT(false, false, true,  false, false, false, true),  now),
            MP("ancient-chitin",  "chitin", "chitin", "Ancient Chitin",  4,  18.0f,MPS(9.0f, 1.6f, 0.4f, 0.6f, 0.93f, 350), MPT(false, false, true,  false, false, false, true),  now),

            // Crystals
            MP("quartz-crystal",    "crystals", "crystal", "Quartz Crystal",    75, 1.2f, MPS(6.0f, 2.6f, 0.5f, 0.3f, 0.70f, 15),   MPT(false, false, false, false, true, false, true), now),
            MP("resonance-crystal", "crystals", "crystal", "Resonance Crystal", 40, 3.0f, MPS(6.5f, 2.8f, 0.8f, 0.6f, 0.78f, 80),   MPT(true,  false, true,  false, true, false, true), now),
            MP("arcane-crystal",    "crystals", "crystal", "Arcane Crystal",    20, 7.0f, MPS(7.5f, 3.0f, 0.9f, 0.9f, 0.85f, 300),  MPT(true,  false, true,  false, true, false, true), now),
            MP("void-crystal",      "crystals", "crystal", "Void Crystal",      8,  18.0f,MPS(8.0f, 3.2f, 1.0f, 1.0f, 0.90f, 800),  MPT(true,  false, true,  false, true, false, true), now),
            MP("eternal-crystal",   "crystals", "crystal", "Eternal Crystal",   3,  40.0f,MPS(9.0f, 3.5f, 1.0f, 1.0f, 0.98f, 2000), MPT(true,  false, true,  false, true, false, true), now),
        };

        db.MaterialProperties.AddRange(props);
        await db.SaveChangesAsync();
    }

    private static MaterialProperty MP(
        string slug, string typeKey, string family, string displayName,
        int rarityWeight, float costScale,
        MaterialPropertyStats stats, MaterialPropertyTraits traits,
        DateTimeOffset now) => new()
    {
        Slug           = slug,
        TypeKey        = typeKey,
        MaterialFamily = family,
        DisplayName    = displayName,
        RarityWeight   = rarityWeight,
        CostScale      = costScale,
        IsActive       = true,
        Version        = 1,
        UpdatedAt      = now,
        Stats          = stats,
        Traits         = traits,
    };

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
                TypeKey      = "melee",
                DisplayName  = "Warrior",
                RarityWeight = 50,
                HitDie       = 10,
                PrimaryStat  = "strength",
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ActorClassStats
                {
                    BaseHealth        = 120,
                    BaseMana          = 20,
                    HealthGrowth      = 12.0f,
                    ManaGrowth        = 2.0f,
                    StrengthGrowth    = 2.5f,
                    DexterityGrowth   = 1.0f,
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
                TypeKey      = "caster",
                DisplayName  = "Mage",
                RarityWeight = 40,
                HitDie       = 6,
                PrimaryStat  = "intelligence",
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ActorClassStats
                {
                    BaseHealth        = 60,
                    BaseMana          = 100,
                    HealthGrowth      = 6.0f,
                    ManaGrowth        = 10.0f,
                    StrengthGrowth    = 0.5f,
                    DexterityGrowth   = 0.5f,
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
                TypeKey      = "active",
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
                    RequiresTarget  = true,
                    IsAoe           = false,
                    HasCooldown     = true,
                    IsInstant       = false,
                    IsChanneled     = false,
                    CanCrit         = true,
                    IsPassive       = false,
                    RequiresWeapon  = true,
                },
            },
            // Warrior passive — flat health bonus
            new Ability
            {
                Slug         = "toughness",
                TypeKey      = "passive",
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
                Effects  = new AbilityEffects(),
                Traits   = new AbilityTraits
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
                TypeKey      = "active",
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
                TypeKey      = "passive",
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
                Traits  = new AbilityTraits
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
                TypeKey      = "military",
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
                    Military = true,
                    Noble    = false,
                    Criminal = false,
                    Merchant = false,
                    Scholar  = false,
                    Religious = false,
                    Regional  = false,
                },
            },
            new Background
            {
                Slug         = "scholar",
                TypeKey      = "academic",
                DisplayName  = "Scholar",
                RarityWeight = 45,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new BackgroundStats
                {
                    StartingGold       = 80,
                    BonusIntelligence  = 2,
                    BonusDexterity     = 1,
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
                    Humanoid    = true,
                    Beast       = false,
                    Undead      = false,
                    Demon       = false,
                    Dragon      = false,
                    Elemental   = false,
                    Construct   = false,
                    Darkvision  = false,
                    Aquatic     = false,
                    Flying      = false,
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
                    Beast       = true,
                    Humanoid    = false,
                    Undead      = false,
                    Demon       = false,
                    Dragon      = false,
                    Elemental   = false,
                    Construct   = false,
                    Darkvision  = false,
                    Aquatic     = false,
                    Flying      = false,
                },
            }
        );

        await db.SaveChangesAsync();
    }
}
