using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using RealmEngine.Shared.Models;
using Veldrath.GameClient.Components.Components.Shared;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Browser inventory/equipment/shop interaction tests (web-first P0 item 3).
/// These assert the actual hub commands emitted by the RCL overlays.
/// </summary>
public class WebInventoryInteractionTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>Initializes the test container with the doubles used by the RCL overlays.</summary>
    public WebInventoryInteractionTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton(_gameState);
        Services.AddSingleton<ILogger<InventoryOverlay>>(NullLogger<InventoryOverlay>.Instance);
        Services.AddSingleton<ILogger<ShopOverlay>>(NullLogger<ShopOverlay>.Instance);
        Services.AddSingleton<IDialogService>(new FakeDialogService());
        Services.AddSingleton<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IResizeObserverFactory, NoopResizeObserverFactory>();
        Services.AddMudServices();
    }

    private void SeedCharacter(int gold = 100) =>
        _gameState.ApplyCharacterSelected(
            new CharacterBasicInfo(
                Guid.NewGuid(), "WebHero", "Warrior", 3, 0,
                30, 30, 10, 10, gold),
            currentZoneId: "fenwick-crossing");

    private static void SelectRow<TComponent>(IRenderedComponent<TComponent> cut, string slug)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.Find($"[data-testid='item-{slug}'] button").Click();

    private static void ClickButton<TComponent>(IRenderedComponent<TComponent> cut, string text)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text).Click();

    /// <summary>Equipping a shield routes it to OffHand with the item slug.</summary>
    [Fact]
    public void Inventory_Equip_Sends_EquipItem_With_Slot_And_Ref()
    {
        SeedCharacter();
        _gameState.ApplyInventoryUpdated(
            [new Item { Slug = "wooden-shield", Name = "Wooden Shield", Type = ItemType.Shield }],
            new Dictionary<string, Item>());

        var cut = Render<InventoryOverlay>();
        SelectRow(cut, "wooden-shield");
        ClickButton(cut, "Equip");

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "EquipItem");
            Assert.Equal("OffHand", ReadString(command.Arg, "Slot"));
            Assert.Equal("wooden-shield", ReadString(command.Arg, "ItemRef"));
        });
    }

    /// <summary>Dropping an item sends the bare slug accepted by the server.</summary>
    [Fact]
    public void Inventory_Drop_Sends_DropItem_With_Ref()
    {
        SeedCharacter();
        _gameState.ApplyInventoryUpdated(
            [new Item { Slug = "rusty-dagger", Name = "Rusty Dagger", Type = ItemType.Weapon }],
            new Dictionary<string, Item>());

        var cut = Render<InventoryOverlay>();
        SelectRow(cut, "rusty-dagger");
        ClickButton(cut, "Drop");

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "DropItem");
            Assert.Equal("rusty-dagger", command.Arg);
        });
    }

    /// <summary>Equipment is shown in the equipment panel, not duplicated in the bag list.</summary>
    [Fact]
    public void Inventory_Does_Not_Duplicate_Equipped_Item()
    {
        SeedCharacter();
        _gameState.ApplyInventoryUpdated(
            [new Item { Slug = "health-potion", Name = "Health Potion", Type = ItemType.Consumable, Quantity = 2 }],
            new Dictionary<string, Item>
            {
                ["MainHand"] = new Item { Slug = "iron-sword", Name = "Iron Sword", Type = ItemType.Weapon },
            });

        var cut = Render<InventoryOverlay>();

        Assert.Contains("Iron Sword", cut.Markup);
        Assert.DoesNotContain("data-testid=\"item-iron-sword\"", cut.Markup);
        Assert.Contains("data-testid=\"item-health-potion\"", cut.Markup);
    }

    /// <summary>The removed UseItem hub method must not leave a dead Use button.</summary>
    [Fact]
    public void Inventory_Does_Not_Render_Use_Button()
    {
        SeedCharacter();
        _gameState.ApplyInventoryUpdated(
            [new Item { Slug = "health-potion", Name = "Health Potion", Type = ItemType.Consumable }],
            new Dictionary<string, Item>());

        var cut = Render<InventoryOverlay>();
        SelectRow(cut, "health-potion");

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Use");
    }

    /// <summary>Buying sends the bare slug accepted by BuyItem(string itemRef).</summary>
    [Fact]
    public void Shop_Buy_Sends_BuyItem_With_Ref()
    {
        SeedCharacter(gold: 100);
        _gameState.ApplyShopCatalogUpdated(
            [new ShopItemEntry("iron-sword", "Iron Sword", ItemRarity.Common, 50)]);

        var cut = Render<ShopOverlay>();
        SelectRow(cut, "iron-sword");
        ClickButton(cut, "Buy");

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "BuyItem");
            Assert.Equal("iron-sword", command.Arg);
        });
    }

    /// <summary>Selling sends the bare slug accepted by SellItem(string itemRef).</summary>
    [Fact]
    public void Shop_Sell_Sends_SellItem_With_Ref()
    {
        SeedCharacter(gold: 100);
        _gameState.ApplyInventoryUpdated(
            [new Item { Slug = "iron-sword", Name = "Iron Sword", Type = ItemType.Weapon, Price = 50 }],
            new Dictionary<string, Item>());

        var cut = Render<ShopOverlay>();
        SelectRow(cut, "iron-sword");
        ClickButton(cut, "Sell");

        cut.WaitForAssertion(() =>
        {
            var command = Assert.Single(_fakeHub.SentCommands, c => c.Method == "SellItem");
            Assert.Equal("iron-sword", command.Arg);
        });
    }

    private static string? ReadString(object? payload, string propertyName) =>
        payload?.GetType().GetProperty(propertyName)?.GetValue(payload)?.ToString();
}
