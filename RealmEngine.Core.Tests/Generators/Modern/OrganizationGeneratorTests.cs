using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Core.Tests.Generators.Modern;

/// <summary>
/// Behavioural tests for <see cref="OrganizationGenerator"/>: active/type filtering, weighted selection,
/// shop discovery (type or trait driven) and the entity→model projection.
/// </summary>
[Trait("Category", "Generators")]
public class OrganizationGeneratorTests
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

    private static OrganizationGenerator CreateGenerator(ContentDbContext db) =>
        new(db, NullLogger<OrganizationGenerator>.Instance);

    private static RealmEngine.Data.Entities.Organization MakeOrganization(
        string slug,
        string orgType = "guilds",
        bool isActive = true,
        string? displayName = null,
        int? wealth = null,
        int rarityWeight = 50,
        bool? hasShop = null) => new()
        {
            Slug = slug,
            OrgType = orgType,
            TypeKey = orgType,
            IsActive = isActive,
            DisplayName = displayName,
            RarityWeight = rarityWeight,
            Stats = new OrganizationStats { Wealth = wealth },
            Traits = new OrganizationTraits { HasShop = hasShop }
        };

    // ── GenerateOrganizationsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GenerateOrganizationsAsync_Returns_Requested_Count_Of_Active_Organizations_Of_The_Type()
    {
        // Arrange — inactive and wrong-type rows must never be selected.
        var databaseName = Seed(db => db.Organizations.AddRange(
            MakeOrganization("mages-guild", displayName: "Mages Guild", wealth: 1200),
            MakeOrganization("thieves-guild", displayName: "Thieves Guild", wealth: 300),
            MakeOrganization("closed-guild", isActive: false),
            MakeOrganization("iron-merchants", orgType: "businesses")));

        using var db = CreateDb(databaseName);

        // Act
        var result = await CreateGenerator(db).GenerateOrganizationsAsync("guilds", count: 8);

        // Assert
        result.Should().HaveCount(8);
        result.Should().OnlyContain(o => o.Type == "guilds");
        result.Should().OnlyContain(o => o.Id == "mages-guild" || o.Id == "thieves-guild");
        result.Should().OnlyContain(o => !string.IsNullOrWhiteSpace(o.Name));
        result.Should().OnlyContain(o => o.Description == string.Empty);
    }

    [Fact]
    public async Task GenerateOrganizationsAsync_Maps_Wealth_And_Display_Name()
    {
        var databaseName = Seed(db => db.Organizations.Add(
            MakeOrganization("mages-guild", displayName: "Mages Guild", wealth: 1200)));

        using var db = CreateDb(databaseName);

        var organization = (await CreateGenerator(db).GenerateOrganizationsAsync("guilds", count: 1)).Single();

        organization.Id.Should().Be("mages-guild");
        organization.Name.Should().Be("Mages Guild");
        organization.Type.Should().Be("guilds");
        organization.Wealth.Should().Be(1200);
    }

    [Fact]
    public async Task GenerateOrganizationsAsync_Falls_Back_To_Slug_And_Zero_Wealth_When_Optionals_Are_Missing()
    {
        var databaseName = Seed(db => db.Organizations.Add(MakeOrganization("nameless-cult", orgType: "factions")));

        using var db = CreateDb(databaseName);

        var organization = (await CreateGenerator(db).GenerateOrganizationsAsync("factions", count: 1)).Single();

        organization.Name.Should().Be("nameless-cult");
        organization.Wealth.Should().Be(0);
    }

    [Fact]
    public async Task GenerateOrganizationsAsync_Treats_Non_Positive_Weights_As_One_And_Still_Selects()
    {
        // Arrange — both rows have a zero weight; the fallback weight keeps them selectable.
        var databaseName = Seed(db => db.Organizations.AddRange(
            MakeOrganization("weightless-a", rarityWeight: 0),
            MakeOrganization("weightless-b", rarityWeight: 0)));

        using var db = CreateDb(databaseName);

        var result = await CreateGenerator(db).GenerateOrganizationsAsync("guilds", count: 10);

        result.Should().HaveCount(10);
        result.Should().OnlyContain(o => o.Id == "weightless-a" || o.Id == "weightless-b");
    }

    [Fact]
    public async Task GenerateOrganizationsAsync_Returns_Empty_When_No_Organization_Matches()
    {
        var databaseName = Seed(db => db.Organizations.Add(
            MakeOrganization("iron-merchants", orgType: "businesses")));

        using var db = CreateDb(databaseName);

        var result = await CreateGenerator(db).GenerateOrganizationsAsync("guilds", count: 5);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateOrganizationsAsync_Returns_Empty_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.Organizations.Add(MakeOrganization("mages-guild")));
        var db = CreateDb(databaseName);
        db.Dispose();

        var result = await CreateGenerator(db).GenerateOrganizationsAsync("guilds", count: 5);

        result.Should().BeEmpty();
    }

    // ── GenerateOrganizationByNameAsync ───────────────────────────────────────

    [Fact]
    public async Task GenerateOrganizationByNameAsync_Returns_The_Matching_Organization()
    {
        var databaseName = Seed(db => db.Organizations.AddRange(
            MakeOrganization("mages-guild", displayName: "Mages Guild", wealth: 900),
            MakeOrganization("thieves-guild")));

        using var db = CreateDb(databaseName);

        var organization = await CreateGenerator(db).GenerateOrganizationByNameAsync("guilds", "thieves-guild");

        organization.Should().NotBeNull();
        organization!.Id.Should().Be("thieves-guild");
        organization.Name.Should().Be("thieves-guild");
        organization.Wealth.Should().Be(0);
    }

    [Fact]
    public async Task GenerateOrganizationByNameAsync_Returns_Null_For_Inactive_Or_Missing_Organizations()
    {
        var databaseName = Seed(db => db.Organizations.Add(
            MakeOrganization("shuttered-guild", isActive: false)));

        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);

        (await generator.GenerateOrganizationByNameAsync("guilds", "shuttered-guild")).Should().BeNull();
        (await generator.GenerateOrganizationByNameAsync("guilds", "never-existed")).Should().BeNull();
    }

    [Fact]
    public async Task GenerateOrganizationByNameAsync_Returns_Null_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.Organizations.Add(MakeOrganization("mages-guild")));
        var db = CreateDb(databaseName);
        db.Dispose();

        var organization = await CreateGenerator(db).GenerateOrganizationByNameAsync("guilds", "mages-guild");

        organization.Should().BeNull();
    }

    // ── GenerateShopAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateShopAsync_Selects_From_Shop_Types_And_Shop_Traits()
    {
        // Arrange — one explicit "shop" org type and one guild flagged with the shop trait.
        var databaseName = Seed(db => db.Organizations.AddRange(
            MakeOrganization("general-store", orgType: "shop", displayName: "General Store", wealth: 500),
            MakeOrganization("guild-store", orgType: "guilds", displayName: "Guild Store", hasShop: true),
            MakeOrganization("mages-guild", orgType: "guilds", displayName: "Mages Guild")));

        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);

        // Act — repeat to prove both eligibilities can be selected.
        var seen = new HashSet<string>();
        for (var i = 0; i < 30; i++)
        {
            var shop = await generator.GenerateShopAsync("general");

            shop.Should().NotBeNull();
            shop!.Name.Should().BeOneOf("General Store", "Guild Store",
                "organizations without the shop trait are not vendors");
            seen.Add(shop.Id);
        }

        // Assert
        seen.Should().BeEquivalentTo(["general-store", "guild-store"]);
    }

    [Fact]
    public async Task GenerateShopAsync_Returns_Null_When_No_Shop_Exists()
    {
        var databaseName = Seed(db => db.Organizations.Add(
            MakeOrganization("mages-guild", hasShop: false)));

        using var db = CreateDb(databaseName);

        (await CreateGenerator(db).GenerateShopAsync("general")).Should().BeNull();
    }

    [Fact]
    public async Task GenerateShopAsync_Returns_Null_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.Organizations.Add(MakeOrganization("general-store", orgType: "shop")));
        var db = CreateDb(databaseName);
        db.Dispose();

        (await CreateGenerator(db).GenerateShopAsync("general")).Should().BeNull();
    }

    [Fact]
    public async Task GenerateShopAsync_Uses_A_Shared_Signature_With_Hydration_And_Inventory_Size()
    {
        var databaseName = Seed(db => db.Organizations.Add(
            MakeOrganization("general-store", orgType: "shop", wealth: 42)));

        using var db = CreateDb(databaseName);

        var shop = await CreateGenerator(db).GenerateShopAsync("general", inventorySize: 5, hydrate: false);

        shop.Should().NotBeNull();
        shop!.Wealth.Should().Be(42);
    }
}
