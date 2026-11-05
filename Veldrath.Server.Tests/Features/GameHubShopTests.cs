using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Features.Shop;
using Veldrath.Server.Hubs;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for the shop, inventory and item hub methods of <see cref="GameHub"/>.
/// Each method is driven through its guard, its failure result, its exception path and its
/// success payload so both the audience and the shape of every broadcast are asserted.
/// </summary>
public class GameHubShopTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static GetInventoryHubResult EmptySnapshot() => new() { Success = true };

    private static void ShouldBeError(FakeClientProxy proxy, string message) =>
        proxy.SentMessages.Last(m => m.Method == "Error").Args[0].Should().Be(message);

    // ── VisitShop ────────────────────────────────────────────────────────────

    [Fact]
    public async Task VisitShop_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.VisitShop(new VisitShopHubRequest("crestfall"));

        ShouldBeError(clients.Caller, "SelectCharacter must be called before VisitShop");
    }

    [Fact]
    public async Task VisitShop_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new VisitShopHubResult { Success = false, ErrorMessage = "Crestfall has no merchant" });

        await hub.VisitShop(new VisitShopHubRequest("crestfall"));

        ShouldBeError(clients.Caller, "Crestfall has no merchant");
    }

    [Fact]
    public async Task VisitShop_Uses_A_Default_Message_When_The_Handler_Gives_None()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new VisitShopHubResult { Success = false });

        await hub.VisitShop(new VisitShopHubRequest("crestfall"));

        ShouldBeError(clients.Caller, "Failed to visit shop");
    }

    [Fact]
    public async Task VisitShop_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<VisitShopHubResult>(new InvalidOperationException("db down"));

        await hub.VisitShop(new VisitShopHubRequest("crestfall"));

        ShouldBeError(clients.Caller, "Failed to visit shop");
    }

    [Fact]
    public async Task VisitShop_Sends_The_Shop_Identity_To_The_Caller()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new VisitShopHubResult { Success = true, ZoneId = "crestfall", ZoneName = "Crestfall" });

        await hub.VisitShop(new VisitShopHubRequest("crestfall"));

        clients.Caller.SentMessages.Should().ContainSingle(m => m.Method == "ShopVisited");
        clients.Caller.SentMessages.Single().Args[0].Should().BeEquivalentTo(new
        {
            ZoneId = "crestfall",
            ZoneName = "Crestfall",
        });
    }

    // ── GetShopCatalog ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetShopCatalog_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetShopCatalog();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetShopCatalog");
    }

    [Fact]
    public async Task GetShopCatalog_Without_A_Zone_Reports_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new GetShopCatalogHubResult { Success = false, ErrorMessage = "ZoneId is required." });

        await hub.GetShopCatalog();

        ShouldBeError(clients.Caller, "ZoneId is required.");
    }

    [Fact]
    public async Task GetShopCatalog_Projects_Each_Item_Into_The_Client_Catalog()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran", "crestfall");
        mediator.Returns(new GetShopCatalogHubResult
        {
            Success = true,
            Items = [new ShopItemDto("health_potion", "Health Potion", 25, 12)],
        });

        await hub.GetShopCatalog();

        var command = mediator.Invocations
            .Select(i => i.Arguments[0]).OfType<GetShopCatalogHubCommand>().Single();
        command.ZoneId.Should().Be("crestfall", "the hub forwards the zone the character is standing in");

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "ShopCatalog").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            ZoneId = "crestfall",
            Catalog = new[]
            {
                new { Slug = "health_potion", Name = "Health Potion", Rarity = RealmEngine.Shared.Models.ItemRarity.Common, Price = 25 },
            },
        });
    }

    [Fact]
    public async Task GetShopCatalog_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<GetShopCatalogHubResult>(new InvalidOperationException("boom"));

        await hub.GetShopCatalog();

        ShouldBeError(clients.Caller, "Failed to load shop catalog");
    }

    // ── BuyItem ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuyItem_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.BuyItem("health_potion");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before BuyItem");
    }

    [Fact]
    public async Task BuyItem_Relays_The_Handler_Error_And_Sends_No_Snapshot()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new BuyItemHubResult { Success = false, ErrorMessage = "Not enough gold. Have 1, need 25." });

        await hub.BuyItem("health_potion");

        ShouldBeError(clients.Caller, "Not enough gold. Have 1, need 25.");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "InventoryLoaded");
    }

    [Fact]
    public async Task BuyItem_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<BuyItemHubResult>(new InvalidOperationException("boom"));

        await hub.BuyItem("health_potion");

        ShouldBeError(clients.Caller, "Failed to buy item");
    }

    [Fact]
    public async Task BuyItem_Confirms_The_Purchase_Refreshes_Inventory_And_Advances_Collect()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new BuyItemHubResult
        {
            Success = true,
            ItemRef = "health_potion",
            GoldSpent = 25,
            RemainingGold = 75,
        });
        mediator.Returns(EmptySnapshot());
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 1 });

        await hub.BuyItem("health_potion");

        var purchased = clients.Caller.SentMessages.Single(m => m.Method == "ItemPurchased").Args[0];
        purchased.Should().BeEquivalentTo(new { CharacterId = characterId, ItemRef = "health_potion", GoldSpent = 25, RemainingGold = 75 });
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "InventoryLoaded");

        var advance = mediator.Invocations.Select(i => i.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>().Single();
        advance.Kind.Should().Be("collect");
        advance.Key.Should().Be("health_potion");
    }

    // ── SellItem ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SellItem_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.SellItem("health_potion");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before SellItem");
    }

    [Fact]
    public async Task SellItem_Confirms_The_Sale_And_Refreshes_Inventory()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId, "Bran", "crestfall");
        mediator.Returns(new SellItemHubResult
        {
            Success = true,
            ItemRef = "health_potion",
            GoldReceived = 12,
            NewGoldTotal = 112,
        });
        mediator.Returns(EmptySnapshot());

        await hub.SellItem("health_potion");

        var command = mediator.Invocations.Select(i => i.Arguments[0]).OfType<SellItemHubCommand>().Single();
        command.ZoneId.Should().Be("crestfall");
        clients.Caller.SentMessages.Single(m => m.Method == "ItemSold").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, ItemRef = "health_potion", GoldReceived = 12, NewGoldTotal = 112 });
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "InventoryLoaded");
    }

    [Fact]
    public async Task SellItem_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new SellItemHubResult { Success = false, ErrorMessage = "Item 'x' not found in inventory." });

        await hub.SellItem("x");

        ShouldBeError(clients.Caller, "Item 'x' not found in inventory.");
    }

    [Fact]
    public async Task SellItem_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<SellItemHubResult>(new InvalidOperationException("boom"));

        await hub.SellItem("x");

        ShouldBeError(clients.Caller, "Failed to sell item");
    }

    // ── DropItem ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DropItem_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.DropItem("health_potion");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before DropItem");
    }

    [Fact]
    public async Task DropItem_Confirms_The_Drop_With_The_Remaining_Quantity()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new DropItemHubResult { Success = true, ItemRef = "health_potion", RemainingQuantity = 2 });
        mediator.Returns(EmptySnapshot());

        await hub.DropItem("health_potion");

        clients.Caller.SentMessages.Single(m => m.Method == "ItemDropped").Args[0]
            .Should().BeEquivalentTo(new { CharacterId = characterId, ItemRef = "health_potion", RemainingQuantity = 2 });
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "InventoryLoaded");
    }

    [Fact]
    public async Task DropItem_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new DropItemHubResult { Success = false, ErrorMessage = "ItemRef is required." });

        await hub.DropItem("");

        ShouldBeError(clients.Caller, "ItemRef is required.");
    }

    [Fact]
    public async Task DropItem_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<DropItemHubResult>(new InvalidOperationException("boom"));

        await hub.DropItem("health_potion");

        ShouldBeError(clients.Caller, "Failed to drop item");
    }

    // ── UseItem ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UseItem_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.UseItem("health_potion");

        ShouldBeError(clients.Caller, "SelectCharacter must be called before UseItem");
    }

    [Fact]
    public async Task UseItem_Reports_The_Resulting_Health_And_Mana()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new UseItemHubResult
        {
            Success = true,
            ItemRef = "health_potion",
            RemainingQuantity = 1,
            CurrentHealth = 30,
            MaxHealth = 30,
            CurrentMana = 10,
            MaxMana = 10,
            Message = "Restored 20 health.",
        });
        mediator.Returns(EmptySnapshot());

        await hub.UseItem("health_potion");

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "ItemUsed").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            ItemRef = "health_potion",
            RemainingQuantity = 1,
            CurrentHealth = 30,
            MaxHealth = 30,
            CurrentMana = 10,
            MaxMana = 10,
            Message = "Restored 20 health.",
        });
        clients.Caller.SentMessages.Should().Contain(m => m.Method == "InventoryLoaded");
    }

    [Fact]
    public async Task UseItem_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new UseItemHubResult { Success = false, ErrorMessage = "Item is not consumable." });

        await hub.UseItem("iron_sword");

        ShouldBeError(clients.Caller, "Item is not consumable.");
    }

    [Fact]
    public async Task UseItem_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Throws<UseItemHubResult>(new InvalidOperationException("boom"));

        await hub.UseItem("health_potion");

        ShouldBeError(clients.Caller, "Failed to use item");
    }

    // ── GetInventory ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInventory_Requires_A_Selected_Character()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, _, _, _) = HubHarness.Create(db, account.Id);

        await hub.GetInventory();

        ShouldBeError(clients.Caller, "SelectCharacter must be called before GetInventory");
    }

    [Fact]
    public async Task GetInventory_Sends_The_Snapshot_With_The_Overlay_Open()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.Items["CharacterId"] = characterId;
        mediator.Returns(EmptySnapshot());

        await hub.GetInventory();

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "InventoryLoaded").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            Items = Array.Empty<object>(),
            Equipped = new Dictionary<string, object>(),
            OpenOverlay = true,
        });
    }

    [Fact]
    public async Task GetInventory_Relays_The_Handler_Error()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Returns(new GetInventoryHubResult { Success = false, ErrorMessage = "Character not found." });

        await hub.GetInventory();

        ShouldBeError(clients.Caller, "Character not found.");
    }

    [Fact]
    public async Task GetInventory_Reports_An_Unexpected_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.Items["CharacterId"] = Guid.NewGuid();
        mediator.Throws<GetInventoryHubResult>(new InvalidOperationException("boom"));

        await hub.GetInventory();

        ShouldBeError(clients.Caller, "Failed to load inventory");
    }

    // ── EquipItem ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EquipItem_Still_Reports_Success_When_The_Snapshot_Refresh_Fails()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new EquipItemHubResult
        {
            Success = true,
            Slot = "MainHand",
            ItemRef = "iron_sword",
            AllEquippedItems = new Dictionary<string, string> { ["MainHand"] = "iron_sword" },
        });
        mediator.Throws<GetInventoryHubResult>(new InvalidOperationException("snapshot failed"));

        await hub.EquipItem(new EquipItemHubRequest("MainHand", "iron_sword"));

        clients.Caller.SentMessages.Should().BeEmpty("the caller is inside a zone, so the equip goes to the group");
        clients.GroupNamed("zone:crestfall").SentMessages.Should()
            .ContainSingle(m => m.Method == "ItemEquipped");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "InventoryLoaded");
    }

    [Fact]
    public async Task EquipItem_Refreshes_The_Callers_Inventory_Without_Forcing_The_Overlay()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        var characterId = Guid.NewGuid();
        ctx.EnterZoneState(characterId);
        mediator.Returns(new EquipItemHubResult
        {
            Success = true,
            Slot = "MainHand",
            ItemRef = "iron_sword",
            AllEquippedItems = new Dictionary<string, string> { ["MainHand"] = "iron_sword" },
        });
        mediator.Returns(EmptySnapshot());

        await hub.EquipItem(new EquipItemHubRequest("MainHand", "iron_sword"));

        var payload = clients.Caller.SentMessages.Single(m => m.Method == "InventoryLoaded").Args[0];
        payload.Should().BeEquivalentTo(new
        {
            CharacterId = characterId,
            OpenOverlay = false,
        });
    }

    [Fact]
    public async Task BuyItem_Keeps_The_Purchase_When_The_Snapshot_Refresh_Fails()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var (hub, clients, _, ctx, mediator, _) = HubHarness.Create(db, account.Id);
        ctx.EnterZoneState(Guid.NewGuid());
        mediator.Returns(new BuyItemHubResult { Success = true, ItemRef = "health_potion", GoldSpent = 25, RemainingGold = 75 });
        mediator.Throws<GetInventoryHubResult>(new InvalidOperationException("catalog unavailable"));
        mediator.Returns(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 0 });

        await hub.BuyItem("health_potion");

        clients.Caller.SentMessages.Should().Contain(m => m.Method == "ItemPurchased");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "InventoryLoaded");
        clients.Caller.SentMessages.Should().NotContain(m => m.Method == "Error");
    }
}
