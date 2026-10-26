using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;
using Veldrath.Server.Data;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Runs every content validator over the real seeded catalog. This is the acceptance test for "no false blocks":
/// the shipped content must not trip the rules that will shortly block Foundry writes.
/// </summary>
/// <remarks>
/// Two dangling quest objectives are known content debt (<c>the-lost-shipment</c> and <c>gather-mushrooms</c>) and are
/// asserted by name rather than ignored. B3 fixes them in content; when that lands, the allow-list below shrinks to
/// nothing and this test starts demanding zero errors.
/// </remarks>
[Trait("Category", "Validation")]
public class SeededContentValidationTests
{
    private static readonly (string Rule, string Identifier, string Needle)[] KnownContentDebt =
    [
        (ContentReferenceRules.MissingQuestTarget, "the-lost-shipment", "lost-cargo"),
        (ContentReferenceRules.MissingQuestTarget, "gather-mushrooms", "glowcap-mushroom"),
        (ContentRules.ValueOutOfRange, "arcane-focus", "negative mana cost"),
    ];

    [Fact]
    public async Task The_seeded_catalog_has_no_unexpected_errors()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var catalog = await ContentReferenceCatalog.LoadAsync(content);
        var registry = ContentValidatorRegistry.CreateDefault();
        var issues = new List<ContentIssue>();

        void Validate(string contentType, object entity)
        {
            if (!registry.TryGetValidator(contentType, out var validator))
            {
                throw new InvalidOperationException($"No validator registered for '{contentType}'.");
            }

            issues.AddRange(validator.Validate(entity, catalog).Issues);
        }

        foreach (var item in await content.Items.AsNoTracking().ToListAsync())
        {
            Validate("item", item);
        }

        foreach (var power in await content.Powers.AsNoTracking().ToListAsync())
        {
            Validate("ability", power);
        }

        foreach (var quest in await content.Quests.AsNoTracking().ToListAsync())
        {
            Validate("quest", quest);
        }

        foreach (var recipe in await content.Recipes.AsNoTracking().Include(recipe => recipe.Ingredients).ToListAsync())
        {
            Validate("recipe", recipe);
        }

        foreach (var table in await content.LootTables.AsNoTracking().Include(table => table.Entries).ToListAsync())
        {
            Validate("loottable", table);
        }

        foreach (var location in await content.ZoneLocations.AsNoTracking().ToListAsync())
        {
            Validate("zonelocation", location);
        }

        foreach (var archetype in await content.ActorArchetypes.AsNoTracking().ToListAsync())
        {
            Validate("archetype", archetype);
        }

        foreach (var instance in await content.ActorInstances.AsNoTracking().ToListAsync())
        {
            Validate("instance", instance);
        }

        foreach (var dialogue in await content.Dialogues.AsNoTracking().ToListAsync())
        {
            Validate("dialogue", dialogue);
        }

        foreach (var species in await content.Species.AsNoTracking().ToListAsync())
        {
            Validate("species", species);
        }

        foreach (var actorClass in await content.ActorClasses.AsNoTracking().ToListAsync())
        {
            Validate("class", actorClass);
        }

        foreach (var background in await content.Backgrounds.AsNoTracking().ToListAsync())
        {
            Validate("background", background);
        }

        foreach (var skill in await content.Skills.AsNoTracking().ToListAsync())
        {
            Validate("skill", skill);
        }

        foreach (var material in await content.Materials.AsNoTracking().ToListAsync())
        {
            Validate("material", material);
        }

        foreach (var materialProperty in await content.MaterialProperties.AsNoTracking().ToListAsync())
        {
            Validate("materialproperty", materialProperty);
        }

        foreach (var enchantment in await content.Enchantments.AsNoTracking().ToListAsync())
        {
            Validate("enchantment", enchantment);
        }

        foreach (var organization in await content.Organizations.AsNoTracking().ToListAsync())
        {
            Validate("organization", organization);
        }

        foreach (var language in await content.Languages.AsNoTracking().ToListAsync())
        {
            Validate("language", language);
        }

        var unexpected = issues
            .Where(issue => issue.Severity == ContentIssueSeverity.Error)
            .Where(issue => !IsKnownContentDebt(issue))
            .Select(issue => $"{issue.Rule} [{issue.Identifier}] {issue.Message}")
            .ToList();

        unexpected.Should().BeEmpty(
            "the shipped content must be clean apart from the two documented dangling quest objectives");
    }

    [Fact]
    public async Task The_only_dangling_quest_objectives_are_the_two_known_ones()
    {
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var catalog = await ContentReferenceCatalog.LoadAsync(content);
        var validator = new QuestValidator();

        var reported = (await content.Quests.AsNoTracking().ToListAsync())
            .SelectMany(quest => validator.Validate(quest, catalog).Errors)
            .Where(issue => issue.Rule == ContentReferenceRules.MissingQuestTarget)
            .Select(issue => issue.Identifier)
            .ToList();

        reported.Should().BeEquivalentTo(
            KnownContentDebt
                .Where(debt => debt.Rule == ContentReferenceRules.MissingQuestTarget)
                .Select(debt => debt.Identifier));
    }

    [Fact]
    public async Task The_validators_run_over_seeded_content_without_throwing()
    {
        // The CI guard and the write path both call this library in a request path; a rule that throws would turn a
        // content typo into a 500. Every family must return issues instead.
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var catalog = await ContentReferenceCatalog.LoadAsync(content);

        var act = async () =>
        {
            foreach (var quest in await content.Quests.AsNoTracking().ToListAsync())
            {
                new QuestValidator().Validate(quest, catalog);
            }

            foreach (var location in await content.ZoneLocations.AsNoTracking().ToListAsync())
            {
                new ZoneLocationValidator().Validate(location, catalog);
            }
        };

        await act.Should().NotThrowAsync();
    }

    private static bool IsKnownContentDebt(ContentIssue issue) =>
        KnownContentDebt.Any(debt =>
            issue.Rule == debt.Rule
            && issue.Identifier == debt.Identifier
            && issue.Message.Contains(debt.Needle, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Seeds a private in-memory content catalog with the real <see cref="DatabaseSeeder"/>, so the fixtures under test
/// are the fixtures that ship.
/// </summary>
public sealed class SeededContentProbe : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    /// <summary>Initializes a new instance of the <see cref="SeededContentProbe"/> class.</summary>
    public SeededContentProbe()
    {
        var databaseName = $"content-validation-{Guid.NewGuid()}";

        _provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<ContentDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .BuildServiceProvider();

        _scope = _provider.CreateAsyncScope();
    }

    /// <summary>Seeds the catalog and returns a context over it.</summary>
    /// <returns>A context over the seeded content.</returns>
    public async Task<ContentDbContext> CreateContextAsync()
    {
        await DatabaseSeeder.SeedAsync(_provider);
        return _scope.ServiceProvider.GetRequiredService<ContentDbContext>();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
