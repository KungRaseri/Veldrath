using Microsoft.AspNetCore.Identity;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Data;

/// <summary>
/// Orchestrates baseline seed operations across all DbContexts on first startup.
/// Each domain seeder is idempotent — it checks for existing rows before inserting.
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
        => await ApplicationDataSeeder.SeedAsync(db);

    /// <summary>Seeds all content (materials, abilities, items, etc.) into <see cref="ContentDbContext"/>.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        await MaterialsSeeder.SeedAsync(db);
        await ActorSeeder.SeedAsync(db);
        await PowersSeeder.SeedAsync(db);
        await ItemsSeeder.SeedAsync(db);
        await ArchetypeSeeder.SeedAsync(db);
        await RecipesSeeder.SeedAsync(db);
        await LootTablesSeeder.SeedAsync(db);
        await QuestsSeeder.SeedAsync(db);
        await ZoneLocationsSeeder.SeedAsync(db);
        await ActorInstancesSeeder.SeedAsync(db);
        await OrganizationsSeeder.SeedAsync(db);
        await DialogueSeeder.SeedAsync(db);
        await TraitDefinitionsSeeder.SeedAsync(db);
        await LanguagesSeeder.SeedAsync(db);
        await ContentRegistrySeeder.SeedAsync(db);
    }

    /// <summary>
    /// Seeds all RBAC roles and their default permission claims idempotently.
    /// Called by the test host after <c>EnsureCreated()</c> and by the production host
    /// after Postgres migrations are applied.
    /// </summary>
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));

            var role = (await roleManager.FindByNameAsync(roleName))!;
            var existingClaims = await roleManager.GetClaimsAsync(role);
            var existingPermissions = existingClaims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var perm in Roles.DefaultPermissionsFor(roleName))
            {
                if (!existingPermissions.Contains(perm))
                    await roleManager.AddClaimAsync(role, new System.Security.Claims.Claim("permission", perm));
            }
        }
    }

}
