using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Weapon"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class WeaponsSeeder
{
    /// <summary>Seeds all weapon rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.Weapons.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Weapons.AddRange(
            new Weapon
            {
                Slug          = "iron-sword",
                TypeKey       = "swords",
                DisplayName   = "Iron Sword",
                WeaponType    = "sword",
                DamageType    = "physical",
                HandsRequired = 1,
                RarityWeight  = 80,
                IsActive      = true,
                Version       = 1,
                UpdatedAt     = now,
                Stats = new WeaponStats
                {
                    DamageMin   = 4,
                    DamageMax   = 8,
                    AttackSpeed = 1.0f,
                    CritChance  = 0.05f,
                    Range       = 1.5f,
                    Weight      = 3.5f,
                    Durability  = 100,
                    Value       = 25,
                },
                Traits = new WeaponTraits
                {
                    TwoHanded = false,
                    Throwable = false,
                    Silvered  = false,
                    Magical   = false,
                    Finesse   = false,
                    Reach     = false,
                    Versatile = true,
                },
            },
            new Weapon
            {
                Slug          = "hunters-bow",
                TypeKey       = "bows",
                DisplayName   = "Hunter's Bow",
                WeaponType    = "bow",
                DamageType    = "physical",
                HandsRequired = 2,
                RarityWeight  = 75,
                IsActive      = true,
                Version       = 1,
                UpdatedAt     = now,
                Stats = new WeaponStats
                {
                    DamageMin   = 3,
                    DamageMax   = 7,
                    AttackSpeed = 0.8f,
                    CritChance  = 0.10f,
                    Range       = 20.0f,
                    Weight      = 2.0f,
                    Durability  = 80,
                    Value       = 35,
                },
                Traits = new WeaponTraits
                {
                    TwoHanded = true,
                    Throwable = false,
                    Silvered  = false,
                    Magical   = false,
                    Finesse   = true,
                    Reach     = false,
                    Versatile = false,
                },
            });

        await db.SaveChangesAsync();
    }
}
