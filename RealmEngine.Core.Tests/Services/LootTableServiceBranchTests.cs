using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Branch tests for <see cref="LootTableService"/>: weighted drops, guaranteed drops, quantity
/// scaling, gold entries and the three chest merge strategies.
/// Rolls are pinned with weight extremes (0 = never, 100 = always) and fixed quantity ranges,
/// so the assertions hold for any seed.
/// </summary>
[Trait("Category", "Services")]
public class LootTableServiceBranchTests
{
    private sealed class StubLootTableRepository : ILootTableRepository
    {
        public Dictionary<string, LootTableData> Tables { get; } = new(StringComparer.Ordinal);

        public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(Tables.Values.ToList());

        public Task<LootTableData?> GetBySlugAsync(string slug) =>
            Task.FromResult(Tables.TryGetValue(slug, out var table) ? table : null);

        public Task<List<LootTableData>> GetByContextAsync(string context) =>
            Task.FromResult(Tables.Values.Where(t => t.Context == context).ToList());
    }

    /// <summary>Never used by <see cref="LootTableService"/>, which only stores the factory.</summary>
    private sealed class UnusedDbContextFactory : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() =>
            throw new NotSupportedException("LootTableService does not query the content database.");
    }

    private readonly StubLootTableRepository _repository = new();
    private readonly LootTableService _service;

    public LootTableServiceBranchTests()
    {
        _service = new LootTableService(
            NullLogger<LootTableService>.Instance,
            _repository,
            new UnusedDbContextFactory(),
            seed: 20260101);
    }

    private static LootTableEntryData Entry(
        string slug,
        int dropWeight = 100,
        int quantityMin = 1,
        int quantityMax = 1,
        bool guaranteed = false,
        string domain = "items/materials/ores") => new()
    {
        ItemDomain = domain,
        ItemSlug = slug,
        DropWeight = dropWeight,
        QuantityMin = quantityMin,
        QuantityMax = quantityMax,
        IsGuaranteed = guaranteed
    };

    private void AddTable(string slug, params LootTableEntryData[] entries) =>
        _repository.Tables[slug] = new LootTableData
        {
            Slug = slug,
            Name = slug,
            Entries = entries.ToList()
        };

    // ── Harvesting ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RollHarvestingDrops_Without_A_Reference_Returns_Empty(string? reference)
    {
        // Act
        var drops = await _service.RollHarvestingDrops(reference!, 1, false);

        // Assert
        drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollHarvestingDrops_With_A_Reference_Missing_The_Colon_Returns_Empty()
    {
        // Act
        var drops = await _service.RollHarvestingDrops("loot-tables/woods/oak", 1, false);

        // Assert
        drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollHarvestingDrops_With_An_Unknown_Table_Returns_Empty()
    {
        // Act
        var drops = await _service.RollHarvestingDrops("@loot-tables/harvesting/woods:mystery", 1, false);

        // Assert
        drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollHarvestingDrops_Should_Emit_A_Guaranteed_Entry_With_A_Formatted_Name()
    {
        // Arrange
        AddTable("oak", Entry("iron-ore", guaranteed: true, quantityMin: 2, quantityMax: 2));

        // Act
        var drops = await _service.RollHarvestingDrops("@loot-tables/harvesting/woods:oak", 1, false);

        // Assert
        var drop = drops.Should().ContainSingle().Subject;
        drop.ItemRef.Should().Be("@items/materials/ores:iron-ore");
        drop.ItemName.Should().Be("Iron Ore");
        drop.Quantity.Should().Be(2);
        drop.IsBonus.Should().BeFalse();
    }

    [Fact]
    public async Task RollHarvestingDrops_Should_Always_Drop_A_Full_Weight_Entry()
    {
        // Arrange — every roll is at most 100, so a weight of 100 always passes.
        AddTable("oak", Entry("oak-log", dropWeight: 100));

        // Act
        var drops = await _service.RollHarvestingDrops("@loot-tables/harvesting/woods:oak", 1, false);

        // Assert
        drops.Should().ContainSingle().Which.ItemRef.Should().EndWith(":oak-log");
    }

    [Fact]
    public async Task RollHarvestingDrops_Should_Never_Drop_A_Zero_Weight_Entry()
    {
        // Arrange — rolls start at 1, so a weight of 0 always fails.
        AddTable("oak", Entry("oak-log", dropWeight: 0));

        // Act
        var drops = await _service.RollHarvestingDrops("@loot-tables/harvesting/woods:oak", 1, false);

        // Assert
        drops.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RollHarvestingDrops_Should_Scale_Quantity_By_The_Base_Yield(int baseYield)
    {
        // Arrange
        AddTable("oak", Entry("oak-log", guaranteed: true, quantityMin: 2, quantityMax: 2));

        // Act
        var drops = await _service.RollHarvestingDrops("@loot-tables/harvesting/woods:oak", baseYield, false);

        // Assert
        drops.Should().ContainSingle().Which.Quantity.Should().Be(2 * baseYield);
    }

    [Fact]
    public async Task RollHarvestingDrops_Should_Handle_Multiple_Entries_In_Order()
    {
        // Arrange
        AddTable(
            "oak",
            Entry("oak-log", guaranteed: true),
            Entry("rare-resin", dropWeight: 0),
            Entry("sap", guaranteed: true, domain: "items/materials/organics"));

        // Act
        var drops = await _service.RollHarvestingDrops("@loot-tables/harvesting/woods:oak", 1, true);

        // Assert
        drops.Should().HaveCount(2);
        drops.Select(d => d.ItemName).Should().Equal("Oak Log", "Sap");
    }

    // ── Enemy ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RollEnemyDrops_Without_A_Reference_Returns_An_Empty_Result(string? reference)
    {
        // Act
        var result = await _service.RollEnemyDrops(reference!);

        // Assert
        result.Drops.Should().BeEmpty();
        result.Gold.Should().Be(0);
    }

    [Fact]
    public async Task RollEnemyDrops_With_An_Invalid_Reference_Returns_An_Empty_Result()
    {
        // Act
        var result = await _service.RollEnemyDrops("no-colon-here");

        // Assert
        result.Drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollEnemyDrops_With_An_Unknown_Table_Returns_An_Empty_Result()
    {
        // Act
        var result = await _service.RollEnemyDrops("@loot-tables/enemies:goblin");

        // Assert
        result.Drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollEnemyDrops_Should_Collect_Guaranteed_Entries()
    {
        // Arrange
        AddTable("goblin", Entry("rusty-dagger", guaranteed: true, domain: "items/weapons/daggers"));

        // Act
        var result = await _service.RollEnemyDrops("@loot-tables/enemies:goblin");

        // Assert
        var drop = result.Drops.Should().ContainSingle().Subject;
        drop.ItemName.Should().Be("Rusty Dagger");
        drop.ItemRef.Should().Be("@items/weapons/daggers:rusty-dagger");
    }

    [Fact]
    public async Task RollEnemyDrops_Should_Skip_Zero_Weight_Entries_And_Honour_Quantity_Ranges()
    {
        // Arrange
        AddTable(
            "goblin",
            Entry("gold-tooth", dropWeight: 0),
            Entry("rusty-dagger", dropWeight: 100, quantityMin: 3, quantityMax: 3));

        // Act
        var result = await _service.RollEnemyDrops("@loot-tables/enemies:goblin");

        // Assert — enemy loot never routes "gold" domains into the gold counter.
        result.Drops.Should().ContainSingle().Which.Quantity.Should().Be(3);
        result.Gold.Should().Be(0);
    }

    // ── Chests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RollChestDrops_With_No_Lookups_Returns_An_Empty_Result()
    {
        // Act
        var result = await _service.RollChestDrops([]);

        // Assert
        result.Gold.Should().Be(0);
        result.Drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollChestDrops_With_A_Null_Lookup_List_Returns_An_Empty_Result()
    {
        // Act
        var result = await _service.RollChestDrops(null!);

        // Assert
        result.Drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollChestDrops_AddPools_Should_Merge_Gold_And_Drops_From_Every_Table()
    {
        // Arrange
        AddTable("common", Entry("gold", domain: "gold", quantityMin: 12, quantityMax: 12));
        AddTable("rare", Entry("ruby", guaranteed: true, domain: "items/materials/gems"));

        // Act
        var result = await _service.RollChestDrops(
            ["@loot-tables/chests:common", "@loot-tables/chests:rare"],
            "addPools");

        // Assert
        result.Gold.Should().Be(12);
        result.Drops.Should().ContainSingle().Which.ItemName.Should().Be("Ruby");
    }

    [Fact]
    public async Task RollChestDrops_PrioritizeFirst_Should_Ignore_Later_Tables()
    {
        // Arrange
        AddTable("first", Entry("gold", domain: "Gold", quantityMin: 5, quantityMax: 5));
        AddTable("second", Entry("gold", domain: "gold", quantityMin: 99, quantityMax: 99));

        // Act
        var result = await _service.RollChestDrops(
            ["@loot-tables/chests:first", "@loot-tables/chests:second"],
            "prioritizeFirst");

        // Assert
        result.Gold.Should().Be(5);
    }

    [Fact]
    public async Task RollChestDrops_PrioritizeLast_Should_Ignore_Earlier_Tables()
    {
        // Arrange
        AddTable("first", Entry("gold", domain: "gold", quantityMin: 5, quantityMax: 5));
        AddTable("second", Entry("gold", domain: "gold", quantityMin: 99, quantityMax: 99));

        // Act
        var result = await _service.RollChestDrops(
            ["@loot-tables/chests:first", "@loot-tables/chests:second"],
            "prioritizeLast");

        // Assert
        result.Gold.Should().Be(99);
    }

    [Fact]
    public async Task RollChestDrops_With_An_Unknown_Strategy_Should_Fall_Back_To_AddPools()
    {
        // Arrange
        AddTable("common", Entry("gold", domain: "gold", quantityMin: 7, quantityMax: 7));
        AddTable("rare", Entry("gold", domain: "gold", quantityMin: 3, quantityMax: 3));

        // Act
        var result = await _service.RollChestDrops(
            ["@loot-tables/chests:common", "@loot-tables/chests:rare"],
            "roundRobin");

        // Assert
        result.Gold.Should().Be(10);
    }

    [Fact]
    public async Task RollChestDrops_Should_Skip_An_Empty_Lookup_And_A_Missing_Table()
    {
        // Arrange
        AddTable("common", Entry("gold", domain: "gold", quantityMin: 4, quantityMax: 4));

        // Act
        var result = await _service.RollChestDrops(
            ["", "   ", "invalid-reference", "@loot-tables/chests:missing", "@loot-tables/chests:common"],
            "addPools");

        // Assert
        result.Gold.Should().Be(4);
        result.Drops.Should().BeEmpty();
    }

    [Fact]
    public async Task RollChestDrops_Should_Skip_Zero_Weight_Entries()
    {
        // Arrange
        AddTable(
            "common",
            Entry("cursed-idol", dropWeight: 0, domain: "items/quest"),
            Entry("linen-cloth", dropWeight: 100, domain: "items/materials/fabrics"));

        // Act
        var result = await _service.RollChestDrops(["@loot-tables/chests:common"]);

        // Assert
        result.Drops.Should().ContainSingle().Which.ItemName.Should().Be("Linen Cloth");
    }

    [Fact]
    public async Task RollChestDrops_Should_Title_Case_Multi_Word_Slugs()
    {
        // Arrange
        AddTable("common", Entry("big-iron-ore", guaranteed: true));

        // Act
        var result = await _service.RollChestDrops(["@loot-tables/chests:common"]);

        // Assert
        result.Drops.Should().ContainSingle().Which.ItemName.Should().Be("Big Iron Ore");
    }
}
