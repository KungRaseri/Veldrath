using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.GameClient.Components.Components.Shared;
using Veldrath.GameClient.Components.Models;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// bUnit tests for the <see cref="ActionBar"/> component's text-table ability hotbar.
/// Verifies slot rows, keybind text, icons, cooldown display, and ability use handling.
/// </summary>
public class ActionBarHotbarTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers stub services.
    /// </summary>
    public ActionBarHotbarTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
    }

    /// <summary>
    /// Verifies the hotbar table renders one row per slot.
    /// </summary>
    [Fact]
    public void Hotbar_Renders_All_Slots()
    {
        var slots = new List<HotbarAbility>
        {
            new(1, "1", null, null, null, 0, false),
            new(2, "2", null, null, null, 0, false),
            new(3, "3", null, null, null, 0, false),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots));

        var slotRows = cut.FindAll(".hotbar-slot");
        Assert.Equal(3, slotRows.Count);
    }

    /// <summary>
    /// Verifies that empty slots show placeholder text instead of an ability name.
    /// </summary>
    [Fact]
    public void Hotbar_Empty_Slots_Show_Placeholder()
    {
        var slots = new List<HotbarAbility>
        {
            new(1, "1", null, null, null, 0, false),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots));

        var emptySlot = cut.Find(".hotbar-slot-empty");
        Assert.Contains("Empty slot", emptySlot.TextContent);
        Assert.Contains("Unassigned", emptySlot.TextContent);
    }

    /// <summary>
    /// Verifies that available ability slots are rendered as ready with their name.
    /// </summary>
    [Fact]
    public void Hotbar_Available_Ability_Shows_Ready()
    {
        var slots = new List<HotbarAbility>
        {
            new(1, "1", "fireball", "Fireball", "icon-fire", 0, true),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots));

        var readySlot = cut.Find(".hotbar-slot-ready");
        Assert.Contains("Fireball", readySlot.TextContent);
        Assert.Contains("Ready", readySlot.TextContent);
    }

    /// <summary>
    /// Verifies that a slot on cooldown shows the remaining time as text.
    /// </summary>
    [Fact]
    public void Hotbar_Cooldown_Shows_Timer()
    {
        var slots = new List<HotbarAbility>
        {
            new(1, "1", "heal", "Heal", "icon-heal", 5, false),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots));

        var cooldownSlot = cut.Find(".hotbar-slot-cooldown");
        Assert.Contains("Cooldown 5 s", cooldownSlot.TextContent);
    }

    /// <summary>
    /// Verifies clicking the Use action of an available ability invokes the callback.
    /// </summary>
    [Fact]
    public void Hotbar_Clicking_Ability_Triggers_Callback()
    {
        string? invokedAbilityId = null;

        var slots = new List<HotbarAbility>
        {
            new(1, "1", "fireball", "Fireball", "icon-fire", 0, true),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots)
            .Add(p => p.OnUseAbility, EventCallback.Factory.Create<string?>(this, id => invokedAbilityId = id)));

        cut.Find(".hotbar-slot-ready button").Click();

        Assert.Equal("fireball", invokedAbilityId);
    }

    /// <summary>
    /// Verifies the hotbar is hidden when ShowHotbar is false.
    /// </summary>
    [Fact]
    public void Hotbar_Hidden_When_ShowHotbar_False()
    {
        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, false));

        Assert.DoesNotContain("hotbar", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the hotbar panel is not rendered when there are no slots.
    /// </summary>
    [Fact]
    public void Hotbar_Not_Rendered_When_No_Slots()
    {
        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true));

        Assert.DoesNotContain("hotbar", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the key bind label is shown on each slot row.
    /// </summary>
    [Fact]
    public void Hotbar_Shows_KeyBind_Label()
    {
        var slots = new List<HotbarAbility>
        {
            new(1, "1", null, null, null, 0, false),
            new(5, "5", null, null, null, 0, false),
            new(10, "0", null, null, null, 0, false),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots));

        var slotRows = cut.FindAll(".hotbar-slot");
        Assert.Equal(3, slotRows.Count);
        Assert.Contains("1", slotRows[0].TextContent);
        Assert.Contains("5", slotRows[1].TextContent);
        Assert.Contains("0", slotRows[2].TextContent);
    }

    /// <summary>
    /// Verifies slot rows render Material icons rather than emoji glyphs.
    /// </summary>
    [Fact]
    public void Hotbar_Renders_Material_Icons()
    {
        var slots = new List<HotbarAbility>
        {
            new(1, "1", "fireball", "Fireball", "icon-fire", 0, true),
            new(2, "2", "heal", "Heal", "icon-heal", 3, false),
            new(3, "3", null, null, null, 0, false),
        };

        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, true)
            .Add(p => p.HotbarSlots, slots));

        var rows = cut.FindAll(".hotbar-slot");
        foreach (var row in rows)
        {
            row.QuerySelector("svg.mud-icon-root").Should().NotBeNull();
        }

        cut.Markup.Should().NotMatchRegex(@"[\uD800-\uDFFF]");
    }

    /// <summary>
    /// Verifies the contextual action buttons use Material icons and text labels without emoji.
    /// </summary>
    [Fact]
    public void ActionButtons_Use_Material_Icons_Without_Emoji()
    {
        var cut = Render<ActionBar>(parameters => parameters
            .Add(p => p.ShowHotbar, false)
            .Add(p => p.ShowAttack, true)
            .Add(p => p.ShowDefend, true)
            .Add(p => p.ShowFlee, true)
            .Add(p => p.ShowSearch, true)
            .Add(p => p.ShowRest, true));

        cut.Markup.Should().Contain("Attack");
        cut.Markup.Should().Contain("Defend");
        cut.Markup.Should().Contain("Flee");
        cut.Markup.Should().Contain("Search");
        cut.Markup.Should().Contain("Rest");
        cut.Markup.Should().NotMatchRegex(@"[\uD800-\uDFFF]");

        var searchButton = cut.FindAll("button").First(button => button.TextContent.Contains("Search"));
        searchButton.QuerySelector("svg.mud-icon-root").Should().NotBeNull();
    }
}
