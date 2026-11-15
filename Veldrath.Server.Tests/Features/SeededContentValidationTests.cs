using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation;
using RealmEngine.Data.Validation.Content;
using Veldrath.Server.Data;
using MapType = RealmEngine.Shared.Models.MapType;
using SharedEdge = RealmEngine.Shared.Models.MapEdgeEntry;
using SharedGraph = RealmEngine.Shared.Models.MapGraph;
using SharedNode = RealmEngine.Shared.Models.MapNodeEntry;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Runs every content validator over the real seeded catalog. This is the acceptance test for "no false blocks":
/// the shipped content must not trip the rules that will shortly block Foundry writes.
/// </summary>
/// <remarks>
/// There is deliberately no allow-list: the shipped content must validate clean, full stop. The three defects this
/// suite originally found were fixed in content in B3, so a regression now fails the build instead of being tolerated.
/// </remarks>
[Trait("Category", "Validation")]
public class SeededContentValidationTests
{
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

        var errors = issues
            .Where(issue => issue.Severity == ContentIssueSeverity.Error)
            .Select(issue => $"{issue.Rule} [{issue.Identifier}] {issue.Message}")
            .ToList();

        errors.Should().BeEmpty("the shipped content must validate clean");
    }

    [Fact]
    public async Task No_quest_objective_points_at_missing_content()
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

        reported.Should().BeEmpty();
    }

    [Fact]
    public async Task An_injected_defect_fails_the_guard_and_names_the_quest_and_target()
    {
        // This is the failure mode the CI job relies on: a bad row must produce a message that names the entity and
        // the missing target, otherwise the job tells an author nothing.
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var catalog = await ContentReferenceCatalog.LoadAsync(content);
        var broken = new Quest
        {
            Slug = "injected-defect",
            TypeKey = "side",
            DisplayName = "Injected Defect",
            MinLevel = 1,
        };
        broken.Objectives.Items.Add(new QuestObjective
        {
            Type = RealmEngine.Shared.Models.QuestObjectiveKinds.Collect,
            Target = "no-such-item",
            Quantity = 1,
        });

        var errors = new QuestValidator().Validate(broken, catalog).Errors;

        errors.Should().Contain(issue =>
            issue.Rule == ContentReferenceRules.MissingQuestTarget
            && issue.Identifier == "injected-defect"
            && issue.Message.Contains("no-such-item"));
    }

    [Fact]
    public async Task The_seeded_world_has_no_stranded_rooms_or_missing_exits()
    {
        // The failure mode this guards is "the seeded world does not load". A dungeon with no way out, a room
        // that nothing connects to, or a hidden room gated on a key that does not exist are all content bugs
        // that only show up when a player is already inside — so they are checked before the seed is accepted.
        await using var probe = new SeededContentProbe();
        await using var content = await probe.CreateContextAsync();

        var maps = await content.Maps
            .Include(map => map.Nodes)
            .Include(map => map.Edges)
            .AsNoTracking()
            .ToListAsync();
        var locations = await content.ZoneLocations.AsNoTracking().ToListAsync();

        // A dungeon is a zone that has both an interior room and a room a dead character resurrects in; that is
        // the lobby the dungeon contract requires, and it is what separates a dungeon from the dev playground.
        var dungeonZoneSlugs = locations
            .Where(location => location.Traits.IsDungeon == true)
            .Select(location => location.ZoneId)
            .Where(zoneId => locations.Any(other => other.ZoneId == zoneId && other.Traits.SafeRespawn == true))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = MapGraphValidator.ValidateWorld(
            maps.Select(ToGraph).ToList(),
            knownRegionSlugs: maps
                .Where(map => map.MapType == MapType.Region)
                .Select(map => map.OwnerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            knownZoneSlugs: maps
                .Where(map => map.MapType == MapType.Zone)
                .Select(map => map.OwnerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            knownLocationSlugs: locations
                .Select(location => location.Slug)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            dungeonZoneSlugs: dungeonZoneSlugs);

        var errors = result.Issues
            .Where(issue => issue.Severity == MapGraphValidationSeverity.Error)
            .Select(issue => $"{issue.Code} [{issue.MapId}/{issue.NodeId}] {issue.Message}")
            .ToList();

        errors.Should().BeEmpty("the seeded world must load: every dungeon escapable and every room reachable");
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

    private static SharedGraph ToGraph(Map map) =>
        new(
            map.Id,
            map.MapType,
            map.OwnerId,
            map.Name,
            map.Description,
            map.EntryNodeId,
            map.Nodes
                .OrderBy(node => node.SortOrder)
                .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
                .Select(node => new SharedNode(
                    node.Id,
                    node.MapId,
                    node.NodeType,
                    node.RefId,
                    node.DisplayName,
                    node.Description,
                    node.IconKey,
                    node.LayoutX,
                    node.LayoutY,
                    node.IsEntryPoint,
                    node.IsExitPoint,
                    node.TargetMapId,
                    node.TargetNodeId,
                    node.IsHidden,
                    node.UnlockType,
                    node.UnlockKey,
                    node.DiscoverThreshold,
                    node.SortOrder))
                .ToList(),
            map.Edges
                .Select(edge => new SharedEdge(
                    edge.Id,
                    edge.MapId,
                    edge.FromNodeId,
                    edge.ToNodeId,
                    edge.Direction,
                    edge.EdgeType,
                    edge.Label,
                    edge.IsTraversable,
                    edge.IsHidden))
                .ToList());
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
