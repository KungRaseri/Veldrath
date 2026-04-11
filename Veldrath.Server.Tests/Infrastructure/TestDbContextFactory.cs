using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// Creates an <see cref="ApplicationDbContext"/> backed by an in-memory SQLite database.
/// Uses a real SQLite connection (not the pure in-memory EF provider) so that FK
/// constraints, unique indexes, and schema migrations are exercised just as in production.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    /// <summary>Creates a fresh <see cref="ApplicationDbContext"/> against the open connection.</summary>
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        DatabaseSeeder.SeedApplicationDataAsync(context).GetAwaiter().GetResult();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
