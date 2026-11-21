using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Persistence;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers <see cref="WorldCanonSweep"/>: the only path that carries a world-text change into a database seeded
/// before it, because both world seeders stop as soon as their tables have rows.
/// </summary>
/// <remarks>
/// The sweep is provider-agnostic on purpose, so these tests drive the real thing over the same SQLite
/// (<see cref="TestDbContextFactory"/>) and in-memory contexts the seeder tests use. They prove the sweep, not the
/// boot wiring: <c>Program.cs</c> only reaches the call site when the provider is Npgsql, because migrations and
/// seeding are skipped for every other provider.
/// </remarks>
[Trait("Category", "Validation")]
public class WorldCanonSweepTests
{
    // The pre-rewrite text, exactly as a database migrated before the canon rewrite still holds it.
    private const string OldEra = "The Age of Embers";
    private const string OldGreymoorDescription =
        "Fog-wreathed Dravan highlands where ancient burial mounds dot the heather and wandering spirits trouble the living.";
    private const string OldMawDescription =
        "A vast volcanic vent complex known as the Maw, lair of Kaldrek's fire-bound ancient and final test of the worthy.";

    private const string RetiredPrefix = "Kaldrek: ";

    [Fact]
    public async Task Sweep_rewrites_a_stale_world_era_region_zone_and_map_node()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var zoneMapNodeId = "region:cinderplain:zone:kaldrek-maw";

        await WriteStaleTextAsync(app, content);

        var result = await WorldCanonSweep.SweepAsync(app, content);

        result.RowsChanged.Should().Be(4);
        result.FieldsChanged.Should().Be(4);
        result.ChangedFields.Should().BeEquivalentTo(new[]
        {
            "world:veldrath.Era",
            "region:greymoor.Description",
            "zone:kaldrek-maw.Description",
            $"mapnode:{zoneMapNodeId}.Description",
        });
        result.UnresolvedFields.Should().BeEmpty();
        result.DriftedFields.Should().BeEmpty();

        await using var reloadedApp = probe.CreateApplicationContext();
        await using var reloadedContent = probe.CreateContentContext();

        (await reloadedApp.Worlds.SingleAsync(world => world.Id == WorldCanonText.WorldId))
            .Era.Should().Be(WorldCanonText.World.Era);

        (await reloadedApp.Regions.SingleAsync(region => region.Id == "greymoor"))
            .Description.Should().Be(WorldCanonText.Regions["greymoor"].Description);

        (await reloadedApp.Zones.SingleAsync(zone => zone.Id == "kaldrek-maw"))
            .Description.Should().Be(WorldCanonText.Zones["kaldrek-maw"].Description);

        (await reloadedContent.MapNodes.SingleAsync(node => node.Id == zoneMapNodeId))
            .Description.Should().Be(WorldCanonText.MapNodes[zoneMapNodeId].Description);
    }

    [Fact]
    public async Task Sweep_rewrites_the_pre_canon_world_description_that_carries_no_retired_token()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        WorldCanonText.ContainsRetiredToken(WorldCanonText.PreCanonWorldDescription, out _)
            .Should().BeFalse("the old sentence names nothing retired, which is why exact-match detection exists");

        var world = await app.Worlds.SingleAsync(candidate => candidate.Id == WorldCanonText.WorldId);
        world.Description = WorldCanonText.PreCanonWorldDescription;
        await app.SaveChangesAsync();

        var result = await WorldCanonSweep.SweepAsync(app, content);

        result.RowsChanged.Should().Be(1);
        result.FieldsChanged.Should().Be(1);
        result.ChangedFields.Should().Equal($"world:{WorldCanonText.WorldId}.Description");
        result.UnresolvedFields.Should().BeEmpty();
        result.DriftedFields.Should().BeEmpty("a recorded pre-rewrite value is swept, not merely reported");

        await using var reloadedApp = probe.CreateApplicationContext();

        var swept = await reloadedApp.Worlds.SingleAsync(candidate => candidate.Id == WorldCanonText.WorldId);
        swept.Description.Should().Be(WorldCanonText.World.Description, "the description must not contradict the era beside it");
        swept.Era.Should().Be(WorldCanonText.World.Era);
    }

    [Fact]
    public async Task Sweep_reports_an_unrecorded_token_free_world_description_and_leaves_it_alone()
    {
        const string unrecorded = "A world opening nobody recorded in KnownStaleValues.";

        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var world = await app.Worlds.SingleAsync(candidate => candidate.Id == WorldCanonText.WorldId);
        world.Description = unrecorded;
        await app.SaveChangesAsync();

        var result = await WorldCanonSweep.SweepAsync(app, content);

        result.RowsChanged.Should().Be(0, "a divergence the sweep cannot recognise is never overwritten");
        result.FieldsChanged.Should().Be(0);
        result.UnresolvedFields.Should().BeEmpty();
        result.DriftedFields.Should().ContainSingle();
        result.DriftedFields[0].Should().StartWith($"world:{WorldCanonText.WorldId}.Description");
        result.DriftedFields[0].Should().Contain(unrecorded, "the report must quote what it found to be actionable");

        await using var reloadedApp = probe.CreateApplicationContext();

        (await reloadedApp.Worlds.SingleAsync(candidate => candidate.Id == WorldCanonText.WorldId))
            .Description.Should().Be(unrecorded);
    }

    [Fact]
    public async Task Sweep_rewrites_stale_display_names_but_leaves_the_slugs()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var zone = await app.Zones.SingleAsync(candidate => candidate.Id == "kaldrek-maw");
        zone.Name = "Kaldrek's Maw";
        await app.SaveChangesAsync();

        var map = await content.Maps.SingleAsync(candidate => candidate.Id == "zone:kaldrek-maw");
        map.Name = "Kaldrek's Maw";

        var zoneNode = await content.MapNodes.SingleAsync(candidate => candidate.Id == "region:cinderplain:zone:kaldrek-maw");
        zoneNode.DisplayName = "Kaldrek's Maw";

        var heartNode = await content.MapNodes.SingleAsync(candidate => candidate.Id == "zone:kaldrek-maw:location:kaldreks-heart");
        heartNode.DisplayName = "Kaldrek's Heart";

        var heart = await content.ZoneLocations.SingleAsync(candidate => candidate.Slug == "kaldreks-heart");
        heart.DisplayName = "Kaldrek's Heart";
        await content.SaveChangesAsync();

        var result = await WorldCanonSweep.SweepAsync(app, content);

        result.RowsChanged.Should().Be(5);
        result.FieldsChanged.Should().Be(5);
        result.UnresolvedFields.Should().BeEmpty();

        await using var reloadedApp = probe.CreateApplicationContext();
        await using var reloadedContent = probe.CreateContentContext();

        (await reloadedApp.Zones.SingleAsync(candidate => candidate.Id == "kaldrek-maw"))
            .Name.Should().Be("The Maw", "the display name carries canon while the id stays an opaque key");
        (await reloadedContent.Maps.SingleAsync(candidate => candidate.Id == "zone:kaldrek-maw"))
            .Name.Should().Be("The Maw");
        (await reloadedContent.MapNodes.SingleAsync(candidate => candidate.Id == "zone:kaldrek-maw:location:kaldreks-heart"))
            .DisplayName.Should().Be("The Maw's Heart");
        (await reloadedContent.ZoneLocations.SingleAsync(candidate => candidate.Slug == "kaldreks-heart"))
            .DisplayName.Should().Be("The Maw's Heart");
    }

    [Fact]
    public async Task Sweep_is_idempotent()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        await WriteStaleTextAsync(app, content);

        var first = await WorldCanonSweep.SweepAsync(app, content);
        first.FieldsChanged.Should().BeGreaterThan(0, "the setup wrote the pre-rewrite text back");
        first.DriftedFields.Should().BeEmpty();

        var second = await WorldCanonSweep.SweepAsync(app, content);

        second.RowsChanged.Should().Be(0);
        second.FieldsChanged.Should().Be(0);
        second.ChangedFields.Should().BeEmpty();
        second.UnresolvedFields.Should().BeEmpty();
        second.DriftedFields.Should().BeEmpty("after the first run every field equals canon, so nothing is left to report");

        await using var reloadedApp = probe.CreateApplicationContext();
        await using var reloadedContent = probe.CreateContentContext();

        (await reloadedApp.Worlds.SingleAsync(world => world.Id == WorldCanonText.WorldId))
            .Era.Should().Be(WorldCanonText.World.Era, "the second run must not move a value");
        (await reloadedApp.Regions.SingleAsync(region => region.Id == "greymoor"))
            .Description.Should().Be(WorldCanonText.Regions["greymoor"].Description);
        (await reloadedContent.MapNodes.SingleAsync(node => node.Id == "region:cinderplain:zone:kaldrek-maw"))
            .Description.Should().Be(WorldCanonText.MapNodes["region:cinderplain:zone:kaldrek-maw"].Description);
    }

    [Fact]
    public async Task Sweep_leaves_authored_text_alone()
    {
        const string authored = "Hand-authored by the map editor.";

        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var region = await app.Regions.SingleAsync(candidate => candidate.Id == "greymoor");
        region.Description = authored;

        var map = await content.Maps.SingleAsync(candidate => candidate.Id == "zone:ashfields");
        map.Description = authored;

        var node = await content.MapNodes.SingleAsync(candidate => candidate.Id == "zone:ashfields:location:ash-shrine");
        node.Description = authored;

        await app.SaveChangesAsync();
        await content.SaveChangesAsync();

        var result = await WorldCanonSweep.SweepAsync(app, content);

        result.RowsChanged.Should().Be(0, "text without a retired token is authored content, not stale seed text");
        result.FieldsChanged.Should().Be(0);
        result.ChangedFields.Should().BeEmpty();
        result.UnresolvedFields.Should().BeEmpty();

        // A region row has no writer but the seeder, so its divergence is reported; a map or node is authored in
        // Foundry, where divergence is the norm and reporting it would warn on every start.
        result.DriftedFields.Should().ContainSingle();
        result.DriftedFields[0].Should().StartWith("region:greymoor.Description");

        await using var reloadedApp = probe.CreateApplicationContext();
        await using var reloadedContent = probe.CreateContentContext();

        (await reloadedApp.Regions.SingleAsync(candidate => candidate.Id == "greymoor")).Description.Should().Be(authored);
        (await reloadedContent.Maps.SingleAsync(candidate => candidate.Id == "zone:ashfields")).Description.Should().Be(authored);
        (await reloadedContent.MapNodes.SingleAsync(candidate => candidate.Id == "zone:ashfields:location:ash-shrine"))
            .Description.Should().Be(authored);
    }

    [Fact]
    public async Task Sweep_never_rewrites_ids_or_slugs()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var zoneIds = await app.Zones.AsNoTracking().Select(zone => zone.Id).ToListAsync();
        var regionIds = await app.Regions.AsNoTracking().Select(region => region.Id).ToListAsync();
        var locationSlugs = await content.ZoneLocations.AsNoTracking().Select(location => location.Slug).ToListAsync();
        var mapIds = await content.Maps.AsNoTracking().Select(map => map.Id).ToListAsync();
        var nodeIds = await content.MapNodes.AsNoTracking().Select(node => node.Id).ToListAsync();
        var nodeRefIds = await content.MapNodes.AsNoTracking().Select(node => node.RefId).ToListAsync();
        var nodeUnlockKeys = await content.MapNodes.AsNoTracking().Select(node => node.UnlockKey).ToListAsync();

        await WriteStaleTextAsync(app, content);
        await WorldCanonSweep.SweepAsync(app, content);

        await using var reloadedApp = probe.CreateApplicationContext();
        await using var reloadedContent = probe.CreateContentContext();

        (await reloadedApp.Zones.AsNoTracking().Select(zone => zone.Id).ToListAsync()).Should().Equal(zoneIds);
        (await reloadedApp.Regions.AsNoTracking().Select(region => region.Id).ToListAsync()).Should().Equal(regionIds);
        (await reloadedContent.ZoneLocations.AsNoTracking().Select(location => location.Slug).ToListAsync()).Should().Equal(locationSlugs);
        (await reloadedContent.Maps.AsNoTracking().Select(map => map.Id).ToListAsync()).Should().Equal(mapIds);
        (await reloadedContent.MapNodes.AsNoTracking().Select(node => node.Id).ToListAsync()).Should().Equal(nodeIds);
        (await reloadedContent.MapNodes.AsNoTracking().Select(node => node.RefId).ToListAsync()).Should().Equal(nodeRefIds);
        (await reloadedContent.MapNodes.AsNoTracking().Select(node => node.UnlockKey).ToListAsync()).Should().Equal(nodeUnlockKeys);

        zoneIds.Should().Contain("kaldrek-maw", "the lowercase slug is an opaque key, not prose");
        locationSlugs.Should().Contain("kaldreks-heart");
        nodeIds.Should().Contain("zone:kaldrek-maw:location:kaldreks-heart");
        nodeUnlockKeys.Should().Contain("kaldreks-heart");
    }

    [Fact]
    public async Task Canon_matches_the_rows_both_seeders_write()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        var world = await app.Worlds.AsNoTracking().SingleAsync();
        world.Id.Should().Be(WorldCanonText.WorldId);
        world.Name.Should().Be(WorldCanonText.World.Name);
        world.Description.Should().Be(WorldCanonText.World.Description);
        world.Era.Should().Be(WorldCanonText.World.Era);

        var regions = await app.Regions.AsNoTracking().ToListAsync();
        regions.Select(region => region.Id).Should().BeEquivalentTo(WorldCanonText.Regions.Keys);
        foreach (var region in regions)
        {
            region.Name.Should().Be(WorldCanonText.Regions[region.Id].Name);
            region.Description.Should().Be(WorldCanonText.Regions[region.Id].Description);
        }

        var zones = await app.Zones.AsNoTracking().ToListAsync();
        zones.Select(zone => zone.Id).Should().BeEquivalentTo(WorldCanonText.Zones.Keys);
        foreach (var zone in zones)
        {
            zone.Name.Should().Be(WorldCanonText.Zones[zone.Id].Name);
            zone.Description.Should().Be(WorldCanonText.Zones[zone.Id].Description);
        }

        // The map graph is the one copy the sweep cannot share with its seeder: MapGraphSeeder lives in
        // RealmEngine.Data and cannot reference this assembly. These assertions are what keep the two honest.
        var maps = await content.Maps.AsNoTracking().ToListAsync();
        maps.Select(map => map.Id).Should().BeEquivalentTo(WorldCanonText.Maps.Keys);
        foreach (var map in maps)
        {
            map.Name.Should().Be(WorldCanonText.Maps[map.Id].Name);
            map.Description.Should().Be(WorldCanonText.Maps[map.Id].Description);
        }

        var nodes = await content.MapNodes.AsNoTracking().ToListAsync();
        foreach (var (id, canon) in WorldCanonText.MapNodes)
        {
            var node = nodes.SingleOrDefault(candidate => candidate.Id == id);
            node.Should().NotBeNull($"{id} is the id the sweep rewrites, so MapGraphSeeder must still seed it");
            node!.DisplayName.Should().Be(canon.Name);
            node.Description.Should().Be(canon.Description);
        }

        foreach (var (slug, displayName) in WorldCanonText.ZoneLocationDisplayNames)
        {
            var location = await content.ZoneLocations.AsNoTracking().SingleAsync(candidate => candidate.Slug == slug);
            location.DisplayName.Should().Be(displayName);
        }
    }

    [Fact]
    public async Task Sweep_restores_every_field_it_holds_canon_for()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        // No pre-rewrite copy of the strings is kept in the tests; a retired token in front of the shipped value
        // is enough to make the field stale, and it proves the sweep rewrites what it claims to cover.
        foreach (var world in await app.Worlds.ToListAsync())
        {
            world.Name = RetiredPrefix + world.Name;
            world.Description = RetiredPrefix + world.Description;
            world.Era = RetiredPrefix + world.Era;
        }

        foreach (var region in await app.Regions.ToListAsync())
        {
            region.Name = RetiredPrefix + region.Name;
            region.Description = RetiredPrefix + region.Description;
        }

        foreach (var zone in await app.Zones.ToListAsync())
        {
            zone.Name = RetiredPrefix + zone.Name;
            zone.Description = RetiredPrefix + zone.Description;
        }

        await app.SaveChangesAsync();

        foreach (var map in await content.Maps.ToListAsync())
        {
            map.Name = RetiredPrefix + map.Name;
            map.Description = RetiredPrefix + map.Description;
        }

        foreach (var node in await content.MapNodes.ToListAsync())
        {
            if (!WorldCanonText.MapNodes.ContainsKey(node.Id))
                continue;

            node.DisplayName = RetiredPrefix + node.DisplayName;
            node.Description = RetiredPrefix + node.Description;
        }

        foreach (var location in await content.ZoneLocations.ToListAsync())
        {
            if (WorldCanonText.ZoneLocationDisplayNames.ContainsKey(location.Slug))
                location.DisplayName = RetiredPrefix + location.DisplayName;
        }

        await content.SaveChangesAsync();

        var result = await WorldCanonSweep.SweepAsync(app, content);

        var expectedFields = 3
            + (WorldCanonText.Regions.Count * 2)
            + (WorldCanonText.Zones.Count * 2)
            + (WorldCanonText.Maps.Count * 2)
            + (WorldCanonText.MapNodes.Count * 2)
            + WorldCanonText.ZoneLocationDisplayNames.Count;

        result.FieldsChanged.Should().Be(expectedFields);
        result.UnresolvedFields.Should().BeEmpty("every id the canon names has a replacement");
        result.DriftedFields.Should().BeEmpty("every field was made stale with a recorded token, so every field is swept");

        await using var reloadedApp = probe.CreateApplicationContext();
        await using var reloadedContent = probe.CreateContentContext();

        (await reloadedApp.Worlds.AsNoTracking().SingleAsync()).Era.Should().Be(WorldCanonText.World.Era);
        (await reloadedApp.Regions.AsNoTracking().ToListAsync())
            .Should().OnlyContain(region => region.Name == WorldCanonText.Regions[region.Id].Name
                                            && region.Description == WorldCanonText.Regions[region.Id].Description);
        (await reloadedApp.Zones.AsNoTracking().ToListAsync())
            .Should().OnlyContain(zone => zone.Name == WorldCanonText.Zones[zone.Id].Name
                                          && zone.Description == WorldCanonText.Zones[zone.Id].Description);
        (await reloadedContent.Maps.AsNoTracking().ToListAsync())
            .Should().OnlyContain(map => map.Name == WorldCanonText.Maps[map.Id].Name
                                         && map.Description == WorldCanonText.Maps[map.Id].Description);
    }

    [Fact]
    public async Task Sweep_reports_stale_text_it_has_no_canon_for_and_leaves_it_alone()
    {
        await using var probe = new CanonSweepProbe();
        await using var app = probe.CreateApplicationContext();
        await using var content = await probe.CreateContentContextAsync();

        app.Zones.Add(new Zone
        {
            Id = "kaldrek-fane",
            Name = "Kaldrek's Fane",
            Description = "A shrine raised to Kaldrek before the Collapse.",
            Type = ZoneType.Dungeon,
            RegionId = "cinderplain",
        });
        await app.SaveChangesAsync();

        var result = await WorldCanonSweep.SweepAsync(app, content);

        result.FieldsChanged.Should().Be(0);
        result.UnresolvedFields.Should().HaveCount(2);
        result.UnresolvedFields.Should().OnlyContain(field => field.StartsWith("zone:kaldrek-fane.", StringComparison.Ordinal));
        result.DriftedFields.Should().BeEmpty("a field with no canon entry is reported once, as unresolved");

        await using var reloadedApp = probe.CreateApplicationContext();

        var fane = await reloadedApp.Zones.SingleAsync(zone => zone.Id == "kaldrek-fane");
        fane.Name.Should().Be("Kaldrek's Fane", "a row the canon does not name is reported, never guessed at");
        fane.Description.Should().Be("A shrine raised to Kaldrek before the Collapse.");
    }

    private static async Task WriteStaleTextAsync(ApplicationDbContext app, ContentDbContext content)
    {
        var world = await app.Worlds.SingleAsync(candidate => candidate.Id == WorldCanonText.WorldId);
        world.Era = OldEra;

        var region = await app.Regions.SingleAsync(candidate => candidate.Id == "greymoor");
        region.Description = OldGreymoorDescription;

        var zone = await app.Zones.SingleAsync(candidate => candidate.Id == "kaldrek-maw");
        zone.Description = OldMawDescription;
        await app.SaveChangesAsync();

        var node = await content.MapNodes.SingleAsync(candidate => candidate.Id == "region:cinderplain:zone:kaldrek-maw");
        node.Description = OldMawDescription;
        await content.SaveChangesAsync();
    }

    /// <summary>
    /// Builds a seeded application context (SQLite) and a seeded content context (in-memory), disposed together —
    /// the same pair the seeder tests use.
    /// </summary>
    private sealed class CanonSweepProbe : IAsyncDisposable
    {
        private readonly TestDbContextFactory _applicationFactory = new();
        private readonly ServiceProvider _contentProvider;
        private readonly List<AsyncServiceScope> _contentScopes = [];

        public CanonSweepProbe()
        {
            // An explicit root keeps every scope on the same in-memory store, so a later context reads what an
            // earlier one saved — the reload the assertions depend on.
            var root = new InMemoryDatabaseRoot();
            var databaseName = $"canon-sweep-{Guid.NewGuid()}";

            _contentProvider = new ServiceCollection()
                .AddLogging()
                .AddDbContext<ContentDbContext>(options => options.UseInMemoryDatabase(databaseName, root))
                .BuildServiceProvider();
        }

        /// <summary>Returns an application context over the shared SQLite connection, seeded with the world.</summary>
        public ApplicationDbContext CreateApplicationContext() => _applicationFactory.CreateContext();

        /// <summary>Seeds the content catalog once and returns a context over it, including the map graph.</summary>
        public async Task<ContentDbContext> CreateContentContextAsync()
        {
            await DatabaseSeeder.SeedAsync(_contentProvider);
            return CreateContentContext();
        }

        /// <summary>Returns a fresh context over the same in-memory content store, unsaved changes and all.</summary>
        public ContentDbContext CreateContentContext()
        {
            var scope = _contentProvider.CreateAsyncScope();
            _contentScopes.Add(scope);
            return scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            foreach (var scope in _contentScopes)
                await scope.DisposeAsync();

            await _contentProvider.DisposeAsync();
            _applicationFactory.Dispose();
        }
    }
}
