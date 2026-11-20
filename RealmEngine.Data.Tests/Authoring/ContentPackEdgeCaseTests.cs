using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Authoring.ContentPacks;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Authoring;

/// <summary>
/// The failure modes a content author actually hits: a pack file in the wrong place, unparseable YAML, an envelope
/// without entities, a reference that does not resolve, and a child collection that must survive a re-import
/// untouched.
/// </summary>
[Trait("Category", "Authoring")]
public sealed class ContentPackEdgeCaseTests
{
    [Fact]
    public void A_missing_pack_directory_is_reported_as_an_issue()
    {
        var read = ContentPackReader.Read(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}"));

        read.HasErrors.Should().BeTrue();
        read.Issues.Should().Contain(issue => issue.Rule == ContentPackRules.DirectoryMissing);
    }

    [Fact]
    public void Unparseable_and_multi_document_yaml_is_rejected()
    {
        using var broken = new PackDirectory(("items/broken.yaml", "entities: ["));
        ContentPackReader.Read(broken.Root).Issues.Should().Contain(issue => issue.Rule == ContentPackRules.FileInvalid);

        using var multiple = new PackDirectory(
            ("items/two.yaml", "entities: []\n---\nentities: []"));
        var issues = ContentPackReader.Read(multiple.Root).Issues;

        issues.Should().Contain(issue => issue.Rule == ContentPackRules.FileInvalid && issue.Message.Contains("exactly one"));
    }

    [Fact]
    public void An_envelope_that_is_not_a_mapping_or_has_no_entity_list_is_rejected()
    {
        using var sequence = new PackDirectory(("items/list.yaml", "- one\n- two\n"));
        ContentPackReader.Read(sequence.Root).Issues
            .Should().Contain(issue => issue.Rule == ContentPackRules.EnvelopeInvalid);

        using var empty = new PackDirectory(("items/empty.yaml", "family: item\n"));
        ContentPackReader.Read(empty.Root).Issues
            .Should().Contain(issue => issue.Rule == ContentPackRules.EnvelopeInvalid && issue.Field == "entities");

        using var scalarEntity = new PackDirectory(("items/scalar.yaml", "entities:\n  - just-a-string\n"));
        ContentPackReader.Read(scalarEntity.Root).Issues
            .Should().Contain(issue => issue.Rule == ContentPackRules.EnvelopeInvalid);
    }

    [Fact]
    public void A_family_header_that_disagrees_with_the_directory_is_an_error()
    {
        using var pack = new PackDirectory(("items/mislabelled.yaml", """
            family: quest
            entities:
              - slug: pack-mislabelled
                typeKey: consumables
                displayName: Mislabelled
                itemType: consumable
            """));

        ContentPackReader.Read(pack.Root).Issues
            .Should().Contain(issue => issue.Rule == ContentPackRules.FamilyMismatch);
    }

    [Fact]
    public void An_unknown_family_directory_is_an_error_naming_the_known_families()
    {
        using var pack = new PackDirectory(("wibbles/one.yaml", "entities: []\n"));
        var issues = ContentPackReader.Read(pack.Root).Issues;

        issues.Should().Contain(issue => issue.Rule == ContentPackRules.FamilyUnknown && issue.Message.Contains("Known families"));
    }

    [Fact]
    public void Block_scalars_stay_text_so_prose_can_hold_colons_and_quotes()
    {
        var json = ContentPackYaml.ToJson(
            """
            displayName: |
              A blade that says "no": twice
            """,
            out var error);

        error.Should().BeNull();
        json!["displayName"]!.GetValue<string>().Should().Contain("A blade that says \"no\": twice");
    }

    [Fact]
    public async Task A_guid_foreign_key_naming_an_unknown_slug_is_reported_with_its_field()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(("archetypes/ghosts.yaml", """
            entities:
              - slug: pack-ghost-wolf
                typeKey: beasts/wolves
                displayName: Pack Ghost Wolf
                minLevel: 1
                maxLevel: 2
                stats:
                  health: 30
                speciesId: no-such-species
            """));

        await using var context = database.NewContext();
        var result = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        result.Succeeded.Should().BeFalse();
        result.Validation.Issues.Should().Contain(issue =>
            issue.Rule == ContentPackRules.ReferenceUnresolved
            && issue.Field == "speciesId"
            && issue.Identifier == "pack-ghost-wolf");
        (await context.ActorArchetypes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_loot_table_and_the_item_it_points_at_import_clean_and_survive_a_second_import()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(
            ("items/loot-potion.yaml", """
                entities:
                  - slug: pack-loot-potion
                    typeKey: consumables
                    displayName: Pack Loot Potion
                    itemType: consumable
                    stats:
                      effectPower: 12
                    traits:
                      consumable: true
                """),
            ("loottables/wolves.yaml", """
                entities:
                  - slug: pack-wolf-drops
                    typeKey: enemies
                    displayName: Pack Wolf Drops
                    entries:
                      - itemDomain: items/general
                        itemSlug: pack-loot-potion
                        dropWeight: 60
                        quantityMin: 1
                        quantityMax: 2
                """));

        await using var first = database.NewContext();
        var created = await new ContentPackImporter(first).ImportAsync(ContentPackReader.Read(pack.Root));

        created.Succeeded.Should().BeTrue(created.Validation.Issues.FirstOrDefault()?.Message);
        created.Created.Should().Be(2);

        await using var second = database.NewContext();
        var again = await new ContentPackImporter(second).ImportAsync(ContentPackReader.Read(pack.Root));

        again.Updated.Should().Be(0, "a child collection that did not change must not rewrite its parent");
        again.Created.Should().Be(0);
        again.Unchanged.Should().Be(2);

        var table = await second.LootTables.AsNoTracking().Include(entry => entry.Entries)
            .SingleAsync(entity => entity.Slug == "pack-wolf-drops");
        table.Entries.Should().ContainSingle().Which.ItemSlug.Should().Be("pack-loot-potion");
    }

    [Fact]
    public async Task Validate_resolves_references_against_the_seeded_catalogue_without_a_database()
    {
        // The quest points at a seeded item. No --database: the tool seeds the tracked seeders in-process, which is
        // what lets the CI guard prove a pack resolves without a Postgres to talk to.
        using var pack = new PackDirectory(("quests/fenwick.yaml", """
            entities:
              - slug: pack-gather-potions
                typeKey: side
                displayName: "Pack: Gather Potions"
                minLevel: 1
                objectives:
                  items:
                    - type: collect
                      target: health-potion
                      quantity: 2
                rewards:
                  items:
                    - type: gold
                      amount: 25
            """));

        var output = new StringWriter();
        var exit = ContentPackConsole.Run(["validate", pack.Root], output, output);

        exit.Should().Be(ContentPackConsole.Success, output.ToString());
        output.ToString().Should().Contain("checked against the seeded catalogue");
        output.ToString().Should().Contain("0 error(s)");
    }

    [Fact]
    public void Validate_against_an_unreachable_database_fails_the_command_rather_than_the_pack()
    {
        using var pack = new PackDirectory(("items/potions.yaml", """
            entities:
              - slug: pack-db-probe
                typeKey: consumables
                displayName: Pack DB Probe
                itemType: consumable
                stats:
                  effectPower: 5
            """));

        var output = new StringWriter();
        var error = new StringWriter();
        var exit = ContentPackConsole.Run(
            ["validate", pack.Root, "--database", "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1"],
            output,
            error);

        exit.Should().Be(ContentPackConsole.UsageError);
        error.ToString().Should().Contain("validation could not run");
    }

    /// <summary>A throwaway SQLite content catalog.</summary>
    private sealed class PackDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ContentDbContext> _options;

        private PackDatabase(SqliteConnection connection, DbContextOptions<ContentDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<PackDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ContentDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new ContentDbContext(options))
            {
                await context.Database.EnsureCreatedAsync();
            }

            return new PackDatabase(connection, options);
        }

        public ContentDbContext NewContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }

    /// <summary>A temporary pack directory, deleted when the test ends.</summary>
    private sealed class PackDirectory : IDisposable
    {
        public PackDirectory(params (string RelativePath, string Content)[] files)
        {
            Root = Path.Combine(Path.GetTempPath(), $"content-pack-edge-{Guid.NewGuid():N}");

            foreach (var (relativePath, content) in files)
            {
                var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
