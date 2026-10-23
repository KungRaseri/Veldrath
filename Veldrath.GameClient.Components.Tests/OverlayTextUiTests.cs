using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using RealmEngine.Shared.Models;
using Veldrath.GameClient.Components.Components.Shared;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Verifies the text-first overlay layouts (character sheet, journal, inventory, shop)
/// render as panels, tables, and detail panes without emoji glyphs.
/// </summary>
public class OverlayTextUiTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers required services.
    /// </summary>
    public OverlayTextUiTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton(_gameState);
        Services.AddSingleton<IGameStateService>(_gameState);
        Services.AddSingleton<IDialogService>(new FakeDialogService());

        // MudBlazor's production key-interceptor and resize-observer services are
        // async-dispose-only, while bUnit disposes the test container synchronously.
        Services.AddSingleton<IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton<IResizeObserverFactory, NoopResizeObserverFactory>();
        Services.AddMudServices();
    }

    private void SelectCharacter(int gold = 50) =>
        _gameState.ApplyCharacterSelected(new CharacterSelectedPayload(
            Guid.NewGuid(), "Aria", "Ranger", 5, 4200, "vale", "region-1",
            18, 20, 7, 10, gold, 2,
            12, 14, 11, 9, 13, 8, [], DateTimeOffset.UtcNow));

    private static Item MakeItem(
        string slug,
        string name,
        ItemType type,
        int price,
        int quantity = 1) => new()
    {
        Slug = slug,
        Name = name,
        Type = type,
        Rarity = ItemRarity.Common,
        Price = price,
        Quantity = quantity,
        Weight = 1.5,
        Description = $"{name} description.",
    };

    /// <summary>
    /// Verifies the character sheet renders vitals, attributes, and progression as text tables.
    /// </summary>
    [Fact]
    public void CharacterSheetOverlay_Renders_Text_Stat_Tables()
    {
        SelectCharacter();

        var cut = Render<CharacterSheetOverlay>();

        cut.Markup.Should().Contain("Aria");
        cut.Markup.Should().Contain("Ranger");
        cut.Markup.Should().Contain("Health");
        cut.Markup.Should().Contain("18 / 20");
        cut.Markup.Should().Contain("Strength");
        cut.Markup.Should().Contain("Progression");
        cut.Find("[data-testid='character-sheet']").Should().NotBeNull();
    }

    /// <summary>
    /// Verifies the character sheet shows an empty state when no character is selected.
    /// </summary>
    [Fact]
    public void CharacterSheetOverlay_Shows_Empty_State()
    {
        var cut = Render<CharacterSheetOverlay>();

        cut.Markup.Should().Contain("No character selected.");
    }

    /// <summary>
    /// Verifies the journal lists active and completed quests and shows objective/reward detail.
    /// </summary>
    [Fact]
    public void JournalOverlay_Renders_Quest_Lists_And_Detail()
    {
        var active = new QuestLogEntry(
            "q1", "The Lost Blade", "Recover the blade from the bandits.",
            1, 3, 250, 120,
            new Dictionary<string, int> { ["Bandits slain"] = 3 },
            new Dictionary<string, int> { ["Bandits slain"] = 1 });
        var completed = new QuestLogEntry(
            "q0", "Prologue", "Arrive in Varenmark.",
            1, 1, 10, 5,
            new Dictionary<string, int>(),
            new Dictionary<string, int>());

        _gameState.ApplyQuestLogUpdated([active], [completed]);

        var cut = Render<JournalOverlay>();

        cut.Markup.Should().Contain("Active Quests (1)");
        cut.Markup.Should().Contain("The Lost Blade");
        cut.Markup.Should().Contain("Completed Quests (1)");

        cut.Find("[data-testid='entity-The Lost Blade']").Click();

        cut.Markup.Should().Contain("Objectives");
        cut.Markup.Should().Contain("Bandits slain");
        cut.Markup.Should().Contain("1 / 3");
        cut.Markup.Should().Contain("250 XP");
        cut.Markup.Should().Contain("120 gold");
    }

    /// <summary>
    /// Verifies the inventory renders an equipment table, an item table, and a detail pane on select.
    /// </summary>
    [Fact]
    public void InventoryOverlay_Renders_Equipment_And_Detail_Pane()
    {
        var sword = MakeItem("iron-sword", "Iron Sword", ItemType.Weapon, 50);
        var potion = MakeItem("healing-potion", "Healing Potion", ItemType.Consumable, 10, quantity: 3);

        _gameState.ApplyInventoryUpdated(
            [sword, potion],
            new Dictionary<string, Item> { ["MainHand"] = sword });

        var cut = Render<InventoryOverlay>();

        cut.Markup.Should().Contain("Equipment");
        cut.Markup.Should().Contain("MainHand");
        cut.Markup.Should().Contain("Inventory (2 items)");
        cut.Markup.Should().Contain("Iron Sword");

        cut.Find("[data-testid='item-healing-potion'] button").Click();

        cut.Find("[data-testid='inventory-detail']").Should().NotBeNull();
        cut.Markup.Should().Contain("Item Stats");
        cut.Markup.Should().Contain("Drop");
    }

    /// <summary>
    /// Verifies the shop renders merchant wares and player items as tables with buy/sell detail actions.
    /// </summary>
    [Fact]
    public void ShopOverlay_Renders_Catalog_And_Sell_Panes()
    {
        SelectCharacter(gold: 100);
        _gameState.ApplyShopCatalogUpdated(
        [
            new ShopItemEntry("greater-potion", "Greater Potion", ItemRarity.Rare, 40),
        ]);
        _gameState.ApplyInventoryUpdated(
            [MakeItem("rusty-dagger", "Rusty Dagger", ItemType.Weapon, 20)],
            new Dictionary<string, Item>());

        var cut = Render<ShopOverlay>();

        cut.Markup.Should().Contain("Trade");
        cut.Markup.Should().Contain("Merchant Wares");
        cut.Markup.Should().Contain("Greater Potion");
        cut.Markup.Should().Contain("Your Items (1)");
        cut.Markup.Should().Contain("Rusty Dagger");

        cut.Find("[data-testid='item-greater-potion'] button").Click();

        cut.Find("[data-testid='shop-detail']").Should().NotBeNull();
        cut.Markup.Should().Contain("Purchase");
        cut.Markup.Should().Contain("Affordable");

        cut.Find("[data-testid='item-rusty-dagger'] button").Click();

        cut.Find("[data-testid='shop-sell-detail']").Should().NotBeNull();
        cut.Markup.Should().Contain("Sale");
        cut.Markup.Should().Contain("Sell");
    }

    /// <summary>
    /// Verifies buying from the shop sends the BuyItem hub command.
    /// </summary>
    [Fact]
    public void ShopOverlay_Buy_Sends_Hub_Command()
    {
        SelectCharacter(gold: 100);
        _gameState.ApplyShopCatalogUpdated(
        [
            new ShopItemEntry("greater-potion", "Greater Potion", ItemRarity.Rare, 40),
        ]);

        var cut = Render<ShopOverlay>();

        cut.Find("[data-testid='item-greater-potion'] button").Click();
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Buy").Click();

        _fakeHub.SentCommands.Should().Contain(command => command.Method == "BuyItem");
    }

    /// <summary>
    /// Verifies the converted overlays contain no emoji/surrogate-pair glyphs.
    /// </summary>
    [Fact]
    public void Overlays_Use_Material_Icons_Without_Emoji()
    {
        SelectCharacter();
        _gameState.ApplyQuestLogUpdated(
            [new QuestLogEntry("q1", "The Lost Blade", "Recover it.", 1, 3, 250, 120,
                new Dictionary<string, int> { ["Bandits slain"] = 3 },
                new Dictionary<string, int> { ["Bandits slain"] = 1 })],
            []);
        _gameState.ApplyInventoryUpdated(
            [MakeItem("iron-sword", "Iron Sword", ItemType.Weapon, 50)],
            new Dictionary<string, Item>());
        _gameState.ApplyShopCatalogUpdated(
            [new ShopItemEntry("greater-potion", "Greater Potion", ItemRarity.Rare, 40)]);

        var journal = Render<JournalOverlay>();
        var inventory = Render<InventoryOverlay>();
        var shop = Render<ShopOverlay>();
        var sheet = Render<CharacterSheetOverlay>();

        foreach (var markup in new[] { journal.Markup, inventory.Markup, shop.Markup, sheet.Markup })
        {
            markup.Should().NotMatchRegex(@"[\uD800-\uDFFF]");
        }
    }
}
