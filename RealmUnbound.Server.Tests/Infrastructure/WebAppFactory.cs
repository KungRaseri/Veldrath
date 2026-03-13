using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RealmUnbound.Server.Data;

namespace RealmUnbound.Server.Tests.Infrastructure;

/// <summary>
/// Spins up the full ASP.NET Core server against a named in-memory SQLite database.
/// A single <see cref="SqliteConnection"/> is held open for the factory's lifetime to
/// keep the named in-memory database alive across all request scopes.
/// All tests in a single <see cref="IClassFixture{T}"/> share the same database instance,
/// so use distinct usernames / character names per test to avoid collisions.
/// </summary>
public sealed class WebAppFactory : WebApplicationFactory<Program>
{
    // Each factory instance gets a unique database so parallel test classes don't collide.
    private readonly string _dbName = $"realm_test_{Guid.NewGuid():N}";
    private string ConnStr => $"Data Source={_dbName};Mode=Memory;Cache=Shared";

    // Keep-alive connection: while this is open, the named in-memory database survives.
    private readonly SqliteConnection _keepAlive;

    public WebAppFactory()
    {
        _keepAlive = new SqliteConnection(ConnStr);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // Production DbContext (Postgres) is replaced below via ConfigureServices.
        // Only non-DB settings are set here.
        builder.UseSetting("Jwt:Key",                      "test-secret-key-for-integration-tests!!");
        builder.UseSetting("Jwt:Issuer",                   "RealmUnbound.Test");
        builder.UseSetting("Jwt:Audience",                 "RealmUnbound.Test");
        builder.UseSetting("Jwt:AccessTokenExpiryMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenExpiryDays",   "30");
        builder.UseSetting("RealmEngine:DataPath",         GetDataPath());

        // Replace the production Postgres DbContext with an in-memory SQLite instance.
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(ConnStr));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure the Identity + game schema exists before any test sends a request.
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _keepAlive.Dispose();
        base.Dispose(disposing);
    }

    private static string GetDataPath()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(WebAppFactory).Assembly.Location)!;
        // bin/Debug/net10.0 → ../../.. → project root → ../RealmEngine.Data/Data/Json
        var solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
        return Path.GetFullPath(Path.Combine(solutionRoot, "..", "RealmEngine.Data", "Data", "Json"));
    }
}

