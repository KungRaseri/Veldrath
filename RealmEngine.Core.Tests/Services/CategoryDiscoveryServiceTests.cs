using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Behavioural tests for <see cref="CategoryDiscoveryService"/>: DB-driven leaf discovery across
/// domains, lazy/idempotent initialization, wildcard matching, membership checks, metadata and
/// statistics — including the per-domain failure fallback.
/// </summary>
[Trait("Category", "Services")]
public class CategoryDiscoveryServiceTests
{
    private sealed class StubDbContextFactory(string databaseName) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<ContentDbContext>().UseInMemoryDatabase(databaseName).Options);
    }

    private sealed class AlreadyDisposedDbContextFactory : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext()
        {
            var db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
            db.Dispose();
            return db;
        }
    }

    private static string SeedStandardCatalog()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

        db.Items.AddRange(
            new RealmEngine.Data.Entities.Item { Slug = "iron-sword", TypeKey = "weapons/swords", ItemType = "weapon" },
            new RealmEngine.Data.Entities.Item { Slug = "steel-axe", TypeKey = "weapons/swords", ItemType = "weapon" },
            new RealmEngine.Data.Entities.Item { Slug = "iron-helm", TypeKey = "armor", ItemType = "armor" },
            new RealmEngine.Data.Entities.Item { Slug = "retired-helm", TypeKey = "legacy", ItemType = "armor", IsActive = false });

        db.Materials.Add(new Material
        {
            Slug = "iron",
            DisplayName = "Iron",
            TypeKey = "metals",
            MaterialFamily = "metal",
            IsActive = true
        });

        db.Powers.Add(new Power { Slug = "fireball", TypeKey = "active/offensive" });
        db.ZoneLocations.Add(new ZoneLocation { Slug = "crypt", TypeKey = "dungeons" });
        db.Organizations.Add(new RealmEngine.Data.Entities.Organization { Slug = "mages-guild", OrgType = "guild", TypeKey = "guilds" });
        db.Dialogues.Add(new Dialogue { Slug = "greeting-1", TypeKey = "greetings" });
        db.Enchantments.Add(new RealmEngine.Data.Entities.Enchantment { Slug = "flaming", TypeKey = "weapon" });

        db.SaveChanges();
        return databaseName;
    }

    private static CategoryDiscoveryService CreateService(string databaseName) =>
        new(new StubDbContextFactory(databaseName), NullLogger<CategoryDiscoveryService>.Instance);

    private static CategoryDiscoveryService CreateInitializedService(string databaseName)
    {
        var service = CreateService(databaseName);
        service.Initialize();
        return service;
    }

    // ── Initialize ────────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_Discovers_Active_Leaf_Categories_Across_Domains()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        // Items are the union of item and material TypeKeys, excluding inactive rows.
        service.GetLeafCategories("items").Should().Equal("armor", "metals", "weapons/swords");
        service.GetLeafCategories("powers").Should().Equal("active/offensive");
        service.GetLeafCategories("locations").Should().Equal("dungeons");
        service.GetLeafCategories("organizations").Should().Equal("guilds");
        service.GetLeafCategories("dialogues").Should().Equal("greetings");
        service.GetLeafCategories("enchantments").Should().Equal("weapon");

        // Domains with no seeded content resolve to an empty list rather than throwing.
        service.GetLeafCategories("species").Should().BeEmpty();
        service.GetLeafCategories("recipes").Should().BeEmpty();
    }

    [Fact]
    public void Initialize_Is_Idempotent_And_Does_Not_Duplicate_Categories()
    {
        var service = CreateService(SeedStandardCatalog());

        service.Initialize();
        var firstPass = service.GetLeafCategories("items").ToList();
        service.Initialize();
        var secondPass = service.GetLeafCategories("items").ToList();

        secondPass.Should().Equal(firstPass, "a second Initialize call is a no-op");
        service.GetStatistics().IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Initialize_Falls_Back_To_Empty_Categories_When_A_Domain_Query_Fails()
    {
        var service = new CategoryDiscoveryService(
            new AlreadyDisposedDbContextFactory(),
            NullLogger<CategoryDiscoveryService>.Instance);

        service.Initialize();

        service.GetLeafCategories("items").Should().BeEmpty();
        service.GetStatistics().IsInitialized.Should().BeTrue();
        service.GetStatistics().TotalCategories.Should().Be(0);
    }

    // ── GetLeafCategories ─────────────────────────────────────────────────────

    [Fact]
    public void GetLeafCategories_Initializes_Lazily_On_First_Use()
    {
        var service = CreateService(SeedStandardCatalog());

        service.GetStatistics().IsInitialized.Should().BeFalse();

        var categories = service.GetLeafCategories("items");

        categories.Should().NotBeEmpty();
        service.GetStatistics().IsInitialized.Should().BeTrue("the first read triggers discovery");
    }

    [Fact]
    public void GetLeafCategories_Returns_Empty_For_An_Unknown_Domain()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        service.GetLeafCategories("constellations").Should().BeEmpty();
    }

    // ── FindCategories ────────────────────────────────────────────────────────

    [Fact]
    public void FindCategories_Returns_All_For_Star_Or_Blank_Pattern()
    {
        var service = CreateInitializedService(SeedStandardCatalog());
        string[] expected = ["armor", "metals", "weapons/swords"];

        service.FindCategories("items", "*").Should().Equal(expected);
        service.FindCategories("items", "").Should().Equal(expected);
        service.FindCategories("items", "   ").Should().Equal(expected);
    }

    [Fact]
    public void FindCategories_Matches_An_Exact_Name_Case_Insensitively()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        service.FindCategories("items", "ARMOR").Should().Equal("armor");
        service.FindCategories("items", "weapons/swords").Should().Equal("weapons/swords");
        service.FindCategories("items", "shields").Should().BeEmpty();
    }

    [Theory]
    [InlineData("weap*", "weapons/swords")]
    [InlineData("*swords", "weapons/swords")]
    [InlineData("weapons/*", "weapons/swords")]
    [InlineData("*pon*", "weapons/swords")]
    [InlineData("*or*", "armor", "weapons/swords")]
    public void FindCategories_Supports_Wildcard_Patterns(string pattern, params string[] expected)
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        service.FindCategories("items", pattern).Should().Equal(expected);
    }

    // ── IsLeafCategory ────────────────────────────────────────────────────────

    [Fact]
    public void IsLeafCategory_Checks_Membership_Case_Insensitively()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        service.IsLeafCategory("items", "weapons/swords").Should().BeTrue();
        service.IsLeafCategory("items", "WEAPONS/SWORDS").Should().BeTrue();
        service.IsLeafCategory("items", "weapons").Should().BeFalse("only leaf TypeKeys are registered");
        service.IsLeafCategory("constellations", "orion").Should().BeFalse();
    }

    // ── GetCategoryInfo ───────────────────────────────────────────────────────

    [Fact]
    public void GetCategoryInfo_Reports_The_Domain_Path_And_Item_Count()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        var info = service.GetCategoryInfo("items", "weapons/swords");

        info.Should().NotBeNull();
        info!.Domain.Should().Be("items");
        info.Path.Should().Be("weapons/swords");
        info.ItemCount.Should().Be(1,
            "the discovery projection unions items and materials, so each TypeKey is counted once");
        info.IsLeaf.Should().BeTrue();
        info.SubCategories.Should().BeEmpty();
    }

    [Fact]
    public void GetCategoryInfo_Returns_Null_For_An_Unknown_Path()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        service.GetCategoryInfo("items", "ships").Should().BeNull();
        service.GetCategoryInfo("constellations", "orion").Should().BeNull();
    }

    // ── GetStatistics ─────────────────────────────────────────────────────────

    [Fact]
    public void GetStatistics_Aggregates_Categories_Per_Domain()
    {
        var service = CreateInitializedService(SeedStandardCatalog());

        var statistics = service.GetStatistics();

        statistics.IsInitialized.Should().BeTrue();
        statistics.TotalCategories.Should().Be(statistics.CategoriesByDomain.Values.Sum());
        statistics.CategoriesByDomain["items"].Should().Be(3);
        statistics.CategoriesByDomain["powers"].Should().Be(1);
        // Every registered domain is present, even when it discovered nothing.
        statistics.TotalDomains.Should().BeGreaterThanOrEqualTo(12);
        statistics.CategoriesByDomain.Should().ContainKey("recipes").WhoseValue.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_Reports_Not_Initialized_Before_First_Use()
    {
        var service = CreateService(SeedStandardCatalog());

        var statistics = service.GetStatistics();

        statistics.IsInitialized.Should().BeFalse();
        statistics.TotalDomains.Should().Be(0);
        statistics.TotalCategories.Should().Be(0);
        statistics.CategoriesByDomain.Should().BeEmpty();
    }
}
