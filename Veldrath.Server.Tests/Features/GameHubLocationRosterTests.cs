using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Map;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for <see cref="GameHub.GetLocationRoster"/>: the read a resumed session makes so the room it
/// wakes in renders its enemies instead of looking empty until the next move.
/// </summary>
public class GameHubLocationRosterTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static void ShouldBeError(FakeClientProxy proxy, string message) =>
        proxy.SentMessages.Last(m => m.Method == "Error").Args[0].Should().Be(message);

    /// <summary>
    /// Reads the roster back out of the anonymous <c>LocationRoster</c> payload. The hub sends anonymous objects on
    /// purpose (that is what the payload-contract suite compares), so the test reads the property it asserts on.
    /// </summary>
    /// <param name="proxy">Proxy that received the broadcast.</param>
    /// <returns>The enemies the payload carried.</returns>
    private static IReadOnlyList<SpawnedEnemySummary> RosterOf(FakeClientProxy proxy)
    {
        var payload = proxy.SentMessages.Last(message => message.Method == "LocationRoster").Args[0]!;
        var roster = payload.GetType().GetProperty("SpawnedEnemies")!.GetValue(payload)!;
        return ((IEnumerable<SpawnedEnemySummary>)roster).ToList();
    }

    [Fact]
    public async Task GetLocationRoster_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetLocationRoster();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetLocationRoster");
    }

    [Fact]
    public async Task GetLocationRoster_Requires_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        ctx.Items["CharacterName"] = "Bran";

        await hub.GetLocationRoster();

        ShouldBeError(clients.Caller, "EnterZone must be called before GetLocationRoster");
    }

    [Fact]
    public async Task GetLocationRoster_Asks_Under_The_Run_The_Connection_Is_Inside()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);

        var characterId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var enemyId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran", "the-halrow", "zone:the-halrow#i1:run");
        ctx.Items["CurrentZoneInstanceId"] = runId;
        mediator.Returns(new GetLocationRosterHubResult
        {
            Success = true,
            LocationSlug = "rootbound-cellars",
            SpawnedEnemies = [new SpawnedEnemySummary(enemyId, "Rootbound Warden", 7, 40, 40)],
        });

        await hub.GetLocationRoster();

        var command = mediator.Invocations.Select(invocation => invocation.Arguments[0])
            .OfType<GetLocationRosterHubCommand>().Single();
        command.CharacterId.Should().Be(characterId);
        command.ZoneGroup.Should().Be("zone:the-halrow#i1:run", "a resumed run owns its group");
        command.ZoneInstanceId.Should().Be(runId, "the roster is keyed on the run, not the shared zone");

        var payload = clients.Caller.SentMessages.Single(message => message.Method == "LocationRoster").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            LocationSlug = "rootbound-cellars",
            SpawnedEnemies = new[]
            {
                new { Id = enemyId, Name = "Rootbound Warden", Level = 7, CurrentHealth = 40, MaxHealth = 40 },
            },
        });
    }

    [Fact]
    public async Task GetLocationRoster_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran", "crestfall", "zone:crestfall");
        mediator.Returns(new GetLocationRosterHubResult { Success = false, ErrorMessage = "Character was not found" });

        await hub.GetLocationRoster();

        ShouldBeError(clients.Caller, "Character was not found");
        clients.Caller.SentMessages.Should().NotContain(message => message.Method == "LocationRoster");
    }

    [Fact]
    public async Task GetLocationRoster_Reports_Each_Content_Warning()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), "Bran", "crestfall", "zone:crestfall");
        mediator.Returns(new GetLocationRosterHubResult
        {
            Success = true,
            LocationSlug = "crestfall-square",
            ContentWarnings = ["Pool 'goblins' references no known actors"],
        });

        await hub.GetLocationRoster();

        // The same failsafe a walk applies: content debt is said out loud, not swallowed.
        clients.Caller.SentMessages.Should().Contain(message =>
            message.Method == "SystemMessage" &&
            (string)message.Args[0]! == "Pool 'goblins' references no known actors");
    }

    [Fact]
    public async Task GetLocationRoster_After_A_Resume_Reads_The_Same_Cohort_Twice()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentMapId = "zone:crestfall";
        character.CurrentNodeId = "zone:crestfall:location:town-square";
        character.CurrentZoneLocationSlug = "town-square";
        await db.SaveChangesAsync();

        var mapRepo = new Mock<IMapGraphRepository>();
        mapRepo.Setup(map => map.GetMapForZoneAsync("crestfall", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SquareMap());

        var locations = new Mock<IZoneLocationRepository>();
        locations.Setup(repo => repo.GetBySlugAsync("town-square"))
            .ReturnsAsync(new ZoneLocationEntry(
                "town-square",
                "Crestfall Square",
                "locations",
                "crestfall",
                10,
                null,
                null,
                ActorPool: [new ActorPoolEntry("town-drunk", 1)]));

        var enemyRepo = new Mock<IEnemyRepository>();
        enemyRepo.Setup(repo => repo.GetBySlugAsync("town-drunk"))
            .ReturnsAsync(new Enemy { Slug = "town-drunk", Name = "Town Drunk", Level = 2, MaxHealth = 18 });

        // The real store and the real handler: the cohort this read creates is the one the database holds, which is
        // what makes "a second resume does not spawn a second cohort" a fact rather than an assertion about a stub.
        var resolver = new ActorPoolResolver(new EnemyGenerator(enemyRepo.Object, NullLogger<EnemyGenerator>.Instance));
        var reader = new LocationRosterReader(
            new EfCoreEnemyInstanceStore(
                db,
                resolver,
                Options.Create(new EnemyWorldOptions()),
                NullLogger<EfCoreEnemyInstanceStore>.Instance),
            new RealmEngine.Data.Repositories.InMemoryActorInstanceRepository(),
            resolver,
            NullLogger<LocationRosterReader>.Instance);
        var handler = new GetLocationRosterHubCommandHandler(
            new CharacterRepository(db),
            locations.Object,
            reader,
            NullLogger<GetLocationRosterHubCommandHandler>.Instance);

        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(It.IsAny<GetLocationRosterHubCommand>(), It.IsAny<CancellationToken>()))
            .Returns((GetLocationRosterHubCommand command, CancellationToken token) => handler.Handle(command, token));

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, mediator: mediator, mapRepo: mapRepo, locations: locations.Object,
            connectionId: "conn-resume-roster");
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        // The login: EnterZone resumes the saved node, and the shell then asks for the roster at it.
        await hub.EnterZone("crestfall");
        await hub.GetLocationRoster();

        var first = RosterOf(clients.Caller);
        first.Should().NotBeEmpty("the pool at the room the character resumed into draws a cohort on first contact");
        var storedAfterFirst = await CountStoredAsync(db);

        // The same login again: a second resume reads the cohort already standing there.
        await hub.EnterZone("crestfall");
        await hub.GetLocationRoster();

        RosterOf(clients.Caller).Select(enemy => enemy.Id).Should().Equal(first.Select(enemy => enemy.Id));
        (await CountStoredAsync(db)).Should().Be(storedAfterFirst, "a resume is a read, not another spawn");
    }

    private static Task<int> CountStoredAsync(ApplicationDbContext db) =>
        db.EnemyInstances.AsNoTracking()
            .CountAsync(row => row.ZoneGroup == "zone:crestfall" && row.LocationSlug == "town-square");

    /// <summary>The zone map the resume lands on: an entry node and the square the character logged out on.</summary>
    /// <returns>The graph the hub reads for the zone.</returns>
    private static MapGraph SquareMap() => new(
        "zone:crestfall", MapType.Zone, "crestfall", "Crestfall", string.Empty,
        "zone:crestfall:entry",
        [
            new MapNodeEntry(
                "zone:crestfall:entry", "zone:crestfall", MapNodeType.EntryPoint, null, "Arrival",
                string.Empty, null, null, null, true, false, null, null, false, null, null, null, 0),
            new MapNodeEntry(
                "zone:crestfall:location:town-square", "zone:crestfall", MapNodeType.Location,
                "town-square", "Crestfall Square", string.Empty, null, null, null, false, false, null, null,
                false, null, null, null, 10),
        ],
        []);
}
