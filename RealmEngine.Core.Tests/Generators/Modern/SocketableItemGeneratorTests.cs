using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Generators.Modern;

/// <summary>
/// Behavioural tests for the five socketable-item generators that share the same shape —
/// <see cref="RuneGenerator"/>, <see cref="OrbGenerator"/>, <see cref="GemGenerator"/>,
/// <see cref="EssenceGenerator"/> and <see cref="CrystalGenerator"/>.
/// Each case drives the real generator over an in-memory item catalog and asserts the projection
/// (slug/name/price/weight), the item-type filter, optional category filtering and failure behaviour.
/// </summary>
[Trait("Category", "Generators")]
public class SocketableItemGeneratorTests
{
    /// <summary>One generator under test, adapted to a provider-agnostic surface.</summary>
    private sealed class Case
    {
        public required string ItemType { get; init; }
        public required Func<ContentDbContext, string?, Task<(string Slug, string Name, int Price, int Weight)?>> One { get; init; }
        public required Func<ContentDbContext, int, string?, Task<int>> ManyCount { get; init; }
    }

    private static readonly Case[] Cases =
    [
        new()
        {
            ItemType = "rune",
            One = async (db, category) =>
            {
                var rune = await new RuneGenerator(db, NullLogger<RuneGenerator>.Instance).GenerateAsync(category);
                return rune is null ? null : (rune.Slug, rune.Name, rune.Price, rune.RarityWeight);
            },
            ManyCount = async (db, count, category) =>
                (await new RuneGenerator(db, NullLogger<RuneGenerator>.Instance).GenerateManyAsync(count, category)).Count
        },
        new()
        {
            ItemType = "orb",
            One = async (db, category) =>
            {
                var orb = await new OrbGenerator(db, NullLogger<OrbGenerator>.Instance).GenerateAsync(category);
                return orb is null ? null : (orb.Slug, orb.Name, orb.Price, orb.RarityWeight);
            },
            ManyCount = async (db, count, category) =>
                (await new OrbGenerator(db, NullLogger<OrbGenerator>.Instance).GenerateManyAsync(count, category)).Count
        },
        new()
        {
            ItemType = "gem",
            One = async (db, category) =>
            {
                var gem = await new GemGenerator(db, NullLogger<GemGenerator>.Instance).GenerateAsync(category);
                return gem is null ? null : (gem.Slug, gem.Name, gem.Price, gem.RarityWeight);
            },
            ManyCount = async (db, count, category) =>
                (await new GemGenerator(db, NullLogger<GemGenerator>.Instance).GenerateManyAsync(count, category)).Count
        },
        new()
        {
            ItemType = "essence",
            One = async (db, category) =>
            {
                var essence = await new EssenceGenerator(db, NullLogger<EssenceGenerator>.Instance).GenerateAsync(category);
                return essence is null ? null : (essence.Slug, essence.Name, essence.Price, essence.RarityWeight);
            },
            ManyCount = async (db, count, category) =>
                (await new EssenceGenerator(db, NullLogger<EssenceGenerator>.Instance).GenerateManyAsync(count, category)).Count
        },
        new()
        {
            ItemType = "crystal",
            One = async (db, category) =>
            {
                var crystal = await new CrystalGenerator(db, NullLogger<CrystalGenerator>.Instance).GenerateAsync(category);
                return crystal is null ? null : (crystal.Slug, crystal.Name, crystal.Price, crystal.RarityWeight);
            },
            ManyCount = async (db, count, category) =>
                (await new CrystalGenerator(db, NullLogger<CrystalGenerator>.Instance).GenerateManyAsync(count, category)).Count
        }
    ];

    private static ContentDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<ContentDbContext>().UseInMemoryDatabase(name).Options);

    private static string Seed(params RealmEngine.Data.Entities.Item[] items)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDb(databaseName);
        db.Items.AddRange(items);
        db.SaveChanges();
        return databaseName;
    }

    private static RealmEngine.Data.Entities.Item MakeItem(
        string slug,
        string itemType,
        string typeKey = "socketables",
        bool isActive = true,
        string? displayName = null,
        int? value = null,
        int rarityWeight = 50) => new()
        {
            Slug = slug,
            ItemType = itemType,
            TypeKey = typeKey,
            IsActive = isActive,
            DisplayName = displayName,
            RarityWeight = rarityWeight,
            Stats = new ItemStats { Value = value }
        };

    [Fact]
    public async Task GenerateAsync_Maps_Active_Items_Of_The_Matching_Item_Type()
    {
        foreach (var testCase in Cases)
        {
            // Arrange — the matching item, a same-named inactive decoy and a different item type.
            var databaseName = Seed(
                MakeItem("primary", testCase.ItemType, displayName: "Primary", value: 120, rarityWeight: 25),
                MakeItem("retired", testCase.ItemType, isActive: false),
                MakeItem($"not-{testCase.ItemType}", "weapon"));

            using var db = CreateDb(databaseName);

            // Act
            var result = await testCase.One(db, null);

            // Assert
            result.Should().NotBeNull($"{testCase.ItemType} generator must find its own item type");
            result!.Value.Slug.Should().Be("primary", $"{testCase.ItemType} generator filters by ItemType");
            result.Value.Name.Should().Be("Primary", $"{testCase.ItemType} generator prefers the display name");
            result.Value.Price.Should().Be(120, $"{testCase.ItemType} generator maps Stats.Value onto Price");
            result.Value.Weight.Should().Be(25, $"{testCase.ItemType} generator copies the rarity weight");
        }
    }

    [Fact]
    public async Task GenerateAsync_Falls_Back_To_The_Slug_And_Zero_Price_When_Optionals_Are_Missing()
    {
        foreach (var testCase in Cases)
        {
            var databaseName = Seed(MakeItem("plain", testCase.ItemType, value: null));
            using var db = CreateDb(databaseName);

            var result = await testCase.One(db, null);

            result.Should().NotBeNull();
            result!.Value.Name.Should().Be("plain", $"{testCase.ItemType} generator falls back to the slug");
            result.Value.Price.Should().Be(0, $"{testCase.ItemType} generator defaults a missing value to 0");
        }
    }

    [Fact]
    public async Task GenerateAsync_Returns_Null_When_Only_Other_Item_Types_Or_Inactive_Items_Exist()
    {
        foreach (var testCase in Cases)
        {
            var databaseName = Seed(
                MakeItem("retired", testCase.ItemType, isActive: false),
                MakeItem("other", "weapon"));

            using var db = CreateDb(databaseName);

            var result = await testCase.One(db, null);

            result.Should().BeNull($"{testCase.ItemType} generator ignores inactive and mismatched items");
        }
    }

    [Fact]
    public async Task GenerateAsync_Applies_The_Optional_Category_Filter()
    {
        foreach (var testCase in Cases)
        {
            // Arrange — two entries of the right item type in different TypeKey categories.
            var databaseName = Seed(
                MakeItem("offensive-1", testCase.ItemType, typeKey: "offensive"),
                MakeItem("defensive-1", testCase.ItemType, typeKey: "defensive"));

            using var db = CreateDb(databaseName);

            // Act
            var result = await testCase.One(db, "defensive");

            // Assert
            result.Should().NotBeNull();
            result!.Value.Slug.Should().Be("defensive-1", "the category narrows the catalog before selection");

            // An unmatched category yields nothing rather than falling back to the whole catalog.
            (await testCase.One(db, "utility")).Should().BeNull();
        }
    }

    [Fact]
    public async Task GenerateAsync_Treats_Non_Positive_Rarity_Weights_As_One_And_Still_Selects()
    {
        foreach (var testCase in Cases)
        {
            var databaseName = Seed(
                MakeItem("weightless", testCase.ItemType, rarityWeight: 0),
                MakeItem("negative-weight", testCase.ItemType, rarityWeight: -10));

            using var db = CreateDb(databaseName);

            var result = await testCase.One(db, null);

            result.Should().NotBeNull($"{testCase.ItemType} generator must still select zero-weight entries");
            result!.Value.Slug.Should().BeOneOf("weightless", "negative-weight");
        }
    }

    [Fact]
    public async Task GenerateManyAsync_Returns_One_Entry_Per_Requested_Count()
    {
        foreach (var testCase in Cases)
        {
            var databaseName = Seed(
                MakeItem("alpha", testCase.ItemType),
                MakeItem("beta", testCase.ItemType));

            using var db = CreateDb(databaseName);

            var count = await testCase.ManyCount(db, 4, null);

            count.Should().Be(4, $"{testCase.ItemType} generator repeats selections to reach the count");
        }
    }

    [Fact]
    public async Task GenerateManyAsync_Returns_Empty_When_The_Catalog_Has_No_Matching_Items()
    {
        foreach (var testCase in Cases)
        {
            var databaseName = Seed(MakeItem("other", "weapon"));
            using var db = CreateDb(databaseName);

            (await testCase.ManyCount(db, 3, null)).Should().Be(0);
        }
    }

    [Fact]
    public async Task Generators_Return_Null_And_Empty_When_The_Database_Query_Throws()
    {
        foreach (var testCase in Cases)
        {
            var databaseName = Seed(MakeItem("alpha", testCase.ItemType));
            var db = CreateDb(databaseName);
            db.Dispose();

            (await testCase.One(db, null)).Should().BeNull($"{testCase.ItemType} generator swallows catalog failures");
            (await testCase.ManyCount(db, 2, null)).Should().Be(0);
        }
    }
}
