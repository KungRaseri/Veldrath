using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Map;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Map;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Hubs;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the zone, map and exploration hub methods of <see cref="GameHub"/>:
/// joining/leaving a zone, travelling the map graph, location discovery and area searches.
/// </summary>
public class GameHubZoneTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static void ShouldBeError(FakeClientProxy proxy, string message) =>
        proxy.SentMessages.Last(m => m.Method == "Error").Args[0].Should().Be(message);

    /// <summary>Builds the run service over the test's own context, the way the harness wires the hub's.</summary>
    /// <param name="db">Context the hub and the service share.</param>
    /// <param name="locations">Zone-location catalog the service reads a room from.</param>
    /// <returns>The service the hub is using.</returns>
    private static EfCoreZoneInstanceService ZoneInstances(
        ApplicationDbContext db,
        RealmEngine.Shared.Abstractions.IZoneLocationRepository locations) =>
        new(
            db,
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            locations,
            NullLogger<EfCoreZoneInstanceService>.Instance);

    // ── EnterZone ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnterZone_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.EnterZone("crestfall");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before EnterZone");
    }

    [Fact]
    public async Task EnterZone_Reports_An_Unknown_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("no-such-zone");

        ShouldBeError(clients.Caller, "Zone 'no-such-zone' does not exist");
    }

    [Fact]
    public async Task EnterZone_Creates_A_Session_Joins_The_Group_And_Announces_Arrival()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, clients, groups, ctx, _, _) = HubHarness.Create(db, account.Id, connectionId: "conn-enter");
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;
        ctx.Items["DifficultyMode"] = "normal";

        await hub.EnterZone("crestfall");

        groups.AddedGroups.Should().Contain("zone:crestfall", "a town is not difficulty-scoped");
        clients.OthersInGroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "PlayerEntered");

        var entered = clients.Caller.SentMessages.Single(m => m.Method == "ZoneEntered").Args[0];
        entered.Should().BeEquivalentTo(new
        {
            Id = "crestfall",
            Name = "Crestfall",
            ZoneType = "Town",
            HasInn = true,
            HasMerchant = true,
        });

        var session = db.PlayerSessions.Should().ContainSingle(s => s.CharacterId == character.Id).Subject;
        session.ConnectionId.Should().Be("conn-enter");
        session.ZoneId.Should().Be("crestfall");
        session.RegionId.Should().Be("varenmark");
    }

    [Fact]
    public async Task EnterZone_Scopes_Wilderness_Groups_By_Difficulty()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, clients, groups, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;
        ctx.Items["DifficultyMode"] = "hardcore";

        await hub.EnterZone("the-droveway");

        groups.AddedGroups.Should().Contain("zone:the-droveway_hardcore");
        clients.OthersInGroupNamed("zone:the-droveway_hardcore").SentMessages.Should()
            .ContainSingle(m => m.Method == "PlayerEntered");
        ctx.Items["CurrentZoneGroupName"].Should().Be("zone:the-droveway_hardcore");
    }

    [Fact]
    public async Task EnterZone_Uses_The_Map_Entry_Node_When_The_Graph_Is_Available()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneId = "the-droveway";
        character.CurrentNodeId = "stale-node";
        await db.SaveChangesAsync();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(m => m.GetMapForZoneAsync("the-droveway", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MapGraph(
                   "map-droveway", MapType.Zone, "the-droveway", "The Droveway", string.Empty,
                   "arrival-node", [], []));

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, mapRepo: mapRepo);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("the-droveway");

        var stored = await db.Characters.FindAsync(character.Id);
        stored!.CurrentMapId.Should().Be("map-droveway");
        stored.CurrentNodeId.Should().Be("arrival-node", "the stale persisted node is not on the graph any more");

        var entered = clients.Caller.SentMessages.Single(m => m.Method == "ZoneEntered").Args[0];
        entered.Should().BeEquivalentTo(new { CurrentMapId = "map-droveway", CurrentNodeId = "arrival-node" });
    }

    [Fact]
    public async Task EnterZone_Resumes_The_Location_Node_A_Login_Projected_Onto_The_Region_Map()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        // What SelectCharacter leaves behind after a login or a respawn at the safe location: the row points at the
        // region entry, and the location slug is the only record of where inside the zone the character stood.
        character.CurrentZoneId = "the-droveway";
        character.CurrentMapId = "region:varenmark";
        character.CurrentNodeId = "region:varenmark:entry";
        character.CurrentZoneLocationSlug = "drove-road";
        await db.SaveChangesAsync();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(m => m.GetMapForZoneAsync("the-droveway", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MapGraph(
                   "zone:the-droveway", MapType.Zone, "the-droveway", "The Droveway", string.Empty,
                   "zone:the-droveway:entry",
                   [
                       new MapNodeEntry(
                           "zone:the-droveway:entry", "zone:the-droveway", MapNodeType.EntryPoint, null, "Arrival",
                           string.Empty, null, null, null, true, false, null, null, false, null, null, null, 0),
                       new MapNodeEntry(
                           "zone:the-droveway:location:drove-road", "zone:the-droveway", MapNodeType.Location,
                           "drove-road", "The Drove Road", string.Empty, null, null, null, false, false, null, null,
                           false, null, null, null, 20),
                   ],
                   []));

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id, mapRepo: mapRepo);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("the-droveway");

        var stored = await db.Characters.FindAsync(character.Id);
        stored!.CurrentNodeId.Should().Be(
            "zone:the-droveway:location:drove-road",
            "the location slug names where the character stood, so a reload must not fall back to the arrival node");
        stored.CurrentZoneLocationSlug.Should().Be("drove-road");

        var entered = clients.Caller.SentMessages.Single(m => m.Method == "ZoneEntered").Args[0];
        entered.Should().BeEquivalentTo(new
        {
            CurrentMapId = "zone:the-droveway",
            CurrentNodeId = "zone:the-droveway:location:drove-road",
        });
    }

    [Fact]
    public async Task SelectCharacter_Keeps_The_Zone_Location_Across_The_Region_Projection()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneId = "the-droveway";
        character.CurrentMapId = "zone:the-droveway";
        character.CurrentNodeId = "zone:the-droveway:location:drove-road";
        character.CurrentZoneLocationSlug = "drove-road";
        await db.SaveChangesAsync();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(m => m.GetMapForRegionAsync("varenmark", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MapGraph(
                   "region:varenmark", MapType.Region, "varenmark", "Varenmark", string.Empty,
                   "region:varenmark:entry", [], []));

        var (hub, _, _, _, _, _) = HubHarness.Create(db, account.Id, mapRepo: mapRepo);

        await hub.SelectCharacter(character.Id);

        var stored = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.CurrentMapId.Should().Be("region:varenmark", "a login still projects the character onto the region map");
        stored.CurrentNodeId.Should().Be("region:varenmark:entry");
        stored.CurrentZoneLocationSlug.Should().Be(
            "drove-road",
            "the location is all that survives the projection, so EnterZone can put the character back where they stood");
    }

    [Fact]
    public async Task EnterZone_Removes_A_Stale_Session_From_Another_Connection()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        await HubHarness.SeedSessionAsync(db, character, "conn-stale", "the-droveway");

        var (hub, _, _, ctx, _, _) = HubHarness.Create(db, account.Id, connectionId: "conn-new");
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        db.PlayerSessions.Should().ContainSingle(s => s.CharacterId == character.Id)
            .Which.ConnectionId.Should().Be("conn-new");
    }

    // ── EnterZone: reconnecting into a run ───────────────────────────────────

    [Fact]
    public async Task EnterZone_Reconnect_Reattaches_The_Character_To_Their_Open_Run()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        // The reconnect path: the session row left with the connection, and the location slug on the character
        // is the only record of where inside the zone they were standing.
        character.CurrentZoneLocationSlug = "vault-of-echoes";
        await db.SaveChangesAsync();

        var run = new ZoneInstance
        {
            ZoneId = "crestfall",
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:crestfall#i1:{Guid.NewGuid():D}",
        };
        db.ZoneInstances.Add(run);
        await db.SaveChangesAsync();

        // Someone else in the same zone, in the shared scope: they must not appear in this run's occupants.
        var strangerAccount = await HubHarness.SeedAccountAsync(db);
        var stranger = await HubHarness.SeedCharacterAsync(db, strangerAccount.Id);
        await HubHarness.SeedSessionAsync(db, stranger, "conn-stranger", zoneId: "crestfall");

        var locations = new Mock<RealmEngine.Shared.Abstractions.IZoneLocationRepository>();
        locations.Setup(repo => repo.GetBySlugAsync("vault-of-echoes"))
                 .ReturnsAsync(new ZoneLocationEntry(
                     "vault-of-echoes", "The Vault of Echoes", "dungeons", "crestfall", 30, 11, 14,
                     IsInstanced: true));

        var (hub, clients, groups, ctx, _, _) =
            HubHarness.Create(db, account.Id, connectionId: "conn-resumed", locations: locations.Object);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        var stored = db.PlayerSessions.Local.Single(s => s.CharacterId == character.Id);
        stored.ZoneInstanceId.Should().Be(run.Id, "the run is still open, so the pointer is written again");
        groups.AddedGroups.Should().Contain(run.ZoneGroup).And.NotContain(
            "zone:crestfall", "the shared scope would leave the character beside their party in the map only");
        ctx.Items["CurrentZoneInstanceId"].Should().Be(run.Id);

        var participant = await db.ZoneInstanceParticipants
            .SingleAsync(row => row.ZoneInstanceId == run.Id && row.CharacterId == character.Id);
        participant.LeftAt.Should().BeNull("coming back into the run reopens the participation");

        var entered = clients.Caller.SentMessages.Single(m => m.Method == "ZoneEntered").Args[0];
        entered.Should().BeEquivalentTo(new
        {
            Occupants = new[] { new { CharacterId = character.Id, CharacterName = character.Name } },
        });
    }

    [Fact]
    public async Task EnterZone_Reconnect_Keeps_The_Shared_Zone_When_The_Run_Has_Closed()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneLocationSlug = "vault-of-echoes";
        await db.SaveChangesAsync();

        // Emptied and waiting to be purged: joining it would put the character in a group nothing will join.
        db.ZoneInstances.Add(new ZoneInstance
        {
            ZoneId = "crestfall",
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:crestfall#i1:{Guid.NewGuid():D}",
            State = ZoneInstanceState.Empty,
        });
        await db.SaveChangesAsync();

        var locations = new Mock<RealmEngine.Shared.Abstractions.IZoneLocationRepository>();
        locations.Setup(repo => repo.GetBySlugAsync("vault-of-echoes"))
                 .ReturnsAsync(new ZoneLocationEntry(
                     "vault-of-echoes", "The Vault of Echoes", "dungeons", "crestfall", 30, 11, 14,
                     IsInstanced: true));
        // No safe location in the zone, so the lobby the character would otherwise wake in does not exist and the
        // saved node stands — the fallback has its own case below.
        locations.Setup(repo => repo.GetByZoneIdAsync("crestfall"))
                 .ReturnsAsync(new List<ZoneLocationEntry>());

        var (hub, _, groups, ctx, _, _) =
            HubHarness.Create(db, account.Id, connectionId: "conn-resumed", locations: locations.Object);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        db.PlayerSessions.Local.Single(s => s.CharacterId == character.Id).ZoneInstanceId
            .Should().BeNull("a swept run is not resumed");
        groups.AddedGroups.Should().Contain("zone:crestfall");
        ctx.Items["CurrentZoneInstanceId"].Should().BeNull();
    }

    /// <summary>
    /// The zone map a swept run wakes on: the entry, the instanced room the character stood on, and the zone's
    /// lobby beside it.
    /// </summary>
    /// <returns>The graph the hub reads for the zone.</returns>
    private static MapGraph SweptRunMap() => new(
        "zone:crestfall", MapType.Zone, "crestfall", "Crestfall", string.Empty,
        "zone:crestfall:entry",
        [
            new MapNodeEntry(
                "zone:crestfall:entry", "zone:crestfall", MapNodeType.EntryPoint, null, "Arrival",
                string.Empty, null, null, null, true, false, null, null, false, null, null, null, 0),
            new MapNodeEntry(
                "zone:crestfall:location:vault-of-echoes", "zone:crestfall", MapNodeType.Location,
                "vault-of-echoes", "The Vault of Echoes", string.Empty, null, null, null, false, false, null, null,
                false, null, null, null, 10),
            new MapNodeEntry(
                "zone:crestfall:location:town-square", "zone:crestfall", MapNodeType.Location,
                "town-square", "Crestfall Square", string.Empty, null, null, null, false, false, null, null,
                false, null, null, null, 20),
        ],
        []);

    /// <summary>
    /// The catalog around a dungeon room and the zone's lobby: the room is instanced, the square is the zone's
    /// safe location, and nothing else is either.
    /// </summary>
    /// <param name="withLobby">Whether the zone has a safe location at all, so a test can drop it.</param>
    /// <param name="roomIsInstanced">Whether the room the character stood on is an instance at all.</param>
    /// <returns>The catalog the hub is built with.</returns>
    private static Mock<RealmEngine.Shared.Abstractions.IZoneLocationRepository> SweptRunLocations(
        bool withLobby = true,
        bool roomIsInstanced = true)
    {
        var locations = new Mock<RealmEngine.Shared.Abstractions.IZoneLocationRepository>();
        locations.Setup(repo => repo.GetBySlugAsync("vault-of-echoes"))
                 .ReturnsAsync(new ZoneLocationEntry(
                     "vault-of-echoes", "The Vault of Echoes", "dungeons", "crestfall", 30, 11, 14,
                     IsInstanced: roomIsInstanced));
        locations.Setup(repo => repo.GetByZoneIdAsync("crestfall"))
                 .ReturnsAsync(withLobby
                     ? new List<ZoneLocationEntry>
                     {
                         new("vault-of-echoes", "The Vault of Echoes", "dungeons", "crestfall", 30, 11, 14, IsInstanced: roomIsInstanced),
                         new("town-square", "Crestfall Square", "locations", "crestfall", 10, null, null, IsSafeRespawn: true),
                     }
                     : new List<ZoneLocationEntry>());

        return locations;
    }

    [Fact]
    public async Task EnterZone_Reconnect_Wakes_In_The_Zones_Lobby_When_The_Run_Emptied()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneLocationSlug = "vault-of-echoes";
        await db.SaveChangesAsync();

        // The run emptied while the character was away: still on disk, past its grace, deliberately not resumed.
        db.ZoneInstances.Add(new ZoneInstance
        {
            ZoneId = "crestfall",
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:crestfall#i1:{Guid.NewGuid():D}",
            State = ZoneInstanceState.Empty,
        });
        await db.SaveChangesAsync();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(map => map.GetMapForZoneAsync("crestfall", It.IsAny<CancellationToken>()))
               .ReturnsAsync(SweptRunMap());

        var (hub, clients, groups, ctx, _, _) = HubHarness.Create(
            db, account.Id, connectionId: "conn-woken", mapRepo: mapRepo, locations: SweptRunLocations().Object);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be(
            "zone:crestfall:location:town-square",
            "a swept run does not hand the room back, so the character wakes in the zone's safe location");
        stored.CurrentZoneLocationSlug.Should().Be("town-square", "the persisted location is where they now stand");

        var entered = clients.Caller.SentMessages.Single(message => message.Method == "ZoneEntered").Args[0];
        entered.Should().BeEquivalentTo(new { CurrentNodeId = "zone:crestfall:location:town-square" });

        // The plan promises the wake-up is explained, once — the same sentence the respawn path says.
        var messages = clients.Caller.SentMessages.Where(message => message.Method == "SystemMessage").ToList();
        messages.Should().ContainSingle().Which.Args[0].Should()
            .Be("The instance you were in has closed. You wake in the shared zone.");

        db.PlayerSessions.Local.Single(session => session.CharacterId == character.Id).ZoneInstanceId
            .Should().BeNull("the run is gone, so nothing points at it");
        groups.AddedGroups.Should().Contain("zone:crestfall");
    }

    [Fact]
    public async Task EnterZone_Reconnect_Wakes_In_The_Lobby_When_The_Run_Was_Purged()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneLocationSlug = "vault-of-echoes";
        await db.SaveChangesAsync();

        // Swept *and* deleted: there is no run row left to read, so the resume cannot depend on finding one.
        db.ZoneInstances.Should().BeEmpty();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(map => map.GetMapForZoneAsync("crestfall", It.IsAny<CancellationToken>()))
               .ReturnsAsync(SweptRunMap());

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, connectionId: "conn-purged", mapRepo: mapRepo, locations: SweptRunLocations().Object);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be("zone:crestfall:location:town-square");
        stored.CurrentZoneLocationSlug.Should().Be("town-square");

        clients.Caller.SentMessages.Where(message => message.Method == "SystemMessage").Should()
            .ContainSingle("one wake-up, one explanation")
            .Which.Args[0].Should().Be("The instance you were in has closed. You wake in the shared zone.");
    }

    [Fact]
    public async Task EnterZone_Reconnect_Keeps_The_Saved_Node_When_The_Zone_Has_No_Lobby()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneLocationSlug = "vault-of-echoes";
        await db.SaveChangesAsync();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(map => map.GetMapForZoneAsync("crestfall", It.IsAny<CancellationToken>()))
               .ReturnsAsync(SweptRunMap());

        // Instanced room, no run left, and no safe location to wake in: a login must not fail, and it must not
        // move the character somewhere the zone does not have.
        var locations = SweptRunLocations(withLobby: false);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, connectionId: "conn-nolobby", mapRepo: mapRepo, locations: locations.Object);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be(
            "zone:crestfall:location:vault-of-echoes", "with no lobby the saved node is the only true position");
        stored.CurrentZoneLocationSlug.Should().Be("vault-of-echoes");

        clients.Caller.SentMessages.Should().NotContain(
            message => message.Method == "SystemMessage",
            "nothing moved, so there is nothing to explain");
    }

    [Fact]
    public async Task EnterZone_Reconnect_Stays_In_A_Room_That_Was_Never_Instanced()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneLocationSlug = "vault-of-echoes";
        await db.SaveChangesAsync();

        var mapRepo = new Mock<RealmEngine.Shared.Abstractions.IMapGraphRepository>();
        mapRepo.Setup(map => map.GetMapForZoneAsync("crestfall", It.IsAny<CancellationToken>()))
               .ReturnsAsync(SweptRunMap());

        var (hub, clients, _, ctx, _, _) = HubHarness.Create(
            db, account.Id, connectionId: "conn-shared", mapRepo: mapRepo,
            locations: SweptRunLocations(roomIsInstanced: false).Object);
        ctx.Items["CharacterId"] = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("crestfall");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be(
            "zone:crestfall:location:vault-of-echoes",
            "a shared room is not an instance, so there is no run to have been swept and no lobby to wake in");
        clients.Caller.SentMessages.Should().NotContain(message => message.Method == "SystemMessage");
    }

    // ── TravelToNode: walking out of a run ───────────────────────────────────

    /// <summary>
    /// Seeds an open run with one member inside, a session pointing at it, and a caller standing on one of the
    /// run's instanced room nodes — the state a walk out of the run starts from.
    /// </summary>
    private static async Task<(ZoneInstance Run, PlayerSession Session)> SeedRunWithMemberAsync(
        ApplicationDbContext db,
        Veldrath.Server.Data.Entities.Character character,
        string connectionId)
    {
        var run = new ZoneInstance
        {
            ZoneId = "crestfall",
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:crestfall#i1:{Guid.NewGuid():D}",
            // Entered an hour ago: measuring the grace from the last join is exactly the bug this pins.
            LastOccupiedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        db.ZoneInstances.Add(run);
        db.ZoneInstanceParticipants.Add(new ZoneInstanceParticipant
        {
            ZoneInstanceId = run.Id,
            CharacterId = character.Id,
        });
        await db.SaveChangesAsync();

        var session = await HubHarness.SeedSessionAsync(db, character, connectionId, zoneId: "crestfall");
        session.ZoneInstanceId = run.Id;
        await db.SaveChangesAsync();

        return (run, session);
    }

    [Fact]
    public async Task TravelToNode_WalkingOutOfARun_ClosesParticipationAndStartsTheGrace()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (run, session) = await SeedRunWithMemberAsync(db, character, "conn-walkout");

        var locations = new Mock<RealmEngine.Shared.Abstractions.IZoneLocationRepository>();
        locations.Setup(repo => repo.GetBySlugAsync("vault-of-echoes"))
                 .ReturnsAsync(new ZoneLocationEntry(
                     "vault-of-echoes", "The Vault of Echoes", "dungeons", "crestfall", 30, 11, 14,
                     IsInstanced: true));

        var (hub, _, groups, ctx, mediator, _) =
            HubHarness.Create(db, account.Id, connectionId: "conn-walkout", locations: locations.Object);
        ctx.EnterZoneState(
            character.Id, character.Name, zoneGroup: run.ZoneGroup, currentMapId: "zone:crestfall");
        ctx.Items["CurrentZoneInstanceId"] = run.Id;

        // The last member walks from the run's instanced room to the zone's shared market. The destination is
        // shared, so the arrival carries no run: `ZoneInstanceId` null and the zone's own scope.
        mediator.Returns(new TravelToNodeHubResult
        {
            Success = true,
            MapId = "zone:crestfall",
            NodeId = "zone:crestfall:location:market",
            ZoneId = "crestfall",
            LocationSlug = "market",
            ZoneGroup = "zone:crestfall",
            ZoneInstanceId = null,
        });

        await hub.TravelToNode(new TravelToNodeHubRequest("zone:crestfall", "zone:crestfall:location:market"));

        var participant = await db.ZoneInstanceParticipants
            .SingleAsync(row => row.ZoneInstanceId == run.Id && row.CharacterId == character.Id);
        participant.LeftAt.Should().NotBeNull(
            "a walk out of the run is a departure, so participation closes exactly as it does on disconnect");

        session.ZoneInstanceId.Should().BeNull("the character is in the shared zone now");
        ctx.Items["CurrentZoneInstanceId"].Should().BeNull();
        groups.RemovedGroups.Should().Contain(run.ZoneGroup);
        groups.AddedGroups.Should().Contain("zone:crestfall");

        // The departure started the emptiness grace, so the 5 s sweep cannot empty a run that was held for an
        // hour on its first tick after the walk-out.
        var service = ZoneInstances(db, locations.Object);
        await service.SweepAsync(TimeSpan.FromMinutes(1));

        (await service.GetAsync(run.Id))!.State.Should().Be(ZoneInstanceState.Open);
        (await service.FindResumableAsync("crestfall", "vault-of-echoes", character.Id))!.Id.Should().Be(
            run.Id, "backtracking into the room rejoins the attempt instead of a fresh run");

        // And the walk back in is the same run, with participation live again.
        (await service.FindOrCreateAsync("crestfall", character.Id)).Id.Should().Be(run.Id);
        db.ZoneInstanceParticipants.Local
            .Single(row => row.ZoneInstanceId == run.Id && row.CharacterId == character.Id)
            .LeftAt.Should().BeNull("walking back into the run reopens the participation");
    }

    [Fact]
    public async Task TravelToNode_WalkingBetweenTwoRoomsOfTheSameRun_ReportsNoDeparture()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (run, _) = await SeedRunWithMemberAsync(db, character, "conn-in-run");

        var (hub, _, _, ctx, mediator, _) = HubHarness.Create(db, account.Id, connectionId: "conn-in-run");
        ctx.EnterZoneState(
            character.Id, character.Name, zoneGroup: run.ZoneGroup, currentMapId: "zone:crestfall");
        ctx.Items["CurrentZoneInstanceId"] = run.Id;

        // Both rooms are instanced, so the handler resolves the same open run: the character never left it.
        mediator.Returns(new TravelToNodeHubResult
        {
            Success = true,
            MapId = "zone:crestfall",
            NodeId = "zone:crestfall:location:crypt",
            ZoneId = "crestfall",
            LocationSlug = "crypt",
            ZoneGroup = run.ZoneGroup,
            ZoneInstanceId = run.Id,
        });

        await hub.TravelToNode(new TravelToNodeHubRequest("zone:crestfall", "zone:crestfall:location:crypt"));

        var participant = await db.ZoneInstanceParticipants
            .SingleAsync(row => row.ZoneInstanceId == run.Id && row.CharacterId == character.Id);
        participant.LeftAt.Should().BeNull("moving inside the run is not a departure");
        db.ZoneInstances.Local.Single(row => row.Id == run.Id).LastOccupiedAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddHours(-1),
            TimeSpan.FromMinutes(1),
            "no leave was reported, so no grace was started");
    }

    [Fact]
    public async Task TravelToNode_FromTheSharedZone_ReportsNoDeparture()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);

        // A character who was never inside a run has no departure to report, and must not be sent one.
        var zoneInstances = new Mock<IZoneInstanceService>();
        var (hub, _, _, ctx, mediator, _) =
            HubHarness.Create(db, account.Id, zoneInstances: zoneInstances.Object);
        ctx.EnterZoneState(character.Id, character.Name, currentMapId: "zone:crestfall");
        mediator.Returns(new TravelToNodeHubResult
        {
            Success = true,
            MapId = "zone:crestfall",
            NodeId = "zone:crestfall:location:market",
            ZoneId = "crestfall",
            LocationSlug = "market",
            ZoneGroup = "zone:crestfall",
            ZoneInstanceId = null,
        });

        await hub.TravelToNode(new TravelToNodeHubRequest("zone:crestfall", "zone:crestfall:location:market"));

        zoneInstances.Verify(
            service => service.LeaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a character who never entered a run has no departure to report");
    }

    // ── LeaveZone ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveZone_Without_A_Session_Still_Acknowledges_The_Leave()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, groups, _, _, _) = HubHarness.Create(db, account.Id, connectionId: "conn-none");

        await hub.LeaveZone();

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "ZoneLeft");
        groups.RemovedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveZone_Removes_The_Session_Leaves_The_Group_And_Notifies_Peers()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        await HubHarness.SeedSessionAsync(db, character, "conn-leaver", "the-droveway");

        var (hub, clients, groups, ctx, _, _) = HubHarness.Create(db, account.Id, connectionId: "conn-leaver");
        ctx.Items["CurrentZoneGroupName"] = "zone:the-droveway_hardcore";

        await hub.LeaveZone();

        db.PlayerSessions.Should().BeEmpty();
        groups.RemovedGroups.Should().BeEquivalentTo(["zone:the-droveway_hardcore"]);
        clients.GroupNamed("zone:the-droveway_hardcore").SentMessages.Should()
            .ContainSingle(m => m.Method == "PlayerLeft");
        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "ZoneLeft");
    }

    [Fact]
    public async Task LeaveZone_Leaves_Only_The_Region_Group_When_On_The_Region_Map()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        await HubHarness.SeedSessionAsync(db, character, "conn-map", zoneId: null);

        var (hub, clients, groups, ctx, _, _) = HubHarness.Create(db, account.Id, connectionId: "conn-map");
        ctx.Items["CurrentRegionId"] = "varenmark";

        await hub.LeaveZone();

        groups.RemovedGroups.Should().BeEquivalentTo(["region:varenmark"]);
        clients.GroupNamed("region:varenmark").SentMessages.Should()
            .BeEmpty("nobody is told a region-map player left a zone");
    }

    // ── GetMapGraph ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMapGraph_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetMapGraph(null);

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetMapGraph");
    }

    [Fact]
    public async Task GetMapGraph_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new GetMapGraphHubResult { Success = false, ErrorMessage = "Character was not found" });

        await hub.GetMapGraph("map-1");

        ShouldBeError(clients.Caller, "Character was not found");
    }

    [Fact]
    public async Task GetMapGraph_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<GetMapGraphHubResult>(new InvalidOperationException("boom"));

        await hub.GetMapGraph(null);

        ShouldBeError(clients.Caller, "Failed to load map graph");
    }

    [Fact]
    public async Task GetMapGraph_Sends_The_Graph_To_The_Caller()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        var result = new GetMapGraphHubResult { Success = true, CurrentNodeId = "node-1" };
        mediator.Returns(result);

        await hub.GetMapGraph("map-1");

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<GetMapGraphHubCommand>().Single();
        command.CharacterId.Should().Be(characterId);
        command.MapId.Should().Be("map-1");
        clients.Caller.SentMessages.Single(m => m.Method == "MapGraphReceived").Args[0]
            .Should().BeSameAs(result);
    }

    // ── TravelToNode ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TravelToNode_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.TravelToNode(new TravelToNodeHubRequest("map-1", "node-2"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before TravelToNode");
    }

    [Fact]
    public async Task TravelToNode_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new TravelToNodeHubResult { Success = false, ErrorMessage = "Nodes are not connected" });

        await hub.TravelToNode(new TravelToNodeHubRequest("map-1", "node-2"));

        ShouldBeError(clients.Caller, "Nodes are not connected");
    }

    [Fact]
    public async Task TravelToNode_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<TravelToNodeHubResult>(new InvalidOperationException("boom"));

        await hub.TravelToNode(new TravelToNodeHubRequest("map-1", "node-2"));

        ShouldBeError(clients.Caller, "Failed to travel to node");
    }

    [Fact]
    public async Task TravelToNode_Reports_The_Move_And_Each_Content_Warning()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), currentMapId: "zone:crestfall");
        var result = new TravelToNodeHubResult
        {
            Success = true,
            NodeId = "node-2",
            ContentWarnings = ["Pool 'goblins' references no known actors"],
        };
        mediator.Returns(result);

        await hub.TravelToNode(new TravelToNodeHubRequest("zone:crestfall", "node-2"));

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<TravelToNodeHubCommand>().Single();
        command.ZoneGroup.Should().Be("zone:crestfall", "a walk on the map the connection stands on keeps its group");

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "NodeTraveled");
        clients.Caller.SentMessages.Should().Contain(m =>
            m.Method == "SystemMessage" && (string)m.Args[0]! == "Pool 'goblins' references no known actors");
    }

    [Fact]
    public async Task TravelToNode_Does_Not_Forward_The_Group_For_A_Walk_Requested_On_Another_Map()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, _, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid(), currentMapId: "zone:crestfall");
        mediator.Returns(new TravelToNodeHubResult { Success = true, MapId = "zone:the-droveway" });

        // The group names the map the connection is standing on. A request for another map is a move between maps,
        // where only the destination's zone can say what the scope is — forwarding this one handed the handler the
        // zone being left, whose roster the destination location then spawned under.
        await hub.TravelToNode(new TravelToNodeHubRequest("zone:the-droveway", "zone:the-droveway:entry"));

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<TravelToNodeHubCommand>().Single();
        command.ZoneGroup.Should().BeEmpty("the destination's zone owns the scope, not the zone the walk left");
    }

    [Fact]
    public async Task TravelToNode_Sends_The_New_Zone_Snapshot_When_The_Walk_Crosses_A_Border()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        // One character per account in slot 1, so the neighbour who is already there needs their own account.
        var residentAccount = await HubHarness.SeedAccountAsync(db);
        var resident = await HubHarness.SeedCharacterAsync(db, residentAccount.Id);
        await HubHarness.SeedSessionAsync(db, resident, "conn-resident", "the-droveway");

        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id, connectionId: "conn-crossing");
        ctx.EnterZoneState(character.Id, character.Name);
        mediator.Returns(new TravelToNodeHubResult
        {
            Success = true,
            MapId = "zone:the-droveway",
            NodeId = "zone:the-droveway:entry",
            ZoneId = "the-droveway",
        });

        await hub.TravelToNode(new TravelToNodeHubRequest("region:varenmark", "the-droveway"));

        // NodeTraveled carries only the zone id, so the walk has to bring the name, the inn and merchant flags
        // and the occupant list with it — otherwise the banner still reads "Crestfall" inside the Droveway.
        var entered = clients.Caller.SentMessages.Single(m => m.Method == "ZoneEntered").Args[0];
        entered.Should().BeEquivalentTo(new
        {
            Id = "the-droveway",
            Name = "The Droveway",
            CurrentMapId = "zone:the-droveway",
            CurrentNodeId = "zone:the-droveway:entry",
            Occupants = new[] { new { CharacterId = resident.Id, CharacterName = resident.Name } },
        });
        ctx.Items["CurrentZoneId"].Should().Be("the-droveway");
    }

    [Fact]
    public async Task TravelToNode_Keeps_The_Zone_Snapshot_When_The_Walk_Stays_Inside_It()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(character.Id, character.Name);
        mediator.Returns(new TravelToNodeHubResult
        {
            Success = true,
            MapId = "zone:crestfall",
            NodeId = "crestfall-market",
            ZoneId = "crestfall",
        });

        await hub.TravelToNode(new TravelToNodeHubRequest("zone:crestfall", "crestfall-market"));

        clients.Caller.SentMessages.Should().NotContain(
            m => m.Method == "ZoneEntered",
            "walking inside one zone leaves the banner untouched");
    }

    [Fact]
    public async Task TravelToNode_Clears_The_Zone_When_The_Walk_Leads_Onto_The_Region_Map()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(character.Id, character.Name);
        mediator.Returns(new TravelToNodeHubResult
        {
            Success = true,
            MapId = "region:varenmark",
            NodeId = "varenmark:crestfall",
            ZoneId = null,
        });

        await hub.TravelToNode(new TravelToNodeHubRequest("zone:crestfall", "crestfall-exit"));

        clients.Caller.SentMessages.Should().NotContain(
            m => m.Method == "ZoneEntered",
            "there is no zone on the region map to describe");
        ctx.Items["CurrentZoneId"].Should().BeNull();
        ctx.Items["CurrentMapId"].Should().Be("region:varenmark");
        ctx.Items["CurrentNodeId"].Should().Be("varenmark:crestfall");
    }

    // ── NavigateToLocation ───────────────────────────────────────────────────

    [Fact]
    public async Task NavigateToLocation_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("market"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before NavigateToLocation");
    }

    [Fact]
    public async Task NavigateToLocation_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new NavigateToLocationHubResult { Success = false, ErrorMessage = "Location does not exist" });

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("market"));

        ShouldBeError(clients.Caller, "Location does not exist");
    }

    [Fact]
    public async Task NavigateToLocation_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<NavigateToLocationHubResult>(new InvalidOperationException("boom"));

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("market"));

        ShouldBeError(clients.Caller, "Failed to navigate to location");
    }

    [Fact]
    public async Task NavigateToLocation_Broadcasts_The_Location_Spawns_Warnings_And_Discoveries()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new NavigateToLocationHubResult
        {
            Success = true,
            LocationSlug = "market",
            LocationDisplayName = "Crestfall Market",
            TypeKey = "location",
            SpawnedEnemies = [new SpawnedEnemySummary(Guid.NewGuid(), "Goblin", 2, 12, 20)],
            ContentWarnings = ["Spawn pool is empty"],
            PassiveDiscoveries =
            [
                new ZoneLocationEntry("hidden-cellar", "Hidden Cellar", "dungeon", "crestfall", 1, null, null),
            ],
        });

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("market"));

        clients.GroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "LocationEntered");
        clients.Caller.SentMessages.Should().Contain(m =>
            m.Method == "SystemMessage" && (string)m.Args[0]! == "Spawn pool is empty");
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "ZoneLocationUnlocked");

        var advance = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>().Single();
        advance.Kind.Should().Be("visit");
        advance.Key.Should().Be("market");
    }

    [Fact]
    public async Task NavigateToLocation_Falls_Back_To_The_Caller_Outside_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new NavigateToLocationHubResult { Success = true, LocationSlug = "market" });
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("market"));

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "LocationEntered");
        clients.GroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    // ── UnlockZoneLocation ───────────────────────────────────────────────────

    [Fact]
    public async Task UnlockZoneLocation_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("crypt", "quest"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before UnlockZoneLocation");
    }

    [Fact]
    public async Task UnlockZoneLocation_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new UnlockZoneLocationHubResult { Success = false, ErrorMessage = "Location is not hidden" });

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("market", "quest"));

        ShouldBeError(clients.Caller, "Location is not hidden");
    }

    [Fact]
    public async Task UnlockZoneLocation_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<UnlockZoneLocationHubResult>(new InvalidOperationException("boom"));

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("crypt", "quest"));

        ShouldBeError(clients.Caller, "Failed to unlock location");
    }

    [Fact]
    public async Task UnlockZoneLocation_Announces_A_New_Discovery()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(new UnlockZoneLocationHubResult
        {
            Success = true,
            LocationSlug = "crypt",
            LocationDisplayName = "Sunken Crypt",
            TypeKey = "dungeon",
            WasAlreadyUnlocked = false,
        });

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("crypt", "quest"));

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "ZoneLocationUnlocked").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            LocationSlug = "crypt",
            LocationDisplayName = "Sunken Crypt",
            TypeKey = "dungeon",
            UnlockSource = "quest",
        });
    }

    [Fact]
    public async Task UnlockZoneLocation_Stays_Silent_When_It_Was_Already_Unlocked()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new UnlockZoneLocationHubResult
        {
            Success = true,
            LocationSlug = "crypt",
            WasAlreadyUnlocked = true,
        });

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("crypt", "quest"));

        clients.Caller.SentMessages.Should().BeEmpty();
    }

    // ── SearchArea ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchArea_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.SearchArea();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before SearchArea");
    }

    [Fact]
    public async Task SearchArea_Requires_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, _, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        await hub.SearchArea();

        ShouldBeError(clients.Caller, "EnterZone must be called before SearchArea");
    }

    [Fact]
    public async Task SearchArea_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new SearchAreaHubResult { Success = false, ErrorMessage = "Character not found" });

        await hub.SearchArea();

        ShouldBeError(clients.Caller, "Character not found");
    }

    [Fact]
    public async Task SearchArea_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<SearchAreaHubResult>(new InvalidOperationException("boom"));

        await hub.SearchArea();

        ShouldBeError(clients.Caller, "Area search failed");
    }

    [Fact]
    public async Task SearchArea_Reports_The_Roll_And_Each_Discovery()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran", "the-droveway");
        mediator.Returns(new SearchAreaHubResult
        {
            Success = true,
            RollValue = 14,
            AnyFound = true,
            Discovered = [new DiscoveredLocation("old-mine", "Old Mine", "dungeon")],
        });

        await hub.SearchArea();

        var searched = clients.Caller.SentMessages.Single(m => m.Method == "AreaSearched").Args[0];
        searched.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            RollValue = 14,
            AnyFound = true,
        });
        var unlocked = clients.Caller.SentMessages.Single(m => m.Method == "ZoneLocationUnlocked").Args[0];
        unlocked.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            LocationSlug = "old-mine",
            LocationDisplayName = "Old Mine",
            TypeKey = "dungeon",
            UnlockSource = "skill_check_active",
        });

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<SearchAreaHubCommand>().Single();
        command.ZoneId.Should().Be("the-droveway");
    }
}
