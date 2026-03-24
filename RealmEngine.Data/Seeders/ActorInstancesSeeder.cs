using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="ActorInstance"/> rows into <see cref="ContentDbContext"/>.</summary>
/// <remarks>
/// Depends on <see cref="ArchetypeSeeder"/> having run first — actor instances require a valid
/// <see cref="ActorArchetype"/> foreign key.
/// </remarks>
public static class ActorInstancesSeeder
{
    /// <summary>Seeds all actor instance rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.ActorInstances.AnyAsync())
            return;

        var goblinScout   = await db.ActorArchetypes.FirstOrDefaultAsync(a => a.Slug == "goblin-scout");
        var banditRuffian = await db.ActorArchetypes.FirstOrDefaultAsync(a => a.Slug == "bandit-ruffian");

        // Skip seeding if archetypes have not been seeded yet.
        if (goblinScout is null || banditRuffian is null)
            return;

        var now = DateTimeOffset.UtcNow;

        db.ActorInstances.AddRange(
            // Boss instances
            new ActorInstance
            {
                Slug            = "elder-goblin-chief",
                TypeKey         = "boss",
                DisplayName     = "Elder Goblin Chief",
                RarityWeight    = 20,
                ArchetypeId     = goblinScout.Id,
                LevelOverride   = 5,
                FactionOverride = "goblin-warband",
                IsActive        = true,
                Version         = 1,
                UpdatedAt       = now,
                StatOverrides = new InstanceStatOverrides
                {
                    Health           = 120,
                    Strength         = 14,
                    Agility          = 10,
                    ArmorClass       = 6,
                    AttackBonus      = 5,
                    Damage           = 12,
                    ExperienceReward = 80,
                    GoldRewardMin    = 10,
                    GoldRewardMax    = 30,
                },
            },

            // Story instances
            new ActorInstance
            {
                Slug            = "captain-blackthorn",
                TypeKey         = "story",
                DisplayName     = "Captain Blackthorn",
                RarityWeight    = 10,
                ArchetypeId     = banditRuffian.Id,
                LevelOverride   = 8,
                FactionOverride = "shadowhand-syndicate",
                IsActive        = true,
                Version         = 1,
                UpdatedAt       = now,
                StatOverrides = new InstanceStatOverrides
                {
                    Health           = 200,
                    Strength         = 16,
                    Agility          = 12,
                    Intelligence     = 10,
                    ArmorClass       = 8,
                    AttackBonus      = 7,
                    Damage           = 18,
                    ExperienceReward = 200,
                    GoldRewardMin    = 30,
                    GoldRewardMax    = 80,
                },
            }
        );

        await db.SaveChangesAsync();
    }
}
