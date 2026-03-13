using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RealmEngine.Data.Persistence;
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

        // Provide a non-null connection string so Program.cs doesn't throw when
        // registering health checks. The real Postgres contexts are replaced below.
        builder.UseSetting("ConnectionStrings:DefaultConnection",   ConnStr);
        builder.UseSetting("Jwt:Key",                               "test-secret-key-for-integration-tests!!");
        builder.UseSetting("Jwt:Issuer",                            "RealmUnbound.Test");
        builder.UseSetting("Jwt:Audience",                          "RealmUnbound.Test");
        builder.UseSetting("Jwt:AccessTokenExpiryMinutes",          "15");
        builder.UseSetting("Jwt:RefreshTokenExpiryDays",            "30");
        builder.UseSetting("RealmEngine:DataPath",                  "");

        // Replace the production Postgres contexts with in-memory SQLite instances.
        builder.ConfigureServices(services =>
        {
            var appCtxDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (appCtxDescriptor is not null)
                services.Remove(appCtxDescriptor);

            var contentCtxDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ContentDbContext>));
            if (contentCtxDescriptor is not null)
                services.Remove(contentCtxDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(ConnStr));

            services.AddDbContext<ContentDbContext>(options =>
                options.UseSqlite(ConnStr));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure the Identity + game schema exists before any test sends a request.
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        sp.GetRequiredService<ContentDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _keepAlive.Dispose();
        base.Dispose(disposing);
    }
}

