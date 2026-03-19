using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Models;
using Xunit;
using DataItem = RealmEngine.Data.Entities.Item;
using DataWeapon = RealmEngine.Data.Entities.Weapon;
using DataArmor = RealmEngine.Data.Entities.Armor;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class ItemDataServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    private static ContentDbContext CreateDb(string dbName) =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static IDbContextFactory<ContentDbContext> CreateFactory(string dbName)
    {
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext())
               .Returns(() => CreateDb(dbName));
        return factory.Object;
    }

    private static ItemDataService CreateService(string dbName) =>
        new(CreateFactory(dbName), NullLogger<ItemDataService>.Instance);

    private static DataWeapon MakeWeapon(string slug, string typeKey, int rarityWeight = 80, bool isActive = true) =>
        new() { Slug = slug, TypeKey = typeKey, WeaponType = "sword", DamageType = "physical", IsActive = isActive, RarityWeight = rarityWeight };

    private static DataArmor MakeArmor(string slug, string typeKey, int rarityWeight = 80, bool isActive = true) =>
        new() { Slug = slug, TypeKey = typeKey, ArmorType = "light", EquipSlot = "chest", IsActive = isActive, RarityWeight = rarityWeight };

    private static DataItem MakeItem(string slug, string typeKey, int rarityWeight = 80, bool isActive = true) =>
        new() { Slug = slug, TypeKey = typeKey, ItemType = "consumable", IsActive = isActive, RarityWeight = rarityWeight };

    // ── LoadCatalog — basic queries ────────────────────────────────────────

    [Fact]
    public void LoadCatalog_WhenDatabaseIsEmpty_ReturnsEmpty()
    {
        var service = CreateService(Guid.NewGuid().ToString());

        service.LoadCatalog("swords").Should().BeEmpty();
    }

    [Fact]
    public void LoadCatalog_WithActiveWeapon_ReturnsTemplate()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("iron-sword", "swords"));
            seed.SaveChanges();
        }

        var result = CreateService(dbName).LoadCatalog("swords");

        result.Should().ContainSingle(t => t.Slug == "iron-sword" && t.Category == "swords");
    }

    [Fact]
    public void LoadCatalog_InactiveWeapon_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("rusty-sword", "swords", isActive: false));
            seed.SaveChanges();
        }

        CreateService(dbName).LoadCatalog("swords").Should().BeEmpty();
    }

    [Fact]
    public void LoadCatalog_QueriesWeaponsArmorsAndItems_WhenAllPresent()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("axe", "axes"));
            seed.Armors.Add(MakeArmor("plate", "chest-armor"));
            seed.Items.Add(MakeItem("potion", "consumables"));
            seed.SaveChanges();
        }
        var service = CreateService(dbName);

        service.LoadCatalog("axes").Should().HaveCount(1).And.Contain(t => t.Slug == "axe");
        service.LoadCatalog("chest-armor").Should().HaveCount(1).And.Contain(t => t.Slug == "plate");
        service.LoadCatalog("consumables").Should().HaveCount(1).And.Contain(t => t.Slug == "potion");
    }

    // ── LoadCatalog — rarity filtering ────────────────────────────────────

    [Fact]
    public void LoadCatalog_WithRarityFilter_ExcludesNonMatchingItems()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            // rarityWeight 80 → Common; 5 → Legendary
            seed.Weapons.Add(MakeWeapon("common-sword", "swords", rarityWeight: 80));
            seed.Weapons.Add(MakeWeapon("legendary-sword", "swords", rarityWeight: 5));
            seed.SaveChanges();
        }

        var result = CreateService(dbName).LoadCatalog("swords", ItemRarity.Common);

        result.Should().ContainSingle(t => t.Slug == "common-sword");
    }

    [Fact]
    public void LoadCatalog_RarityMappedCorrectly_ForDifferentWeights()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("w-common", "swords", rarityWeight: 80));
            seed.Weapons.Add(MakeWeapon("w-uncommon", "swords", rarityWeight: 60));
            seed.Weapons.Add(MakeWeapon("w-rare", "swords", rarityWeight: 30));
            seed.Weapons.Add(MakeWeapon("w-epic", "swords", rarityWeight: 15));
            seed.Weapons.Add(MakeWeapon("w-legendary", "swords", rarityWeight: 5));
            seed.SaveChanges();
        }

        var service = CreateService(dbName);

        service.LoadCatalog("swords", ItemRarity.Common).Should().ContainSingle(t => t.Slug == "w-common");
        service.LoadCatalog("swords", ItemRarity.Uncommon).Should().ContainSingle(t => t.Slug == "w-uncommon");
        service.LoadCatalog("swords", ItemRarity.Rare).Should().ContainSingle(t => t.Slug == "w-rare");
        service.LoadCatalog("swords", ItemRarity.Epic).Should().ContainSingle(t => t.Slug == "w-epic");
        service.LoadCatalog("swords", ItemRarity.Legendary).Should().ContainSingle(t => t.Slug == "w-legendary");
    }

    // ── LoadCatalog — caching ──────────────────────────────────────────────

    [Fact]
    public void LoadCatalog_CachesResult_SecondCallDoesNotCreateNewDbContext()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("sword", "swords"));
            seed.SaveChanges();
        }

        int callCount = 0;
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() =>
        {
            callCount++;
            return CreateDb(dbName);
        });
        var service = new ItemDataService(factory.Object, NullLogger<ItemDataService>.Instance);

        service.LoadCatalog("swords");
        service.LoadCatalog("swords");

        callCount.Should().Be(1, "the second call should use the cached result");
    }

    [Fact]
    public void LoadCatalog_DifferentCacheKeysForRarityFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("sword", "swords", rarityWeight: 80));
            seed.SaveChanges();
        }

        int callCount = 0;
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() =>
        {
            callCount++;
            return CreateDb(dbName);
        });
        var service = new ItemDataService(factory.Object, NullLogger<ItemDataService>.Instance);

        service.LoadCatalog("swords");
        service.LoadCatalog("swords", ItemRarity.Common);

        callCount.Should().Be(2, "unfiltered and filtered catalog are separate cache entries");
    }

    // ── LoadCatalog — error handling ───────────────────────────────────────

    [Fact]
    public void LoadCatalog_WhenDbFactoryThrows_ReturnsEmpty()
    {
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Throws<InvalidOperationException>();
        var service = new ItemDataService(factory.Object, NullLogger<ItemDataService>.Instance);

        service.LoadCatalog("swords").Should().BeEmpty();
    }

    // ── LoadMultipleCategories ─────────────────────────────────────────────

    [Fact]
    public void LoadMultipleCategories_AggregatesAllCategories()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("sword", "swords"));
            seed.Armors.Add(MakeArmor("chest", "chest-armor"));
            seed.SaveChanges();
        }
        var service = CreateService(dbName);

        var result = service.LoadMultipleCategories(["swords", "chest-armor"]);

        result.Should().HaveCount(2)
              .And.Contain(t => t.Slug == "sword")
              .And.Contain(t => t.Slug == "chest");
    }

    [Fact]
    public void LoadMultipleCategories_EmptyList_ReturnsEmpty()
    {
        CreateService(Guid.NewGuid().ToString())
            .LoadMultipleCategories([])
            .Should().BeEmpty();
    }

    // ── ClearCache ─────────────────────────────────────────────────────────

    [Fact]
    public void ClearCache_ForcesReload_OnNextCall()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var seed = CreateDb(dbName))
        {
            seed.Weapons.Add(MakeWeapon("sword", "swords"));
            seed.SaveChanges();
        }

        int callCount = 0;
        var factory = new Mock<IDbContextFactory<ContentDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() =>
        {
            callCount++;
            return CreateDb(dbName);
        });
        var service = new ItemDataService(factory.Object, NullLogger<ItemDataService>.Instance);

        service.LoadCatalog("swords");
        service.ClearCache();
        service.LoadCatalog("swords");

        callCount.Should().Be(2, "cache was cleared so the second call must hit the database");
    }
}
