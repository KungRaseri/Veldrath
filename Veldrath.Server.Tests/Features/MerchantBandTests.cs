using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Shop;
using Veldrath.Server.Tests.Infrastructure;
using YamlDotNet.RepresentationModel;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Pins the merchant band: the code's copy of <c>game-context/budget.yaml</c> may not drift from the approved file, and
/// every seeded shopkeeper's purse must sit inside the band for its town.
/// </summary>
/// <remarks>
/// The band is approved data. A constant edited in C# and not in the YAML (or the other way round) is a quiet change
/// to the economy, so the first test parses the real file and compares it with <see cref="MerchantBands"/>.
/// </remarks>
public class MerchantBandTests : IDisposable
{
    private readonly TestDbContextFactory _appFactory = new();
    private readonly SqliteConnection _contentConnection;
    private readonly ContentDbContext _content;

    /// <summary>Builds the content catalog the purse assertions read.</summary>
    public MerchantBandTests()
    {
        _contentConnection = new SqliteConnection("Data Source=:memory:");
        _contentConnection.Open();

        _content = new ContentDbContext(
            new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_contentConnection).Options);
        _content.Database.GetService<IRelationalDatabaseCreator>().CreateTables();
        ArchetypeSeeder.SeedAsync(_content).GetAwaiter().GetResult();
        ItemsSeeder.SeedAsync(_content).GetAwaiter().GetResult();
        ZoneStockSeeder.SeedAsync(_content).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _content.Dispose();
        _contentConnection.Dispose();
        _appFactory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string BudgetYamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "game-context", "budget.yaml")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository's budget.yaml must be reachable from the test binaries");
        return Path.Combine(directory!.FullName, "game-context", "budget.yaml");
    }

    private static YamlMappingNode MerchantsSection()
    {
        var stream = new YamlStream();
        using var reader = new StreamReader(BudgetYamlPath());
        stream.Load(reader);

        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        return (YamlMappingNode)root.Children[new YamlScalarNode("merchants")];
    }

    private static YamlNode MerchantValue(string key)
    {
        var merchants = MerchantsSection();
        merchants.Children.Should().ContainKey(new YamlScalarNode(key));
        return merchants.Children[new YamlScalarNode(key)];
    }

    private static int[] IntSequence(string key) =>
        ((YamlSequenceNode)MerchantValue(key)).Children
            .Select(node => int.Parse(((YamlScalarNode)node).Value!, CultureInfo.InvariantCulture))
            .ToArray();

    private static double[] DoubleSequence(string key) =>
        ((YamlSequenceNode)MerchantValue(key)).Children
            .Select(node => double.Parse(((YamlScalarNode)node).Value!, CultureInfo.InvariantCulture))
            .ToArray();

    [Fact]
    public void Gold_Bands_Match_The_Approved_Budget()
    {
        var approved = (YamlMappingNode)MerchantValue("gold_by_town_level");

        var fromYaml = approved.Children
            .Select(pair =>
            {
                var levelText = ((YamlScalarNode)pair.Key).Value!;
                var level = int.Parse(levelText.TrimStart('L'), CultureInfo.InvariantCulture);
                var range = ((YamlSequenceNode)pair.Value).Children
                    .Select(node => int.Parse(((YamlScalarNode)node).Value!, CultureInfo.InvariantCulture))
                    .ToArray();

                return new MerchantBand(level, range[0], range[1]);
            })
            .OrderBy(band => band.TownLevel)
            .ToList();

        MerchantBands.GoldByTownLevel.Should().BeEquivalentTo(
            fromYaml,
            options => options.WithStrictOrdering(),
            "the code mirrors merchants.gold_by_town_level and must not drift from the approved file");
    }

    [Fact]
    public void Stock_Size_And_Price_Bands_Match_The_Approved_Budget()
    {
        var stockSize = IntSequence("stock_size");
        MerchantBands.StockMin.Should().Be(stockSize[0]);
        MerchantBands.StockMax.Should().Be(stockSize[1]);

        var markup = DoubleSequence("price_markup");
        (MerchantBands.PriceMarkupPercent / 100.0).Should().BeInRange(markup[0], markup[1]);

        var buyback = DoubleSequence("buyback_rate");
        (MerchantBands.BuybackRatePercent / 100.0).Should().BeInRange(buyback[0], buyback[1]);
    }

    [Fact]
    public void Markup_Floor_Stays_Above_Or_Equal_To_The_Buyback_Ceiling()
    {
        var markup = DoubleSequence("price_markup");
        var buyback = DoubleSequence("buyback_rate");

        markup[0].Should().BeGreaterThanOrEqualTo(
            buyback[1],
            "below that a merchant sells for less than it pays back, and buy -> sell prints gold");

        (MerchantBands.PriceMarkupPercent / 100.0).Should().BeGreaterThanOrEqualTo(
            MerchantBands.BuybackRatePercent / 100.0);
    }

    [Fact]
    public async Task Every_Seeded_Shopkeeper_Purse_Is_Inside_Its_Towns_Band()
    {
        await using var db = _appFactory.CreateContext();
        await LocationNpcsSeeder.SeedAsync(db);

        var placements = await db.LocationNpcs.AsNoTracking().ToListAsync();
        var zones = await db.Zones.AsNoTracking().ToDictionaryAsync(zone => zone.Id);
        var archetypes = await _content.ActorArchetypes.AsNoTracking()
            .ToDictionaryAsync(actor => actor.Slug);

        placements.Should().NotBeEmpty("the merchant towns have placed townspeople");

        foreach (var placement in placements)
        {
            if (!archetypes.TryGetValue(placement.ArchetypeSlug, out var archetype))
                continue;

            if (archetype.Traits.Shopkeeper != true)
                continue;

            var zone = zones[placement.ZoneId];
            var purse = archetype.Stats.TradeGold ?? 0;

            MerchantBands.Contains(zone.MinLevel, purse).Should().BeTrue(
                $"{placement.ArchetypeSlug} keeps {purse} gold at {placement.ZoneId} (town level {zone.MinLevel}), "
                + $"which must sit inside {MerchantBands.ForTownLevel(zone.MinLevel)}");
        }
    }

    [Fact]
    public async Task Gold_Basis_Reproduces_Each_Towns_Band_From_Its_Authored_Shelf()
    {
        await using var db = _appFactory.CreateContext();

        var merchantTowns = await db.Zones.AsNoTracking()
            .Where(zone => zone.HasMerchant && !zone.IsDevOnly)
            .OrderBy(zone => zone.MinLevel)
            .ToListAsync();

        // The dearest purchasable item each town actually stocks is the shelf ceiling gold_basis is written from.
        var valueBySlug = await _content.Items.AsNoTracking()
            .Where(item => item.IsActive && item.Stats.Value > 0)
            .ToDictionaryAsync(item => item.Slug, item => item.Stats.Value!.Value);

        var shelves = ZoneStockSeeder.Authors()
            .GroupBy(row => row.ZoneId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.ItemSlug).ToArray());

        var stockSize = IntSequence("stock_size");
        var buyback = DoubleSequence("buyback_rate");
        var approved = (YamlMappingNode)MerchantValue("gold_by_town_level");

        foreach (var town in merchantTowns)
        {
            var ceiling = shelves[town.Id].Max(slug => valueBySlug[slug]);
            var band = ((YamlSequenceNode)approved.Children[new YamlScalarNode($"L{town.MinLevel}")]).Children
                .Select(node => int.Parse(((YamlScalarNode)node).Value!, CultureInfo.InvariantCulture))
                .ToArray();

            RoundToNearestFifty(stockSize[0] * buyback[0] * ceiling).Should().Be(band[0],
                $"merchants.gold_basis sizes {town.Id}'s L{town.MinLevel} floor from its shelf ceiling "
                + $"({ceiling} gold), at stock_size {stockSize[0]} x buyback {buyback[0]}");

            RoundToNearestFifty(stockSize[1] * buyback[1] * ceiling).Should().Be(band[1],
                $"merchants.gold_basis sizes {town.Id}'s L{town.MinLevel} ceiling from its shelf ceiling "
                + $"({ceiling} gold), at stock_size {stockSize[1]} x buyback {buyback[1]}");
        }
    }

    private static int RoundToNearestFifty(double value) =>
        (int)(Math.Round(value / 50.0, MidpointRounding.AwayFromZero) * 50);

    [Fact]
    public async Task Every_Reachable_Merchant_Town_Has_A_Band_And_Stock()
    {
        await using var db = _appFactory.CreateContext();

        var merchantTowns = await db.Zones.AsNoTracking()
            .Where(zone => zone.HasMerchant && !zone.IsDevOnly)
            .ToListAsync();

        merchantTowns.Should().HaveCount(4, "crestfall, aldenmere, tolvaren and skarhold trade; playground is dev-only");

        var stockCounts = await _content.ZoneStockItems
            .AsNoTracking()
            .GroupBy(row => row.ZoneId)
            .Select(group => new { ZoneId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ZoneId, row => row.Count);

        foreach (var town in merchantTowns)
        {
            stockCounts.Should().ContainKey(town.Id, $"{town.Id} is a merchant town and needs an authored shelf");
            stockCounts[town.Id].Should().BeInRange(MerchantBands.StockMin, MerchantBands.StockMax);

            MerchantBands.ForTownLevel(town.MinLevel).TownLevel.Should().Be(town.MinLevel,
                $"{town.Id} is a seeded town and its level defines its band");
        }
    }
}
