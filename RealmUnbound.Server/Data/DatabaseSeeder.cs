using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using RealmUnbound.Server.Data.Seeders;

namespace RealmUnbound.Server.Data;

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
        await ItemsSeeder.SeedAsync(db);
        await ContentRegistrySeeder.SeedAsync(db);
    }

}
