using Veldrath.Contracts.Map;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Services;
using Veldrath.GameClient.Core.Tests.Infrastructure;

namespace Veldrath.GameClient.Core.Tests;

/// <summary>Tests for map-graph state and hub command helpers.</summary>
public sealed class MapStateTests
{
    private const string MapId = "zone:crestfall";
    private const string EntryNodeId = "zone:crestfall:entry";
    private const string SquareNodeId = "zone:crestfall:location:crestfall-square";

    [Fact]
    public void ApplyMapGraph_SetsMapNodeAndSummaries()
    {
        var state = new GameStateService();

        state.ApplyMapGraph(new MapGraphReceivedPayload(
            Success: true,
            ErrorMessage: null,
            Map: MakeGraph(),
            CurrentNodeId: SquareNodeId,
            AvailableMaps: [MakeSummary()]));

        state.CurrentMapId.Should().Be(MapId);
        state.CurrentNodeId.Should().Be(SquareNodeId);
        state.CurrentMap.Should().NotBeNull();
        state.CurrentMap!.Nodes.Should().ContainSingle(n => n.Id == SquareNodeId);
        state.AvailableMaps.Should().ContainSingle(m => m.Id == MapId);
    }

    [Fact]
    public void ApplyMapGraph_UsesEntryNode_WhenCurrentNodeMissing()
    {
        var state = new GameStateService();

        state.ApplyMapGraph(new MapGraphReceivedPayload(
            Success: true,
            ErrorMessage: null,
            Map: MakeGraph(),
            CurrentNodeId: null));

        state.CurrentNodeId.Should().Be(EntryNodeId);
    }

    [Fact]
    public void ApplyMapGraph_IgnoresFailedPayload()
    {
        var state = new GameStateService();

        state.ApplyMapGraph(new MapGraphReceivedPayload(
            Success: false,
            ErrorMessage: "Map not found",
            Map: null,
            CurrentNodeId: null));

        state.CurrentMap.Should().BeNull();
        state.CurrentMapId.Should().BeNull();
        state.AvailableMaps.Should().BeEmpty();
    }

    [Fact]
    public void ApplyNodeTraveled_UpdatesPositionAndLocation()
    {
        var state = new GameStateService();
        state.ApplyMapGraph(new MapGraphReceivedPayload(true, null, MakeGraph(), EntryNodeId));

        state.ApplyNodeTraveled(new NodeTraveledPayload(
            Success: true,
            ErrorMessage: null,
            MapId: MapId,
            NodeId: SquareNodeId,
            NodeType: "Location",
            MapChanged: false,
            Message: "Travelled to The Crestfall Square.",
            ZoneId: "crestfall",
            RegionId: "varenmark",
            LocationSlug: "crestfall-square"));

        state.CurrentMapId.Should().Be(MapId);
        state.CurrentNodeId.Should().Be(SquareNodeId);
        state.CurrentZoneLocationSlug.Should().Be("crestfall-square");
        state.IsAtLocation.Should().BeTrue();
        state.CurrentMap.Should().NotBeNull();
    }

    [Fact]
    public void ApplyNodeTraveled_MapChangeClearsLoadedGraph()
    {
        var state = new GameStateService();
        state.ApplyMapGraph(new MapGraphReceivedPayload(true, null, MakeGraph(), EntryNodeId));

        state.ApplyNodeTraveled(new NodeTraveledPayload(
            Success: true,
            ErrorMessage: null,
            MapId: "zone:the-droveway",
            NodeId: "zone:the-droveway:entry",
            NodeType: "EntryPoint",
            MapChanged: true,
            Message: "Entered The Droveway.",
            ZoneId: "the-droveway",
            RegionId: "varenmark",
            LocationSlug: null));

        state.CurrentMapId.Should().Be("zone:the-droveway");
        state.CurrentNodeId.Should().Be("zone:the-droveway:entry");
        state.CurrentMap.Should().BeNull();
    }

    [Fact]
    public void ApplyCharacterSelected_CarriesMapPosition()
    {
        var state = new GameStateService();

        state.ApplyCharacterSelected(new CharacterSelectedPayload(
            Guid.NewGuid(), "TestHero", "Warrior", 5, 2500L,
            "crestfall", "varenmark", 80, 100, 30, 50, 100, 2,
            15, 12, 14, 8, 10, 11, [], DateTimeOffset.UtcNow,
            CurrentMapId: MapId,
            CurrentNodeId: EntryNodeId));

        state.CurrentMapId.Should().Be(MapId);
        state.CurrentNodeId.Should().Be(EntryNodeId);
    }

    [Fact]
    public void ApplyZoneEntered_CarriesMapPosition()
    {
        var state = new GameStateService();

        state.ApplyZoneEntered(new ZoneEnteredPayload(
            "crestfall", "Crestfall", "A market town.", "town", [],
            CurrentMapId: MapId,
            CurrentNodeId: EntryNodeId));

        state.CurrentMapId.Should().Be(MapId);
        state.CurrentNodeId.Should().Be(EntryNodeId);
    }

    [Fact]
    public async Task RequestMapGraphAsync_SendsNullArgument_WhenMapIdMissing()
    {
        var hub = new FakeGameHubConnectionService();

        await hub.RequestMapGraphAsync();

        // SignalR hub methods do not support optional parameters, so the null map id
        // must still be sent as an explicit argument.
        hub.SentCommands.Should().ContainSingle(c => c.MethodName == "GetMapGraph" && c.Arg1 == null);
    }

    [Fact]
    public async Task RequestMapGraphAsync_SendsMapId()
    {
        var hub = new FakeGameHubConnectionService();

        await hub.RequestMapGraphAsync(MapId);

        hub.SentCommands.Should().ContainSingle(c =>
            c.MethodName == "GetMapGraph" && (string?)c.Arg1 == MapId);
    }

    [Fact]
    public async Task TravelToNodeAsync_SendsHubRequest()
    {
        var hub = new FakeGameHubConnectionService();

        await hub.TravelToNodeAsync(MapId, SquareNodeId);

        var sent = hub.SentCommands.Should()
            .ContainSingle(c => c.MethodName == "TravelToNode")
            .Subject;

        var request = sent.Arg1.Should().BeOfType<TravelToNodeHubRequest>().Subject;
        request.MapId.Should().Be(MapId);
        request.NodeId.Should().Be(SquareNodeId);
    }

    private static MapGraphDto MakeGraph() =>
        new(
            MapId,
            "Zone",
            "crestfall",
            "Crestfall",
            "A prosperous crossroads market town.",
            EntryNodeId,
            [
                new MapNodeDto(
                    EntryNodeId, MapId, "EntryPoint", null, "Arrival", "Arrival point.",
                    "login", 10, 8, true, false, null, null, false, null, null, null, 0),
                new MapNodeDto(
                    SquareNodeId, MapId, "Location", "crestfall-square", "The Crestfall Square",
                    "The square.", "place", 10, 10, false, false, null, null, false, null, null, null, 10),
            ],
            []);

    private static MapSummaryDto MakeSummary() =>
        new(MapId, "Zone", "crestfall", "Crestfall", "A market town.", EntryNodeId, 2, 0);
}
