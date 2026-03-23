using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds <see cref="Item"/> and <see cref="Enchantment"/> baseline rows into <see cref="ContentDbContext"/>.</summary>
public static class ItemsSeeder
{
    /// <summary>Seeds all item and enchantment rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        await SeedItemsAsync(db);
        await SeedEnchantmentsAsync(db);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    // ItemStats: weight, stackSize, value, effectPower, duration
    private static ItemStats ISt(float? w, int? ss, int? v, float? ep, float? d) =>
        new() { Weight = w, StackSize = ss, Value = v, EffectPower = ep, Duration = d };

    // ItemTraits: stackable, questItem, unique, soulbound, consumable, magical
    private static ItemTraits ITr(bool? st, bool? qi, bool? un, bool? sb, bool? co, bool? mg) =>
        new() { Stackable = st, QuestItem = qi, Unique = un, Soulbound = sb, Consumable = co, Magical = mg };

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

    // EnchantmentStats: bonusDamage, bonusArmor, bonusStrength, bonusDex, bonusInt, manaCostReduction, attackSpeedBonus, value
    private static EnchantmentStats ESt(int? bd, int? ba, int? bstr, int? bdex, int? bint, float? mcr, float? asb, int? v) =>
        new() { BonusDamage = bd, BonusArmor = ba, BonusStrength = bstr, BonusDexterity = bdex, BonusIntelligence = bint, ManaCostReduction = mcr, AttackSpeedBonus = asb, Value = v };

    // EnchantmentTraits: stackable, exclusive, requiresMagicItem, cursed, permanent
    private static EnchantmentTraits ETr(bool? st, bool? ex, bool? rmi, bool? cu, bool? pe) =>
        new() { Stackable = st, Exclusive = ex, RequiresMagicItem = rmi, Cursed = cu, Permanent = pe };

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

    // ── Items ─────────────────────────────────────────────────────────────────

    private static async Task SeedItemsAsync(ContentDbContext db)
    {
        if (await db.Items.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var items = new List<Item>
        {
            // Consumables — single-use, stackable
            I("health-potion",      "consumables", "consumable", "Health Potion",      90, ISt(0.10f, 20, 15,  50.0f, null),  ITr(true, false, false, false, true, false), now),
            I("mana-potion",        "consumables", "consumable", "Mana Potion",        90, ISt(0.10f, 20, 15,  40.0f, null),  ITr(true, false, false, false, true, false), now),
            I("antidote",           "consumables", "consumable", "Antidote",           85, ISt(0.05f, 10, 10,   1.0f, null),  ITr(true, false, false, false, true, false), now),
            I("elixir-of-strength", "consumables", "consumable", "Elixir of Strength", 40, ISt(0.10f,  5, 80,  10.0f, 300.0f), ITr(true, false, false, false, true, true),  now),

            // Crystals — magical reagents, stackable
            I("soul-crystal", "crystals", "crystal", "Soul Crystal", 50, ISt(0.20f, 50,  25, null, null), ITr(true, false, false, false, false, true), now),
            I("void-shard",   "crystals", "crystal", "Void Shard",   20, ISt(0.50f, 20, 150, null, null), ITr(true, false, false, false, false, true), now),

            // Gems — cut stones used in crafting and socketing
            I("fire-ruby",      "gems", "gem", "Fire Ruby",      25, ISt(0.10f, 1, 200, null, null), ITr(false, false, false, false, false, true), now),
            I("frost-sapphire", "gems", "gem", "Frost Sapphire", 25, ISt(0.10f, 1, 200, null, null), ITr(false, false, false, false, false, true), now),
            I("storm-topaz",    "gems", "gem", "Storm Topaz",    30, ISt(0.10f, 1, 150, null, null), ITr(false, false, false, false, false, true), now),

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
            EE("arcane-binding",  "arcane", "any", "Arcane Binding",  40, ESt(null, null, 3,    3,     3, null,  null, 120), ETr(false, false, true,  false, false), now),
            EE("runic-resonance", "arcane", "any", "Runic Resonance", 15, ESt(null, null, null, null, 10, 0.10f, null, 350), ETr(false, true,  true,  false, true),  now),
        };

        db.Enchantments.AddRange(enchantments);
        await db.SaveChangesAsync();
    }
}
