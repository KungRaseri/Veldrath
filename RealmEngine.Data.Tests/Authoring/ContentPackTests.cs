using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Authoring.ContentPacks;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Authoring;

/// <summary>
/// Covers the content-pack pipeline end to end: reading a pack directory, running the shared validators over it and
/// upserting it by slug.
/// </summary>
/// <remarks>
/// The provider is SQLite rather than the in-memory one because the content model stores owned types as JSON columns:
/// SQLite is the provider that actually round-trips that JSON, so "the second import reports zero changes" is proven
/// against stored values and not against a CLR object that never left memory.
/// </remarks>
[Trait("Category", "Authoring")]
public sealed class ContentPackTests
{
    private const string TwoPotionPack = """
        # The worked example the pipeline tests run against.
        family: item
        entities:
          - slug: pack-health-potion
            typeKey: consumables
            displayName: Pack Health Potion
            itemType: consumable
            rarityWeight: 90
            stats:
              weight: 0.1
              value: 15
              effectPower: 20
              duration: 0
            traits:
              stackable: true
              consumable: true
          - slug: pack-mana-potion
            typeKey: consumables
            displayName: Pack Mana Potion
            itemType: consumable
            rarityWeight: 90
            stats:
              weight: 0.1
              value: 15
              effectPower: 18
            traits:
              stackable: true
              consumable: true
        """;

    [Fact]
    public void Yaml_scalars_keep_the_types_the_entity_schema_expects()
    {
        var json = ContentPackYaml.ToJson(
            """
            weight: 0.25
            stackSize: 20
            stackable: true
            displayName: "007"
            nothing: ~
            """,
            out var error);

        error.Should().BeNull();

        json!["weight"]!.GetValue<double>().Should().Be(0.25);
        json["stackSize"]!.GetValue<long>().Should().Be(20);
        json["stackable"]!.GetValue<bool>().Should().BeTrue();
        json["displayName"]!.GetValue<string>().Should().Be("007", "a quoted scalar is text, so the leading zero survives");
        json["nothing"].Should().BeNull();
    }

    [Fact]
    public void Reading_a_pack_reports_its_family_and_entities()
    {
        using var pack = new PackDirectory(("items/potions.yaml", TwoPotionPack));

        var read = ContentPackReader.Read(pack.Root);

        read.HasErrors.Should().BeFalse();
        read.Files.Should().ContainSingle();
        read.Files[0].Family.Should().Be("item");
        read.Entries.Select(entry => entry.Slug).Should().Equal("pack-health-potion", "pack-mana-potion");
    }

    [Fact]
    public async Task Importing_the_same_pack_twice_creates_each_row_once_and_updates_nothing_the_second_time()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(("items/potions.yaml", TwoPotionPack));

        var first = await new ContentPackImporter(database.NewContext()).ImportAsync(ContentPackReader.Read(pack.Root));

        first.Succeeded.Should().BeTrue();
        first.Created.Should().Be(2);
        first.Updated.Should().Be(0);
        first.Families.Should().ContainSingle().Which.Family.Should().Be("item");

        await using var second = database.NewContext();
        var again = await new ContentPackImporter(second).ImportAsync(ContentPackReader.Read(pack.Root));

        again.Created.Should().Be(0);
        again.Updated.Should().Be(0);
        again.Unchanged.Should().Be(2);
        (await second.Items.CountAsync(item => item.Slug.StartsWith("pack-"))).Should().Be(2);
    }

    [Fact]
    public async Task Changing_one_field_updates_exactly_one_row_and_reports_the_rest_unchanged()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(("items/potions.yaml", TwoPotionPack));

        await new ContentPackImporter(database.NewContext()).ImportAsync(ContentPackReader.Read(pack.Root));

        pack.Write("items/potions.yaml", TwoPotionPack.Replace("effectPower: 18", "effectPower: 26", StringComparison.Ordinal));

        await using var context = database.NewContext();
        var result = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        result.Updated.Should().Be(1);
        result.Created.Should().Be(0);
        result.Unchanged.Should().Be(1);

        var updated = await context.Items.AsNoTracking().SingleAsync(item => item.Slug == "pack-mana-potion");
        updated.Stats.EffectPower.Should().Be(26);
        updated.Version.Should().Be(2, "an updated row is versioned, an untouched one is not");

        var untouched = await context.Items.AsNoTracking().SingleAsync(item => item.Slug == "pack-health-potion");
        untouched.Version.Should().Be(1);
    }

    [Fact]
    public async Task A_dangling_reference_is_an_error_that_writes_nothing_and_exits_non_zero()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(("quests/fenwick.yaml", """
            entities:
              - slug: hunt-the-unwritten
                typeKey: side
                displayName: Hunt the Unwritten
                minLevel: 1
                objectives:
                  items:
                    - type: kill
                      target: no-such-slug
                      quantity: 1
            """));

        var output = new StringWriter();
        var exit = ContentPackConsole.Run(["validate", pack.Root, "--no-baseline"], output, output);

        exit.Should().Be(ContentPackConsole.InvalidContent);
        output.ToString().Should().Contain(ContentReferenceRules.MissingQuestTarget);
        output.ToString().Should().Contain("no-such-slug");

        await using var context = database.NewContext();
        var result = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        result.Succeeded.Should().BeFalse();
        result.Written.Should().BeFalse();
        (await context.Quests.CountAsync()).Should().Be(0, "a refused pack must not half-land");
    }

    [Fact]
    public async Task A_pack_whose_slug_collides_with_a_seeded_row_updates_that_row_instead_of_duplicating_it()
    {
        await using var database = await PackDatabase.CreateAsync();
        var seededContext = database.NewContext();
        await ItemsSeeder.SeedAsync(seededContext);

        var seeded = await seededContext.Items.AsNoTracking().SingleAsync(item => item.Slug == "health-potion");

        using var pack = new PackDirectory(("items/potions.yaml", """
            entities:
              - slug: health-potion
                typeKey: consumables
                displayName: Health Potion (Rebalanced)
                itemType: consumable
                rarityWeight: 90
                stats:
                  weight: 0.1
                  stackSize: 20
                  value: 18
                  effectPower: 22
                  duration: 0
                traits:
                  stackable: true
                  questItem: false
                  consumable: true
            """));

        await using var context = database.NewContext();
        var result = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        result.Succeeded.Should().BeTrue();
        result.Created.Should().Be(0);
        result.Updated.Should().Be(1);

        var rows = await context.Items.AsNoTracking().Where(item => item.Slug == "health-potion").ToListAsync();
        rows.Should().ContainSingle("the seeder's row is updated in place, so live game data still points at it");
        rows[0].Id.Should().Be(seeded.Id);
        rows[0].DisplayName.Should().Be("Health Potion (Rebalanced)");
        rows[0].Version.Should().Be(seeded.Version + 1);
    }

    [Fact]
    public async Task A_warning_only_pack_validates_clean_and_exits_zero()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(("items/loud.yaml", """
            entities:
              - slug: pack-loud-potion
                typeKey: consumables
                displayName: Pack Loud Potion
                itemType: consumable
                rarityWeight: 500
                stats:
                  effectPower: 10
                traits:
                  consumable: true
            """));

        var output = new StringWriter();
        var exit = ContentPackConsole.Run(["validate", pack.Root, "--no-baseline"], output, output);

        exit.Should().Be(ContentPackConsole.Success, "warnings are surfaced, not blocking");
        output.ToString().Should().Contain(ContentRules.RarityWeightOutOfRange);
        output.ToString().Should().Contain("1 warning(s)");
    }

    [Fact]
    public void A_duplicate_slug_inside_one_family_is_an_error()
    {
        using var pack = new PackDirectory(
            ("items/potions.yaml", TwoPotionPack),
            ("items/more-potions.yaml", TwoPotionPack));

        var read = ContentPackReader.Read(pack.Root);

        read.HasErrors.Should().BeTrue();
        read.Issues.Should().Contain(issue => issue.Rule == ContentPackRules.DuplicateSlug);
    }

    [Fact]
    public void A_pack_file_outside_a_family_directory_is_an_error()
    {
        using var pack = new PackDirectory(("loose.yaml", """
            entities:
              - slug: loose-potion
                typeKey: consumables
                displayName: Loose Potion
                itemType: consumable
            """));

        var read = ContentPackReader.Read(pack.Root);

        read.HasErrors.Should().BeTrue();
        read.Issues.Should().Contain(issue => issue.Rule == ContentPackRules.FamilyDirectoryRequired);
    }

    [Fact]
    public async Task A_guid_foreign_key_written_as_a_slug_is_resolved_on_import()
    {
        await using var database = await PackDatabase.CreateAsync();

        var lootTable = new LootTable { Slug = "pack-wolf-loot", TypeKey = "enemies", DisplayName = "Pack Wolf Loot" };
        await using (var seed = database.NewContext())
        {
            seed.LootTables.Add(lootTable);
            await seed.SaveChangesAsync();
        }

        using var pack = new PackDirectory(("archetypes/wolves.yaml", """
            entities:
              - slug: pack-timber-wolf
                typeKey: beasts/wolves
                displayName: Pack Timber Wolf
                minLevel: 1
                maxLevel: 3
                stats:
                  health: 40
                  experienceReward: 12
                lootTableId: pack-wolf-loot
            """));

        await using var context = database.NewContext();
        var result = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        result.Succeeded.Should().BeTrue(result.Validation.Issues.FirstOrDefault()?.Message);
        result.Created.Should().Be(1);

        var archetype = await context.ActorArchetypes.AsNoTracking().SingleAsync(entity => entity.Slug == "pack-timber-wolf");
        archetype.LootTableId.Should().Be(lootTable.Id);
    }

    [Fact]
    public void The_command_surface_reports_usage_problems_with_exit_code_two()
    {
        var output = new StringWriter();

        ContentPackConsole.Run([], output, output).Should().Be(ContentPackConsole.Success);
        output.ToString().Should().Contain("usage: veldrath-content");

        var error = new StringWriter();
        ContentPackConsole.Run(["validate"], output, error).Should().Be(ContentPackConsole.UsageError,
            "a command without a directory is a usage error");

        ContentPackConsole.Run(["explode", "."], output, error).Should().Be(ContentPackConsole.UsageError);

        ContentPackConsole.Run(["validate", Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid())], output, error)
            .Should().Be(ContentPackConsole.UsageError, "a missing pack directory is not invalid content, it is a wrong command line");
    }

    [Fact]
    public void The_help_example_imports_the_shipped_families_in_dependency_order()
    {
        var output = new StringWriter();

        ContentPackConsole.Run(["--help"], output, output).Should().Be(ContentPackConsole.Success);

        var help = output.ToString();

        // `import content/` validates the whole directory before it writes anything, and a GUID foreign key
        // written as a slug resolves against stored content — so the example has to be the family-by-family
        // sequence, not a single run that fails on a fresh catalogue.
        help.Should().NotContain(
            "import content/ --database",
            "the whole-directory import cannot bootstrap a fresh catalogue, so the help must not offer it");

        string[] families =
        [
            "import content/items",
            "import content/loottables",
            "import content/archetypes",
            "import content/zonelocations",
            "import content/languages",
        ];

        var positions = families.Select(family => help.IndexOf(family, StringComparison.Ordinal)).ToArray();

        positions.Should().OnlyContain(position => position >= 0, "the example should name every shipped family: {0}", help);
        positions.Should().BeInAscendingOrder("a family has to be imported before the family that names it");
        help.Should().Contain("must already", "the help should say why the single run is not a shortcut");
    }

    [Fact]
    public async Task Import_refuses_to_run_without_a_database_and_dry_run_writes_nothing()
    {
        await using var database = await PackDatabase.CreateAsync();
        using var pack = new PackDirectory(("items/potions.yaml", TwoPotionPack));

        var output = new StringWriter();
        var error = new StringWriter();
        ContentPackConsole.Run(["import", pack.Root], output, error, connectionString: string.Empty)
            .Should().Be(ContentPackConsole.UsageError);
        error.ToString().Should().Contain("needs a content database");

        // A dry run reports the counts it would write and saves nothing, so a second real import still creates both.
        await using var context = database.NewContext();
        var planned = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root), dryRun: true);

        planned.Created.Should().Be(2);
        planned.Written.Should().BeFalse();
        (await context.Items.CountAsync()).Should().Be(0);

        var committed = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));
        committed.Created.Should().Be(2);
        committed.Written.Should().BeTrue();
        (await context.Items.CountAsync()).Should().Be(2);
    }

    [Fact]
    public void An_unreadable_entity_payload_is_reported_against_the_entity_and_its_file()
    {
        using var pack = new PackDirectory(("items/broken.yaml", """
            entities:
              - slug: pack-broken-potion
                itemType: consumable
                stats:
                  value: "not a number"
            """));

        var output = new StringWriter();
        var exit = ContentPackConsole.Run(["validate", pack.Root, "--no-baseline"], output, output);

        exit.Should().Be(ContentPackConsole.InvalidContent);
        output.ToString().Should().Contain(ContentPackRules.EntityUnreadable);
        output.ToString().Should().Contain("pack-broken-potion");
    }

    // The packs the child-collection tests rewrite between imports. Kept as constants so an edit is one visible
    // difference rather than a second hand-written document.
    private const string LootTablePack = """
        entities:
          - slug: pack-wolf-drops
            typeKey: enemies
            displayName: Pack Wolf Drops
            entries:
              - itemDomain: items/general
                itemSlug: health-potion
                dropWeight: 60
                quantityMin: 1
                quantityMax: 2
        """;

    private const string LootTablePackWithAddedEntry = """
        entities:
          - slug: pack-wolf-drops
            typeKey: enemies
            displayName: Pack Wolf Drops (Revised)
            entries:
              - itemDomain: items/general
                itemSlug: health-potion
                dropWeight: 60
                quantityMin: 1
                quantityMax: 2
              - itemDomain: items/general
                itemSlug: glowcap-mushroom
                dropWeight: 25
                quantityMin: 1
                quantityMax: 1
        """;

    private const string RecipePack = """
        entities:
          - slug: pack-iron-tonic
            typeKey: alchemy
            displayName: Pack Iron Tonic
            outputItemDomain: items/general
            outputItemSlug: health-potion
            outputQuantity: 1
            craftingSkill: alchemy
            craftingLevel: 1
            ingredients:
              - itemDomain: items/materials
                itemSlug: iron
                quantity: 2
              - itemDomain: items/general
                itemSlug: glowcap-mushroom
                quantity: 1
        """;

    private const string RecipePackWithOneIngredient = """
        entities:
          - slug: pack-iron-tonic
            typeKey: alchemy
            displayName: Pack Iron Tonic
            outputItemDomain: items/general
            outputItemSlug: health-potion
            outputQuantity: 1
            craftingSkill: alchemy
            craftingLevel: 1
            ingredients:
              - itemDomain: items/materials
                itemSlug: iron
                quantity: 2
        """;

    [Fact]
    public async Task Reimporting_a_loot_table_that_gained_an_entry_updates_it_once_and_never_duplicates_the_entries()
    {
        await using var database = await PackDatabase.CreateAsync();
        await using (var seed = database.NewContext())
        {
            await ItemsSeeder.SeedAsync(seed);
        }

        using var pack = new PackDirectory(("loottables/wolves.yaml", LootTablePack));

        await using (var created = database.NewContext())
        {
            var result = await new ContentPackImporter(created).ImportAsync(ContentPackReader.Read(pack.Root));
            result.Succeeded.Should().BeTrue(result.Validation.Issues.FirstOrDefault()?.Message);
            result.Created.Should().Be(1);
        }

        pack.Write("loottables/wolves.yaml", LootTablePackWithAddedEntry);

        await using var context = database.NewContext();
        var updated = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        updated.Updated.Should().Be(1, "the parent changed, and it is the only entity in the pack");
        updated.Created.Should().Be(0);

        var table = await context.LootTables.AsNoTracking().Include(entity => entity.Entries)
            .SingleAsync(entity => entity.Slug == "pack-wolf-drops");

        table.Version.Should().Be(2);
        table.DisplayName.Should().Be("Pack Wolf Drops (Revised)");
        table.Entries.Select(entry => entry.ItemSlug).Should().BeEquivalentTo("health-potion", "glowcap-mushroom");

        await using var third = database.NewContext();
        var again = await new ContentPackImporter(third).ImportAsync(ContentPackReader.Read(pack.Root));

        again.Updated.Should().Be(0, "the second import wrote what the pack says, so a third has nothing to do");
        again.Unchanged.Should().Be(1);
        (await third.LootTableEntries.CountAsync()).Should().Be(2, "an added child is inserted once, not once per import");
    }

    [Fact]
    public async Task A_scalar_only_edit_on_a_parent_with_children_keeps_the_child_rows_it_did_not_change()
    {
        await using var database = await PackDatabase.CreateAsync();
        await using (var seed = database.NewContext())
        {
            await ItemsSeeder.SeedAsync(seed);
        }

        using var pack = new PackDirectory(("loottables/wolves.yaml", LootTablePack));

        await using (var created = database.NewContext())
        {
            await new ContentPackImporter(created).ImportAsync(ContentPackReader.Read(pack.Root));
        }

        Guid entryId;
        await using (var before = database.NewContext())
        {
            entryId = (await before.LootTableEntries.AsNoTracking().SingleAsync()).Id;
        }

        pack.Write(
            "loottables/wolves.yaml",
            LootTablePack.Replace("displayName: Pack Wolf Drops", "displayName: Pack Wolf Drops (Renamed)", StringComparison.Ordinal));

        await using var context = database.NewContext();
        var updated = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        updated.Updated.Should().Be(1);
        updated.Created.Should().Be(0);

        var table = await context.LootTables.AsNoTracking().Include(entity => entity.Entries)
            .SingleAsync(entity => entity.Slug == "pack-wolf-drops");

        table.Version.Should().Be(2);
        table.Entries.Should().ContainSingle("a scalar edit must not rewrite the child collection")
            .Which.Id.Should().Be(entryId, "an unchanged child row is left alone, not deleted and re-inserted");

        await using var third = database.NewContext();
        var again = await new ContentPackImporter(third).ImportAsync(ContentPackReader.Read(pack.Root));

        again.Updated.Should().Be(0);
        again.Unchanged.Should().Be(1);
    }

    [Fact]
    public async Task Removing_a_recipe_ingredient_deletes_only_that_child_row()
    {
        await using var database = await PackDatabase.CreateAsync();
        await using (var seed = database.NewContext())
        {
            await ItemsSeeder.SeedAsync(seed);
            await MaterialsSeeder.SeedAsync(seed);
        }

        using var pack = new PackDirectory(("recipes/alchemy.yaml", RecipePack));

        await using (var created = database.NewContext())
        {
            var result = await new ContentPackImporter(created).ImportAsync(ContentPackReader.Read(pack.Root));
            result.Succeeded.Should().BeTrue(result.Validation.Issues.FirstOrDefault()?.Message);
            result.Created.Should().Be(1);
        }

        pack.Write("recipes/alchemy.yaml", RecipePackWithOneIngredient);

        await using var context = database.NewContext();
        var updated = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        updated.Updated.Should().Be(1);
        updated.Created.Should().Be(0);

        var recipe = await context.Recipes.AsNoTracking().Include(entity => entity.Ingredients)
            .SingleAsync(entity => entity.Slug == "pack-iron-tonic");

        recipe.Version.Should().Be(2);
        recipe.Ingredients.Should().ContainSingle().Which.ItemSlug.Should().Be("iron");

        await using var third = database.NewContext();
        var again = await new ContentPackImporter(third).ImportAsync(ContentPackReader.Read(pack.Root));

        again.Updated.Should().Be(0);
        again.Unchanged.Should().Be(1);
        (await third.RecipeIngredients.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task An_owned_json_collection_edit_is_written_back_to_the_parent_row()
    {
        await using var database = await PackDatabase.CreateAsync();
        await using (var seed = database.NewContext())
        {
            await ItemsSeeder.SeedAsync(seed);
        }

        static string QuestPack(string quantity) => $$"""
            entities:
              - slug: pack-quest-log
                typeKey: side
                displayName: Pack Quest Log
                minLevel: 1
                objectives:
                  items:
                    - type: collect
                      target: health-potion
                      quantity: {{quantity}}
            """;

        using var pack = new PackDirectory(("quests/log.yaml", QuestPack("2")));

        await using (var created = database.NewContext())
        {
            var result = await new ContentPackImporter(created).ImportAsync(ContentPackReader.Read(pack.Root));
            result.Succeeded.Should().BeTrue(result.Validation.Issues.FirstOrDefault()?.Message);
        }

        pack.Write("quests/log.yaml", QuestPack("3"));

        await using var context = database.NewContext();
        var updated = await new ContentPackImporter(context).ImportAsync(ContentPackReader.Read(pack.Root));

        updated.Updated.Should().Be(1, "an owned JSON collection is part of the entity, so editing it is an update");

        var quest = await context.Quests.AsNoTracking().SingleAsync(entity => entity.Slug == "pack-quest-log");
        quest.Objectives.Items.Should().ContainSingle().Which.Quantity.Should().Be(3);
    }

    /// <summary>A throwaway SQLite content catalog; each context sees the same in-memory database.</summary>
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

        /// <summary>A fresh context over the same database, so a second import starts with nothing tracked.</summary>
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
            Root = Path.Combine(Path.GetTempPath(), $"content-pack-{Guid.NewGuid():N}");

            foreach (var (relativePath, content) in files)
            {
                Write(relativePath, content);
            }
        }

        public string Root { get; }

        public void Write(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
