using Veldrath.Contracts.Map;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Chests;
using Veldrath.Server.Features.Characters.Harvesting;
using Veldrath.Server.Features.Characters.Npcs;
using Veldrath.Server.Features.Map;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the harvesting and chest hub methods of <see cref="Veldrath.Server.Hubs.GameHub"/>:
/// <c>GetLocationNodes</c>, <c>HarvestNode</c>, <c>GetLocationChests</c>, <c>OpenChest</c> and the
/// <c>EnterLocation</c> arrival that refreshes both lists.
/// </summary>
/// <remarks>
/// The hub is a thin adapter, so each method is driven through its guard, its failure result, its exception
/// path and its success payload — both the audience and the shape of the broadcast are asserted.
/// </remarks>
public class GameHubHarvestChestTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static void ShouldBeError(FakeClientProxy proxy, string message) =>
        proxy.SentMessages.Last(m => m.Method == "Error").Args[0].Should().Be(message);

    private static LocationNodeDto Node(string nodeId = "wood-road-glowcap-1") =>
        new(nodeId, "Glowcap Patch", "common", 60, "Depleted", true);

    private static LocationChestDto Chest(Guid id) =>
        new(id, "Mossy Strongbox", "old-clearing", "Authored");

    /// <summary>Stubs a successful arrival whose list refreshes answer with nothing.</summary>
    private static void StubArrival(Mock<ISender> mediator, string zoneGroup)
    {
        mediator.Returns(new EnterZoneLocationHubResult
        {
            Success = true,
            Arrival = new TravelToNodeHubResult
            {
                Success = true,
                ZoneId = "ashlen-wood",
                LocationSlug = "old-clearing",
                ZoneGroup = zoneGroup,
            },
        });
        mediator.Returns<IReadOnlyList<LocationNodeDto>>(Array.Empty<LocationNodeDto>());
        mediator.Returns<IReadOnlyList<LocationChestDto>>(Array.Empty<LocationChestDto>());
        mediator.Returns<IReadOnlyList<LocationNpcDto>>(Array.Empty<LocationNpcDto>());
    }

    // ── GetLocationNodes ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationNodes_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetLocationNodes();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetLocationNodes");
    }

    [Fact]
    public async Task GetLocationNodes_Sends_The_List_To_The_Caller()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        List<LocationNodeDto> nodes = [Node()];
        mediator.Returns<IReadOnlyList<LocationNodeDto>>(nodes);

        await hub.GetLocationNodes();

        clients.Caller.SentMessages.Single(m => m.Method == "LocationNodes").Args[0]
            .Should().BeEquivalentTo(new { Nodes = nodes });
    }

    [Fact]
    public async Task GetLocationNodes_Still_Sends_An_Empty_List_When_The_Read_Fails()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<IReadOnlyList<LocationNodeDto>>(new InvalidOperationException("db down"));

        await hub.GetLocationNodes();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "LocationNodes").Args[0];
        payload.Should().BeEquivalentTo(new { Nodes = Array.Empty<LocationNodeDto>() });
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "Error");
    }

    // ── HarvestNode ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HarvestNode_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.HarvestNode(new HarvestNodeRequest("wood-road-glowcap-1"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before HarvestNode");
    }

    [Fact]
    public async Task HarvestNode_Requires_A_Node_Id()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.HarvestNode(null!);
        await hub.HarvestNode(new HarvestNodeRequest("   "));

        clients.Caller.SentMessages.Where(m => m.Method == "Error").Should().HaveCount(2)
            .And.OnlyContain(m => (string)m.Args[0]! == "A node id is required.");
        mediator.Invocations.Should().BeEmpty("a payload without a node never reaches the handler");
    }

    [Fact]
    public async Task HarvestNode_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new HarvestNodeHubResult { Success = false, ErrorMessage = "This resource no longer exists." });
        mediator.Returns<IReadOnlyList<LocationNodeDto>>(Array.Empty<LocationNodeDto>());

        await hub.HarvestNode(new HarvestNodeRequest("wood-road-glowcap-1"));

        ShouldBeError(clients.Caller, "This resource no longer exists.");
        mediator.Invocations.Select(i => i.Arguments[0]).OfType<HarvestNodeHubCommand>().Single()
            .Should().Be(new HarvestNodeHubCommand(characterId, "wood-road-glowcap-1"));
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "Harvested");
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "LocationNodes",
            "a failed attempt can still have worn the node down, so the list is refreshed either way");
    }

    [Fact]
    public async Task HarvestNode_Uses_A_Default_Message_When_The_Handler_Gives_None()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new HarvestNodeHubResult { Success = false });
        mediator.Returns<IReadOnlyList<LocationNodeDto>>(Array.Empty<LocationNodeDto>());

        await hub.HarvestNode(new HarvestNodeRequest("wood-road-glowcap-1"));

        ShouldBeError(clients.Caller, "Nothing came away this time.");
    }

    [Fact]
    public async Task HarvestNode_Reports_The_Haul_Advances_Both_Collect_Keys_And_Refreshes_The_List()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);

        var result = new HarvestNodeHubResult
        {
            Success = true,
            NodeId = "wood-road-glowcap-1",
            DisplayName = "Glowcap Patch",
            Materials = [new HarvestedMaterial("@items/general:glowcap-mushroom", "Glowcap Mushroom", 2)],
            NodeHealthRemaining = 60,
            Message = "Gathered 2 materials from the Glowcap Patch.",
        };
        mediator.Returns(result);
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });
        mediator.Returns(new GetInventoryHubResult { Success = false });
        List<LocationNodeDto> nodes = [Node()];
        mediator.Returns<IReadOnlyList<LocationNodeDto>>(nodes);

        await hub.HarvestNode(new HarvestNodeRequest("  wood-road-glowcap-1  "));

        mediator.Invocations.Select(i => i.Arguments[0]).OfType<HarvestNodeHubCommand>().Single()
            .NodeId.Should().Be("wood-road-glowcap-1", "the payload is trimmed before it is dispatched");

        // Content objectives are authored with slugs while loot hands back catalog references, so both keys
        // have to be advanced or a `gather-mushrooms` objective never completes.
        mediator.Invocations.Select(i => i.Arguments[0]).OfType<AdvanceQuestObjectivesHubCommand>()
            .Select(command => $"{command.Kind}:{command.Key}")
            .Should().Equal("collect:glowcap-mushroom", "collect:@items/general:glowcap-mushroom");

        clients.Caller.SentMessages.Single(m => m.Method == "Harvested").Args[0].Should().BeSameAs(result);
        clients.Caller.SentMessages.Single(m => m.Method == "LocationNodes").Args[0]
            .Should().BeEquivalentTo(new { Nodes = nodes });
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "Error");
    }

    [Fact]
    public async Task HarvestNode_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<HarvestNodeHubResult>(new InvalidOperationException("boom"));

        await hub.HarvestNode(new HarvestNodeRequest("wood-road-glowcap-1"));

        ShouldBeError(clients.Caller, "Failed to harvest that resource.");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "Harvested");
    }

    // ── GetLocationChests ────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationChests_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetLocationChests();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetLocationChests");
    }

    [Fact]
    public async Task GetLocationChests_Sends_The_List_To_The_Caller()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        List<LocationChestDto> chests = [Chest(Guid.NewGuid())];
        mediator.Returns<IReadOnlyList<LocationChestDto>>(chests);

        await hub.GetLocationChests();

        clients.Caller.SentMessages.Single(m => m.Method == "LocationChests").Args[0]
            .Should().BeEquivalentTo(new { Chests = chests });
    }

    [Fact]
    public async Task GetLocationChests_Still_Sends_An_Empty_List_When_The_Read_Fails()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<IReadOnlyList<LocationChestDto>>(new InvalidOperationException("db down"));

        await hub.GetLocationChests();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "LocationChests").Args[0];
        payload.Should().BeEquivalentTo(new { Chests = Array.Empty<LocationChestDto>() });
    }

    // ── OpenChest ────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenChest_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.OpenChest(new OpenChestRequest(Guid.NewGuid()));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before OpenChest");
    }

    [Fact]
    public async Task OpenChest_Requires_A_Chest_Id()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());

        await hub.OpenChest(null!);
        await hub.OpenChest(new OpenChestRequest(Guid.Empty));

        clients.Caller.SentMessages.Where(m => m.Method == "Error").Should().HaveCount(2)
            .And.OnlyContain(m => (string)m.Args[0]! == "A chest id is required.");
        mediator.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenChest_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        var chestId = Guid.NewGuid();
        mediator.Returns(new OpenChestHubResult { Success = false, ErrorMessage = "That chest is no longer there." });
        mediator.Returns<IReadOnlyList<LocationChestDto>>(Array.Empty<LocationChestDto>());

        await hub.OpenChest(new OpenChestRequest(chestId));

        ShouldBeError(clients.Caller, "That chest is no longer there.");
        mediator.Invocations.Select(i => i.Arguments[0]).OfType<OpenChestHubCommand>().Single()
            .Should().Be(new OpenChestHubCommand(characterId, chestId));
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "ChestOpened");
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "LocationChests",
            "someone else may have won the chest, so the list is re-sent either way");
    }

    [Fact]
    public async Task OpenChest_Uses_A_Default_Message_When_The_Handler_Gives_None()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new OpenChestHubResult { Success = false });
        mediator.Returns<IReadOnlyList<LocationChestDto>>(Array.Empty<LocationChestDto>());

        await hub.OpenChest(new OpenChestRequest(Guid.NewGuid()));

        ShouldBeError(clients.Caller, "The chest would not open.");
    }

    [Fact]
    public async Task OpenChest_Announces_The_Loot_To_The_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        var chestId = Guid.NewGuid();
        var result = new OpenChestHubResult
        {
            Success = true,
            ChestId = chestId,
            DisplayName = "Mossy Strongbox",
            Items = [new ChestLootItem("@items/general:health-potion", "Health Potion", 1)],
            Message = "Opened the Mossy Strongbox: Health Potion.",
        };
        mediator.Returns(result);
        mediator.Returns(new GetInventoryHubResult { Success = false });
        mediator.Returns<IReadOnlyList<LocationChestDto>>(Array.Empty<LocationChestDto>());

        await hub.OpenChest(new OpenChestRequest(chestId));

        clients.GroupNamed("zone:crestfall").SentMessages.Single(m => m.Method == "ChestOpened").Args[0]
            .Should().BeSameAs(result);
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "LocationChests");
    }

    [Fact]
    public async Task OpenChest_Falls_Back_To_The_Caller_Outside_A_Zone()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new OpenChestHubResult { Success = true, ChestId = Guid.NewGuid() });
        mediator.Returns(new GetInventoryHubResult { Success = false });
        mediator.Returns<IReadOnlyList<LocationChestDto>>(Array.Empty<LocationChestDto>());

        await hub.OpenChest(new OpenChestRequest(Guid.NewGuid()));

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "ChestOpened");
        clients.GroupNamed("zone:crestfall").SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenChest_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<OpenChestHubResult>(new InvalidOperationException("boom"));

        await hub.OpenChest(new OpenChestRequest(Guid.NewGuid()));

        ShouldBeError(clients.Caller, "Failed to open that chest.");
    }

    // ── GetLocationNpcs ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationNpcs_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetLocationNpcs();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetLocationNpcs");
    }

    [Fact]
    public async Task GetLocationNpcs_Still_Sends_An_Empty_List_When_The_Read_Fails()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<IReadOnlyList<LocationNpcDto>>(new InvalidOperationException("db down"));

        await hub.GetLocationNpcs();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "LocationNpcs").Args[0];
        payload.Should().BeEquivalentTo(new { Npcs = Array.Empty<LocationNpcDto>() });
    }

    // ── EnterLocation ────────────────────────────────────────────────────────

    [Fact]
    public async Task EnterLocation_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before EnterLocation");
    }

    [Fact]
    public async Task EnterLocation_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new EnterZoneLocationHubResult { Success = false, ErrorMessage = "You cannot reach that place." });

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        ShouldBeError(clients.Caller, "You cannot reach that place.");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "NodeTraveled");
    }

    [Fact]
    public async Task EnterLocation_Refuses_An_Arrival_That_Carries_Nowhere()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new EnterZoneLocationHubResult { Success = true, Arrival = null });

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        ShouldBeError(clients.Caller, "Failed to enter location");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "NodeTraveled");
    }

    [Fact]
    public async Task EnterLocation_Moves_The_Group_And_Refreshes_What_Is_Here()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, clients, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id, connectionId: "conn-loc");
        ctx.EnterZoneState(character.Id, character.Name, "ashlen-wood", "zone:ashlen-wood");

        mediator.Returns(new EnterZoneLocationHubResult
        {
            Success = true,
            Arrival = new TravelToNodeHubResult
            {
                Success = true,
                MapId = "zone:ashlen-wood",
                NodeId = "ashlen-wood:old-clearing",
                ZoneId = "ashlen-wood",
                LocationSlug = "old-clearing",
                ZoneGroup = "zone:ashlen-wood#i1:2f9c",
                ZoneInstanceId = null,
                ContentWarnings = ["Pool 'forest-goblins' references no known actors"],
            },
        });
        List<LocationNodeDto> nodes = [Node()];
        List<LocationChestDto> chests = [Chest(Guid.NewGuid())];
        mediator.Returns<IReadOnlyList<LocationNodeDto>>(nodes);
        mediator.Returns<IReadOnlyList<LocationChestDto>>(chests);
        mediator.Returns<IReadOnlyList<LocationNpcDto>>(Array.Empty<LocationNpcDto>());

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        groups.RemovedGroups.Should().Contain("zone:ashlen-wood", "an instanced arrival leaves the shared group");
        groups.AddedGroups.Should().Contain("zone:ashlen-wood#i1:2f9c");
        ctx.Items["CurrentZoneGroupName"].Should().Be("zone:ashlen-wood#i1:2f9c");

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "NodeTraveled");
        clients.Caller.SentMessages.Single(m => m.Method == "LocationNodes").Args[0]
            .Should().BeEquivalentTo(new { Nodes = nodes });
        clients.Caller.SentMessages.Single(m => m.Method == "LocationChests").Args[0]
            .Should().BeEquivalentTo(new { Chests = chests });
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "LocationNpcs");
        clients.Caller.SentMessages.Should().Contain(m =>
            m.Method == "SystemMessage" && (string)m.Args[0]! == "Pool 'forest-goblins' references no known actors");

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<EnterZoneLocationHubCommand>().Single();
        command.ZoneId.Should().Be("ashlen-wood");
        command.LocationSlug.Should().Be("old-clearing");
        command.ZoneGroup.Should().Be("zone:ashlen-wood", "the previous group is forwarded for the peer roster");
    }

    [Fact]
    public async Task EnterLocation_Joins_The_Arrival_Group_When_There_Was_No_Zone_Group()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, _, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id, connectionId: "conn-new");
        ctx.Items["CharacterId"] = character.Id;
        StubArrival(mediator, "zone:ashlen-wood#i1:2f9c");

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        groups.RemovedGroups.Should().BeEmpty("there was no group to leave");
        groups.AddedGroups.Should().Contain("zone:ashlen-wood#i1:2f9c");
        ctx.Items["CurrentZoneGroupName"].Should().Be("zone:ashlen-wood#i1:2f9c");
    }

    [Fact]
    public async Task EnterLocation_Stays_In_Its_Group_When_The_Arrival_Group_Is_The_Same()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        var (hub, _, groups, ctx, mediator, _) = HubHarness.Create(db, account.Id, connectionId: "conn-same");
        ctx.EnterZoneState(character.Id, character.Name, "ashlen-wood", "zone:ashlen-wood");
        StubArrival(mediator, "zone:ashlen-wood");

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        groups.AddedGroups.Should().BeEmpty("a room inside the same zone keeps the connection where it is");
        groups.RemovedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task EnterLocation_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<EnterZoneLocationHubResult>(new InvalidOperationException("boom"));

        await hub.EnterLocation(new EnterZoneLocationHubRequest("ashlen-wood", "old-clearing"));

        ShouldBeError(clients.Caller, "Failed to enter location");
    }
}
