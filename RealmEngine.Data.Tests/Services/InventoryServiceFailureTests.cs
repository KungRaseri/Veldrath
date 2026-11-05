using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Tests.Services;

/// <summary>
/// Failure-path tests for the inventory services: a storage fault must be reported as <see langword="false"/>
/// (never as an exception) and must stop a batch add at the first item it could not persist.
/// </summary>
[Trait("Category", "Service")]
public class InventoryServiceFailureTests
{
    private const string Hero = "Aria";

    private sealed class SaveGameScope : ISaveGameContext
    {
        public string SaveGameId { get; private set; } = "save-1";

        public void Activate(string saveGameId) => SaveGameId = saveGameId;
    }

    private static ItemDrop Drop(string? itemRef, int quantity = 1) =>
        new() { ItemRef = itemRef!, ItemName = "item", Quantity = quantity };

    // ── EfCoreInventoryService ────────────────────────────────────────────────

    private static (EfCoreInventoryService Service, GameDbContext Db) BrokenEfCoreService()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new GameDbContext(options);
        var service = new EfCoreInventoryService(db, new SaveGameScope(), NullLogger<EfCoreInventoryService>.Instance);

        // Disposing the context makes every query throw, which is how a lost connection surfaces here.
        db.Dispose();
        return (service, db);
    }

    [Fact]
    public async Task EfCore_AddItemsStopsAtTheFirstFailure()
    {
        var (service, _) = BrokenEfCoreService();

        var added = await service.AddItemsAsync(Hero, [Drop("ore"), Drop("herb")]);

        added.Should().BeFalse();
    }

    [Fact]
    public async Task EfCore_AddItemReportsAFailureInsteadOfThrowing()
    {
        var (service, _) = BrokenEfCoreService();

        (await service.AddItemAsync(Hero, "ore", 2)).Should().BeFalse();
    }

    [Fact]
    public async Task EfCore_RemoveItemReportsAFailureInsteadOfThrowing()
    {
        var (service, _) = BrokenEfCoreService();

        (await service.RemoveItemAsync(Hero, "ore", 1)).Should().BeFalse();
    }

    [Fact]
    public async Task EfCore_ReduceDurabilityReportsAFailureInsteadOfThrowing()
    {
        var (service, _) = BrokenEfCoreService();

        (await service.ReduceItemDurabilityAsync(Hero, "ore", 5)).Should().BeFalse();
    }

    // ── InMemoryInventoryService ──────────────────────────────────────────────

    [Fact]
    public async Task InMemory_AddItemsReportsAFailureForAnUnusableItemReference()
    {
        var service = new InMemoryInventoryService(NullLogger<InMemoryInventoryService>.Instance);

        var added = await service.AddItemsAsync(Hero, [Drop(null)]);

        added.Should().BeFalse();
        service.GetInventory(Hero).Should().BeEmpty();
    }

    [Fact]
    public async Task InMemory_AddItemReportsAFailureForAnUnusableItemReference()
    {
        var service = new InMemoryInventoryService(NullLogger<InMemoryInventoryService>.Instance);

        (await service.AddItemAsync(Hero, null!, 1)).Should().BeFalse();
    }

    [Fact]
    public async Task InMemory_RemoveItemReportsAFailureForAnUnusableItemReference()
    {
        var service = new InMemoryInventoryService(NullLogger<InMemoryInventoryService>.Instance);
        await service.AddItemAsync(Hero, "ore", 1);

        (await service.RemoveItemAsync(Hero, null!, 1)).Should().BeFalse();
        service.GetInventory(Hero)["ore"].Should().Be(1);
    }
}
