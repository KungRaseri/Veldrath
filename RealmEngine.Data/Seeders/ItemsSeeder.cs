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

    // Factory helpers
    // ItemStats: weight, stackSize, value, effectPower, duration
    private static ItemStats ISt(float? w, int? ss, int? v, float? ep, float? d) =>
        new() { Weight = w, StackSize = ss, Value = v, EffectPower = ep, Duration = d };

    // ItemTraits: stackable, questItem, unique, soulbound, consumable, magical
    private static ItemTraits ITr(bool? st, bool? qi, bool? un, bool? sb, bool? co, bool? mg) =>
        new() { Stackable = st, QuestItem = qi, Unique = un, Soulbound = sb, Consumable = co, Magical = mg };

    // EnchantmentTraits: stackable, exclusive, requiresMagicItem, cursed, permanent
    private static EnchantmentTraits ETr(bool? st, bool? ex, bool? rmi, bool? cu, bool? pe) =>
        new() { Stackable = st, Exclusive = ex, RequiresMagicItem = rmi, Cursed = cu, Permanent = pe };

    // Items
    private static async Task SeedItemsAsync(ContentDbContext db)
    {
        if (await db.Items.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var items = new List<Item>
        {
            // Consumables — single-use, stackable
            new() { Slug = "health-potion",      TypeKey = "consumables", ItemType = "consumable", DisplayName = "Health Potion",      RarityWeight = 90, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 20, 15,  50.0f, null),  Traits = ITr(true, false, false, false, true, false) },
            new() { Slug = "mana-potion",        TypeKey = "consumables", ItemType = "consumable", DisplayName = "Mana Potion",        RarityWeight = 90, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 20, 15,  40.0f, null),  Traits = ITr(true, false, false, false, true, false) },
            new() { Slug = "antidote",           TypeKey = "consumables", ItemType = "consumable", DisplayName = "Antidote",           RarityWeight = 85, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.05f, 10, 10,   1.0f, null),  Traits = ITr(true, false, false, false, true, false) },
            new() { Slug = "elixir-of-strength", TypeKey = "consumables", ItemType = "consumable", DisplayName = "Elixir of Strength", RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f,  5, 80,  10.0f, 300.0f), Traits = ITr(true, false, false, false, true, true)  },

            // Crystals — magical reagents, stackable
            new() { Slug = "soul-crystal", TypeKey = "crystals", ItemType = "crystal", DisplayName = "Soul Crystal", RarityWeight = 50, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.20f, 50,  25, null, null), Traits = ITr(true, false, false, false, false, true) },
            new() { Slug = "void-shard",   TypeKey = "crystals", ItemType = "crystal", DisplayName = "Void Shard",   RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.50f, 20, 150, null, null), Traits = ITr(true, false, false, false, false, true) },

            // Gems — cut stones used in crafting and socketing
            new() { Slug = "fire-ruby",      TypeKey = "gems", ItemType = "gem", DisplayName = "Fire Ruby",      RarityWeight = 25, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 1, 200, null, null), Traits = ITr(false, false, false, false, false, true) },
            new() { Slug = "frost-sapphire", TypeKey = "gems", ItemType = "gem", DisplayName = "Frost Sapphire", RarityWeight = 25, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 1, 200, null, null), Traits = ITr(false, false, false, false, false, true) },
            new() { Slug = "storm-topaz",    TypeKey = "gems", ItemType = "gem", DisplayName = "Storm Topaz",    RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 1, 150, null, null), Traits = ITr(false, false, false, false, false, true) },

            // Runes — inscribable glyphs applied to equipment
            new() { Slug = "rune-of-fire",  TypeKey = "runes", ItemType = "rune", DisplayName = "Rune of Fire",  RarityWeight = 60, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.05f, 10, 30, null, null), Traits = ITr(true, false, false, false, false, true) },
            new() { Slug = "rune-of-frost", TypeKey = "runes", ItemType = "rune", DisplayName = "Rune of Frost", RarityWeight = 60, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.05f, 10, 30, null, null), Traits = ITr(true, false, false, false, false, true) },
            new() { Slug = "rune-of-power", TypeKey = "runes", ItemType = "rune", DisplayName = "Rune of Power", RarityWeight = 35, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.05f,  5, 80, null, null), Traits = ITr(true, false, false, false, false, true) },

            // Essences — distilled energy from slain enemies
            new() { Slug = "essence-of-fire",   TypeKey = "essences", ItemType = "essence", DisplayName = "Essence of Fire",   RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 30, 20, null, null), Traits = ITr(true, false, false, false, false, true) },
            new() { Slug = "essence-of-shadow", TypeKey = "essences", ItemType = "essence", DisplayName = "Essence of Shadow", RarityWeight = 45, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 20, 50, null, null), Traits = ITr(true, false, false, false, false, true) },
            new() { Slug = "essence-of-nature", TypeKey = "essences", ItemType = "essence", DisplayName = "Essence of Nature", RarityWeight = 60, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.10f, 30, 25, null, null), Traits = ITr(true, false, false, false, false, true) },

            // Orbs — rare consumable aura items
            new() { Slug = "orb-of-force", TypeKey = "orbs", ItemType = "orb", DisplayName = "Orb of Force", RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.80f, 3, 300, 30.0f, 60.0f), Traits = ITr(false, false, false, false, true, true) },
            new() { Slug = "orb-of-decay", TypeKey = "orbs", ItemType = "orb", DisplayName = "Orb of Decay", RarityWeight = 15, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.90f, 2, 400, 25.0f, 30.0f), Traits = ITr(false, false, false, false, true, true) },

            // Scrolls — single-use magical documents
            new() { Slug = "scroll-of-fireball", TypeKey = "scrolls", ItemType = "scroll", DisplayName = "Scroll of Fireball", RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(0.05f, 5, 60, 25.0f, null), Traits = ITr(true, false, false, false, true, true) },

            // Crafting components — refined from raw materials
            new() { Slug = "iron-ingot", TypeKey = "crafting-components", ItemType = "component", DisplayName = "Iron Ingot", RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now, Stats = ISt(1.0f, 50, 5, null, null), Traits = ITr(true, false, false, false, false, false) },

            // Weapons — ItemType="weapon", TypeKey matches WeaponCategoryToProficiencies keys
            new()
            {
                Slug = "iron-sword", TypeKey = "heavy-blades", ItemType = "weapon", DisplayName = "Iron Sword",
                WeaponType = "sword", DamageType = "physical", HandsRequired = 1,
                RarityWeight = 80, IsActive = true, Version = 1, UpdatedAt = now,
                Stats = new() { Weight = 3.5f, Value = 25, DamageMin = 4, DamageMax = 8, AttackSpeed = 1.0f, Durability = 100 },
                Traits = new() { Versatile = true },
            },
            new()
            {
                Slug = "hunters-bow", TypeKey = "bows", ItemType = "weapon", DisplayName = "Hunter's Bow",
                WeaponType = "bow", DamageType = "physical", HandsRequired = 2,
                RarityWeight = 75, IsActive = true, Version = 1, UpdatedAt = now,
                Stats = new() { Weight = 2.0f, Value = 35, DamageMin = 3, DamageMax = 7, AttackSpeed = 0.8f, Durability = 80 },
                Traits = new() { TwoHanded = true },
            },

            // Armor — ItemType="armor", TypeKey matches ArmorCategoryToProficiencies keys
            new()
            {
                Slug = "leather-cap", TypeKey = "light", ItemType = "armor", DisplayName = "Leather Cap",
                ArmorType = "light", EquipSlot = "head",
                RarityWeight = 85, IsActive = true, Version = 1, UpdatedAt = now,
                Stats = new() { Weight = 0.5f, Value = 12, ArmorRating = 2, Durability = 60 },
                Traits = new() { StealthPenalty = false },
            },
            new()
            {
                Slug = "iron-chestplate", TypeKey = "heavy", ItemType = "armor", DisplayName = "Iron Chestplate",
                ArmorType = "heavy", EquipSlot = "chest",
                RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now,
                Stats = new() { Weight = 15.0f, Value = 80, ArmorRating = 8, Durability = 150, MovementPenalty = 0.1f },
                Traits = new() { StealthPenalty = true },
            },
        };

        db.Items.AddRange(items);
        await db.SaveChangesAsync();
    }

    // Enchantments
    private static async Task SeedEnchantmentsAsync(ContentDbContext db)
    {
        if (await db.Enchantments.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var enchantments = new List<Enchantment>
        {
            // Weapon enchantments — elemental and martial
            new() { Slug = "flame-strike",        TypeKey = "elemental", TargetSlot = "weapon", DisplayName = "Flame Strike",        RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = 8,  BonusArmor = null, BonusStrength = null, BonusDexterity = null, BonusIntelligence = null, ManaCostReduction = null,  AttackSpeedBonus = null,  Value = 30  }, Traits = ETr(false, true,  false, false, false) },
            new() { Slug = "frostbite-edge",      TypeKey = "elemental", TargetSlot = "weapon", DisplayName = "Frostbite Edge",      RarityWeight = 70, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = 6,  BonusArmor = null, BonusStrength = null, BonusDexterity = null, BonusIntelligence = null, ManaCostReduction = null,  AttackSpeedBonus = 0.10f, Value = 35  }, Traits = ETr(false, true,  false, false, false) },
            new() { Slug = "vorpal-edge",         TypeKey = "martial",   TargetSlot = "weapon", DisplayName = "Vorpal Edge",         RarityWeight = 30, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = 15, BonusArmor = null, BonusStrength = null, BonusDexterity = null, BonusIntelligence = null, ManaCostReduction = null,  AttackSpeedBonus = 0.15f, Value = 200 }, Traits = ETr(false, true,  false, false, true)  },
            new() { Slug = "lightning-conductor", TypeKey = "elemental", TargetSlot = "weapon", DisplayName = "Lightning Conductor", RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = 12, BonusArmor = null, BonusStrength = null, BonusDexterity = null, BonusIntelligence = null, ManaCostReduction = null,  AttackSpeedBonus = 0.20f, Value = 250 }, Traits = ETr(false, true,  true,  false, false) },

            // Armor enchantments — defensive and shadow
            new() { Slug = "stone-skin",  TypeKey = "defensive", TargetSlot = "armor", DisplayName = "Stone Skin",  RarityWeight = 65, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = null, BonusArmor = 12, BonusStrength = null, BonusDexterity = null, BonusIntelligence = null, ManaCostReduction = null, AttackSpeedBonus = null, Value = 40  }, Traits = ETr(false, false, false, false, false) },
            new() { Slug = "iron-will",   TypeKey = "defensive", TargetSlot = "armor", DisplayName = "Iron Will",   RarityWeight = 45, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = null, BonusArmor =  8, BonusStrength = 4,    BonusDexterity = null, BonusIntelligence = null, ManaCostReduction = null, AttackSpeedBonus = null, Value = 80  }, Traits = ETr(false, false, false, false, false) },
            new() { Slug = "shadow-veil", TypeKey = "shadow",    TargetSlot = "armor", DisplayName = "Shadow Veil", RarityWeight = 20, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = null, BonusArmor =  5, BonusStrength = null, BonusDexterity = 8,    BonusIntelligence = null, ManaCostReduction = null, AttackSpeedBonus = null, Value = 180 }, Traits = ETr(false, false, false, false, false) },

            // Any-slot enchantments — caster-oriented
            new() { Slug = "arcane-binding",  TypeKey = "arcane", TargetSlot = "any", DisplayName = "Arcane Binding",  RarityWeight = 40, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = null, BonusArmor = null, BonusStrength = 3,    BonusDexterity = 3,    BonusIntelligence = 3,  ManaCostReduction = null,  AttackSpeedBonus = null, Value = 120 }, Traits = ETr(false, false, true,  false, false) },
            new() { Slug = "runic-resonance", TypeKey = "arcane", TargetSlot = "any", DisplayName = "Runic Resonance", RarityWeight = 15, IsActive = true, Version = 1, UpdatedAt = now, Stats = new() { BonusDamage = null, BonusArmor = null, BonusStrength = null, BonusDexterity = null, BonusIntelligence = 10, ManaCostReduction = 0.10f, AttackSpeedBonus = null, Value = 350 }, Traits = ETr(false, true,  true,  false, true)  },
        };

        db.Enchantments.AddRange(enchantments);
        await db.SaveChangesAsync();
    }
}
