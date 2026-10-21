using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Veldrath.GameClient.Components.Components.Shared;
using Veldrath.GameClient.Components.Models;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>Rendering and interaction tests for the text-first UI primitives.</summary>
public class TextUiFoundationTests : BunitContext
{
    /// <summary>Registers the MudBlazor services and sync-disposable test doubles used by the components.</summary>
    public TextUiFoundationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IResizeObserverFactory, NoopResizeObserverFactory>();
        Services.AddMudServices();
    }

    [Fact]
    public void TextPanel_RendersTitleSubtitleIconAndBody()
    {
        var cut = Render<TextPanel>(parameters => parameters
            .Add(p => p.Title, "Inventory")
            .Add(p => p.Subtitle, "12 items")
            .Add(p => p.Icon, Icons.Material.Filled.Backpack)
            .Add(p => p.TestId, "inventory-panel")
            .AddChildContent("<span data-testid=\"panel-body\">Body content</span>"));

        cut.Find("[data-testid='inventory-panel']").Should().NotBeNull();
        cut.Markup.Should().Contain("Inventory");
        cut.Markup.Should().Contain("12 items");
        cut.Find("[data-testid='panel-body']").TextContent.Should().Be("Body content");
    }

    [Fact]
    public void StatRow_RendersLabelAndValue()
    {
        var cut = Render<StatRow>(parameters => parameters
            .Add(p => p.Label, "Health")
            .Add(p => p.Value, "80 / 100")
            .Add(p => p.TestId, "hp-row"));

        cut.Find("[data-testid='hp-row']").TextContent.Should().Contain("Health");
        cut.Find("[data-testid='hp-row']").TextContent.Should().Contain("80 / 100");
    }

    [Fact]
    public void StatTable_RendersEveryStat()
    {
        var cut = Render<StatTable>(parameters => parameters
            .Add(p => p.Title, "Attributes")
            .Add(p => p.Stats,
            [
                new StatEntry("Strength", "15", Icons.Material.Filled.FitnessCenter),
                new StatEntry("Dexterity", "12"),
            ]));

        cut.Markup.Should().Contain("Strength");
        cut.Markup.Should().Contain("15");
        cut.Markup.Should().Contain("Dexterity");
        cut.Markup.Should().Contain("12");
    }

    [Fact]
    public void StatTable_RendersEmptyMessage()
    {
        var cut = Render<StatTable>(parameters => parameters
            .Add(p => p.Stats, [])
            .Add(p => p.EmptyText, "No stats."));

        cut.Markup.Should().Contain("No stats.");
    }

    [Fact]
    public void EntityList_RendersItemsAndRaisesSelection()
    {
        EntityListItem? selected = null;
        var cut = Render<EntityList>(parameters => parameters
            .Add(p => p.Title, "Locations")
            .Add(p => p.Items,
            [
                new EntityListItem("square", "The Square", "Town centre", Icons.Material.Filled.Place, "Current", IsSelected: true),
                new EntityListItem("inn", "The Inn", "Rest here"),
            ])
            .Add(p => p.OnSelect, item => selected = item));

        cut.Markup.Should().Contain("The Square");
        cut.Markup.Should().Contain("Current");
        cut.Find("[data-testid='entity-inn']").Click();

        selected.Should().NotBeNull();
        selected!.Id.Should().Be("inn");
    }

    [Fact]
    public void EntityList_DisabledItem_IsNotSelectable()
    {
        EntityListItem? selected = null;
        var cut = Render<EntityList>(parameters => parameters
            .Add(p => p.Items,
            [
                new EntityListItem("locked", "Locked Gate", IsDisabled: true),
            ])
            .Add(p => p.OnSelect, item => selected = item));

        cut.Find("[data-testid='entity-locked']").HasAttribute("disabled").Should().BeTrue();
        selected.Should().BeNull();
    }

    [Fact]
    public void ItemTable_RendersRowsAndHeaders()
    {
        var cut = Render<ItemTable>(parameters => parameters
            .Add(p => p.Title, "Backpack")
            .Add(p => p.Items,
            [
                new ItemTableEntry("sword", "Iron Sword", 1, "MainHand", "25g", Icons.Material.Filled.Gavel, IsEquipped: true),
                new ItemTableEntry("potion", "Healing Potion", 3, "Consumable", "10g"),
            ]));

        cut.Markup.Should().Contain("Iron Sword");
        cut.Markup.Should().Contain("Healing Potion");
        cut.Markup.Should().Contain("Equipped");
        cut.Markup.Should().Contain("Qty");
        cut.Markup.Should().Contain("Value");
    }

    [Fact]
    public void ItemTable_SelectButton_RaisesRow()
    {
        ItemTableEntry? selected = null;
        var cut = Render<ItemTable>(parameters => parameters
            .Add(p => p.Items,
            [
                new ItemTableEntry("potion", "Healing Potion", 3, "Consumable", "10g"),
            ])
            .Add(p => p.Selectable, true)
            .Add(p => p.OnSelect, item => selected = item));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Select")).Click();

        selected.Should().NotBeNull();
        selected!.Id.Should().Be("potion");
    }

    [Fact]
    public void ItemTable_RendersEmptyMessage()
    {
        var cut = Render<ItemTable>(parameters => parameters
            .Add(p => p.Items, [])
            .Add(p => p.EmptyText, "Nothing carried."));

        cut.Markup.Should().Contain("Nothing carried.");
    }

    [Fact]
    public void DetailPane_RendersTitleDescriptionAndContent()
    {
        var cut = Render<DetailPane>(parameters => parameters
            .Add(p => p.Title, "The Crestfall Square")
            .Add(p => p.Subtitle, "Location")
            .Add(p => p.Description, "A busy market square.")
            .Add(p => p.TestId, "detail-pane")
            .AddChildContent("<span data-testid=\"detail-body\">Travellers gather here.</span>"));

        cut.Find("[data-testid='detail-pane']").Should().NotBeNull();
        cut.Markup.Should().Contain("The Crestfall Square");
        cut.Markup.Should().Contain("A busy market square.");
        cut.Find("[data-testid='detail-body']").TextContent.Should().Be("Travellers gather here.");
    }

    [Fact]
    public void DetailPane_RendersEmptyMessage_WhenNoSelection()
    {
        var cut = Render<DetailPane>(parameters => parameters
            .Add(p => p.EmptyText, "Select a node."));

        cut.Markup.Should().Contain("Select a node.");
    }
}
