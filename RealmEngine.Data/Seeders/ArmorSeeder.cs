using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Armor"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class ArmorSeeder
{
    /// <summary>Seeds all armor rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.Armors.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Armors.AddRange(
            new Armor
            {
                Slug         = "leather-cap",
                TypeKey      = "light-armor",
                DisplayName  = "Leather Cap",
                ArmorType    = "light",
                EquipSlot    = "head",
                RarityWeight = 85,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArmorStats
                {
                    ArmorRating     = 2,
                    MagicResist     = 0,
                    Weight          = 0.5f,
                    Durability      = 60,
                    MovementPenalty = 0.0f,
                    Value           = 12,
                },
                Traits = new ArmorTraits
                {
                    StealthPenalty   = false,
                    FireResist       = false,
                    ColdResist       = false,
                    LightningResist  = false,
                    Cursed           = false,
                    Magical          = false,
                },
            },
            new Armor
            {
                Slug         = "iron-chestplate",
                TypeKey      = "heavy-armor",
                DisplayName  = "Iron Chestplate",
                ArmorType    = "heavy",
                EquipSlot    = "chest",
                RarityWeight = 70,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new ArmorStats
                {
                    ArmorRating     = 8,
                    MagicResist     = 1,
                    Weight          = 15.0f,
                    Durability      = 150,
                    MovementPenalty = 0.1f,
                    Value           = 80,
                },
                Traits = new ArmorTraits
                {
                    StealthPenalty  = true,
                    FireResist      = false,
                    ColdResist      = false,
                    LightningResist = false,
                    Cursed          = false,
                    Magical         = false,
                },
            });

        await db.SaveChangesAsync();
    }
}
