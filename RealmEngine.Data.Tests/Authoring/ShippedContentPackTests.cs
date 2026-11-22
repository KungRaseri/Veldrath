using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Authoring.ContentPacks;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;

namespace RealmEngine.Data.Tests.Authoring;

/// <summary>
/// Guards the shipped packs against the seeders they mirror, and the two-run import order they depend on.
/// </summary>
/// <remarks>
/// <para>
/// A pack entity replaces the stored row's authored values, so a pack that drifts from its seeder silently rewrites
/// live content the first time someone imports it. The archetype pack is the case that matters: it repeats 32 stat
/// blocks that also live in <see cref="ArchetypeSeeder"/>, and it is the only thing that binds an archetype to a loot
/// table on a catalogue that was seeded before the tables were imported.
/// </para>
/// <para>
/// This is the local half of the CI content guard. <c>validate content/</c> proves the packs resolve; this proves the
/// two-run import lands 32 bound archetypes, that a second run of either pack changes nothing, and that the pack still
/// agrees with the seeders — none of which a validator can see.
/// </para>
/// </remarks>
[Trait("Category", "Authoring")]
public sealed class ShippedContentPackTests
{
    [Fact]
    public async Task The_two_run_import_binds_all_32_archetypes_and_a_second_run_changes_nothing()
    {
        var root = FindRepositoryRoot();
        await using var connection = await CreateSeededCatalogueAsync();
        var options = Options(connection);

        var lootTables = ContentPackReader.Read(Path.Combine(root, "content", "loottables"));
        var archetypes = ContentPackReader.Read(Path.Combine(root, "content", "archetypes"));

        lootTables.HasErrors.Should().BeFalse("the shipped loot-table pack is readable");
        archetypes.HasErrors.Should().BeFalse("the shipped archetype pack is readable");

        // Read the authored bindings now: the importer resolves each slug into a GUID in the payload it was handed,
        // so after an import the payload no longer carries what the author wrote.
        var authored = AuthoredBindings(archetypes);

        await using var context = new ContentDbContext(options);

        // Run one: the tables the archetypes name. Two of the 32 slugs are seeded and re-authored in place.
        var tables = await new ContentPackImporter(context).ImportAsync(lootTables);
        tables.Succeeded.Should().BeTrue(tables.Validation.Issues.FirstOrDefault()?.Message);
        tables.Created.Should().Be(30, "two of the 32 tables are seeded and are re-authored in place");
        tables.Updated.Should().Be(2, "goblin-common-drops and wolf-common-drops keep their slug and change their weights");

        // Run two: the archetypes. Every row already exists (the seeder inserted all 34 hostiles), so this is a
        // binding run: two rows already pointed at their seeded table, and the other 30 gain their LootTableId.
        var bound = await new ContentPackImporter(context).ImportAsync(archetypes);
        bound.Succeeded.Should().BeTrue(bound.Validation.Issues.FirstOrDefault()?.Message);
        bound.Created.Should().Be(0, "the seeder creates every archetype the pack names; the pack binds, it does not seed");
        bound.Updated.Should().Be(30, "30 of the 32 bound rows had a null LootTableId: the binding is the change");
        bound.Unchanged.Should().Be(3, "goblin-scout and forest-wolf already pointed at their seeded tables, and the blacksmith NPC authors no table");

        // A second run of either pack is a no-op — the property content packs promise, and the one a child row's
        // stored order used to break for every multi-row loot table.
        var tablesAgain = await new ContentPackImporter(context).ImportAsync(lootTables);
        tablesAgain.Created.Should().Be(0);
        tablesAgain.Updated.Should().Be(0, "a re-imported table is the same set of rows, whatever order the store keeps");

        var again = await new ContentPackImporter(context).ImportAsync(archetypes);
        again.Created.Should().Be(0);
        again.Updated.Should().Be(0);
        again.Unchanged.Should().Be(archetypes.Entries.Count());

        // Every archetype the pack names drops the table it names, and that table exists.
        var knownTables = await context.LootTables.AsNoTracking()
            .ToDictionaryAsync(table => table.Id, table => table.Slug);

        foreach (var (slug, lootSlug) in authored)
        {
            var stored = await context.ActorArchetypes.AsNoTracking().SingleAsync(entity => entity.Slug == slug);

            stored.LootTableId.Should().NotBeNull($"{slug} names a loot table and the table exists by now");
            knownTables.Should().ContainKey(stored.LootTableId!.Value);
            knownTables[stored.LootTableId.Value].Should().Be(lootSlug);
        }
    }

    [Fact]
    public async Task The_shipped_archetype_pack_mirrors_the_seeder_value_for_value()
    {
        var root = FindRepositoryRoot();
        await using var connection = await CreateSeededCatalogueAsync();
        var options = Options(connection);

        var lootTables = ContentPackReader.Read(Path.Combine(root, "content", "loottables"));
        var archetypes = ContentPackReader.Read(Path.Combine(root, "content", "archetypes"));
        var authored = AuthoredBindings(archetypes);

        await using var context = new ContentDbContext(options);

        var tables = await new ContentPackImporter(context).ImportAsync(lootTables);
        tables.Succeeded.Should().BeTrue(tables.Validation.Issues.FirstOrDefault()?.Message);

        // Bind the seeded rows the way the pack says, so the next import isolates the authored values from the one
        // column the seeder could not set: a stat line that drifted reports its row as updated.
        foreach (var (slug, lootSlug) in authored)
        {
            var stored = await context.ActorArchetypes.SingleAsync(entity => entity.Slug == slug);
            stored.LootTableId = await context.LootTables
                .Where(table => table.Slug == lootSlug)
                .Select(table => table.Id)
                .SingleAsync();
        }

        await context.SaveChangesAsync();

        var mirror = await new ContentPackImporter(context).ImportAsync(archetypes);

        mirror.Succeeded.Should().BeTrue(mirror.Validation.Issues.FirstOrDefault()?.Message);
        mirror.Created.Should().Be(0);
        mirror.Updated.Should().Be(0, "a pack that drifted from the seeder would rewrite these rows on import");
        mirror.Unchanged.Should().Be(archetypes.Entries.Count());
    }

    [Fact]
    public void The_shipped_archetype_pack_binds_every_roster_archetype_the_loot_design_names()
    {
        var root = FindRepositoryRoot();
        var archetypes = ContentPackReader.Read(Path.Combine(root, "content", "archetypes"));

        archetypes.Entries.Should().HaveCount(33,
            "the 32 bound rows plus village-blacksmith, the NPC whose trade purse the same directory re-authors");

        var bound = AuthoredBindings(archetypes).ToList();

        bound.Should().HaveCount(32, "loot-tables.md §3 binds 32 archetypes; the two bosses keep their seeded tables");
        bound.Should().OnlyContain(entry => !string.IsNullOrWhiteSpace(entry.Value));
        bound.Should().NotContain(entry => entry.Key == "bandit-captain", "the boss tables are not re-authored");
        bound.Should().NotContain(entry => entry.Key == "fire-ancient", "the boss tables are not re-authored");
    }

    /// <summary>Reads each entry's authored <c>lootTableId</c> slug, before an import replaces it with a GUID.</summary>
    /// <remarks>
    /// Not every archetype in the directory binds a table: the blacksmith NPC costs no kill and drops nothing, so the
    /// pack leaves its <c>lootTableId</c> out on purpose.
    /// </remarks>
    private static Dictionary<string, string> AuthoredBindings(ContentPackReadResult read) =>
        read.Entries
            .Select(entry => (entry.Slug, Table: entry.Payload["lootTableId"]?.GetValue<string>()))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Table))
            .ToDictionary(entry => entry.Slug, entry => entry.Table!, StringComparer.Ordinal);

    /// <summary>
    /// Opens an in-memory catalogue holding exactly what the server seeds on a fresh database: the items the tables
    /// name, the seeded loot tables, and the 34 hostile archetypes plus the 3 NPCs.
    /// </summary>
    private static async Task<SqliteConnection> CreateSeededCatalogueAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = Options(connection);

        await using (var schema = new ContentDbContext(options))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using (var seed = new ContentDbContext(options))
        {
            await MaterialsSeeder.SeedAsync(seed);
            await ItemsSeeder.SeedAsync(seed);
            await LootTablesSeeder.SeedAsync(seed);
            await ArchetypeSeeder.SeedAsync(seed);
        }

        return connection;
    }

    private static DbContextOptions<ContentDbContext> Options(SqliteConnection connection) =>
        new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options;

    /// <summary>
    /// Walks up from the test assembly until it finds the repository root — the directory holding <c>content/</c>.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "content", "archetypes")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"The repository root was not found above '{AppContext.BaseDirectory}': expected a content/archetypes directory.");
    }
}
