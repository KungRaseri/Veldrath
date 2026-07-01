using Bunit;
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
/// bUnit tests for the <see cref="ActionBar"/> component's hotbar ability quick-slot functionality.
/// Verifies hotbar rendering, slot states, cooldown display, and ability click handling.
/// </summary>
public class ActionBarHotbarTests : TestContext
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
    /// Verifies the hotbar renders the specified number of slots.
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

        // Should render 3 slot buttons.
        var slotButtons = cut.FindAll(".hotbar-slot");
        Assert.Equal(3, slotButtons.Count);
    }

    /// <summary>
    /// Verifies that empty slots show the placeholder indicator.
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
        Assert.NotNull(emptySlot);
        Assert.Contains("\u2014", emptySlot.TextContent);
    }

    /// <summary>
    /// Verifies that available ability slots are rendered as ready.
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
        Assert.NotNull(readySlot);
    }

    /// <summary>
    /// Verifies that a slot on cooldown shows the cooldown timer.
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
        Assert.NotNull(cooldownSlot);
        Assert.Contains("5", cooldownSlot.TextContent);
    }

    /// <summary>
    /// Verifies clicking an available ability slot invokes the OnUseAbility callback.
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

        var readySlot = cut.Find(".hotbar-slot-ready");
        readySlot.Click();

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

        // Should not render any hotbar elements.
        Assert.DoesNotContain("hotbar", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the key bind label is shown on each slot.
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

        var slotButtons = cut.FindAll(".hotbar-slot");
        Assert.Equal(3, slotButtons.Count);
        Assert.Contains("1", slotButtons[0].TextContent);
        Assert.Contains("5", slotButtons[1].TextContent);
        Assert.Contains("0", slotButtons[2].TextContent);
    }
}
