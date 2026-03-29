using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="LootTable"/> rows (with their <see cref="LootTableEntry"/> lines) into <see cref="ContentDbContext"/>.</summary>
public static class LootTablesSeeder
{
    /// <summary>Seeds all loot table rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.LootTables.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllLootTables(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.LootTables.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static LootTable[] GetAllLootTables(DateTimeOffset now) =>
    [
            // Enemy drops
            new LootTable
            {
                Slug         = "goblin-common-drops",
                TypeKey      = "enemies",
                DisplayName  = "Goblin Common Drops",
                RarityWeight = 80,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Traits = new LootTableTraits
                {
                    Common = true,
                    Boss   = false,
                    Elite  = false,
                    Rare   = false,
                    IsChest      = false,
                    IsHarvesting = false,
                },
                Entries =
                [
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "health-potion",
                        DropWeight  = 60,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = false,
                    },
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "iron-ingot",
                        DropWeight  = 40,
                        QuantityMin = 1,
                        QuantityMax = 2,
                        IsGuaranteed = false,
                    },
                ],
            },

            new LootTable
            {
                Slug         = "wolf-common-drops",
                TypeKey      = "enemies",
                DisplayName  = "Wolf Common Drops",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Traits = new LootTableTraits
                {
                    Common = true,
                    Boss   = false,
                    Elite  = false,
                    Rare   = false,
                    IsChest      = false,
                    IsHarvesting = false,
                },
                Entries =
                [
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "essence-of-nature",
                        DropWeight  = 70,
                        QuantityMin = 1,
                        QuantityMax = 2,
                        IsGuaranteed = false,
                    },
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "health-potion",
                        DropWeight  = 30,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = false,
                    },
                ],
            },

            // Chest loot
            new LootTable
            {
                Slug         = "forest-chest-loot",
                TypeKey      = "chests",
                DisplayName  = "Forest Chest Loot",
                RarityWeight = 50,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Traits = new LootTableTraits
                {
                    Common = true,
                    Boss   = false,
                    Elite  = false,
                    Rare   = false,
                    IsChest      = true,
                    IsHarvesting = false,
                },
                Entries =
                [
                    new LootTableEntry
                    {
                        ItemDomain   = "items/general",
                        ItemSlug     = "health-potion",
                        DropWeight   = 80,
                        QuantityMin  = 1,
                        QuantityMax  = 2,
                        IsGuaranteed = true,
                    },
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "fire-ruby",
                        DropWeight  = 15,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = false,
                    },
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "rune-of-fire",
                        DropWeight  = 25,
                        QuantityMin = 1,
                        QuantityMax = 2,
                        IsGuaranteed = false,
                    },
                ],
            },

            // Boss drops
            new LootTable
            {
                Slug         = "bandit-boss-drops",
                TypeKey      = "enemies",
                DisplayName  = "Bandit Boss Drops",
                RarityWeight = 30,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Traits = new LootTableTraits
                {
                    Common = false,
                    Boss   = true,
                    Elite  = false,
                    Rare   = true,
                    IsChest      = false,
                    IsHarvesting = false,
                },
                Entries =
                [
                    new LootTableEntry
                    {
                        ItemDomain   = "items/general",
                        ItemSlug     = "iron-sword",
                        DropWeight   = 100,
                        QuantityMin  = 1,
                        QuantityMax  = 1,
                        IsGuaranteed = true,
                    },
                    new LootTableEntry
                    {
                        ItemDomain  = "items/general",
                        ItemSlug    = "void-shard",
                        DropWeight  = 20,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = false,
                    },
                ],
            }
    ];
}
