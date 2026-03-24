using System.Reactive.Linq;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Zones;

namespace RealmUnbound.Client.Tests.ViewModels;

public class MapViewModelTests : TestBase
{
    private static MapViewModel MakeVm(FakeZoneService? zones = null,
        string? currentZoneId = null, string? currentRegionId = null, Guid? characterId = null)
        => new(zones ?? new FakeZoneService(), currentZoneId, currentRegionId, characterId);

    // ── World level ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WorldLevel_Should_Load_One_Node_Per_Region()
    {
        var zones = new FakeZoneService
        {
            Regions =
            [
                new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth"),
                new("stormreach", "Stormreach", "desc", "Highland", 5, 15, false, "draveth"),
            ]
        };

        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        vm.Nodes.Should().HaveCount(2);
        vm.Nodes.Select(n => n.Id).Should().BeEquivalentTo(["everwood", "stormreach"]);
    }

    [Fact]
    public async Task WorldLevel_Node_NodeType_Should_Be_Region()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")]
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        vm.Nodes.Should().ContainSingle()
            .Which.NodeType.Should().Be("region");
    }

    [Fact]
    public async Task WorldLevel_CurrentRegion_Node_Should_Be_Marked_Current()
    {
        var zones = new FakeZoneService
        {
            Regions =
            [
                new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth"),
                new("stormreach", "Stormreach", "desc", "Highland", 5, 15, false, "draveth"),
            ]
        };
        var vm = MakeVm(zones, currentRegionId: "everwood");
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        vm.Nodes.Single(n => n.Id == "everwood").IsCurrent.Should().BeTrue();
        vm.Nodes.Single(n => n.Id == "stormreach").IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task WorldLevel_Should_Set_MapLevel_To_World()
    {
        var vm = MakeVm();
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        vm.MapLevel.Should().Be(MapLevel.World);
        vm.IsWorldLevel.Should().BeTrue();
    }

    [Fact]
    public async Task WorldLevel_CanDrillOut_Should_Be_False()
    {
        var vm = MakeVm();
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        vm.CanDrillOut.Should().BeFalse();
    }

    [Fact]
    public async Task WorldLevel_Title_Should_Be_WorldMap()
    {
        var vm = MakeVm();
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        vm.Title.Should().Be("World Map");
    }

    // ── Region level ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DrillInto_Region_Should_Load_Zone_Nodes()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
            Zones =
            [
                new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood"),
                new("darkwood-hollow", "Darkwood Hollow", "desc", "Wilderness", 2, 20, false, 0, "everwood"),
            ]
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        var regionNode = vm.Nodes.Single(n => n.Id == "everwood");
        await vm.DrillIntoCommand.Execute(regionNode);

        vm.MapLevel.Should().Be(MapLevel.Region);
        vm.Nodes.Should().HaveCount(2);
        vm.Nodes.Select(n => n.Id).Should().BeEquivalentTo(["fenwick-crossing", "darkwood-hollow"]);
    }

    [Fact]
    public async Task DrillInto_Region_CanDrillOut_Should_Be_True()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        await vm.DrillIntoCommand.Execute(vm.Nodes.First());

        vm.CanDrillOut.Should().BeTrue();
    }

    [Fact]
    public async Task DrillOut_From_Region_Should_Return_To_World()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        await vm.DrillIntoCommand.Execute(vm.Nodes.First());
        await vm.DrillOutCommand.Execute();

        vm.MapLevel.Should().Be(MapLevel.World);
    }

    // ── Zone level ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DrillInto_Zone_Should_Load_Location_Nodes()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
            Zones = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
            Locations = [
                new("fenwick-inn", "The Inn", "town", "fenwick-crossing", "location", 10, null, null),
                new("fenwick-market", "Market", "town", "fenwick-crossing", "location", 10, null, null),
            ]
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // region
        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // zone

        vm.MapLevel.Should().Be(MapLevel.Zone);
        vm.Nodes.Should().HaveCount(2);
        vm.Nodes.Select(n => n.Id).Should().BeEquivalentTo(["fenwick-inn", "fenwick-market"]);
    }

    [Fact]
    public async Task DrillInto_Zone_Node_NodeType_Should_Be_Location()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
            Zones = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
            Locations = [new("fenwick-inn", "The Inn", "town", "fenwick-crossing", "location", 10, null, null)]
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single());
        await vm.DrillIntoCommand.Execute(vm.Nodes.Single());

        vm.Nodes.Should().ContainSingle()
            .Which.NodeType.Should().Be("location");
    }

    [Fact]
    public async Task DrillOut_From_Zone_Should_Return_To_Region()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
            Zones = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Region
        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Zone
        await vm.DrillOutCommand.Execute();                    // → Region

        vm.MapLevel.Should().Be(MapLevel.Region);
    }

    // ── Node selection ──────────────────────────────────────────────────────

    [Fact]
    public async Task SelectNode_Should_Set_SelectedNode()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")]
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        var node = vm.Nodes.Single();
        await vm.SelectNodeCommand.Execute(node);

        vm.SelectedNode.Should().Be(node);
    }

    // ── Layout ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AfterLoad_Nodes_Should_Have_NonZero_Positions()
    {
        var zones = new FakeZoneService
        {
            Regions =
            [
                new("a", "A", "d", "Forest", 1, 10, true, "draveth"),
                new("b", "B", "d", "Forest", 1, 10, false, "draveth"),
            ]
        };
        var vm = MakeVm(zones);
        await Task.Yield(); // allow fire-and-forget LoadWorldLevelAsync to complete

        // At least one node should have a position beyond origin
        vm.Nodes.Should().OnlyContain(n => n.X > 0 || n.Y > 0);
    }
}
