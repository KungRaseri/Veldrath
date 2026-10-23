using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Veldrath.Contracts.Map;
using Veldrath.GameClient.Components.Components.Shared;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>Rendering and interaction tests for <see cref="CardinalMap"/> and <see cref="MapDetailPanel"/>.</summary>
public class CardinalMapComponentTests : BunitContext
{
    private const string Entry = "zone:test:entry";
    private const string Square = "zone:test:location:square";
    private const string Exit = "zone:test:exit:target";

    /// <summary>Registers MudBlazor services and sync-disposable test doubles.</summary>
    public CardinalMapComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IResizeObserverFactory, NoopResizeObserverFactory>();
        Services.AddMudServices();
    }

    [Fact]
    public void CardinalMap_RendersNodesAndConnectors()
    {
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry));

        cut.Find("[data-testid='cardinal-map']").Should().NotBeNull();
        cut.Find($"[data-testid='map-node-{Entry}']").Should().NotBeNull();
        cut.Find($"[data-testid='map-node-{Square}']").Should().NotBeNull();
        cut.Find($"[data-testid='map-node-{Exit}']").Should().NotBeNull();
        cut.FindAll(".map-connector").Should().NotBeEmpty();
        cut.Markup.Should().Contain("You are here");
    }

    [Fact]
    public void CardinalMap_UsesCompactCellDimensions()
    {
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry));

        // Three nodes in one row: 3 * 160px cells + 2 * 8px canvas padding.
        cut.Find(".cardinal-map-canvas").GetAttribute("style")
            .Should().Contain("width:496px")
            .And.Contain("height:86px");
    }

    [Fact]
    public void CardinalMap_RendersDirectionLabels_OnConnectors()
    {
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry));

        var labels = cut.FindAll(".map-connector-label");
        labels.Should().HaveCount(2);
        labels.Should().OnlyContain(label => label.TextContent.Trim() == "→");
    }

    [Fact]
    public void CardinalMap_RendersCompactEntryAndExitBadges()
    {
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry));

        cut.Find($"[data-testid='map-node-{Entry}'] .map-node-badge-entry")
            .TextContent.Trim().Should().Be("Entry");
        cut.Find($"[data-testid='map-node-{Exit}'] .map-node-badge-exit")
            .TextContent.Trim().Should().Be("Exit");
    }

    [Fact]
    public void CardinalMap_KeyNavigation_PrefersGraphEdgeDirection()
    {
        // Entry is East of the square in the authored layout, but the edge says South.
        // ArrowDown must follow the edge to the square instead of falling back to space.
        var graph = new MapGraphDto(
            "zone:test", "Zone", "test", "Test Zone", "A test map.", Entry,
            [
                Node(Entry, "EntryPoint", null, "Arrival", 0, 0, isEntry: true, sortOrder: 0),
                Node(Square, "Location", "square", "The Square", 1, 0, sortOrder: 1),
            ],
            [Edge(Entry, Square, "South")]);

        MapNodeDto? selected = null;
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, graph)
            .Add(p => p.CurrentNodeId, Entry)
            .Add(p => p.OnSelect, node => selected = node));

        cut.Find("[data-testid='cardinal-map']")
            .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });

        selected.Should().NotBeNull();
        selected!.Id.Should().Be(Square);
    }

    [Fact]
    public void CardinalMap_Enter_DoesNotTravelToCurrentNode()
    {
        MapNodeDto? travelled = null;
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry)
            .Add(p => p.OnTravel, node => travelled = node));

        cut.Find("[data-testid='cardinal-map']")
            .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        travelled.Should().BeNull();
    }

    [Fact]
    public void CardinalMap_ClickSelectsNode()
    {
        MapNodeDto? selected = null;
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry)
            .Add(p => p.OnSelect, node => selected = node));

        cut.Find($"[data-testid='map-node-{Square}']").Click();

        selected.Should().NotBeNull();
        selected!.Id.Should().Be(Square);
    }

    [Fact]
    public void CardinalMap_ArrowKeySelectsNearestNode()
    {
        MapNodeDto? selected = null;
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry)
            .Add(p => p.OnSelect, node => selected = node));

        cut.Find("[data-testid='cardinal-map']")
            .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });

        selected.Should().NotBeNull();
        selected!.Id.Should().Be(Square);
    }

    [Fact]
    public void CardinalMap_EnterRequestsTravelForSelection()
    {
        MapNodeDto? travelled = null;
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph())
            .Add(p => p.CurrentNodeId, Entry)
            .Add(p => p.SelectedNodeId, Square)
            .Add(p => p.OnTravel, node => travelled = node));

        cut.Find("[data-testid='cardinal-map']")
            .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Enter" });

        travelled.Should().NotBeNull();
        travelled!.Id.Should().Be(Square);
    }

    [Fact]
    public void CardinalMap_RendersEmptyState_WhenMapMissing()
    {
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, null));

        cut.Markup.Should().Contain("No map graph loaded.");
    }

    [Fact]
    public void CardinalMap_MarksHiddenNodes()
    {
        var cut = Render<CardinalMap>(parameters => parameters
            .Add(p => p.Map, MakeGraph(hiddenSquare: true))
            .Add(p => p.CurrentNodeId, Entry));

        cut.Find($"[data-testid='map-node-{Square}']").ClassList.Should().Contain("map-node-hidden");
    }

    [Fact]
    public void MapDetailPanel_RendersDetailsAndTravelState()
    {
        MapNodeDto? travelled = null;
        var node = MakeGraph().Nodes.Single(n => n.Id == Square);
        var cut = Render<MapDetailPanel>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsCurrent, false)
            .Add(p => p.CanTravel, true)
            .Add(p => p.OnTravel, value => travelled = value));

        cut.Markup.Should().Contain("The Square");
        cut.Markup.Should().Contain("Travel");
        cut.FindAll("button").Single(button => button.TextContent.Contains("Travel")).Click();

        travelled.Should().NotBeNull();
        travelled!.Id.Should().Be(Square);
    }

    [Fact]
    public void MapDetailPanel_DisablesTravelAtCurrentNode()
    {
        var node = MakeGraph().Nodes.Single(n => n.Id == Entry);
        var cut = Render<MapDetailPanel>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.IsCurrent, true)
            .Add(p => p.CanTravel, true));

        cut.Markup.Should().Contain("You are here");
        cut.FindAll("button").Single(button => button.TextContent.Contains("You are here"))
            .HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void MapDetailPanel_RendersEmptyState_WhenNoNodeSelected()
    {
        var cut = Render<MapDetailPanel>(parameters => parameters
            .Add(p => p.Node, null));

        cut.Markup.Should().Contain("Select a node to see its details.");
    }

    private static MapGraphDto MakeGraph(bool hiddenSquare = false) =>
        new(
            "zone:test",
            "Zone",
            "test",
            "Test Zone",
            "A test map.",
            Entry,
            [
                Node(Entry, "EntryPoint", null, "Arrival", 0, 0, isEntry: true, sortOrder: 0),
                Node(Square, "Location", "square", "The Square", 1, 0, hidden: hiddenSquare, sortOrder: 1),
                Node(Exit, "ZoneExit", "target", "Road Out", 2, 0, isExit: true, sortOrder: 2,
                    targetMapId: "zone:target", targetNodeId: "zone:target:entry"),
            ],
            [
                Edge(Entry, Square, "East"),
                Edge(Square, Exit, "East"),
            ]);

    private static MapNodeDto Node(
        string id,
        string nodeType,
        string? refId,
        string name,
        int x,
        int y,
        bool isEntry = false,
        bool isExit = false,
        bool hidden = false,
        int sortOrder = 0,
        string? targetMapId = null,
        string? targetNodeId = null) =>
        new(
            id,
            "zone:test",
            nodeType,
            refId,
            name,
            $"{name} description.",
            "place",
            x,
            y,
            isEntry,
            isExit,
            targetMapId,
            targetNodeId,
            hidden,
            UnlockType: hidden ? "skill_check_passive" : null,
            UnlockKey: null,
            DiscoverThreshold: hidden ? 5 : null,
            sortOrder);

    private static MapEdgeDto Edge(string from, string to, string direction) =>
        new(
            Guid.NewGuid(),
            "zone:test",
            from,
            to,
            direction,
            "Walk",
            Label: null,
            IsTraversable: true,
            IsHidden: false);
}
