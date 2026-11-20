using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// A throwaway content catalog seeded with the tracked seeders, for validating a pack without a database.
/// </summary>
/// <remarks>
/// A pack references seeded content by design — a quest points at the sword a smith sells, an archetype at the loot
/// table it drops — so validating against only the pack's own slugs would report every such reference as dangling and
/// teach authors to ignore the checker. Seeding an in-process SQLite-free provider with the same
/// <see cref="RealmEngine.Data.Seeders"/> the server boots with gives the CI guard the real catalogue with no service
/// to start and no credentials to hold.
/// <para>
/// The provider is EF Core's in-memory one: validation only reads slugs, and the in-memory provider avoids a native
/// dependency in the CI job. Nothing here is ever written to.
/// </para>
/// </remarks>
public sealed class SeededContentCatalog : IAsyncDisposable
{
    private readonly ContentDbContext _context;

    private SeededContentCatalog(ContentDbContext context)
    {
        _context = context;
    }

    /// <summary>Gets the seeded content context, for reference lookups.</summary>
    public ContentDbContext Context => _context;

    /// <summary>
    /// Seeds a private in-memory catalog with every content seeder the server boots with.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The seeded catalog. Dispose it to release the in-memory store.</returns>
    public static async Task<SeededContentCatalog> CreateAsync(CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase($"content-pack-baseline-{Guid.NewGuid()}")
            .Options;

        var context = new ContentDbContext(options);

        // The order is the one DatabaseSeeder uses: content that resolves another slug by lookup needs its target to
        // exist first (loot tables before archetypes, for example).
        await MaterialsSeeder.SeedAsync(context);
        await ActorSeeder.SeedAsync(context);
        await PowersSeeder.SeedAsync(context);
        await ItemsSeeder.SeedAsync(context);
        await LootTablesSeeder.SeedAsync(context);
        await ArchetypeSeeder.SeedAsync(context);
        await RecipesSeeder.SeedAsync(context);
        await QuestsSeeder.SeedAsync(context);
        await ZoneLocationsSeeder.SeedAsync(context);
        await ZoneLocationsConnectionSeeder.SeedAsync(context);
        await MapGraphSeeder.SeedAsync(context);
        await ActorInstancesSeeder.SeedAsync(context);
        await OrganizationsSeeder.SeedAsync(context);
        await DialogueSeeder.SeedAsync(context);
        await TraitDefinitionsSeeder.SeedAsync(context);
        await LanguagesSeeder.SeedAsync(context);
        await ContentRegistrySeeder.SeedAsync(context);

        ct.ThrowIfCancellationRequested();
        return new SeededContentCatalog(context);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
