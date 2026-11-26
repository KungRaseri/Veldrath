using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;

namespace RealmEngine.Data.Tests.Seeders;

/// <summary>
/// Tests for <see cref="ZoneStockSeeder"/>: each merchant town gets a shelf that is a real subset of the purchasable
/// catalog, sized inside the approved band.
/// </summary>
[Trait("Category", "Seeder")]
public class ZoneStockSeederTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly string[] _merchantTowns = ["crestfall", "aldenmere", "tolvaren", "skarhold"];

    [Fact]
    public void Authors_Only_The_Reachable_Merchant_Towns()
    {
        var zones = ZoneStockSeeder.Authors().Select(row => row.ZoneId).Distinct().ToArray();

        zones.Should().BeEquivalentTo(_merchantTowns);
        zones.Should().NotContain("playground", "the dev sandbox is IsDevOnly and gets no authored roster");
    }

    [Fact]
    public void Every_Town_Stocks_Between_The_Bands_Minimum_And_Maximum()
    {
        var byZone = ZoneStockSeeder.Authors().GroupBy(row => row.ZoneId);

        foreach (var zone in byZone)
        {
            zone.Should().HaveCountGreaterThanOrEqualTo(ZoneStockSeeder.MinimumStockSize);
            zone.Should().HaveCountLessThanOrEqualTo(ZoneStockSeeder.MaximumStockSize);
        }
    }

    [Fact]
    public void No_Town_Stocks_The_Same_Item_Twice()
    {
        var authored = ZoneStockSeeder.Authors();

        authored.Should().OnlyHaveUniqueItems(row => new { row.ZoneId, row.ItemSlug });
    }

    [Fact]
    public async Task Every_Stocked_Item_Is_A_Purchasable_Catalog_Item()
    {
        await using var db = CreateDbContext();
        await ItemsSeeder.SeedAsync(db);
        await ZoneStockSeeder.SeedAsync(db);

        var fromCatalog = await db.Items
            .AsNoTracking()
            .Where(item => item.IsActive && item.Stats.Value > 0)
            .Select(item => item.Slug)
            .ToListAsync();

        var stocked = await db.ZoneStockItems
            .AsNoTracking()
            .Select(row => row.ItemSlug)
            .Distinct()
            .ToListAsync();

        stocked.Should().BeSubsetOf(fromCatalog,
            "a shelf may only hold items the shop could actually sell (an active item with a price)");
        stocked.Should().NotContain("lost-cargo", "the quest cargo is worth zero gold and is not for sale");
    }

    [Fact]
    public async Task Two_Towns_Do_Not_Offer_The_Same_Shelf()
    {
        await using var db = CreateDbContext();
        await ZoneStockSeeder.SeedAsync(db);

        var shelves = (await db.ZoneStockItems.AsNoTracking().ToListAsync())
            .GroupBy(row => row.ZoneId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.ItemSlug).Order(StringComparer.Ordinal).ToArray());

        shelves.Should().HaveCount(_merchantTowns.Length);

        // A town is its stock list: identical shelves would mean the per-zone table bought nothing.
        shelves.Values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await using var db = CreateDbContext();

        await ZoneStockSeeder.SeedAsync(db);
        var afterFirst = await db.ZoneStockItems.CountAsync();

        await ZoneStockSeeder.SeedAsync(db);
        var afterSecond = await db.ZoneStockItems.CountAsync();

        afterSecond.Should().Be(afterFirst);
        afterFirst.Should().Be(ZoneStockSeeder.Authors().Length);
    }
}
