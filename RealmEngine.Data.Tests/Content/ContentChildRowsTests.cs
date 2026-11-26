using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Content;

/// <summary>
/// Proves how <see cref="ContentChildRows"/> reconciles child rows: it mutates matches in place, replaces the set, and
/// only touches a collection the payload names.
/// </summary>
/// <remarks>
/// This is the shared half of three write paths (the PUT endpoint, the review-queue approval and the content-pack
/// importer), so it is tested here against SQLite rather than through any one of them. SQLite is the provider in this
/// suite that actually enforces the composite key on <c>RecipeIngredient</c> and the surrogate key on
/// <c>LootTableEntry</c>, which is what decides whether a reconcile is an UPDATE or an INSERT.
/// </remarks>
public class ContentChildRowsTests
{
    [Fact]
    public async Task Reconcile_mutates_a_matched_child_in_place_and_replaces_the_rows_the_author_dropped()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        // iron is edited, pine is dropped and oak is added — the three cases in one pass.
        var authored = new Recipe
        {
            Slug = existing.Slug,
            Ingredients = [Ingredient("iron", 9), Ingredient("oak", 2)],
        };

        ContentChildRows.Reconcile(db, authored, existing);

        // The tracked parent reports exactly what was authored: a deleted child must not linger in its collection.
        existing.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "oak");
        existing.Ingredients.Single(ingredient => ingredient.ItemSlug == "iron").Quantity.Should().Be(9);
        existing.Ingredients.Should().OnlyContain(ingredient => ingredient.RecipeId == existing.Id);

        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        stored.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "oak");
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "iron").Quantity.Should().Be(9);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "oak").Quantity.Should().Be(2);
    }

    [Fact]
    public async Task ApplyNamedAsync_leaves_a_collection_the_payload_never_names_alone()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        // What the deserialiser produces from a payload with no `ingredients` property at all.
        var authored = new Recipe { Slug = existing.Slug };

        using var document = JsonDocument.Parse($$"""{"slug":"{{existing.Slug}}"}""");
        await ContentChildRows.ApplyNamedAsync(db, authored, existing, document.RootElement);

        existing.Ingredients.Should().HaveCount(2);

        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        stored.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "pine");
    }

    [Fact]
    public async Task ApplyNamedAsync_clears_a_collection_the_payload_names_as_empty()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        var authored = new Recipe { Slug = existing.Slug };

        using var document = JsonDocument.Parse("""{"slug":"iron-sword-recipe","ingredients":[]}""");
        await ContentChildRows.ApplyNamedAsync(db, authored, existing, document.RootElement);

        existing.Ingredients.Should().BeEmpty();

        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        stored.Ingredients.Should().BeEmpty();
    }

    /// <summary>
    /// An explicit <c>null</c> is not an authored set. The payload names the property, but it names it as nothing at
    /// all — reading that as "the author wrote an empty list" replaces the stored children with nothing, which is a
    /// silent delete of rows the payload never showed.
    /// </summary>
    [Fact]
    public async Task ApplyNamedAsync_leaves_a_collection_the_payload_names_as_null_alone()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        using var document = JsonDocument.Parse("""{"slug":"iron-sword-recipe","ingredients":null}""");

        // The real deserialiser, not a hand-built entity: the point is that a null collection is what arrives.
        var authored = JsonSerializer.Deserialize<Recipe>(
            document.RootElement.GetRawText(),
            ContentEntityTypes.JsonOptions)!;
        authored.Ingredients.Should().BeNull();

        await ContentChildRows.ApplyNamedAsync(db, authored, existing, document.RootElement);

        existing.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "pine");

        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        stored.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "pine");
    }

    /// <summary>
    /// The importer reconciles every collection the entity type has, so it has no payload to consult. A null collection
    /// there must still mean "not authored", never "authored as nothing".
    /// </summary>
    [Fact]
    public async Task Reconcile_leaves_a_collection_the_entity_carries_as_null_alone()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        var authored = new Recipe { Slug = existing.Slug, Ingredients = null! };

        ContentChildRows.Reconcile(db, authored, existing);
        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.Recipes
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == "iron-sword-recipe");

        stored.Ingredients.Should().HaveCount(2);
    }

    /// <summary>
    /// The write path's payload-shape rule: a null child collection is an error that names the field, for every
    /// authored child collection the model maps — including the families whose own validator never looks at them.
    /// </summary>
    [Fact]
    public async Task NullCollectionIssues_names_the_child_collection_an_entity_carries_as_null()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullCollectionIssues(
            new Species { Slug = "wolf", TypeKey = "beast", DisplayName = "Wolf", PowerPool = null! },
            db.Model);

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentRules.ChildCollectionNull);
        issues[0].Severity.Should().Be(ContentIssueSeverity.Error);
        issues[0].Field.Should().Be("powerPool");
        issues[0].Identifier.Should().Be("wolf");
    }

    /// <summary>
    /// The sibling rule: a null owned <em>object</em> is not a collection at all, so it is refused by its own rule. The
    /// deserialiser yields a null reference and every family validator dereferences it (<c>entity.Stats.MinLevel</c>),
    /// which is the unhandled 500 this rule closes.
    /// </summary>
    [Fact]
    public async Task NullOwnedObjectIssues_names_the_owned_object_an_entity_carries_as_null()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullOwnedObjectIssues(
            new ZoneLocation
            {
                Slug = "drove-road",
                TypeKey = "locations",
                DisplayName = "Drove Road",
                ZoneId = "varenmark",
                Stats = null!,
            },
            db.Model);

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentRules.OwnedObjectNull);
        issues[0].Severity.Should().Be(ContentIssueSeverity.Error);
        issues[0].Field.Should().Be("stats");
        issues[0].Identifier.Should().Be("drove-road");
    }

    /// <summary>Every owned reference a single family carries is reported, not just the first one found.</summary>
    [Fact]
    public async Task NullOwnedObjectIssues_names_every_owned_object_of_a_family()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullOwnedObjectIssues(
            new Quest
            {
                Slug = "null-owned-quest",
                TypeKey = "side",
                DisplayName = "Null Owned Quest",
                Stats = null!,
                Traits = null!,
                Objectives = null!,
                Rewards = null!,
            },
            db.Model);

        issues.Select(issue => issue.Field).Should().BeEquivalentTo("stats", "traits", "objectives", "rewards");
        issues.Should().OnlyContain(issue =>
            issue.Rule == ContentRules.OwnedObjectNull && issue.Severity == ContentIssueSeverity.Error);
    }

    /// <summary>
    /// A stored row carries every owned object as an instance, so the rule is silent for it — which is what keeps
    /// <c>GET /api/content/{type}/{slug}/validate</c> clean.
    /// </summary>
    [Fact]
    public async Task NullOwnedObjectIssues_is_silent_for_an_entity_whose_owned_objects_are_initialized()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        ContentChildRows.NullOwnedObjectIssues(
                new Language { Slug = "calethic", TypeKey = "imperial", DisplayName = "Calethic" },
                db.Model)
            .Should().BeEmpty();

        ContentChildRows.NullOwnedObjectIssues(
                new ZoneLocation { Slug = "drove-road", TypeKey = "locations", DisplayName = "Drove Road" },
                db.Model)
            .Should().BeEmpty();
    }

    /// <summary>An initialized collection — what an omitted property deserialises to — is not a null one.</summary>
    [Fact]
    public async Task NullCollectionIssues_is_silent_when_every_collection_is_initialized()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        ContentChildRows.NullCollectionIssues(
                new Species { Slug = "wolf", TypeKey = "beast", DisplayName = "Wolf" },
                db.Model)
            .Should().BeEmpty();
    }

    /// <summary>
    /// The rule reaches an owned collection too (<c>ZoneLocation.ActorPool</c> is the only one a write-path entity
    /// carries): it is not reconciled, it is assigned wholesale by <see cref="ContentValueCopy.ApplyAuthoredValues"/>,
    /// so a null there would clear the stored pool exactly like a null child-row collection would.
    /// </summary>
    [Fact]
    public async Task NullCollectionIssues_names_an_owned_collection_an_entity_carries_as_null()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullCollectionIssues(
            new ZoneLocation
            {
                Slug = "drove-road",
                TypeKey = "locations",
                DisplayName = "Drove Road",
                ZoneId = "varenmark",
                ActorPool = null!,
            },
            db.Model);

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentRules.ChildCollectionNull);
        issues[0].Severity.Should().Be(ContentIssueSeverity.Error);
        issues[0].Field.Should().Be("actorPool");
        issues[0].Identifier.Should().Be("drove-road");
    }

    /// <summary>
    /// The rule reaches <b>one level into an owned object</b>, where no entity navigation carries the list: every one of
    /// a language's lists lives inside <c>Phonology</c>/<c>Morphology</c>/<c>RegisterSystem</c>, and a null there is
    /// stored, wipes the seeded inventory, and then 500s the language read family
    /// (<c>EfCoreLanguageRepository</c> dereferences <c>Phonology.Consonants</c>).
    /// </summary>
    [Fact]
    public async Task NullCollectionIssues_names_a_collection_nested_inside_an_owned_object()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullCollectionIssues(
            new Language
            {
                Slug = "calethic",
                TypeKey = "imperial",
                DisplayName = "Calethic",
                Phonology = new LanguagePhonology
                {
                    // An owned collection (OwnsMany) and a primitive collection (PrimitiveCollection) are different
                    // metadata; a null in either one reaches the JSON column.
                    Consonants = null!,
                    AllowedSyllablePatterns = null!,
                },
                Morphology = new LanguageMorphology { Roots = null! },
            },
            db.Model);

        issues.Select(issue => issue.Field).Should().BeEquivalentTo(
            "phonology.consonants",
            "phonology.allowedSyllablePatterns",
            "morphology.roots");
        issues.Should().OnlyContain(issue =>
            issue.Rule == ContentRules.ChildCollectionNull && issue.Severity == ContentIssueSeverity.Error);
        issues.Should().OnlyContain(issue => issue.Identifier == "calethic");
    }

    /// <summary>
    /// The primitive-collection half on a second family: a dialogue's lines are a JSON array of strings, not owned rows,
    /// and the reader that dereferences them is the dialogue validator.
    /// </summary>
    [Fact]
    public async Task NullCollectionIssues_names_a_primitive_collection_nested_inside_an_owned_object()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullCollectionIssues(
            new Dialogue
            {
                Slug = "greeting-woods",
                TypeKey = "greetings",
                DisplayName = "Greeting",
                Stats = new DialogueStats { Lines = null! },
            },
            db.Model);

        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be(ContentRules.ChildCollectionNull);
        issues[0].Field.Should().Be("stats.lines");
    }

    /// <summary>
    /// The quest shape, which is what the round-3 review found on the validator side: both lists are <c>items</c> inside
    /// an owned object, so the two fields are distinguished by their owner rather than by the leaf name.
    /// </summary>
    [Fact]
    public async Task NullCollectionIssues_distinguishes_two_nested_lists_with_the_same_leaf_name()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var issues = ContentChildRows.NullCollectionIssues(
            new Quest
            {
                Slug = "null-nested-quest",
                TypeKey = "side",
                DisplayName = "Null Nested Quest",
                Objectives = new QuestObjectives { Items = null! },
                Rewards = new QuestRewards { Items = null! },
            },
            db.Model);

        issues.Select(issue => issue.Field).Should().BeEquivalentTo("objectives.items", "rewards.items");
    }

    /// <summary>
    /// A stored entity is silent: owned JSON materialises every list as a list, so the rule only fires on a payload that
    /// names one as null — which is what keeps <c>GET /api/content/{type}/{slug}/validate</c> clean.
    /// </summary>
    [Fact]
    public async Task NullCollectionIssues_is_silent_for_a_stored_entity_whose_nested_lists_are_initialized()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        ContentChildRows.NullCollectionIssues(
                new Language { Slug = "calethic", TypeKey = "imperial", DisplayName = "Calethic" },
                db.Model)
            .Should().BeEmpty();
    }

    /// <summary>
    /// A replaced child that still carries the surrogate id it was read with must be inserted under a fresh id: the
    /// stored row it came from is deleted in the same save, so reusing the id would collide.
    /// </summary>
    [Fact]
    public async Task A_replaced_surrogate_keyed_child_is_inserted_under_a_new_id()
    {
        var (connection, options) = await CreateDatabaseAsync();

        using var keepAlive = connection;
        await using var db = new ContentDbContext(options);

        var existing = await db.LootTables
            .Include(table => table.Entries)
            .SingleAsync(table => table.Slug == "wolf-loot");

        var replaced = existing.Entries.Single(entry => entry.ItemSlug == "health-potion");
        var replacedId = replaced.Id;

        // The row exactly as the detail read hands it back to an editor, with only the weight changed.
        var authored = new LootTable
        {
            Slug = existing.Slug,
            Entries =
            [
                new LootTableEntry
                {
                    Id = replacedId,
                    ItemDomain = replaced.ItemDomain,
                    ItemSlug = replaced.ItemSlug,
                    DropWeight = 15,
                    QuantityMin = 1,
                    QuantityMax = 1,
                },
                new LootTableEntry
                {
                    Id = existing.Entries.Single(entry => entry.ItemSlug == "iron-ingot").Id,
                    ItemDomain = "items/general",
                    ItemSlug = "iron-ingot",
                    DropWeight = 40,
                    QuantityMin = 1,
                    QuantityMax = 1,
                },
            ],
        };

        ContentChildRows.Reconcile(db, authored, existing);
        await db.SaveChangesAsync();

        await using var read = new ContentDbContext(options);
        var stored = await read.LootTables
            .Include(table => table.Entries)
            .SingleAsync(table => table.Slug == "wolf-loot");

        stored.Entries.Should().HaveCount(2);
        var potion = stored.Entries.Single(entry => entry.ItemSlug == "health-potion");
        potion.DropWeight.Should().Be(15);
        potion.Id.Should().NotBe(replacedId);
    }

    /// <summary>Creates an in-memory SQLite catalog holding one recipe and one loot table, each with two child rows.</summary>
    /// <returns>The open connection (keep it alive for the database to survive) and the context options.</returns>
    private static async Task<(SqliteConnection Connection, DbContextOptions<ContentDbContext> Options)> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options;

        await using var db = new ContentDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Recipes.Add(new Recipe
        {
            Slug = "iron-sword-recipe",
            TypeKey = "blacksmithing",
            DisplayName = "Iron Sword",
            RarityWeight = 80,
            IsActive = true,
            OutputItemDomain = "items/general",
            OutputItemSlug = "iron-sword",
            CraftingSkill = "blacksmithing",
            CraftingLevel = 1,
            Ingredients = [Ingredient("iron", 3), Ingredient("pine", 1)],
        });

        await db.SaveChangesAsync();

        db.LootTables.Add(new LootTable
        {
            Slug = "wolf-loot",
            TypeKey = "enemies",
            DisplayName = "Wolf Loot",
            RarityWeight = 60,
            IsActive = true,
            Entries =
            [
                new LootTableEntry
                {
                    ItemDomain = "items/general",
                    ItemSlug = "health-potion",
                    DropWeight = 60,
                    QuantityMin = 1,
                    QuantityMax = 1,
                },
                new LootTableEntry
                {
                    ItemDomain = "items/general",
                    ItemSlug = "iron-ingot",
                    DropWeight = 40,
                    QuantityMin = 1,
                    QuantityMax = 1,
                },
            ],
        });

        await db.SaveChangesAsync();

        return (connection, options);
    }

    private static RecipeIngredient Ingredient(string slug, int quantity) => new()
    {
        ItemDomain = "items/materials",
        ItemSlug = slug,
        Quantity = quantity,
    };
}
