using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Map;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Tests.Infrastructure;
using CharacterEntity = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Unit tests for <see cref="GetLocationRosterHubCommandHandler"/> — the read a resumed session makes so the room
/// it wakes in renders its enemies instead of looking empty until the next move.
/// </summary>
public class GetLocationRosterHubCommandHandlerTests
{
    private static readonly Guid CharacterId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RunId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string RoomSlug = "rootbound-cellars";
    private const string RunGroup = "zone:the-halrow#i1:run";

    [Fact]
    public async Task Handle_Returns_The_Roster_At_The_Location_The_Character_Stands_On()
    {
        var store = new FakeEnemyInstanceStore();
        var enemy = Alive("Rootbound Warden", level: 7, health: 42);
        store.Add(RunGroup, RoomSlug, enemy);

        var (handler, _, _) = MakeHandler(store);

        var result = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, RunGroup, RunId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be(RoomSlug);
        result.SpawnedEnemies.Should().HaveCount(1);
        result.SpawnedEnemies[0].Id.Should().Be(enemy.Id);
        result.SpawnedEnemies[0].Name.Should().Be("Rootbound Warden");
        result.SpawnedEnemies[0].Level.Should().Be(7);
        result.SpawnedEnemies[0].CurrentHealth.Should().Be(42);

        // The run's own scope, not the shared zone: a resume inside a run reads the roster its party can see.
        store.RosterRequests.Should().Equal((RunGroup, RoomSlug));
    }

    [Fact]
    public async Task Handle_Spawns_On_First_Read_And_Does_Not_Add_A_Second_Cohort_On_The_Next_Resume()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, _, _) = MakeHandler(store);

        var first = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, RunGroup, RunId),
            CancellationToken.None);

        // Nothing was stored yet, so the read had to draw the pool: a resumed character sees the cohort a walk in
        // would have created, not an empty room.
        first.SpawnedEnemies.Should().NotBeEmpty();
        var aliveAfterFirst = await store.CountAliveAsync(RunGroup, RoomSlug);

        var second = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, RunGroup, RunId),
            CancellationToken.None);

        // The second resume reads the same cohort instead of inserting another one beside it.
        second.SpawnedEnemies.Select(e => e.Id)
            .Should().Equal(first.SpawnedEnemies.Select(e => e.Id));
        (await store.CountAliveAsync(RunGroup, RoomSlug)).Should().Be(aliveAfterFirst);
    }

    [Fact]
    public async Task Handle_Answers_With_No_Roster_When_The_Character_Is_Not_Standing_On_A_Location()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, characterRepo, locationRepo) = MakeHandler(store, character =>
        {
            character.CurrentNodeId = "zone:crestfall:entry";
            character.CurrentZoneLocationSlug = null;
        });

        var result = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, "zone:crestfall"),
            CancellationToken.None);

        // Standing on an entry point is a place to stand, not a room: an empty answer, never a refusal.
        result.Success.Should().BeTrue();
        result.LocationSlug.Should().BeNull();
        result.SpawnedEnemies.Should().BeEmpty();
        store.RosterRequests.Should().BeEmpty();
        locationRepo.Verify(r => r.GetBySlugAsync(It.IsAny<string>()), Times.Never);
        characterRepo.Verify(r => r.GetByIdAsync(CharacterId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Reports_A_Saved_Slug_Missing_From_The_Catalog_As_Content_Debt()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, _, locationRepo) = MakeHandler(store, locationFound: false);

        var result = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, RunGroup, RunId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SpawnedEnemies.Should().BeEmpty();
        result.ContentWarnings.Should().ContainSingle()
            .Which.Should().Contain($"the location '{RoomSlug}' is missing from the content catalog");
        store.RosterRequests.Should().BeEmpty("a slug with no content row cannot key a roster");
        locationRepo.Verify(r => r.GetBySlugAsync(RoomSlug), Times.Once);
    }

    [Fact]
    public async Task Handle_Ignores_A_Saved_Slug_That_Belongs_To_Another_Zone()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, _, _) = MakeHandler(store, locationZoneId: "crestfall");

        var result = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, RunGroup, RunId),
            CancellationToken.None);

        // A content move that left the saved slug behind must not spawn a cohort under this zone's scope, where
        // nobody standing here would ever load it.
        result.Success.Should().BeTrue();
        result.SpawnedEnemies.Should().BeEmpty();
        store.RosterRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Fails_Without_A_Scope_Or_A_Character()
    {
        var store = new FakeEnemyInstanceStore();
        var (handler, _, _) = MakeHandler(store);

        var noScope = await handler.Handle(
            new GetLocationRosterHubCommand(CharacterId, "  "),
            CancellationToken.None);
        var noCharacter = await handler.Handle(
            new GetLocationRosterHubCommand(Guid.Empty, RunGroup),
            CancellationToken.None);

        noScope.Success.Should().BeFalse();
        noScope.ErrorMessage.Should().Contain("not inside a zone");
        noCharacter.Success.Should().BeFalse();
        noCharacter.ErrorMessage.Should().Contain("Character ID");
        store.RosterRequests.Should().BeEmpty();
    }

    private static SpawnedEnemy Alive(string name, int level, int health) =>
        new()
        {
            ArchetypeSlug = "rootbound-warden",
            Name = name,
            Level = level,
            CurrentHealth = health,
            MaxHealth = health,
        };

    private static ZoneLocationEntry Location(string zoneId = "the-halrow") =>
        new(
            RoomSlug,
            "The Rootbound Cellars",
            "dungeons",
            zoneId,
            100,
            null,
            null,
            ActorPool: [new ActorPoolEntry("rootbound-warden", 1)]);

    private static (
        GetLocationRosterHubCommandHandler Handler,
        Mock<ICharacterRepository> CharacterRepo,
        Mock<IZoneLocationRepository> LocationRepo) MakeHandler(
        FakeEnemyInstanceStore store,
        Action<CharacterEntity>? configure = null,
        bool locationFound = true,
        string locationZoneId = "the-halrow")
    {
        var character = new CharacterEntity
        {
            Id = CharacterId,
            Level = 7,
            CurrentZoneId = "the-halrow",
            CurrentNodeId = "zone:the-halrow:location:rootbound-cellars",
            CurrentZoneLocationSlug = RoomSlug,
        };

        configure?.Invoke(character);

        var characterRepo = new Mock<ICharacterRepository>();
        characterRepo.Setup(r => r.GetByIdAsync(CharacterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);

        var enemyRepo = new Mock<IEnemyRepository>();
        enemyRepo.Setup(r => r.GetBySlugAsync("rootbound-warden"))
            .ReturnsAsync(new Enemy { Slug = "rootbound-warden", Name = "Rootbound Warden", Level = 7, MaxHealth = 42 });

        var locationRepo = new Mock<IZoneLocationRepository>();
        locationRepo.Setup(r => r.GetBySlugAsync(RoomSlug))
            .ReturnsAsync(locationFound ? Location(locationZoneId) : (ZoneLocationEntry?)null);

        var rosters = new LocationRosterReader(
            store,
            new RealmEngine.Data.Repositories.InMemoryActorInstanceRepository(),
            new ActorPoolResolver(new EnemyGenerator(enemyRepo.Object, NullLogger<EnemyGenerator>.Instance)),
            NullLogger<LocationRosterReader>.Instance);

        var handler = new GetLocationRosterHubCommandHandler(
            characterRepo.Object,
            locationRepo.Object,
            rosters,
            NullLogger<GetLocationRosterHubCommandHandler>.Instance);

        return (handler, characterRepo, locationRepo);
    }
}
