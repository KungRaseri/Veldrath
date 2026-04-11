using Avalonia.Headless.XUnit;
using System.Reactive.Linq;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;
using Veldrath.Contracts.Content;
using Veldrath.Contracts.Zones;

namespace Veldrath.Client.Tests.ViewModels;

public class MapViewModelTests : TestBase
{
    private static MapViewModel MakeVm(FakeZoneService? zones = null,
        string? currentZoneId = null, string? currentRegionId = null,
        string? currentZoneLocationSlug = null, Guid? characterId = null)
        => new(zones ?? new FakeZoneService(), currentZoneId, currentRegionId, currentZoneLocationSlug, characterId);

    // ------ Full graph loading ------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task FullGraph_Loads_Region_And_Zone_Nodes()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "veldrath")],
            Zones   =
            [
                new("fenwick-crossing", "Fenwick Crossing", "desc", "Town",       1, 50, true,  0, "everwood"),
                new("darkwood-hollow",  "Darkwood Hollow",  "desc", "Wilderness", 2, 20, false, 0, "everwood"),
            ]
        };

        var vm = MakeVm(zones);
        await Task.Yield();

        // 1 region + 2 zones = 3 nodes
        vm.Nodes.Should().HaveCount(3);
        vm.Nodes.Select(n => n.Id).Should().BeEquivalentTo("everwood", "fenwick-crossing", "darkwood-hollow");
    }

    [Fact]
    public async Task RegionNodes_Have_RegionNodeType()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "veldrath")],
        };

        var vm = MakeVm(zones);
        await Task.Yield();

        vm.Nodes.Single(n => n.Id == "everwood").NodeType.Should().Be("region_header");
    }

    [Fact]
    public async Task ZoneNodes_Have_ZoneNodeType()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "veldrath")],
            Zones   = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
        };

        var vm = MakeVm(zones);
        await Task.Yield();

        vm.Nodes.Single(n => n.Id == "fenwick-crossing").NodeType.Should().Be("zone");
    }

    [Fact]
    public async Task ZoneNodes_Carry_RegionId_And_RegionLabel()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "veldrath")],
            Zones   = [new("fenwick-crossing", "Fenwick Crossing", "desc", "Town", 1, 50, true, 0, "everwood")],
        };

        var vm = MakeVm(zones);
        await Task.Yield();

        var zoneNode = vm.Nodes.Single(n => n.NodeType == "zone");
        zoneNode.RegionId.Should().Be("everwood");
        zoneNode.RegionLabel.Should().Be("Everwood");
    }

    [Fact]
    public async Task CurrentRegion_Node_Should_Be_Marked_Current()
    {
        var zones = new FakeZoneService
        {
            Regions =
            [
                new("everwood",   "Everwood",   "desc", "Forest",   1, 10, true,  "veldrath"),
                new("stormreach", "Stormreach", "desc", "Highland", 5, 15, false, "veldrath"),
            ]
        };

        var vm = MakeVm(zones, currentRegionId: "everwood");
        await Task.Yield();

        vm.Nodes.Single(n => n.Id == "everwood").IsCurrent.Should().BeTrue();
        vm.Nodes.Single(n => n.Id == "stormreach").IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task CurrentZone_Node_Should_Be_Marked_Current()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "veldrath")],
            Zones   =
            [
                new("fenwick-crossing", "Fenwick Crossing", "desc", "Town",       1, 50, true,  0, "everwood"),
                new("darkwood-hollow",  "Darkwood Hollow",  "desc", "Wilderness", 2, 20, false, 0, "everwood"),
            ]
        };

        var vm = MakeVm(zones, currentZoneId: "fenwick-crossing");
        await Task.Yield();

        vm.Nodes.Single(n => n.Id == "fenwick-crossing").IsCurrent.Should().BeTrue();
        vm.Nodes.Single(n => n.Id == "darkwood-hollow").IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task Title_Should_Be_WorldMap()
    {
        var vm = MakeVm();
        await Task.Yield();

        vm.Title.Should().Be("World Map");
    }

    // ------ Node selection ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task SelectNode_Should_Set_SelectedNode()
    {
        var zones = new FakeZoneService
        {
            Regions = [new("everwood", "Everwood", "desc", "Forest", 1, 10, true, "veldrath")]
        };

        var vm = MakeVm(zones);
        await Task.Yield();

        var node = vm.Nodes.Single(n => n.NodeType == "region_header");
        await vm.SelectNodeCommand.Execute(node);

        vm.SelectedNode.Should().Be(node);
    }

    // ------ Layout ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AfterLoad_Nodes_Should_Have_NonZero_Positions()
    {
        var zones = new FakeZoneService
        {
            Regions =
            [
                new("a", "A", "d", "Forest", 1, 10, true,  "veldrath"),
                new("b", "B", "d", "Forest", 1, 10, false, "veldrath"),
            ],
            Zones =
            [
                new("z1", "Z1", "d", "Town", 1, 50, true, 0, "a"),
                new("z2", "Z2", "d", "Town", 1, 50, true, 0, "b"),
            ]
        };

        var vm = MakeVm(zones);
        await Task.Yield();

        vm.Nodes.Should().OnlyContain(n => n.X > 0 || n.Y > 0);
    }

    // ------ IsRegionNode ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void IsRegionNode_Returns_True_For_Region_NodeType()
    {
        var node = new MapNodeViewModel("id", "Region", "region");
        node.IsRegionNode.Should().BeTrue();
    }

    [Fact]
    public void IsRegionNode_Returns_False_For_Zone_NodeType()
    {
        var node = new MapNodeViewModel("id", "Zone", "zone");
        node.IsRegionNode.Should().BeFalse();
    }

    // ------ MapNodeViewModel.DisplayLabel ------------------------------------------------------------------------------------------------------------------------

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
