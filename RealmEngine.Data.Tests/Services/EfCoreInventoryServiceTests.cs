using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Tests.Services;

/// <summary>
/// Scoping stub for <see cref="ISaveGameContext"/> so inventory rows can be partitioned per test.
/// </summary>
internal sealed class FakeSaveGameContext : ISaveGameContext
{
    public string SaveGameId { get; private set; } = string.Empty;

    public void Activate(string saveGameId) => SaveGameId = saveGameId;
}

/// <summary>
/// Behaviour tests for <see cref="EfCoreInventoryService"/>: stack merging, capacity checks,
/// removal floors, durability decay and save-game scoping.
/// </summary>
[Trait("Category", "Service")]
public class EfCoreInventoryServiceTests : IDisposable
{
    private const string Hero = "Aria";

    private readonly GameDbContext _db;
    private readonly FakeSaveGameContext _context;
    private readonly EfCoreInventoryService _service;

    public EfCoreInventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new GameDbContext(options);
        _context = new FakeSaveGameContext();
        _context.Activate("save-1");
        _service = new EfCoreInventoryService(_db, _context, NullLogger<EfCoreInventoryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static ItemDrop Drop(string itemRef, int quantity = 1) =>
        new() { ItemRef = itemRef, ItemName = itemRef, Quantity = quantity };

    // ── AddItem ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_NewItemRef_CreatesARecordScopedToTheActiveSave()
    {
        // Act
        var added = await _service.AddItemAsync(Hero, "@items/ore:iron", 3);

        // Assert
        added.Should().BeTrue();
        var record = await _db.InventoryRecords.SingleAsync();
        record.CharacterName.Should().Be(Hero);
        record.SaveGameId.Should().Be("save-1");
        record.ItemRef.Should().Be("@items/ore:iron");
        record.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task AddItem_ExistingItemRef_MergesIntoTheExistingStack()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "@items/ore:iron", 3);

        // Act
        var added = await _service.AddItemAsync(Hero, "@items/ore:iron", 4);

        // Assert
        added.Should().BeTrue();
        _db.InventoryRecords.Should().ContainSingle();
        (await _db.InventoryRecords.SingleAsync()).Quantity.Should().Be(7);
    }

    [Fact]
    public async Task AddItem_SameItemDifferentSaveGame_CreatesASeparateRecord()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "@items/ore:iron", 3);

        // Act
        _context.Activate("save-2");
        await _service.AddItemAsync(Hero, "@items/ore:iron", 5);

        // Assert
        _db.InventoryRecords.Should().HaveCount(2);
        var bySave = await _db.InventoryRecords.ToDictionaryAsync(r => r.SaveGameId, r => r.Quantity);
        bySave["save-1"].Should().Be(3);
        bySave["save-2"].Should().Be(5);
    }

    [Fact]
    public async Task AddItem_SameItemDifferentCharacter_CreatesASeparateRecord()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "@items/ore:iron", 3);

        // Act
        await _service.AddItemAsync("Bran", "@items/ore:iron", 2);

        // Assert
        _db.InventoryRecords.Should().HaveCount(2);
    }

    // ── AddItems ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItems_WithMultipleDrops_AddsEveryEntry()
    {
        // Act
        var added = await _service.AddItemsAsync(Hero, [Drop("@items/ore:iron", 2), Drop("@items/ore:gold", 1)]);

        // Assert
        added.Should().BeTrue();
        _db.InventoryRecords.Should().HaveCount(2);
        (await _service.GetItemCountAsync(Hero, "@items/ore:iron")).Should().Be(2);
        (await _service.GetItemCountAsync(Hero, "@items/ore:gold")).Should().Be(1);
    }

    [Fact]
    public async Task AddItems_WithAnEmptyList_ReturnsTrueWithoutWriting()
    {
        // Act
        var added = await _service.AddItemsAsync(Hero, []);

        // Assert
        added.Should().BeTrue();
        _db.InventoryRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task AddItems_WithANullList_Throws()
    {
        // Act
        var act = () => _service.AddItemsAsync(Hero, null!);

        // Assert
        await act.Should().ThrowAsync<NullReferenceException>();
    }

    // ── Capacity ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasInventorySpaceAsync_IsTrueBelowOneHundredSlots()
    {
        // Act & Assert
        (await _service.HasInventorySpaceAsync(Hero, 99)).Should().BeTrue();
        (await _service.HasInventorySpaceAsync(Hero, 100)).Should().BeTrue();
        (await _service.HasInventorySpaceAsync(Hero, 101)).Should().BeFalse();
    }

    [Fact]
    public async Task HasInventorySpaceAsync_AccountsForOccupiedSlots()
    {
        // Arrange — three distinct item refs
        await _service.AddItemAsync(Hero, "a", 1);
        await _service.AddItemAsync(Hero, "b", 1);
        await _service.AddItemAsync(Hero, "c", 1);

        // Act & Assert
        (await _service.HasInventorySpaceAsync(Hero, 97)).Should().BeTrue();
        (await _service.HasInventorySpaceAsync(Hero, 98)).Should().BeFalse();
    }

    [Fact]
    public async Task HasInventorySpaceAsync_CountsOnlyTheCurrentSaveGame()
    {
        // Arrange — 100 slots used in another save
        for (var i = 0; i < 100; i++)
        {
            _db.InventoryRecords.Add(new InventoryRecord
            {
                SaveGameId = "other-save",
                CharacterName = Hero,
                ItemRef = $"item-{i}",
                Quantity = 1,
            });
        }
        await _db.SaveChangesAsync();

        // Act & Assert
        (await _service.HasInventorySpaceAsync(Hero, 100)).Should().BeTrue();
    }

    // ── Read ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemCountAsync_ReturnsZeroForAnUnknownItem()
    {
        // Act & Assert
        (await _service.GetItemCountAsync(Hero, "missing")).Should().Be(0);
    }

    [Fact]
    public async Task GetItemCountAsync_ReturnsTheStackSize()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "ore", 11);

        // Act & Assert
        (await _service.GetItemCountAsync(Hero, "ore")).Should().Be(11);
    }

    // ── Remove ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItem_MissingItem_ReturnsFalse()
    {
        // Act & Assert
        (await _service.RemoveItemAsync(Hero, "missing", 1)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItem_MoreThanHeld_ReturnsFalseAndLeavesTheStackIntact()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "ore", 2);

        // Act
        var removed = await _service.RemoveItemAsync(Hero, "ore", 3);

        // Assert
        removed.Should().BeFalse();
        (await _service.GetItemCountAsync(Hero, "ore")).Should().Be(2);
    }

    [Fact]
    public async Task RemoveItem_PartialAmount_ReducesTheStack()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "ore", 5);

        // Act
        var removed = await _service.RemoveItemAsync(Hero, "ore", 2);

        // Assert
        removed.Should().BeTrue();
        (await _service.GetItemCountAsync(Hero, "ore")).Should().Be(3);
    }

    [Fact]
    public async Task RemoveItem_ExactAmount_DeletesTheRecord()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "ore", 5);

        // Act
        var removed = await _service.RemoveItemAsync(Hero, "ore", 5);

        // Assert
        removed.Should().BeTrue();
        _db.InventoryRecords.Should().BeEmpty();
    }

    // ── Durability ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReduceItemDurability_MissingItem_ReturnsFalse()
    {
        // Act & Assert
        (await _service.ReduceItemDurabilityAsync(Hero, "missing", 5)).Should().BeFalse();
    }

    [Fact]
    public async Task ReduceItemDurability_WithoutAPriorValue_TreatsDurabilityAsOneHundred()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "sword", 1);

        // Act
        var reduced = await _service.ReduceItemDurabilityAsync(Hero, "sword", 30);

        // Assert
        reduced.Should().BeTrue();
        (await _db.InventoryRecords.SingleAsync()).Durability.Should().Be(70);
    }

    [Fact]
    public async Task ReduceItemDurability_AccumulatesAcrossCalls()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "sword", 1);
        await _service.ReduceItemDurabilityAsync(Hero, "sword", 30);

        // Act
        await _service.ReduceItemDurabilityAsync(Hero, "sword", 20);

        // Assert
        (await _db.InventoryRecords.SingleAsync()).Durability.Should().Be(50);
    }

    [Fact]
    public async Task ReduceItemDurability_FloorsAtZero()
    {
        // Arrange
        await _service.AddItemAsync(Hero, "sword", 1);

        // Act
        await _service.ReduceItemDurabilityAsync(Hero, "sword", 500);

        // Assert
        (await _db.InventoryRecords.SingleAsync()).Durability.Should().Be(0);
    }
}
