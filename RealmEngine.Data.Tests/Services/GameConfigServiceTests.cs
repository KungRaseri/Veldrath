using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Services;

namespace RealmEngine.Data.Tests.Services;

[Trait("Category", "Services")]
public class NullGameConfigServiceTests
{
    [Theory]
    [InlineData("experience")]
    [InlineData("rarity")]
    [InlineData("budget")]
    [InlineData("")]
    public void GetData_AlwaysReturnsNull(string key)
    {
        var svc = new NullGameConfigService();
        svc.GetData(key).Should().BeNull();
    }
}

[Trait("Category", "Services")]
public class DbGameConfigServiceTests : IDisposable
{
    private readonly ContentDbContext _db;
    private readonly FakeDbContextFactory _factory;

    public DbGameConfigServiceTests()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ContentDbContext(options);
        _factory = new FakeDbContextFactory(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetData_ReturnsJson_WhenKeyExists()
    {
        _db.GameConfigs.Add(new GameConfig { ConfigKey = "experience", Data = """{"baseXp":100}""" });
        _db.SaveChanges();
        var svc = new DbGameConfigService(_factory);

        svc.GetData("experience").Should().Be("""{"baseXp":100}""");
    }

    [Fact]
    public void GetData_ReturnsNull_WhenKeyMissing()
    {
        var svc = new DbGameConfigService(_factory);
        svc.GetData("no-such-key").Should().BeNull();
    }

    [Fact]
    public void GetData_ReturnsCorrectRow_WhenMultipleKeys()
    {
        _db.GameConfigs.AddRange(
            new GameConfig { ConfigKey = "rarity", Data = """{"tier":"common"}""" },
            new GameConfig { ConfigKey = "budget", Data = """{"gold":500}""" });
        _db.SaveChanges();
        var svc = new DbGameConfigService(_factory);

        svc.GetData("budget").Should().Be("""{"gold":500}""");
        svc.GetData("rarity").Should().Be("""{"tier":"common"}""");
    }

    /// <summary>
    /// Minimal IDbContextFactory stub that creates a new context pointing at the same
    /// named in-memory database for each call.  DbGameConfigService disposes each
    /// context it creates (via a using statement), so we cannot hand back the shared
    /// instance — a fresh context per call prevents ObjectDisposedException.
    /// </summary>
    private sealed class FakeDbContextFactory(DbContextOptions<ContentDbContext> options)
        : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() => new(options);
    }
}
