using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Proves a payload that names a collection as JSON <c>null</c> is refused with a named issue instead of crashing a
/// validator with a null dereference, or reaching the write path as an empty authored set.
/// </summary>
/// <remarks>
/// An omitted property deserialises to the property initializer (<c>[]</c>); an explicit <c>null</c> is the only way a
/// content entity arrives carrying a null collection. The rule is one shared check in
/// <see cref="ContentChildRows.NullCollectionIssues"/> so it covers the families whose own validator never reads the
/// collection — species, class, archetype, instance and power only check their scalars and their inverse navigations are
/// not authored at all — the owned <c>ZoneLocation.ActorPool</c>, which <c>ContentValueCopy.ApplyAuthoredValues</c>
/// assigns wholesale, and the collections nested one level inside an owned object (<c>phonology.consonants</c>,
/// <c>stats.lines</c>, <c>objectives.items</c>), which no entity navigation carries and the structural validator never
/// reads.
/// </remarks>
[Trait("Category", "Validation")]
public class NullChildCollectionValidationTests
{
    /// <summary>
    /// Every collection-bearing family the content map resolves — the seven child-row families, the owned
    /// <c>ZoneLocation.ActorPool</c>, and the lists nested inside owned objects: the payload-shape rule reports the
    /// field, and the family validator neither throws nor contradicts it.
    /// </summary>
    [Fact]
    public async Task A_null_child_collection_is_an_error_naming_the_field_for_every_family_that_has_one()
    {
        await using var db = new ContentDbContext(
            new DbContextOptionsBuilder<ContentDbContext>().UseSqlite("DataSource=:memory:").Options);

        (string TypeKey, string Field, ContentBase Entity)[] cases =
        [
            ("recipe", "ingredients", new Recipe
            {
                Slug = "null-child-recipe",
                TypeKey = "blacksmithing",
                DisplayName = "Null Child Recipe",
                OutputItemSlug = "iron-sword",
                CraftingSkill = "blacksmithing",
                Ingredients = null!,
            }),
            ("loottable", "entries", new LootTable
            {
                Slug = "null-child-loot",
                TypeKey = "enemies",
                DisplayName = "Null Child Loot",
                Entries = null!,
            }),
            ("archetype", "powerPool", new ActorArchetype
            {
                Slug = "null-child-archetype",
                TypeKey = "humanoids",
                DisplayName = "Null Child Archetype",
                PowerPool = null!,
            }),
            ("species", "powerPool", new Species
            {
                Slug = "null-child-species",
                TypeKey = "beasts",
                DisplayName = "Null Child Species",
                PowerPool = null!,
            }),
            ("class", "powerUnlocks", new ActorClass
            {
                Slug = "null-child-class",
                TypeKey = "warriors",
                DisplayName = "Null Child Class",
                PowerUnlocks = null!,
            }),
            ("instance", "powerPool", new ActorInstance
            {
                Slug = "null-child-instance",
                TypeKey = "humanoids",
                DisplayName = "Null Child Instance",
                PowerPool = null!,
            }),
            // `power` is a read alias for the same table; `ability` is the key the write path validates and the schema
            // describes (ContentValidatorCoverageTests forbids a registry key with no schema).
            ("ability", "speciesPool", new Power
            {
                Slug = "null-child-power",
                TypeKey = "innate",
                DisplayName = "Null Child Power",
                PowerType = "innate",
                SpeciesPool = null!,
            }),
            // Not a child table: `ActorPool` is owned JSON, assigned wholesale by ContentValueCopy.ApplyAuthoredValues,
            // so a null there clears the stored spawn pool without any reconcile involved.
            ("zonelocation", "actorPool", new ZoneLocation
            {
                Slug = "null-child-location",
                TypeKey = "locations",
                DisplayName = "Null Child Location",
                ZoneId = "fenwick-crossing",
                ActorPool = null!,
            }),
            // One level in: `Phonology.Consonants` is an owned collection inside owned JSON. No entity navigation
            // carries it, `language` runs the structural validator (which reads none of the eleven lists), and a stored
            // null 500s every language read via EfCoreLanguageRepository.
            ("language", "phonology.consonants", new Language
            {
                Slug = "null-child-language",
                TypeKey = "imperial",
                DisplayName = "Null Child Language",
                Phonology = new LanguagePhonology { Consonants = null! },
            }),
            // One level in, primitive collection: `Stats.Lines` is a JSON array of strings on owned JSON.
            ("dialogue", "stats.lines", new Dialogue
            {
                Slug = "null-child-dialogue",
                TypeKey = "greetings",
                DisplayName = "Null Child Dialogue",
                Stats = new DialogueStats { Lines = null! },
            }),
            // One level in, with two lists that share a leaf name: the field has to name the owner.
            ("quest", "objectives.items", new Quest
            {
                Slug = "null-child-quest",
                TypeKey = "side",
                DisplayName = "Null Child Quest",
                Objectives = new QuestObjectives { Items = null! },
            }),
        ];

        foreach (var (typeKey, field, entity) in cases)
        {
            ContentEntityTypes.TryResolve(typeKey, out var resolver).Should().BeTrue(typeKey);

            var result = ContentEntityTypes.Validate(resolver, entity, db.Model, ContentReferenceCatalog.Empty);

            result.Errors.Should().Contain(
                issue => issue.Rule == ContentRules.ChildCollectionNull && issue.Field == field,
                $"a null '{field}' on '{typeKey}' must be refused by name");
        }
    }

    /// <summary>
    /// The family validators are also called directly (the offline pack validator and the CI guard do), so a null
    /// collection must produce an ordinary issue there too — never an unhandled <c>NullReferenceException</c>.
    /// </summary>
    [Fact]
    public void The_family_validators_tolerate_a_null_collection_instead_of_throwing()
    {
        var registry = ContentValidatorRegistry.CreateDefault();

        var recipe = new Recipe { Slug = "null-recipe", TypeKey = "blacksmithing", DisplayName = "Null", Ingredients = null! };
        var loot = new LootTable { Slug = "null-loot", TypeKey = "enemies", DisplayName = "Null", Entries = null! };
        var location = new ZoneLocation { Slug = "null-location", TypeKey = "towns", DisplayName = "Null", ActorPool = null! };
        var quest = new Quest
        {
            Slug = "null-quest",
            TypeKey = "side",
            DisplayName = "Null",
            // Nested inside the owned object: no entity navigation carries it. The payload-shape rule now walks one
            // level into owned objects and refuses it by name, but this validator is also called directly (the offline
            // pack validator and the CI guard do), so it has to tolerate the null on its own as well.
            Objectives = new QuestObjectives { Items = null! },
        };
        var dialogue = new Dialogue
        {
            Slug = "null-dialogue",
            TypeKey = "townsfolk",
            DisplayName = "Null",
            Stats = new DialogueStats { Lines = null! },
        };
        var language = new Language
        {
            Slug = "null-language",
            TypeKey = "imperial",
            DisplayName = "Null",
            Phonology = new LanguagePhonology { Consonants = null! },
        };

        registry.Validate("recipe", recipe, ContentReferenceCatalog.Empty).Issues.Should().NotBeEmpty();
        registry.Validate("loottable", loot, ContentReferenceCatalog.Empty).Issues.Should().NotBeEmpty();
        registry.Validate("zonelocation", location, ContentReferenceCatalog.Empty).Issues.Should().NotBeEmpty();
        registry.Validate("dialogue", dialogue, ContentReferenceCatalog.Empty).Issues.Should().NotBeEmpty();

        // A family whose validator never reads its owned lists (`language` runs the structural rules only) must not
        // throw either: the shape rule refuses the payload, and the validator reads past it.
        FluentActions.Invoking(() => registry.Validate("language", language, ContentReferenceCatalog.Empty))
            .Should().NotThrow("the structural validator never reads the owned phonology lists");

        // A null nested collection is the same class of shape error as a null child-row collection: it must read as
        // "no objectives" (the rule this validator already reports), not as a NullReferenceException.
        registry.Validate("quest", quest, ContentReferenceCatalog.Empty).Issues
            .Should().Contain(issue => issue.Rule == ContentRules.QuestWithoutObjectives);
    }
}
