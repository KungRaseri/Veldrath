using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Exploration.Services;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration;

/// <summary>
/// Unit tests for <see cref="DungeonGeneratorService"/>.
/// </summary>
[Trait("Category", "Feature")]
public class DungeonGeneratorServiceTests
{
    private static DungeonGeneratorService CreateService() =>
        new(new EnemyGenerator(
                Mock.Of<IEnemyRepository>(),
                NullLogger<EnemyGenerator>.Instance),
            NullLogger<DungeonGeneratorService>.Instance);

    private static Location MakeDungeon(int dangerRating = 3, List<string>? features = null) =>
        new()
        {
            Id = "dungeon-1",
            Name = "Test Crypt",
            Description = "A test dungeon.",
            Type = "dungeons",
            Level = 5,
            DangerRating = dangerRating,
            Features = features ?? []
        };

    // ------------------------------------------------------------------
    // GenerateDungeonAsync — argument validation
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateDungeonAsync_Throws_WhenLocationIsNotDungeon()
    {
        var service = CreateService();
        var nonDungeon = new Location { Id = "town-1", Name = "Ironhaven", Description = "A peaceful town.", Type = "towns" };

        var act = () => service.GenerateDungeonAsync(nonDungeon, 5);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("location");
    }

    // ------------------------------------------------------------------
    // GenerateDungeonAsync — room count by danger rating
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateDungeonAsync_RoomCount_InRange_ForLowDanger()
    {
        var service = CreateService();
        var dungeon = await service.GenerateDungeonAsync(MakeDungeon(dangerRating: 2), 5);

        dungeon.TotalRooms.Should().BeInRange(5, 7);
        dungeon.Rooms.Should().HaveCount(dungeon.TotalRooms);
    }

    [Fact]
    public async Task GenerateDungeonAsync_RoomCount_InRange_ForMediumDanger()
    {
        var service = CreateService();
        var dungeon = await service.GenerateDungeonAsync(MakeDungeon(dangerRating: 5), 5);

        dungeon.TotalRooms.Should().BeInRange(8, 11);
        dungeon.Rooms.Should().HaveCount(dungeon.TotalRooms);
    }

    [Fact]
    public async Task GenerateDungeonAsync_RoomCount_InRange_ForHardDanger()
    {
        var service = CreateService();
        var dungeon = await service.GenerateDungeonAsync(MakeDungeon(dangerRating: 7), 5);

        dungeon.TotalRooms.Should().BeInRange(10, 13);
        dungeon.Rooms.Should().HaveCount(dungeon.TotalRooms);
    }

    [Fact]
    public async Task GenerateDungeonAsync_RoomCount_InRange_ForDeadlyDanger()
    {
        var service = CreateService();
        var dungeon = await service.GenerateDungeonAsync(MakeDungeon(dangerRating: 9), 5);

        dungeon.TotalRooms.Should().BeInRange(12, 15);
        dungeon.Rooms.Should().HaveCount(dungeon.TotalRooms);
    }

    // ------------------------------------------------------------------
    // GenerateDungeonAsync — boss room placement
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateDungeonAsync_LastRoom_IsAlwaysBossType()
    {
        var service = CreateService();
        var dungeon = await service.GenerateDungeonAsync(MakeDungeon(dangerRating: 3), 5);

        dungeon.Rooms.Last().Type.Should().Be("boss");
    }

    [Fact]
    public async Task GenerateDungeonAsync_FirstRoom_IsNeverBossType()
    {
        var service = CreateService();
        var dungeon = await service.GenerateDungeonAsync(MakeDungeon(dangerRating: 3), 5);

        dungeon.Rooms.First().Type.Should().NotBe("boss");
    }

    // ------------------------------------------------------------------
    // GetDungeonEnemyCategory — keyword-to-category mapping
    // ------------------------------------------------------------------

    private static string InvokeGetDungeonEnemyCategory(DungeonGeneratorService service, Location location)
    {
        var method = typeof(DungeonGeneratorService).GetMethod(
            "GetDungeonEnemyCategory",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (string)method.Invoke(service, [location])!;
    }

    [Theory]
    [InlineData("undead")]
    [InlineData("crypt")]
    [InlineData("tomb")]
    public void GetDungeonEnemyCategory_ReturnsUndead_ForUndeadKeywords(string feature)
    {
        var service = CreateService();
        var location = MakeDungeon(features: [feature]);

        var category = InvokeGetDungeonEnemyCategory(service, location);

        category.Should().Be("undead");
    }

    [Theory]
    [InlineData("demon")]
    [InlineData("infernal")]
    public void GetDungeonEnemyCategory_ReturnsDemons_ForDemonicKeywords(string feature)
    {
        var service = CreateService();
        var location = MakeDungeon(features: [feature]);

        var category = InvokeGetDungeonEnemyCategory(service, location);

        category.Should().Be("demons");
    }

    [Fact]
    public void GetDungeonEnemyCategory_ReturnsElementals_ForElementalFeature()
    {
        var service = CreateService();
        var location = MakeDungeon(features: ["elemental"]);

        var category = InvokeGetDungeonEnemyCategory(service, location);

        category.Should().Be("elementals");
    }

    [Theory]
    [InlineData("construct")]
    [InlineData("mechanical")]
    public void GetDungeonEnemyCategory_ReturnsConstructs_ForConstructKeywords(string feature)
    {
        var service = CreateService();
        var location = MakeDungeon(features: [feature]);

        var category = InvokeGetDungeonEnemyCategory(service, location);

        category.Should().Be("constructs");
    }

    [Fact]
    public void GetDungeonEnemyCategory_ReturnsHumanoids_WhenNoMatchingFeatures()
    {
        var service = CreateService();
        var location = MakeDungeon(features: ["forest", "river"]);

        var category = InvokeGetDungeonEnemyCategory(service, location);

        category.Should().Be("humanoids");
    }

    [Fact]
    public void GetDungeonEnemyCategory_ReturnsHumanoids_WhenFeaturesIsEmpty()
    {
        var service = CreateService();
        var location = MakeDungeon(features: []);

        var category = InvokeGetDungeonEnemyCategory(service, location);

        category.Should().Be("humanoids");
    }
}
