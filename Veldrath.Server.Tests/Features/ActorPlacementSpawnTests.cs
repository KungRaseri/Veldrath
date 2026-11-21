using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the placed-actor spawn path end to end, against the real seeded catalog: how many actors stand at a location,
/// what the named one is worth, and — the reason the instance slug is stored on the row — what a reloaded boss still
/// hits for.
/// </summary>
/// <remarks>
/// Before A2 a placed instance was unreachable: the two seeded rows were not referenced by any location, the spawn
/// path never queried them, and the store rebuilt every template from the archetype slug. These assertions are the
/// acceptance test for the composed template surviving the round trip through the database.
/// </remarks>
public class ActorPlacementSpawnTests : IAsyncDisposable
{
    private const string ZoneGroup = "zone:ashlen-wood";
    private const string LocationSlug = "old-clearing";

    private readonly TestDbContextFactory _factory = new();
    private readonly SeededContentProbe _content = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await _content.DisposeAsync();
    }

    [Fact]
    public async Task The_old_clearing_spawns_the_elder_chief_among_its_roster()
    {
        await using var world = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        var (store, resolver, instances) = CreateSpawnPath(world, content);

        var location = await content.ZoneLocations.AsNoTracking()
            .SingleAsync(entry => entry.Slug == LocationSlug);
        var placements = await instances.GetPlacedAsync(LocationSlug);

        placements.Should().ContainSingle(
            instance => instance.Slug == "elder-goblin-chief",
            "the boss is placed in the clearing, not only catalogued");

        var roster = await store.GetAliveOrSpawnAsync(
            ZoneGroup,
            LocationSlug,
            _ => resolver.SpawnRosterAsync(Pool(location), placements),
            null,
            CancellationToken.None);

        roster.Should().OnlyContain(enemy => enemy.Name != "Elder Goblin Chief" || enemy.InstanceSlug == "elder-goblin-chief");

        var chief = roster.Single(enemy => enemy.Name == "Elder Goblin Chief");
        chief.MaxHealth.Should().Be(120);
        chief.CurrentHealth.Should().Be(120);
        chief.Level.Should().Be(5);
        chief.BaseXp.Should().Be(80);
        chief.GoldReward.Should().BeInRange(10, 30);
        chief.Template.Strength.Should().Be(14);
        chief.Template.BasePhysicalDamage.Should().Be(
            12,
            "the composed template is what combat reads, so the override has to survive onto the spawn");
        chief.ArchetypeSlug.Should().Be("goblin-scout", "the archetype stays the identity the store reconciles on");
    }

    [Fact]
    public async Task A_reloaded_row_keeps_the_instance_slug_and_therefore_its_override_damage()
    {
        await using var world = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        var (store, resolver, instances) = CreateSpawnPath(world, content);

        var location = await content.ZoneLocations.AsNoTracking().SingleAsync(entry => entry.Slug == LocationSlug);
        var placements = await instances.GetPlacedAsync(LocationSlug);

        var first = await store.GetAliveOrSpawnAsync(
            ZoneGroup,
            LocationSlug,
            _ => resolver.SpawnRosterAsync(Pool(location), placements),
            null,
            CancellationToken.None);

        // Second arrival: the rows exist, so the store reads them back and rebuilds each template from the row. This is
        // the path that used to rebuild the archetype's template and fight at goblin-scout damage.
        var second = await store.GetAliveOrSpawnAsync(
            ZoneGroup,
            LocationSlug,
            _ => throw new InvalidOperationException("a stored roster must not be respawned"),
            null,
            CancellationToken.None);

        second.Should().HaveCount(first.Count);
        var chief = second.Single(enemy => enemy.Name == "Elder Goblin Chief");
        chief.InstanceSlug.Should().Be("elder-goblin-chief");
        chief.Template.BasePhysicalDamage.Should().Be(12);
        chief.Template.Strength.Should().Be(14);
        chief.MaxHealth.Should().Be(120);

        world.EnemyInstances.Count(row => row.LocationSlug == LocationSlug && row.InstanceSlug == "elder-goblin-chief")
            .Should().Be(1, "arriving twice yields one row, not two chiefs");
    }

    [Fact]
    public async Task The_collapsed_vault_spawns_captain_blackthorn()
    {
        await using var world = _factory.CreateContext();
        var content = await _content.CreateContextAsync();
        var (store, resolver, instances) = CreateSpawnPath(world, content);

        var location = await content.ZoneLocations.AsNoTracking()
            .SingleAsync(entry => entry.Slug == "collapsed-vault");
        var placements = await instances.GetPlacedAsync("collapsed-vault");

        var roster = await store.GetAliveOrSpawnAsync(
            "zone:the-halrow",
            "collapsed-vault",
            _ => resolver.SpawnRosterAsync(Pool(location), placements),
            null,
            CancellationToken.None);

        var captain = roster.Single(enemy => enemy.Name == "Captain Blackthorn");
        captain.MaxHealth.Should().Be(200);
        captain.Level.Should().Be(8);
        captain.Template.BasePhysicalDamage.Should().Be(18);
        captain.InstanceSlug.Should().Be("captain-blackthorn");
    }

    /// <summary>Reads a location's pool into the spawn model the resolver takes.</summary>
    /// <param name="location">Seeded zone-location row.</param>
    /// <returns>The weighted pool entries.</returns>
    private static List<ActorPoolEntry> Pool(RealmEngine.Data.Entities.ZoneLocation location) =>
        [.. location.ActorPool.Select(entry => new ActorPoolEntry(entry.ArchetypeSlug, entry.Weight))];

    /// <summary>Builds the production spawn path over the two real seeded databases.</summary>
    /// <param name="world">Application database holding live enemy rows.</param>
    /// <param name="content">Seeded content catalog.</param>
    /// <returns>The store, the resolver that feeds it, and the instance repository.</returns>
    private static (EfCoreEnemyInstanceStore Store, ActorPoolResolver Resolver, EfCoreActorInstanceRepository Instances)
        CreateSpawnPath(ApplicationDbContext world, RealmEngine.Data.Persistence.ContentDbContext content)
    {
        var instances = new EfCoreActorInstanceRepository(
            content, NullLogger<EfCoreActorInstanceRepository>.Instance);
        var archetypes = new EfCoreEnemyRepository(content, NullLogger<EfCoreEnemyRepository>.Instance);
        var resolver = new ActorPoolResolver(
            new EnemyGenerator(archetypes, NullLogger<EnemyGenerator>.Instance),
            NullLogger<ActorPoolResolver>.Instance,
            randomSource: null,
            instances);

        var store = new EfCoreEnemyInstanceStore(
            world,
            resolver,
            Options.Create(new EnemyWorldOptions()),
            NullLogger<EfCoreEnemyInstanceStore>.Instance);

        return (store, resolver, instances);
    }
}
