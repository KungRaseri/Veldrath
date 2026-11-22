using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Shop;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="ShopMerchantResolver"/>: the shop is a person standing where the character is, and nothing
/// else is a shop.
/// </summary>
/// <remarks>
/// The application database is seeded with the real zones and the real placements, and the content database with the
/// real archetypes, because the question being answered is exactly "do the seeded rows line up". A hand-written
/// fixture would pass while the world was empty.
/// </remarks>
public class ShopMerchantResolverTests : IDisposable
{
    private readonly TestDbContextFactory _appFactory = new();
    private readonly Veldrath.Server.Data.ApplicationDbContext _app;
    private readonly SqliteConnection _contentConnection;
    private readonly ContentDbContext _content;
    private readonly ShopMerchantResolver _resolver;
    private readonly ZoneStockReader _stock;

    /// <summary>Builds both catalogs the resolver reads.</summary>
    public ShopMerchantResolverTests()
    {
        _contentConnection = new SqliteConnection("Data Source=:memory:");
        _contentConnection.Open();

        _content = new ContentDbContext(
            new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_contentConnection).Options);

        // ContentDbContext carries JSON-owned columns, so create its tables directly rather than through
        // EnsureCreated (which is a no-op once another context has created tables on the same connection).
        _content.Database.GetService<IRelationalDatabaseCreator>().CreateTables();
        ArchetypeSeeder.SeedAsync(_content).GetAwaiter().GetResult();
        ZoneLocationsSeeder.SeedAsync(_content).GetAwaiter().GetResult();
        ZoneStockSeeder.SeedAsync(_content).GetAwaiter().GetResult();

        _app = _appFactory.CreateContext();
        LocationNpcsSeeder.SeedAsync(_app).GetAwaiter().GetResult();

        _resolver = new ShopMerchantResolver(_app, _content, NullLogger<ShopMerchantResolver>.Instance);
        _stock = new ZoneStockReader(_content);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _content.Dispose();
        _app.Dispose();
        _contentConnection.Dispose();
        _appFactory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Character At(string zoneId, string? locationSlug) =>
        new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Name = "Shopper",
            ClassName = "Warrior",
            SlotIndex = 1,
            CurrentZoneId = zoneId,
            CurrentZoneLocationSlug = locationSlug,
        };

    [Fact]
    public async Task Resolves_The_Blacksmith_At_The_Market()
    {
        var resolution = await _resolver.ResolveAsync(
            At("crestfall", "aurelian-market"),
            "crestfall",
            CancellationToken.None);

        resolution.Success.Should().BeTrue();
        resolution.Refusal.Should().Be(ShopRefusal.None);

        var merchant = resolution.Merchant!;
        merchant.ArchetypeSlug.Should().Be("village-blacksmith");
        merchant.DisplayName.Should().Be("Village Blacksmith");
        merchant.ZoneId.Should().Be("crestfall");
        merchant.LocationSlug.Should().Be("aurelian-market");
        merchant.Gold.Should().Be(250, "the blacksmith's purse is reseeded to the top of the L0 band");
    }

    [Fact]
    public async Task Resolves_The_Tavern_Keeper_At_The_Inn()
    {
        var resolution = await _resolver.ResolveAsync(
            At("crestfall", "hearthbound-inn"),
            "crestfall",
            CancellationToken.None);

        resolution.Success.Should().BeTrue();
        resolution.Merchant!.ArchetypeSlug.Should().Be("tavern-keeper");
        resolution.Merchant.Gold.Should().Be(200);
    }

    [Theory]
    [InlineData("aldenmere", "ironhollow-keep", "keep-castellan", "Kaenvren")]
    [InlineData("aldenmere", "aldenmere-marketplace", "mead-seller", "Raekmeld")]
    [InlineData("tolvaren", "cliff-road-market", "ship-chandler", "Laeldern")]
    [InlineData("skarhold", "forge-quarter", "forge-master", "Daeldvaen")]
    [InlineData("skarhold", "caldera-market", "ash-glass-seller", "Raekdaeld")]
    public async Task Resolves_The_Rostered_Shopkeeper_Of_Each_Merchant_Town(
        string zoneId,
        string locationSlug,
        string archetypeSlug,
        string displayName)
    {
        // D3's roster (docs/design/content/towns.md §4.2–4.4) is the placement half of the shop: the resolver reads
        // the archetype's Shopkeeper flag off the person standing at the character's own zone+location, so a town
        // whose placement regresses stops trading. Each case is one merchant town's market, against the real seeders.
        var resolution = await _resolver.ResolveAsync(
            At(zoneId, locationSlug),
            zoneId,
            CancellationToken.None);

        resolution.Success.Should().BeTrue(
            $"{archetypeSlug} is placed at {zoneId}/{locationSlug} by LocationNpcsSeeder and is a shopkeeper");
        resolution.Refusal.Should().Be(ShopRefusal.None);

        var merchant = resolution.Merchant!;
        merchant.ArchetypeSlug.Should().Be(archetypeSlug);
        merchant.DisplayName.Should().Be(displayName, "each new archetype is one named person");
        merchant.ZoneId.Should().Be(zoneId);
        merchant.LocationSlug.Should().Be(locationSlug);

        var townLevel = await _app.Zones
            .AsNoTracking()
            .Where(zone => zone.Id == zoneId)
            .Select(zone => zone.MinLevel)
            .SingleAsync();

        MerchantBands.Contains(townLevel, merchant.Gold).Should().BeTrue(
            $"{archetypeSlug} keeps {merchant.Gold} at {zoneId} (town level {townLevel}), which must sit inside "
            + $"{MerchantBands.ForTownLevel(townLevel)}");
    }

    [Fact]
    public async Task Every_Merchant_Location_Outside_The_Sandbox_Resolves_A_Shopkeeper_And_Its_Shelf()
    {
        // The client is told which locations have a shop (ZoneLocation.HasMerchant, projected as ZoneDto.HasMerchant)
        // and offers the button from that flag alone, so every flagged location a player can reach has to have a
        // person behind it. This is the invariant, not a list: a new merchant location without a shopkeeper fails here
        // instead of offering a shop that refuses every trade.
        var merchantZones = await _app.Zones
            .AsNoTracking()
            .Where(zone => zone.HasMerchant && !zone.IsDevOnly)
            .Select(zone => zone.Id)
            .ToListAsync();

        var locations = await _content.ZoneLocations.AsNoTracking().ToListAsync();
        var merchantLocations = locations
            .Where(location => merchantZones.Contains(location.ZoneId) && location.Traits.HasMerchant == true)
            .ToList();

        merchantLocations.Should().HaveCount(6,
            "crestfall, aldenmere (keep + marketplace), tolvaren (cliff road) and skarhold (forge + caldera market) "
            + "each advertise a shop; the dev sandbox's shop is excluded as IsDevOnly");

        var authoredShelf = ZoneStockSeeder.Authors();

        foreach (var location in merchantLocations)
        {
            var resolution = await _resolver.ResolveAsync(
                At(location.ZoneId, location.Slug),
                location.ZoneId,
                CancellationToken.None);

            resolution.Success.Should().BeTrue(
                $"{location.ZoneId}/{location.Slug} offers a shop the client can see, so somebody has to stand there");

            var shelf = await _stock.GetItemSlugsAsync(location.ZoneId, CancellationToken.None);
            shelf.Should().NotBeEmpty($"{location.ZoneId} is a merchant town with an authored shelf");
            shelf.Should().BeSubsetOf(
                authoredShelf.Where(row => row.ZoneId == location.ZoneId).Select(row => row.ItemSlug),
                "the shelf a merchant location sells is the town's authored stock");
        }
    }

    [Fact]
    public async Task Refuses_A_Rostered_Townsperson_Who_Trades_Nothing()
    {
        // The harbour-master is placed at tolvaren's harbour, which is a town location in a merchant zone, but the
        // roster has him teach and refuse berth rather than sell — so the trait gate, not the zone gate, refuses here.
        var resolution = await _resolver.ResolveAsync(
            At("tolvaren", "tolvaren-harbour"),
            "tolvaren",
            CancellationToken.None);

        resolution.Success.Should().BeFalse();
        resolution.Refusal.Should().Be(ShopRefusal.NoMerchant);
    }

    [Fact]
    public async Task Refuses_When_Nobody_At_The_Location_Is_A_Shopkeeper()
    {
        // The wayfarer elder is placed here, with dialogue and a quest hook, but trades nothing.
        var resolution = await _resolver.ResolveAsync(
            At("crestfall", "crestfall-square"),
            "crestfall",
            CancellationToken.None);

        resolution.Success.Should().BeFalse();
        resolution.Refusal.Should().Be(ShopRefusal.NoMerchant);
    }

    [Fact]
    public async Task Refuses_In_The_Wilderness()
    {
        var resolution = await _resolver.ResolveAsync(
            At("the-droveway", "droveway-crossing"),
            "the-droveway",
            CancellationToken.None);

        resolution.Success.Should().BeFalse();
        resolution.Refusal.Should().Be(ShopRefusal.NoMerchant);
    }

    [Fact]
    public async Task Refuses_When_The_Character_Is_Not_At_A_Location()
    {
        var resolution = await _resolver.ResolveAsync(
            At("crestfall", null),
            "crestfall",
            CancellationToken.None);

        resolution.Success.Should().BeFalse();
        resolution.Refusal.Should().Be(ShopRefusal.NoMerchant);
    }

    [Fact]
    public async Task Refuses_When_The_Client_Names_Another_Zone()
    {
        var resolution = await _resolver.ResolveAsync(
            At("crestfall", "aurelian-market"),
            "skarhold",
            CancellationToken.None);

        resolution.Success.Should().BeFalse();
        resolution.Refusal.Should().Be(ShopRefusal.ZoneMismatch);
    }

    [Fact]
    public async Task Refuses_An_Unauthored_Location()
    {
        var resolution = await _resolver.ResolveAsync(
            At("crestfall", "somewhere-that-does-not-exist"),
            "crestfall",
            CancellationToken.None);

        resolution.Success.Should().BeFalse();
        resolution.Refusal.Should().Be(ShopRefusal.NoMerchant);
    }
}
