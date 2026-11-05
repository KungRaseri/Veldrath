using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Contracts.Map;
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
        ctx.EnterZoneState(Guid.NewGuid());
        var result = new TravelToNodeHubResult
        {
            Success = true,
            NodeId = "node-2",
            ContentWarnings = ["Pool 'goblins' references no known actors"],
        };
        mediator.Returns(result);

        await hub.TravelToNode(new TravelToNodeHubRequest("map-1", "node-2"));

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<TravelToNodeHubCommand>().Single();
        command.ZoneGroup.Should().Be("zone:crestfall", "the zone group is forwarded for peer broadcasts");

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "NodeTraveled");
        clients.Caller.SentMessages.Should().Contain(m =>
            m.Method == "SystemMessage" && (string)m.Args[0]! == "Pool 'goblins' references no known actors");
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
