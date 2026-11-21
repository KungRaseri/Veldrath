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

        _app = _appFactory.CreateContext();
        LocationNpcsSeeder.SeedAsync(_app).GetAwaiter().GetResult();

        _resolver = new ShopMerchantResolver(_app, _content, NullLogger<ShopMerchantResolver>.Instance);
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
