using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Data;

public class RegionRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Seeded_Regions()
    {
        await using var db = _factory.CreateContext();
        var repo   = new RegionRepository(db);
        var result = await repo.GetAllAsync();

        result.Should().HaveCount(4);
        result.Should().Contain(r => r.Id == "varenmark");
        result.Should().Contain(r => r.Id == "greymoor");
        result.Should().Contain(r => r.Id == "saltcliff");
        result.Should().Contain(r => r.Id == "cinderplain");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Regions_Ordered_By_MinLevel()
    {
        await using var db = _factory.CreateContext();
        var repo   = new RegionRepository(db);
        var result = await repo.GetAllAsync();

        result.Select(r => r.MinLevel).Should().BeInAscendingOrder();
        result[0].Id.Should().Be("varenmark"); // L0 first
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Region_When_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new RegionRepository(db);
        var result = await repo.GetByIdAsync("greymoor");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Greymoor");
        result.MinLevel.Should().Be(5);
        result.MaxLevel.Should().Be(14);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new RegionRepository(db);
        var result = await repo.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConnectedAsync_Should_Return_Adjacent_Regions_For_Greymoor()
    {
        await using var db = _factory.CreateContext();
        // greymoor connects outward to varenmark, saltcliff, cinderplain
        var repo   = new RegionRepository(db);
        var result = await repo.GetConnectedAsync("greymoor");

        result.Should().HaveCount(3);
        result.Should().Contain(r => r.Id == "varenmark");
        result.Should().Contain(r => r.Id == "saltcliff");
        result.Should().Contain(r => r.Id == "cinderplain");
    }

    [Fact]
    public async Task GetConnectedAsync_Should_Return_One_Region_For_Varenmark()
    {
        await using var db = _factory.CreateContext();
        // varenmark only connects outward to greymoor
        var repo   = new RegionRepository(db);
        var result = await repo.GetConnectedAsync("varenmark");

        result.Should().ContainSingle(r => r.Id == "greymoor");
    }

    [Fact]
    public async Task GetConnectedAsync_Should_Return_Empty_For_Unknown_Region()
    {
        await using var db = _factory.CreateContext();
        var repo   = new RegionRepository(db);
        var result = await repo.GetConnectedAsync("nonexistent");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_Should_Include_Starter_Region()
    {
        await using var db = _factory.CreateContext();
        var repo   = new RegionRepository(db);
        var result = await repo.GetAllAsync();

        result.Should().ContainSingle(r => r.IsStarter);
        result.Single(r => r.IsStarter).Id.Should().Be("varenmark");
    }
}
