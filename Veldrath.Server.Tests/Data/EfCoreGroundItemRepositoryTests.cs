using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Data;

/// <summary>
/// Tests for <see cref="EfCoreGroundItemRepository"/> — the durable store behind dropped items. Uses in-memory
/// SQLite so the ordering and the delete actually round-trip through the database.
/// </summary>
public class EfCoreGroundItemRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();

    public void Dispose() => _dbFactory.Dispose();

    private static GroundItem Item(
        string zoneId,
        string locationSlug,
        string itemRef = "iron-sword",
        int quantity = 1,
        DateTimeOffset? droppedAt = null) => new()
        {
            ZoneId = zoneId,
            LocationSlug = locationSlug,
            ItemRef = itemRef,
            Quantity = quantity,
            DroppedAt = droppedAt ?? DateTimeOffset.UtcNow,
        };

    [Fact]
    public async Task AddAsync_PersistsTheItemAndReturnsItById()
    {
        await using var db = _dbFactory.CreateContext();
        var repo = new EfCoreGroundItemRepository(db);
        var item = Item("crestfall", "square", "iron-sword", 3);

        await repo.AddAsync(item);

        var loaded = await repo.GetByIdAsync(item.Id);
        loaded.Should().NotBeNull();
        loaded!.ItemRef.Should().Be("iron-sword");
        loaded.Quantity.Should().Be(3);
        loaded.ZoneId.Should().Be("crestfall");
        loaded.LocationSlug.Should().Be("square");
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        await using var db = _dbFactory.CreateContext();
        var repo = new EfCoreGroundItemRepository(db);

        var loaded = await repo.GetByIdAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task GetAtLocationAsync_ReturnsOnlyThatLocationOrderedOldestFirst()
    {
        await using var db = _dbFactory.CreateContext();
        var repo = new EfCoreGroundItemRepository(db);
        var old = Item("crestfall", "square", "torch", droppedAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        var recent = Item("crestfall", "square", "rope", droppedAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(old);
        await repo.AddAsync(recent);
        await repo.AddAsync(Item("crestfall", "market", "apple"));
        await repo.AddAsync(Item("aldenmere", "square", "apple"));

        var atSquare = await repo.GetAtLocationAsync("crestfall", "square");

        atSquare.Should().HaveCount(2);
        atSquare.Select(i => i.Id).Should().ContainInOrder(old.Id, recent.Id);
        atSquare.Should().NotContain(i => i.LocationSlug == "market");
        atSquare.Should().OnlyContain(i => i.ZoneId == "crestfall");
    }

    [Fact]
    public async Task GetAtLocationAsync_EmptyLocation_ReturnsEmptyList()
    {
        await using var db = _dbFactory.CreateContext();
        var repo = new EfCoreGroundItemRepository(db);

        var atSquare = await repo.GetAtLocationAsync("crestfall", "nowhere");

        atSquare.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheRow()
    {
        await using var db = _dbFactory.CreateContext();
        var repo = new EfCoreGroundItemRepository(db);
        var item = Item("crestfall", "square");
        await repo.AddAsync(item);

        await repo.RemoveAsync(item);

        (await repo.GetByIdAsync(item.Id)).Should().BeNull();
        (await repo.GetAtLocationAsync("crestfall", "square")).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_KeepsSiblingItemsAtTheSameLocation()
    {
        await using var db = _dbFactory.CreateContext();
        var repo = new EfCoreGroundItemRepository(db);
        var doomed = Item("crestfall", "square", "rusty-dagger");
        var survivor = Item("crestfall", "square", "iron-sword");
        await repo.AddAsync(doomed);
        await repo.AddAsync(survivor);

        await repo.RemoveAsync(doomed);

        var remaining = await repo.GetAtLocationAsync("crestfall", "square");
        remaining.Should().ContainSingle().Which.Id.Should().Be(survivor.Id);
    }
}
