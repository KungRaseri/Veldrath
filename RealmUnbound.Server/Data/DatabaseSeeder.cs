using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data;

/// <summary>
/// Seeds the content database with baseline game data on first startup.
/// Each section is idempotent — it checks for existing rows before inserting.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>Seeds world geography (World, Regions, Zones, connections) into <see cref="ApplicationDbContext"/>.</summary>
    public static async Task SeedApplicationDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedApplicationDataAsync(db);
    }

    /// <summary>Seeds world geography (World, Regions, Zones, connections) directly into the supplied <see cref="ApplicationDbContext"/>.</summary>
    public static async Task SeedApplicationDataAsync(ApplicationDbContext db)
    {
        await SeedWorldAsync(db);
        await SeedRegionsAsync(db);
        await SeedRegionConnectionsAsync(db);
        await SeedZonesAsync(db);
        await SeedZoneConnectionsAsync(db);
    }

    /// <summary>Seeds all content (materials, abilities, items, etc.) into <see cref="ContentDbContext"/>.</summary>
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
        await SeedClassAbilityUnlocksAsync(db);
        await SeedSpeciesAbilityPoolsAsync(db);
        await SeedItemsAsync(db);
        await SeedEnchantmentsAsync(db);
        await SeedContentRegistryAsync(db);
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
                TypeKey      = "scholar",
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

    // ── Items ─────────────────────────────────────────────────────────────────

    private static async Task SeedItemsAsync(ContentDbContext db)
    {
        if (await db.Items.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var items = new List<Item>
        {
            // Consumables — single-use, stackable
            I("health-potion",      "consumables", "consumable", "Health Potion",      90, ISt(0.10f, 20, 15, 50.0f, null),   ITr(true, false, false, false, true, false), now),
            I("mana-potion",        "consumables", "consumable", "Mana Potion",        90, ISt(0.10f, 20, 15, 40.0f, null),   ITr(true, false, false, false, true, false), now),
            I("antidote",           "consumables", "consumable", "Antidote",           85, ISt(0.05f, 10, 10,  1.0f, null),   ITr(true, false, false, false, true, false), now),
            I("elixir-of-strength", "consumables", "consumable", "Elixir of Strength", 40, ISt(0.10f,  5, 80, 10.0f, 300.0f), ITr(true, false, false, false, true, true),  now),

            // Crystals — magical reagents, stackable
            I("soul-crystal", "crystals", "crystal", "Soul Crystal", 50, ISt(0.20f, 50,  25, null, null), ITr(true, false, false, false, false, true), now),
            I("void-shard",   "crystals", "crystal", "Void Shard",   20, ISt(0.50f, 20, 150, null, null), ITr(true, false, false, false, false, true), now),

            // Gems — cut stones used in crafting and socketing
            I("fire-ruby",     "gems", "gem", "Fire Ruby",      25, ISt(0.10f, 1, 200, null, null), ITr(false, false, false, false, false, true), now),
            I("frost-sapphire","gems", "gem", "Frost Sapphire", 25, ISt(0.10f, 1, 200, null, null), ITr(false, false, false, false, false, true), now),
            I("storm-topaz",   "gems", "gem", "Storm Topaz",    30, ISt(0.10f, 1, 150, null, null), ITr(false, false, false, false, false, true), now),

            // Runes — inscribable glyphs applied to equipment
            I("rune-of-fire",  "runes", "rune", "Rune of Fire",  60, ISt(0.05f, 10, 30, null, null), ITr(true, false, false, false, false, true), now),
            I("rune-of-frost", "runes", "rune", "Rune of Frost", 60, ISt(0.05f, 10, 30, null, null), ITr(true, false, false, false, false, true), now),
            I("rune-of-power", "runes", "rune", "Rune of Power", 35, ISt(0.05f,  5, 80, null, null), ITr(true, false, false, false, false, true), now),

            // Essences — distilled energy from slain enemies
            I("essence-of-fire",   "essences", "essence", "Essence of Fire",   70, ISt(0.10f, 30, 20, null, null), ITr(true, false, false, false, false, true), now),
            I("essence-of-shadow", "essences", "essence", "Essence of Shadow", 45, ISt(0.10f, 20, 50, null, null), ITr(true, false, false, false, false, true), now),
            I("essence-of-nature", "essences", "essence", "Essence of Nature", 60, ISt(0.10f, 30, 25, null, null), ITr(true, false, false, false, false, true), now),

            // Orbs — rare consumable aura items
            I("orb-of-force", "orbs", "orb", "Orb of Force", 20, ISt(0.80f, 3, 300, 30.0f, 60.0f), ITr(false, false, false, false, true, true), now),
            I("orb-of-decay", "orbs", "orb", "Orb of Decay", 15, ISt(0.90f, 2, 400, 25.0f, 30.0f), ITr(false, false, false, false, true, true), now),
        };

        db.Items.AddRange(items);
        await db.SaveChangesAsync();
    }

    // slug, typeKey, itemType, displayName, rarityWeight, stats, traits, now
    private static Item I(
        string slug, string typeKey, string itemType, string displayName,
        int rarityWeight,
        ItemStats stats, ItemTraits traits,
        DateTimeOffset now) => new()
    {
        Slug         = slug,
        TypeKey      = typeKey,
        ItemType     = itemType,
        DisplayName  = displayName,
        RarityWeight = rarityWeight,
        IsActive     = true,
        Version      = 1,
        UpdatedAt    = now,
        Stats        = stats,
        Traits       = traits,
    };

    // ItemStats: weight, stackSize, value, effectPower, duration
    private static ItemStats ISt(float? w, int? ss, int? v, float? ep, float? d) =>
        new() { Weight = w, StackSize = ss, Value = v, EffectPower = ep, Duration = d };

    // ItemTraits: stackable, questItem, unique, soulbound, consumable, magical
    private static ItemTraits ITr(bool? st, bool? qi, bool? un, bool? sb, bool? co, bool? mg) =>
        new() { Stackable = st, QuestItem = qi, Unique = un, Soulbound = sb, Consumable = co, Magical = mg };

    // ── Enchantments ──────────────────────────────────────────────────────────

    private static async Task SeedEnchantmentsAsync(ContentDbContext db)
    {
        if (await db.Enchantments.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var enchantments = new List<Enchantment>
        {
            // Weapon enchantments — elemental and martial
            EE("flame-strike",        "elemental", "weapon", "Flame Strike",        70, ESt(8,    null, null, null, null, null,  null,  30),  ETr(false, true,  false, false, false), now),
            EE("frostbite-edge",      "elemental", "weapon", "Frostbite Edge",      70, ESt(6,    null, null, null, null, null,  0.10f, 35),  ETr(false, true,  false, false, false), now),
            EE("vorpal-edge",         "martial",   "weapon", "Vorpal Edge",         30, ESt(15,   null, null, null, null, null,  0.15f, 200), ETr(false, true,  false, false, true),  now),
            EE("lightning-conductor", "elemental", "weapon", "Lightning Conductor", 20, ESt(12,   null, null, null, null, null,  0.20f, 250), ETr(false, true,  true,  false, false), now),

            // Armor enchantments — defensive and shadow
            EE("stone-skin",  "defensive", "armor", "Stone Skin",  65, ESt(null, 12,   null, null, null, null, null, 40),  ETr(false, false, false, false, false), now),
            EE("iron-will",   "defensive", "armor", "Iron Will",   45, ESt(null,  8,    4,   null, null, null, null, 80),  ETr(false, false, false, false, false), now),
            EE("shadow-veil", "shadow",    "armor", "Shadow Veil", 20, ESt(null,  5,   null,    8, null, null, null, 180), ETr(false, false, false, false, false), now),

            // Any-slot enchantments — caster-oriented
            EE("arcane-binding",  "arcane", "any", "Arcane Binding",  40, ESt(null, null, 3,    3,  3, null,  null, 120), ETr(false, false, true,  false, false), now),
            EE("runic-resonance", "arcane", "any", "Runic Resonance", 15, ESt(null, null, null, null, 10, 0.10f, null, 350), ETr(false, true,  true,  false, true),  now),
        };

        db.Enchantments.AddRange(enchantments);
        await db.SaveChangesAsync();
    }

    // slug, typeKey, targetSlot, displayName, rarityWeight, stats, traits, now
    private static Enchantment EE(
        string slug, string typeKey, string? targetSlot, string displayName,
        int rarityWeight,
        EnchantmentStats stats, EnchantmentTraits traits,
        DateTimeOffset now) => new()
    {
        Slug         = slug,
        TypeKey      = typeKey,
        TargetSlot   = targetSlot,
        DisplayName  = displayName,
        RarityWeight = rarityWeight,
        IsActive     = true,
        Version      = 1,
        UpdatedAt    = now,
        Stats        = stats,
        Traits       = traits,
    };

    // EnchantmentStats: bonusDamage, bonusArmor, bonusStrength, bonusDex, bonusInt, manaCostReduction, attackSpeedBonus, value
    private static EnchantmentStats ESt(int? bd, int? ba, int? bstr, int? bdex, int? bint, float? mcr, float? asb, int? v) =>
        new() { BonusDamage = bd, BonusArmor = ba, BonusStrength = bstr, BonusDexterity = bdex, BonusIntelligence = bint, ManaCostReduction = mcr, AttackSpeedBonus = asb, Value = v };

    // EnchantmentTraits: stackable, exclusive, requiresMagicItem, cursed, permanent
    private static EnchantmentTraits ETr(bool? st, bool? ex, bool? rmi, bool? cu, bool? pe) =>
        new() { Stackable = st, Exclusive = ex, RequiresMagicItem = rmi, Cursed = cu, Permanent = pe };

    // ── Content Registry ──────────────────────────────────────────────────────

    private static async Task SeedContentRegistryAsync(ContentDbContext db)
    {
        // Collect all entity IDs already registered so we can skip them.
        var registered = await db.ContentRegistry.Select(r => r.EntityId).ToHashSetAsync();

        var entries = new List<ContentRegistry>();

        void Register(Guid id, string tableName, string domain, string typeKey, string slug)
        {
            if (!registered.Contains(id))
                entries.Add(new ContentRegistry { EntityId = id, TableName = tableName, Domain = domain, TypeKey = typeKey, Slug = slug });
        }

        foreach (var e in await db.ActorClasses.AsNoTracking().ToListAsync())
            Register(e.Id, "ActorClasses", "actors/classes", e.TypeKey, e.Slug);

        foreach (var e in await db.Abilities.AsNoTracking().ToListAsync())
            Register(e.Id, "Abilities", "abilities", e.TypeKey, e.Slug);

        foreach (var e in await db.Skills.AsNoTracking().ToListAsync())
            Register(e.Id, "Skills", "actors/skills", e.TypeKey, e.Slug);

        foreach (var e in await db.Backgrounds.AsNoTracking().ToListAsync())
            Register(e.Id, "Backgrounds", "actors/backgrounds", e.TypeKey, e.Slug);

        foreach (var e in await db.Species.AsNoTracking().ToListAsync())
            Register(e.Id, "Species", "actors/species", e.TypeKey, e.Slug);

        foreach (var e in await db.Materials.AsNoTracking().ToListAsync())
            Register(e.Id, "Materials", "items/materials", e.TypeKey, e.Slug);

        foreach (var e in await db.MaterialProperties.AsNoTracking().ToListAsync())
            Register(e.Id, "MaterialProperties", "items/material-properties", e.TypeKey, e.Slug);

        foreach (var e in await db.Items.AsNoTracking().ToListAsync())
            Register(e.Id, "Items", "items/general", e.TypeKey, e.Slug);

        foreach (var e in await db.Enchantments.AsNoTracking().ToListAsync())
            Register(e.Id, "Enchantments", "items/enchantments", e.TypeKey, e.Slug);

        if (entries.Count > 0)
        {
            db.ContentRegistry.AddRange(entries);
            await db.SaveChangesAsync();
        }
    }

    // ── Class Ability Unlocks ───────────────────────────────────────────────

    private static async Task SeedClassAbilityUnlocksAsync(ContentDbContext db)
    {
        if (await db.ClassAbilityUnlocks.AnyAsync())
            return;

        var warrior = await db.ActorClasses.FirstOrDefaultAsync(c => c.Slug == "warrior");
        var mage    = await db.ActorClasses.FirstOrDefaultAsync(c => c.Slug == "mage");

        var powerStrike  = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "power-strike");
        var toughness    = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "toughness");
        var fireball     = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "fireball");
        var arcaneFocus  = await db.Abilities.FirstOrDefaultAsync(a => a.Slug == "arcane-focus");

        if (warrior is null || mage is null || powerStrike is null ||
            toughness is null || fireball is null || arcaneFocus is null)
            return;

        db.ClassAbilityUnlocks.AddRange(
            new ClassAbilityUnlock { ClassId = warrior.Id, AbilityId = powerStrike.Id, LevelRequired = 1, Rank = 1 },
            new ClassAbilityUnlock { ClassId = warrior.Id, AbilityId = toughness.Id,   LevelRequired = 1, Rank = 1 },
            new ClassAbilityUnlock { ClassId = mage.Id,    AbilityId = fireball.Id,     LevelRequired = 1, Rank = 1 },
            new ClassAbilityUnlock { ClassId = mage.Id,    AbilityId = arcaneFocus.Id,  LevelRequired = 1, Rank = 1 }
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

    // ── World ─────────────────────────────────────────────────────────────────

    private static async Task SeedWorldAsync(ApplicationDbContext db)
    {
        if (await db.Worlds.AnyAsync())
            return;

        db.Worlds.Add(new World
        {
            Id          = "draveth",
            Name        = "Draveth",
            Description = "A world of scattered kingdoms, ancient ruins, and contested wilds, shaped by centuries of war and forgotten magic.",
            Era         = "The Age of Embers",
        });

        await db.SaveChangesAsync();
    }

    // ── Regions ───────────────────────────────────────────────────────────────

    private static async Task SeedRegionsAsync(ApplicationDbContext db)
    {
        if (await db.Regions.AnyAsync())
            return;

        db.Regions.AddRange(
            new Region { Id = "thornveil",   Name = "Thornveil",   Description = "A dense Eiraveth forest shrouding ruins of the first kingdoms, where moss-covered paths lead beginners into adventure.", Type = RegionType.Forest,   MinLevel = 0,  MaxLevel = 6,  IsStarter = true,  IsDiscoverable = true, WorldId = "draveth" },
            new Region { Id = "greymoor",    Name = "Greymoor",    Description = "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.",    Type = RegionType.Highland, MinLevel = 5,  MaxLevel = 14, IsStarter = false, IsDiscoverable = true, WorldId = "draveth" },
            new Region { Id = "saltcliff",   Name = "Saltcliff",   Description = "Thysmara sea cliffs battered by storm winds, home to sailors, smugglers, and the drowned remnants of a sunken empire.",  Type = RegionType.Coastal,  MinLevel = 10, MaxLevel = 20, IsStarter = false, IsDiscoverable = true, WorldId = "draveth" },
            new Region { Id = "cinderplain", Name = "Cinderplain", Description = "Kaldrek's scorched expanse of ash and cooled lava, where forge-tempered warbands and fire-touched creatures stake their claim.", Type = RegionType.Volcanic, MinLevel = 18, MaxLevel = 30, IsStarter = false, IsDiscoverable = true, WorldId = "draveth" }
        );

        await db.SaveChangesAsync();
    }

    // ── Region Connections ────────────────────────────────────────────────────

    private static async Task SeedRegionConnectionsAsync(ApplicationDbContext db)
    {
        if (await db.RegionConnections.AnyAsync())
            return;

        db.RegionConnections.AddRange(
            new RegionConnection { FromRegionId = "thornveil",   ToRegionId = "greymoor"    },
            new RegionConnection { FromRegionId = "greymoor",    ToRegionId = "thornveil"   },
            new RegionConnection { FromRegionId = "greymoor",    ToRegionId = "saltcliff"   },
            new RegionConnection { FromRegionId = "saltcliff",   ToRegionId = "greymoor"    },
            new RegionConnection { FromRegionId = "greymoor",    ToRegionId = "cinderplain" },
            new RegionConnection { FromRegionId = "cinderplain", ToRegionId = "greymoor"    }
        );

        await db.SaveChangesAsync();
    }

    // ── Zones ─────────────────────────────────────────────────────────────────

    private static async Task SeedZonesAsync(ApplicationDbContext db)
    {
        if (await db.Zones.AnyAsync())
            return;

        db.Zones.AddRange(
            // ── Thornveil ──────────────────────────────────────────────────────────
            new Zone { Id = "fenwick-crossing",  Name = "Fenwick's Crossing",    Description = "A well-worn crossroads inn and market at the forest's edge, where new adventurers take their first uncertain steps.",              Type = ZoneType.Town,       MinLevel = 0,  MaxPlayers = 0, IsStarter = true,  HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "thornveil"   },
            new Zone { Id = "greenveil-paths",   Name = "The Greenveil Paths",   Description = "Winding root-bound trails beneath a canopy of ancient oaks, alive with sprites and overly curious wildlife.",                    Type = ZoneType.Wilderness, MinLevel = 1,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "thornveil"   },
            new Zone { Id = "thornveil-hollow",  Name = "Thornveil Hollow",      Description = "A shaded hollow deep in the forest where old Eiraveth wards have begun to fail and darker things stir.",                         Type = ZoneType.Wilderness, MinLevel = 3,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "thornveil"   },
            new Zone { Id = "verdant-barrow",    Name = "The Verdant Barrow",    Description = "A barrow complex reclaimed by roots and bioluminescent fungi, prowled by the restless dead of a forgotten village.",              Type = ZoneType.Dungeon,    MinLevel = 4,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "thornveil"   },
            // ── Greymoor ───────────────────────────────────────────────────────────
            new Zone { Id = "aldenmere",         Name = "Aldenmere",             Description = "A grey-stoned Dravan waytown built atop old foundations, known for its mead, its mercenaries, and its many secrets.",             Type = ZoneType.Town,       MinLevel = 5,  MaxPlayers = 0, IsStarter = false, HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            new Zone { Id = "pale-moor",         Name = "The Pale Moor",         Description = "Endless fog-blanketed moors where waymarks shift overnight and travellers learn quickly to trust local guides.",                   Type = ZoneType.Wilderness, MinLevel = 7,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            new Zone { Id = "soddenfen",         Name = "Soddenfen",             Description = "A waterlogged fenland buzzing with plague insects and half-submerged ruins, avoided by all with good sense.",                     Type = ZoneType.Wilderness, MinLevel = 9,  MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            new Zone { Id = "barrow-deeps",      Name = "The Barrow Deeps",      Description = "A sprawling underground burial network carved through the moorland, where ancestor-beasts guard ancient Dravan relics.",           Type = ZoneType.Dungeon,    MinLevel = 11, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "greymoor"    },
            // ── Saltcliff ──────────────────────────────────────────────────────────
            new Zone { Id = "tolvaren",          Name = "Tolvaren",              Description = "A salt-caked Thysmara port city clinging to the cliff face, its harbour full of storm-worn ships and bold-faced traders.",         Type = ZoneType.Town,       MinLevel = 10, MaxPlayers = 0, IsStarter = false, HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            new Zone { Id = "tidewrack-flats",   Name = "The Tidewrack Flats",   Description = "Tide-scoured mudflats littered with shipwreck timber and kelp-draped bones, hunted by scavengers and wading predators.",          Type = ZoneType.Wilderness, MinLevel = 12, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            new Zone { Id = "saltcliff-heights", Name = "The Saltcliff Heights", Description = "Wind-blasted clifftops above the Thysmara sea, dotted with lighthouse ruins and contested by rival gull-rider clans.",            Type = ZoneType.Wilderness, MinLevel = 14, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            new Zone { Id = "sunken-name",       Name = "The Sunken Name",       Description = "The drowned heart of a once-great Thysmara empire, now a flooded ruin accessible only to the brave and the waterproofed.",         Type = ZoneType.Dungeon,    MinLevel = 16, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "saltcliff"   },
            // ── Cinderplain ────────────────────────────────────────────────────────
            new Zone { Id = "skarhold",          Name = "Skarhold",              Description = "A Kaldrek forge-city built inside a dormant caldera, where the best smiths in Draveth ply their trade under sulphur skies.",      Type = ZoneType.Town,       MinLevel = 18, MaxPlayers = 0, IsStarter = false, HasInn = true,  HasMerchant = true,  IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "ashfields",         Name = "The Ashfields",         Description = "Grey ash plains stretching to the horizon, dotted with fused obsidian trees and the scorched skulls of fallen armies.",            Type = ZoneType.Wilderness, MinLevel = 20, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "smoldering-reach",  Name = "The Smoldering Reach",  Description = "A cracked lava field where vents of superheated gas erupt without warning and heat-adapted predators hunt at dusk.",               Type = ZoneType.Wilderness, MinLevel = 23, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" },
            new Zone { Id = "kaldrek-maw",       Name = "Kaldrek's Maw",         Description = "A vast volcanic vent complex known as the Maw, lair of Kaldrek's fire-bound ancient and final test of the worthy.",               Type = ZoneType.Dungeon,    MinLevel = 26, MaxPlayers = 0, IsStarter = false, HasInn = false, HasMerchant = false, IsPvpEnabled = false, IsDiscoverable = true, RegionId = "cinderplain" }
        );

        await db.SaveChangesAsync();
    }

    // ── Zone Connections ──────────────────────────────────────────────────────

    private static async Task SeedZoneConnectionsAsync(ApplicationDbContext db)
    {
        if (await db.ZoneConnections.AnyAsync())
            return;

        db.ZoneConnections.AddRange(
            // ── Thornveil internal ─────────────────────────────────────────────────
            new ZoneConnection { FromZoneId = "fenwick-crossing",  ToZoneId = "greenveil-paths"  },
            new ZoneConnection { FromZoneId = "greenveil-paths",   ToZoneId = "fenwick-crossing" },
            new ZoneConnection { FromZoneId = "greenveil-paths",   ToZoneId = "thornveil-hollow" },
            new ZoneConnection { FromZoneId = "thornveil-hollow",  ToZoneId = "greenveil-paths"  },
            new ZoneConnection { FromZoneId = "thornveil-hollow",  ToZoneId = "verdant-barrow"   },
            new ZoneConnection { FromZoneId = "verdant-barrow",    ToZoneId = "thornveil-hollow" },
            // ── Thornveil → Greymoor border ────────────────────────────────────────
            new ZoneConnection { FromZoneId = "thornveil-hollow",  ToZoneId = "aldenmere"        },
            new ZoneConnection { FromZoneId = "aldenmere",         ToZoneId = "thornveil-hollow" },
            // ── Greymoor internal ──────────────────────────────────────────────────
            new ZoneConnection { FromZoneId = "aldenmere",         ToZoneId = "pale-moor"        },
            new ZoneConnection { FromZoneId = "pale-moor",         ToZoneId = "aldenmere"        },
            new ZoneConnection { FromZoneId = "pale-moor",         ToZoneId = "soddenfen"        },
            new ZoneConnection { FromZoneId = "soddenfen",         ToZoneId = "pale-moor"        },
            new ZoneConnection { FromZoneId = "soddenfen",         ToZoneId = "barrow-deeps"     },
            new ZoneConnection { FromZoneId = "barrow-deeps",      ToZoneId = "soddenfen"        },
            // ── Greymoor → Saltcliff border ────────────────────────────────────────
            new ZoneConnection { FromZoneId = "soddenfen",         ToZoneId = "tolvaren"         },
            new ZoneConnection { FromZoneId = "tolvaren",          ToZoneId = "soddenfen"        },
            // ── Saltcliff internal ─────────────────────────────────────────────────
            new ZoneConnection { FromZoneId = "tolvaren",          ToZoneId = "tidewrack-flats"  },
            new ZoneConnection { FromZoneId = "tidewrack-flats",   ToZoneId = "tolvaren"         },
            new ZoneConnection { FromZoneId = "tidewrack-flats",   ToZoneId = "saltcliff-heights"},
            new ZoneConnection { FromZoneId = "saltcliff-heights", ToZoneId = "tidewrack-flats"  },
            new ZoneConnection { FromZoneId = "saltcliff-heights", ToZoneId = "sunken-name"      },
            new ZoneConnection { FromZoneId = "sunken-name",       ToZoneId = "saltcliff-heights"},
            // ── Greymoor → Cinderplain border ──────────────────────────────────────
            new ZoneConnection { FromZoneId = "barrow-deeps",      ToZoneId = "skarhold"         },
            new ZoneConnection { FromZoneId = "skarhold",          ToZoneId = "barrow-deeps"     },
            // ── Cinderplain internal ───────────────────────────────────────────────
            new ZoneConnection { FromZoneId = "skarhold",          ToZoneId = "ashfields"        },
            new ZoneConnection { FromZoneId = "ashfields",         ToZoneId = "skarhold"         },
            new ZoneConnection { FromZoneId = "ashfields",         ToZoneId = "smoldering-reach" },
            new ZoneConnection { FromZoneId = "smoldering-reach",  ToZoneId = "ashfields"        },
            new ZoneConnection { FromZoneId = "smoldering-reach",  ToZoneId = "kaldrek-maw"      },
            new ZoneConnection { FromZoneId = "kaldrek-maw",       ToZoneId = "smoldering-reach" }
        );

        await db.SaveChangesAsync();
    }
}
