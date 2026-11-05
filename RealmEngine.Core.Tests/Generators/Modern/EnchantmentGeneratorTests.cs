using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Core.Tests.Generators.Modern;

/// <summary>
/// Behavioural tests for <see cref="EnchantmentGenerator"/>: slug lookup, active-only weighted sampling
/// and the entity→model projection (display name, value, rarity weight).
/// </summary>
[Trait("Category", "Generators")]
public class EnchantmentGeneratorTests
{
    private static ContentDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<ContentDbContext>().UseInMemoryDatabase(name).Options);

    private static string Seed(Action<ContentDbContext> seed)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDb(databaseName);
        seed(db);
        db.SaveChanges();
        return databaseName;
    }

    private static EnchantmentGenerator CreateGenerator(ContentDbContext db) =>
        new(db, NullLogger<EnchantmentGenerator>.Instance);

    private static RealmEngine.Data.Entities.Enchantment MakeEnchantment(
        string slug,
        bool isActive = true,
        string? displayName = null,
        int? value = null,
        int rarityWeight = 50) => new()
        {
            Slug = slug,
            TypeKey = "weapon",
            IsActive = isActive,
            DisplayName = displayName,
            RarityWeight = rarityWeight,
            Stats = new EnchantmentStats { Value = value }
        };

    // ── GenerateEnchantmentAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GenerateEnchantmentAsync_Maps_The_Entity_By_Slug()
    {
        var databaseName = Seed(db => db.Enchantments.AddRange(
            MakeEnchantment("flaming", displayName: "Flaming", value: 250, rarityWeight: 15),
            MakeEnchantment("frostbrand")));

        using var db = CreateDb(databaseName);

        var enchantment = await CreateGenerator(db).GenerateEnchantmentAsync("flaming");

        enchantment.Should().NotBeNull();
        enchantment!.Slug.Should().Be("flaming");
        enchantment.Name.Should().Be("flaming");
        enchantment.DisplayName.Should().Be("Flaming");
        enchantment.Value.Should().Be(250);
        enchantment.RarityWeight.Should().Be(15);
        enchantment.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateEnchantmentAsync_Falls_Back_To_The_Slug_And_Zero_Value_When_Optionals_Are_Missing()
    {
        var databaseName = Seed(db => db.Enchantments.Add(MakeEnchantment("keen")));

        using var db = CreateDb(databaseName);

        var enchantment = await CreateGenerator(db).GenerateEnchantmentAsync("keen");

        enchantment.Should().NotBeNull();
        enchantment!.DisplayName.Should().Be("keen");
        enchantment.Value.Should().Be(0);
    }

    [Fact]
    public async Task GenerateEnchantmentAsync_Returns_Null_For_Unknown_Or_Inactive_Slugs()
    {
        var databaseName = Seed(db => db.Enchantments.Add(
            MakeEnchantment("retired", isActive: false)));

        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);

        (await generator.GenerateEnchantmentAsync("retired")).Should().BeNull();
        (await generator.GenerateEnchantmentAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task GenerateEnchantmentAsync_Returns_Null_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.Enchantments.Add(MakeEnchantment("flaming")));
        var db = CreateDb(databaseName);
        db.Dispose();

        (await CreateGenerator(db).GenerateEnchantmentAsync("flaming")).Should().BeNull();
    }

    // ── GenerateEnchantmentsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateEnchantmentsAsync_Returns_Requested_Count_From_Active_Entries_Only()
    {
        // Arrange — one inactive decoy that must never surface.
        var databaseName = Seed(db => db.Enchantments.AddRange(
            MakeEnchantment("flaming", value: 100),
            MakeEnchantment("frostbrand", value: 200),
            MakeEnchantment("retired", value: 999, isActive: false)));

        using var db = CreateDb(databaseName);

        var enchantments = await CreateGenerator(db).GenerateEnchantmentsAsync(6);

        enchantments.Should().HaveCount(6);
        enchantments.Should().OnlyContain(e => e.Slug == "flaming" || e.Slug == "frostbrand");
        enchantments.Should().OnlyContain(e => e.Value == 100 || e.Value == 200);
        enchantments.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GenerateEnchantmentsAsync_Treats_Non_Positive_Weights_As_One_And_Still_Selects()
    {
        var databaseName = Seed(db => db.Enchantments.AddRange(
            MakeEnchantment("weightless-a", rarityWeight: 0),
            MakeEnchantment("weightless-b", rarityWeight: -5)));

        using var db = CreateDb(databaseName);

        var enchantments = await CreateGenerator(db).GenerateEnchantmentsAsync(10);

        enchantments.Should().HaveCount(10);
        enchantments.Should().OnlyContain(e => e.Slug == "weightless-a" || e.Slug == "weightless-b");
    }

    [Fact]
    public async Task GenerateEnchantmentsAsync_Returns_Empty_When_No_Active_Enchantments_Exist()
    {
        var databaseName = Seed(db => db.Enchantments.Add(
            MakeEnchantment("retired", isActive: false)));

        using var db = CreateDb(databaseName);

        var enchantments = await CreateGenerator(db).GenerateEnchantmentsAsync(4);

        enchantments.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEnchantmentsAsync_Returns_Empty_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.Enchantments.Add(MakeEnchantment("flaming")));
        var db = CreateDb(databaseName);
        db.Dispose();

        var enchantments = await CreateGenerator(db).GenerateEnchantmentsAsync(3);

        enchantments.Should().BeEmpty();
    }
}
