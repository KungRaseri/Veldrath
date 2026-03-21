using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Data;

public class WorldRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Seeded_Worlds()
    {
        await using var db = _factory.CreateContext();
        var repo   = new WorldRepository(db);
        var result = await repo.GetAllAsync();

        result.Should().ContainSingle(w => w.Id == "draveth");
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Correct_World_Properties()
    {
        await using var db = _factory.CreateContext();
        var repo   = new WorldRepository(db);
        var result = await repo.GetAllAsync();

        var draveth = result.Single();
        draveth.Name.Should().Be("Draveth");
        draveth.Era.Should().Be("The Age of Embers");
        draveth.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_World_When_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new WorldRepository(db);
        var result = await repo.GetByIdAsync("draveth");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Draveth");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var repo   = new WorldRepository(db);
        var result = await repo.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Worlds_Ordered_By_Name()
    {
        await using var db = _factory.CreateContext();
        var repo   = new WorldRepository(db);
        var result = await repo.GetAllAsync();

        result.Select(w => w.Name).Should().BeInAscendingOrder();
    }
}
