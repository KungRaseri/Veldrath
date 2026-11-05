using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;
using Background = RealmEngine.Data.Entities.Background;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Tests for repository fallbacks and fault tolerance: background attribute bonuses, the in-memory catalogue
/// accessors, a node save that cannot be indexed, and a Hall of Fame write that must never throw at the caller.
/// </summary>
[Trait("Category", "Repository")]
public class RepositoryFallbackTests
{
    private static ContentDbContext CreateContentDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GameDbContext CreateGameDbContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Background Background(string typeKey, BackgroundStats stats) => new()
    {
        Slug = "test-background",
        TypeKey = typeKey,
        DisplayName = "Test Background",
        IsActive = true,
        Stats = stats,
    };

    [Theory]
    [InlineData("dexterity", 4)]
    [InlineData("intelligence", 5)]
    [InlineData("constitution", 6)]
    [InlineData("charisma", 0)]
    public async Task Backgrounds_ReportThePrimaryBonusForTheirAttribute(string typeKey, int expectedBonus)
    {
        await using var db = CreateContentDbContext();
        db.Backgrounds.Add(Background(typeKey, new BackgroundStats
        {
            BonusStrength = 1,
            BonusDexterity = 4,
            BonusIntelligence = 5,
            BonusConstitution = 6,
        }));
        await db.SaveChangesAsync();
        var repo = new EfCoreBackgroundRepository(db, NullLogger<EfCoreBackgroundRepository>.Instance);

        var background = await repo.GetBackgroundByIdAsync("test-background");

        background.Should().NotBeNull();
        background!.PrimaryBonus.Should().Be(expectedBonus);
        background.PrimaryAttribute.Should().Be(typeKey);
    }

    [Fact]
    public void EquipmentSetRepository_ResolvesSetsByNameAndById()
    {
        var repo = new InMemoryEquipmentSetRepository();

        repo.GetAll().Should().HaveCount(5);
        repo.GetByName("Stormcaller").Should().NotBeNull();
        repo.GetByName("stormcaller")!.Name.Should().Be("Stormcaller");
        repo.GetById("Stormcaller")!.Name.Should().Be("Stormcaller");
        repo.GetByName("no-such-set").Should().BeNull();
    }

    [Fact]
    public async Task InMemoryNodeRepository_ReportsAFailedSave()
    {
        var repo = new InMemoryNodeRepository(NullLogger<InMemoryNodeRepository>.Instance);
        var node = new HarvestableNode { NodeId = null!, LocationId = "grey-cup" };

        var saved = await repo.SaveNodeAsync(node);

        saved.Should().BeFalse();
        (await repo.GetNodesByLocationAsync("grey-cup")).Should().BeEmpty();
    }

    [Fact]
    public void HallOfFame_AddEntrySwallowsAStorageFault()
    {
        var db = CreateGameDbContext();
        var repo = new EfCoreHallOfFameRepository(db, NullLogger<EfCoreHallOfFameRepository>.Instance);
        db.Dispose();

        var act = () => repo.AddEntry(new HallOfFameEntry
        {
            CharacterName = "Fallen Hero",
            Level = 10,
            TotalEnemiesDefeated = 5,
        });

        act.Should().NotThrow("a fame write must never break the death path that calls it");
    }

    [Fact]
    public async Task MapGraphRepository_ReturnsNullForBlankIdentifiers()
    {
        await using var db = CreateContentDbContext();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        (await repo.GetMapAsync("  ")).Should().BeNull();
        (await repo.GetNodeAsync(" ")).Should().BeNull();
        (await repo.GetOutgoingEdgesAsync("woods", " ")).Should().BeEmpty();
        (await repo.GetMapForZoneAsync("")).Should().BeNull();
    }
}
