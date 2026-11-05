using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Generators.Modern;

/// <summary>
/// Behavioural tests for <see cref="LocationGenerator"/>: catalog filtering, entity→model mapping,
/// danger-scaled loot rolls and level-appropriate enemy selection.
/// </summary>
[Trait("Category", "Generators")]
public class LocationGeneratorTests
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

    private static LocationGenerator CreateGenerator(ContentDbContext db) =>
        new(db, NullLogger<LocationGenerator>.Instance);

    private static ZoneLocation MakeLocation(
        string slug,
        string typeKey = "dungeons",
        bool isActive = true,
        string? displayName = null,
        int? minLevel = null,
        int? dangerLevel = null,
        bool? hasMerchant = null,
        bool? isTown = null) => new()
        {
            Slug = slug,
            TypeKey = typeKey,
            IsActive = isActive,
            DisplayName = displayName,
            ZoneId = "test-zone",
            Stats = new ZoneLocationStats { MinLevel = minLevel, DangerLevel = dangerLevel },
            Traits = new ZoneLocationTraits { HasMerchant = hasMerchant, IsTown = isTown }
        };

    // ── GenerateLocationsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateLocationsAsync_Returns_Requested_Count_Of_Active_Locations_Of_The_Type()
    {
        // Arrange — one inactive and one wrong-type entry must never be selected.
        var databaseName = Seed(db => db.ZoneLocations.AddRange(
            MakeLocation("crypt", displayName: "The Crypt", minLevel: 7, dangerLevel: 6),
            MakeLocation("catacombs"),
            MakeLocation("sealed-vault", isActive: false),
            MakeLocation("darkwood", typeKey: "forests")));

        using var db = CreateDb(databaseName);

        // Act
        var result = await CreateGenerator(db).GenerateLocationsAsync("dungeons", count: 6);

        // Assert
        result.Should().HaveCount(6);
        result.Should().OnlyContain(l => l.Type == "dungeons" && l.LocationType == "dungeons");
        result.Should().OnlyContain(l => l.Id == "crypt" || l.Id == "catacombs");
    }

    [Fact]
    public async Task GenerateLocationsAsync_Maps_Stats_And_Traits_Onto_The_Model()
    {
        // Arrange
        var databaseName = Seed(db => db.ZoneLocations.Add(
            MakeLocation("crypt", displayName: "The Crypt", minLevel: 7, dangerLevel: 6, hasMerchant: true, isTown: true)));

        using var db = CreateDb(databaseName);

        // Act
        var location = (await CreateGenerator(db).GenerateLocationsAsync("dungeons", count: 1)).Single();

        // Assert
        location.Id.Should().Be("crypt");
        location.Name.Should().Be("The Crypt");
        location.Description.Should().BeEmpty();
        location.Type.Should().Be("dungeons");
        location.LocationType.Should().Be("dungeons");
        location.Level.Should().Be(7);
        location.DangerRating.Should().Be(6);
        location.HasShop.Should().BeTrue("the merchant trait drives the shop flag");
        location.HasInn.Should().BeTrue("the town trait drives the inn flag");
        location.IsSafeZone.Should().BeTrue("the town trait drives the safe-zone flag");
        location.IsStartingZone.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateLocationsAsync_Applies_Defaults_When_Optional_Stats_And_Traits_Are_Missing()
    {
        // Arrange — slug-only row: no display name, no level, no danger, no traits.
        var databaseName = Seed(db => db.ZoneLocations.Add(MakeLocation("unmapped")));

        using var db = CreateDb(databaseName);

        // Act
        var location = (await CreateGenerator(db).GenerateLocationsAsync("dungeons", count: 1)).Single();

        // Assert
        location.Name.Should().Be("unmapped", "the slug is the fallback display name");
        location.Level.Should().Be(1, "a missing minimum level defaults to 1");
        location.DangerRating.Should().Be(1, "a missing danger level defaults to 1");
        location.HasShop.Should().BeFalse();
        location.HasInn.Should().BeFalse();
        location.IsSafeZone.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateLocationsAsync_Returns_Empty_When_No_Location_Matches_The_Type()
    {
        var databaseName = Seed(db => db.ZoneLocations.AddRange(
            MakeLocation("crypt"),
            MakeLocation("sealed-vault", isActive: false)));

        using var db = CreateDb(databaseName);

        var result = await CreateGenerator(db).GenerateLocationsAsync("towns", count: 5);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateLocationsAsync_Returns_Empty_When_The_Database_Query_Throws()
    {
        // Arrange — a disposed context makes the catalog query throw.
        var databaseName = Seed(db => db.ZoneLocations.Add(MakeLocation("crypt")));
        var db = CreateDb(databaseName);
        db.Dispose();

        // Act
        var result = await CreateGenerator(db).GenerateLocationsAsync("dungeons", count: 5);

        // Assert
        result.Should().BeEmpty("the generator swallows catalog failures and logs them");
    }

    // ── GenerateLocationByNameAsync ───────────────────────────────────────────

    [Fact]
    public async Task GenerateLocationByNameAsync_Returns_The_Matching_Location_Regardless_Of_Type()
    {
        var databaseName = Seed(db => db.ZoneLocations.AddRange(
            MakeLocation("crypt", displayName: "The Crypt", minLevel: 4, dangerLevel: 5, isTown: true),
            MakeLocation("darkwood", typeKey: "forests")));

        using var db = CreateDb(databaseName);

        var location = await CreateGenerator(db).GenerateLocationByNameAsync("dungeons", "crypt");

        location.Should().NotBeNull();
        location!.Id.Should().Be("crypt");
        location.Name.Should().Be("The Crypt");
        location.Level.Should().Be(4);
        location.DangerRating.Should().Be(5);
        location.IsSafeZone.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateLocationByNameAsync_Returns_Null_When_The_Location_Is_Inactive_Or_Missing()
    {
        var databaseName = Seed(db => db.ZoneLocations.AddRange(
            MakeLocation("crypt"),
            MakeLocation("vault", isActive: false)));

        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);

        (await generator.GenerateLocationByNameAsync("dungeons", "vault")).Should().BeNull();
        (await generator.GenerateLocationByNameAsync("dungeons", "nowhere")).Should().BeNull();
    }

    [Fact]
    public async Task GenerateLocationByNameAsync_Returns_Null_When_The_Database_Query_Throws()
    {
        var databaseName = Seed(db => db.ZoneLocations.Add(MakeLocation("crypt")));
        var db = CreateDb(databaseName);
        db.Dispose();

        var location = await CreateGenerator(db).GenerateLocationByNameAsync("dungeons", "crypt");

        location.Should().BeNull();
    }

    // ── GenerateLocationLoot ──────────────────────────────────────────────────

    [Fact]
    public void GenerateLocationLoot_Scales_Gold_And_Experience_With_The_Danger_Rating()
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);
        var location = new Location
        {
            Id = "crypt",
            Name = "Crypt",
            Description = "",
            Type = "dungeons",
            DangerRating = 5
        };

        for (var i = 0; i < 50; i++)
        {
            var loot = generator.GenerateLocationLoot(location);

            loot.GoldAmount.Should().BeInRange(25, 100, "danger 5 rolls gold in [danger*5, danger*20]");
            loot.ExperienceAmount.Should().BeInRange(50, 250, "danger 5 rolls xp in [danger*10, danger*50]");
            loot.ShouldDropItem.Should().Be(loot.ItemCategory is not null);
            if (loot.ShouldDropItem)
            {
                loot.ItemCategory.Should().Be("weapons");
                loot.SuggestedItemRarity.Should().NotBeNull();
            }
            else
            {
                loot.SuggestedItemRarity.Should().BeNull();
            }
        }
    }

    [Fact]
    public void GenerateLocationLoot_Never_Offers_A_Rarity_Above_Common_For_A_Safe_Location()
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);
        var location = new Location
        {
            Id = "haven",
            Name = "Haven",
            Description = "",
            Type = "towns",
            DangerRating = 0
        };

        for (var i = 0; i < 50; i++)
        {
            var loot = generator.GenerateLocationLoot(location);

            // danger 0 → gold/xp always 0, and the rarity branch collapses to Common when it drops.
            loot.GoldAmount.Should().Be(0);
            loot.ExperienceAmount.Should().Be(0);
            if (loot.ShouldDropItem)
                loot.SuggestedItemRarity.Should().Be(ItemRarity.Common);
        }
    }

    [Theory]
    [InlineData(1, ItemRarity.Common)]
    [InlineData(3, ItemRarity.Uncommon)]
    [InlineData(5, ItemRarity.Rare)]
    [InlineData(7, ItemRarity.Epic)]
    [InlineData(9, ItemRarity.Legendary)]
    public void GenerateLocationLoot_Maps_Danger_Rating_Onto_Suggested_Rarity(int danger, ItemRarity expected)
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var generator = CreateGenerator(db);
        var location = new Location
        {
            Id = "site",
            Name = "Site",
            Description = "",
            Type = "wilderness",
            DangerRating = danger
        };

        var drops = 0;
        for (var i = 0; i < 200; i++)
        {
            var loot = generator.GenerateLocationLoot(location);

            if (loot.ShouldDropItem)
            {
                drops++;
                loot.SuggestedItemRarity.Should().Be(expected);
            }
            else
            {
                loot.SuggestedItemRarity.Should().BeNull();
            }
        }

        drops.Should().BeGreaterThan(0, "the drop chance for danger {0} is high enough to drop within 200 rolls", danger);
    }

    // ── GenerateLocationAppropriateEnemyAsync ─────────────────────────────────

    private static readonly Enemy[] MixedLevelEnemies =
    [
        new() { Slug = "skeleton", Name = "Skeleton", Level = 5, RarityWeight = 10 },
        new() { Slug = "wraith", Name = "Wraith", Level = 6, RarityWeight = 10 },
        new() { Slug = "lich", Name = "Lich", Level = 20, RarityWeight = 10 }
    ];

    private static (EnemyGenerator Generator, Mock<IEnemyRepository> Repository) CreateEnemyGenerator(
        IReadOnlyList<Enemy> enemies,
        IRandomSource? randomSource = null)
    {
        var repository = new Mock<IEnemyRepository>();
        repository.Setup(r => r.GetByFamilyAsync(It.IsAny<string>()))
                  .ReturnsAsync(enemies.ToList());
        return (new EnemyGenerator(repository.Object, NullLogger<EnemyGenerator>.Instance, randomSource), repository);
    }

    [Fact]
    public async Task GenerateLocationAppropriateEnemyAsync_Prefers_Enemies_Within_Three_Levels()
    {
        // Arrange — location level 2: the skeleton (5) is the only enemy within ±3 levels.
        // The generator's weighted sampler is scripted so all ten candidates are the skeleton;
        // an unseeded sampler could legitimately draw only the out-of-range lich and fall back.
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var location = new Location
        {
            Id = "crypt",
            Name = "Crypt",
            Description = "",
            Type = "dungeons",
            Level = 2,
            DangerRating = 5,
            LocationType = "dungeon"
        };
        var (enemyGenerator, repository) = CreateEnemyGenerator(MixedLevelEnemies, FakeRandomSource.Returning(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        // Act
        var enemy = await CreateGenerator(db).GenerateLocationAppropriateEnemyAsync(location, enemyGenerator);

        // Assert — only the skeleton is within ±3 levels of 2, so it must be chosen.
        enemy.Should().NotBeNull();
        enemy!.Slug.Should().Be("skeleton");
        repository.Verify(r => r.GetByFamilyAsync("undead"), Times.Once,
            "dungeons draw from the undead family");
    }

    [Fact]
    public async Task GenerateLocationAppropriateEnemyAsync_Falls_Back_To_Any_Enemy_When_No_Level_Matches()
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var location = new Location
        {
            Id = "grove",
            Name = "Grove",
            Description = "",
            Type = "wilderness",
            Level = 40,
            DangerRating = 3,
            LocationType = "forest"
        };
        var (enemyGenerator, repository) = CreateEnemyGenerator([
            new Enemy { Slug = "wolf", Name = "Wolf", Level = 2, RarityWeight = 0 },
            new Enemy { Slug = "bear", Name = "Bear", Level = 4, RarityWeight = 5 }
        ]);

        var enemy = await CreateGenerator(db).GenerateLocationAppropriateEnemyAsync(location, enemyGenerator);

        enemy.Should().NotBeNull();
        enemy!.Slug.Should().BeOneOf("wolf", "bear");
        repository.Verify(r => r.GetByFamilyAsync("beasts"), Times.Once,
            "forests draw from the beasts family");
    }

    [Fact]
    public async Task GenerateLocationAppropriateEnemyAsync_Returns_Null_When_The_Family_Has_No_Enemies()
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var location = new Location
        {
            Id = "ruin",
            Name = "Ruin",
            Description = "",
            Type = "ruins",
            Level = 3,
            DangerRating = 2,
            LocationType = "ruins"
        };
        var (enemyGenerator, _) = CreateEnemyGenerator([]);

        var enemy = await CreateGenerator(db).GenerateLocationAppropriateEnemyAsync(location, enemyGenerator);

        enemy.Should().BeNull();
    }

    [Theory]
    [InlineData("dungeon", "undead")]
    [InlineData("forest", "beasts")]
    [InlineData("swamp", "beasts")]
    [InlineData("ruins", "undead")]
    [InlineData("cave", "beasts")]
    [InlineData("settlement", "humanoids")]
    [InlineData(null, "humanoids")]
    public async Task GenerateLocationAppropriateEnemyAsync_Maps_Location_Type_Onto_Enemy_Family(
        string? locationType,
        string expectedFamily)
    {
        var databaseName = Seed(db => { });
        using var db = CreateDb(databaseName);
        var location = new Location
        {
            Id = "site",
            Name = "Site",
            Description = "",
            Type = locationType ?? "unknown",
            Level = 5,
            DangerRating = 2,
            LocationType = locationType
        };
        var (enemyGenerator, repository) = CreateEnemyGenerator([
            new Enemy { Slug = "brute", Name = "Brute", Level = 5, RarityWeight = 1 }
        ]);

        var enemy = await CreateGenerator(db).GenerateLocationAppropriateEnemyAsync(location, enemyGenerator);

        enemy.Should().NotBeNull();
        repository.Verify(r => r.GetByFamilyAsync(expectedFamily), Times.Once);
    }
}
