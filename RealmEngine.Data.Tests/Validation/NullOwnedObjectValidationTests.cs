using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Tests.Validation;

/// <summary>
/// Proves a payload that names an owned-object navigation (<c>stats</c>, <c>traits</c>, <c>phonology</c>, …) as JSON
/// <c>null</c> is refused with a named issue instead of crashing a validator with a null dereference.
/// </summary>
/// <remarks>
/// An omitted owned object deserialises to the property initializer (<c>new()</c>); an explicit <c>null</c> is the only
/// way a content entity arrives carrying a null owned reference, and the family validators dereference those
/// (<c>entity.Stats.MinLevel</c>, <c>entity.Stats.Lines</c>). The rule is one shared, model-driven check in
/// <see cref="ContentChildRows.NullOwnedObjectIssues"/> so every family is covered, including the ones whose validator
/// never reads the object (the structural families).
/// </remarks>
[Trait("Category", "Validation")]
public class NullOwnedObjectValidationTests
{
    /// <summary>
    /// Every owned reference the EF model maps, across every family the content map resolves: the payload-shape rule
    /// reports the field by name and the family validator is never handed a shape it would dereference.
    /// </summary>
    /// <remarks>
    /// Model-driven on purpose: a hand-written list would leave the next owned value out, which is how this defect
    /// class reached the write path twice. <see cref="ContentEntityTypes.KnownTypes"/> includes the read aliases
    /// (<c>weapon</c>, <c>spell</c>, …); they resolve to the same entity type as their family and are covered by the
    /// same assertions.
    /// </remarks>
    [Fact]
    public async Task A_null_owned_object_is_an_error_naming_the_field_for_every_family_that_has_one()
    {
        await using var db = new ContentDbContext(
            new DbContextOptionsBuilder<ContentDbContext>().UseSqlite("DataSource=:memory:").Options);

        var checkedFields = new List<string>();

        foreach (var typeKey in ContentEntityTypes.KnownTypes)
        {
            ContentEntityTypes.TryResolve(typeKey, out var resolver).Should().BeTrue(typeKey);

            var entityType = db.Model.FindEntityType(resolver.EntityType);
            if (entityType is null)
            {
                continue;
            }

            var ownedReferences = entityType.GetNavigations()
                .Where(navigation =>
                    !navigation.IsCollection
                    && navigation.TargetEntityType.IsOwned()
                    && navigation.PropertyInfo is { CanWrite: true })
                .ToList();

            foreach (var navigation in ownedReferences)
            {
                var entity = (ContentBase)Activator.CreateInstance(resolver.EntityType)!;
                entity.Slug = $"null-owned-{typeKey}-{navigation.Name.ToLowerInvariant()}";
                entity.TypeKey = "test";
                entity.DisplayName = "Null Owned Object";
                navigation.PropertyInfo!.SetValue(entity, null);

                var result = ContentEntityTypes.Validate(
                    resolver,
                    entity,
                    db.Model,
                    ContentReferenceCatalog.Empty);

                var field = JsonNamingPolicy.CamelCase.ConvertName(navigation.Name);
                result.Errors.Should().Contain(
                    issue => issue.Rule == ContentRules.OwnedObjectNull && issue.Field == field,
                    $"a null '{field}' on '{typeKey}' must be refused by name");

                checkedFields.Add($"{typeKey}.{field}");
            }
        }

        // Pin the reach: a family whose validator dereferences the object (the 500), an owned reference nested in no
        // entity navigation (a language's phonology), the four owned references a quest carries, and a structural
        // family whose validator reads none of them.
        checkedFields.Should().Contain(
            "zonelocation.stats",
            "dialogue.stats",
            "item.stats",
            "ability.effects",
            "instance.statOverrides",
            "language.phonology",
            "language.registerSystem",
            "quest.objectives",
            "quest.rewards",
            "class.stats");
    }

    /// <summary>
    /// The rule is silent for a stored row: owned JSON materialises every owned object as an instance, so
    /// <c>GET /api/content/{type}/{slug}/validate</c> stays clean and the rule only fires on a payload that names the
    /// field as null.
    /// </summary>
    [Fact]
    public async Task A_stored_row_loads_its_owned_objects_non_null_and_the_rule_is_silent()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options;

        await using (var write = new ContentDbContext(options))
        {
            await write.Database.EnsureCreatedAsync();
            write.ZoneLocations.Add(new ZoneLocation
            {
                Slug = "stored-owned-location",
                TypeKey = "locations",
                DisplayName = "Stored Owned Location",
                ZoneId = "varenmark",
            });
            write.Languages.Add(new Language
            {
                Slug = "stored-owned-language",
                TypeKey = "imperial",
                DisplayName = "Stored Owned Language",
            });
            await write.SaveChangesAsync();
        }

        await using var read = new ContentDbContext(options);
        var location = await read.ZoneLocations.SingleAsync(row => row.Slug == "stored-owned-location");
        var language = await read.Languages.SingleAsync(row => row.Slug == "stored-owned-language");

        location.Stats.Should().NotBeNull("owned JSON materialises, never as null");
        location.Traits.Should().NotBeNull();
        language.Phonology.Should().NotBeNull();
        language.Morphology.Should().NotBeNull();
        language.RegisterSystem.Should().NotBeNull();

        ContentChildRows.NullOwnedObjectIssues(location, read.Model).Should().BeEmpty();
        ContentChildRows.NullOwnedObjectIssues(language, read.Model).Should().BeEmpty();
    }
}
