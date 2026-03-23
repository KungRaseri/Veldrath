using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>Seeds baseline <see cref="Organization"/> rows into <see cref="ContentDbContext"/>.</summary>
public static class OrganizationsSeeder
{
    /// <summary>Seeds all organization rows (idempotent).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        if (await db.Organizations.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.Organizations.AddRange(
            new Organization
            {
                Slug         = "iron-anvil-guild",
                TypeKey      = "guilds",
                DisplayName  = "Iron Anvil Guild",
                OrgType      = "guild",
                RarityWeight = 60,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new OrganizationStats
                {
                    MemberCount                 = 50,
                    Wealth                      = 2000,
                    ReputationThresholdFriendly = 100,
                    ReputationThresholdHostile  = -50,
                },
                Traits = new OrganizationTraits
                {
                    Hostile          = false,
                    Joinable         = true,
                    HasShop          = true,
                    QuestGiver       = true,
                    PoliticalFaction = false,
                },
            },
            new Organization
            {
                Slug         = "shadowhand-syndicate",
                TypeKey      = "syndicates",
                DisplayName  = "Shadowhand Syndicate",
                OrgType      = "faction",
                RarityWeight = 30,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new OrganizationStats
                {
                    MemberCount                 = 30,
                    Wealth                      = 5000,
                    ReputationThresholdFriendly = 200,
                    ReputationThresholdHostile  = -100,
                },
                Traits = new OrganizationTraits
                {
                    Hostile          = false,
                    Joinable         = true,
                    HasShop          = true,
                    QuestGiver       = true,
                    PoliticalFaction = true,
                },
            });

        await db.SaveChangesAsync();
    }
}
