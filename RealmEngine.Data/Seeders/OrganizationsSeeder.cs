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
        var now = DateTimeOffset.UtcNow;
        var existing = await db.Organizations.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var missing = GetAllOrganizations(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.Organizations.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static Organization[] GetAllOrganizations(DateTimeOffset now) =>
    [
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
            },
            // The goblin packs of the Ashlen Wood and the Droveway answer to a chief, not to a syndicate.
            // Organization has no description column, so that sentence lives here; the slug is what the elder
            // goblin chief's FactionOverride resolves against.
            new Organization
            {
                Slug         = "goblin-warband",
                TypeKey      = "warbands",
                DisplayName  = "Goblin Warband",
                OrgType      = "faction",
                RarityWeight = 40,
                IsActive     = true,
                Version      = 1,
                UpdatedAt    = now,
                Stats = new OrganizationStats
                {
                    MemberCount                 = 40,
                    Wealth                      = 300,
                    ReputationThresholdFriendly = 50,
                    ReputationThresholdHostile  = -20,
                },
                Traits = new OrganizationTraits
                {
                    Hostile          = true,
                    Joinable         = false,
                    HasShop          = false,
                    QuestGiver       = false,
                    PoliticalFaction = false,
                },
            }
    ];
}
