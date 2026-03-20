using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace RealmUnbound.Server.Tests.Infrastructure;

/// <summary>
/// Creates a <see cref="GameDbContext"/> backed by an in-memory SQLite database.
/// Uses a real SQLite connection so FK constraints, unique indexes, and the schema
/// are exercised the same way as in production (Postgres).
/// </summary>
public sealed class TestGameDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>Initializes a new instance of <see cref="TestGameDbContextFactory"/>.</summary>
    public TestGameDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    /// <summary>Creates a fresh <see cref="GameDbContext"/> against the open connection.</summary>
    public GameDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new GameDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();
}
