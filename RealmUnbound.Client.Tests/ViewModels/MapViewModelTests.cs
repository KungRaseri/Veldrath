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
        string? currentZoneId = null, string? currentRegionId = null,
        string? currentZoneLocationSlug = null, Guid? characterId = null)
        => new(zones ?? new FakeZoneService(), currentZoneId, currentRegionId, currentZoneLocationSlug, characterId);

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

    [Fact]
    public async Task ZoneLevel_CurrentLocation_Node_Should_Be_Marked_Current()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
            Zones = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
            Locations =
            [
                new("fenwick-inn", "The Inn", "town", "fenwick-crossing", "location", 10, null, null),
                new("fenwick-market", "Market", "town", "fenwick-crossing", "location", 10, null, null),
            ]
        };
        var vm = MakeVm(zones, currentZoneLocationSlug: "fenwick-inn");
        await Task.Yield();

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Region
        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Zone

        vm.Nodes.Single(n => n.Id == "fenwick-inn").IsCurrent.Should().BeTrue();
        vm.Nodes.Single(n => n.Id == "fenwick-market").IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task ZoneLevel_HiddenLocation_Node_Should_Have_IsHidden_True()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "draveth")],
            Zones = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
            Locations =
            [
                new("fenwick-inn", "The Inn", "town", "fenwick-crossing", "location", 10, null, null),
                new("secret-vault", "???", "town", "fenwick-crossing", "location", 10, null, null, IsHidden: true),
            ]
        };
        var vm = MakeVm(zones);
        await Task.Yield();

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Region
        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Zone

        vm.Nodes.Single(n => n.Id == "secret-vault").IsHidden.Should().BeTrue();
        vm.Nodes.Single(n => n.Id == "fenwick-inn").IsHidden.Should().BeFalse();
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

    // ── NullSelectionHint ────────────────────────────────────────────────────

    [Fact]
    public async Task NullSelectionHint_At_WorldLevel_Should_Mention_DoubleClick()
    {
        var vm = MakeVm();
        await Task.Yield();

        vm.NullSelectionHint.Should().Contain("Double-click");
    }

    [Fact]
    public async Task NullSelectionHint_At_ZoneLevel_Should_Not_Mention_DoubleClick()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("r", "R", "d", "Forest", 1, 10, true, "draveth")],
            Zones = [new("z", "Z", "d", "Town", 1, 50, true, 0, "r")],
        };
        var vm = MakeVm(zones);
        await Task.Yield();

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Region
        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Zone

        vm.NullSelectionHint.Should().NotContain("Double-click");
    }

    // ── DrillInLabel ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DrillInLabel_At_WorldLevel_Should_Say_ViewRegion()
    {
        var vm = MakeVm();
        await Task.Yield();

        vm.DrillInLabel.Should().Contain("Region");
    }

    [Fact]
    public async Task DrillInLabel_At_RegionLevel_Should_Say_ViewLocations()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("r", "R", "d", "Forest", 1, 10, true, "draveth")],
        };
        var vm = MakeVm(zones);
        await Task.Yield();

        await vm.DrillIntoCommand.Execute(vm.Nodes.Single()); // → Region

        vm.DrillInLabel.Should().Contain("Location");
    }

    // ── MapNodeViewModel.DisplayLabel ────────────────────────────────────────

    [Fact]
    public void DisplayLabel_Returns_Label_When_Not_Hidden()
    {
        var node = new MapNodeViewModel("id", "Fenwick Inn", "location");
        node.DisplayLabel.Should().Be("Fenwick Inn");
    }

    [Fact]
    public void DisplayLabel_Returns_QuestionMarks_When_Hidden()
    {
        var node = new MapNodeViewModel("id", "Secret Vault", "location") { IsHidden = true };
        node.DisplayLabel.Should().Be("???");
    }
}
