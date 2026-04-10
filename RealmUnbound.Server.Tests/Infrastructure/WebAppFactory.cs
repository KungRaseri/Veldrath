using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RealmEngine.Core;
using RealmEngine.Core.Features.Progression.Services;
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
        // Rate limiting must not interfere with integration tests; set a very high permit limit.
        builder.UseSetting("RateLimit:FoundryWritesPerMinute",  "100000");
        builder.UseSetting("RateLimit:AdminActionsPerMinute",   "100000");
        builder.UseSetting("RateLimit:AuthAttemptsPerMinute",   "100000");

        // Replace the production Postgres contexts with in-memory SQLite instances.
        // EF Core 8+ registers IDbContextOptionsConfiguration<T> (not DbContextOptions<T>),
        // so we must remove those to avoid a dual-provider conflict when re-adding with SQLite.
        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations<ApplicationDbContext>(services);
            RemoveDbContextRegistrations<ContentDbContext>(services);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(ConnStr)
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            services.AddDbContext<ContentDbContext>(options =>
                options.UseSqlite(ConnStr)
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            // Catalog initialization runs before the SQLite schema is created (tables don't exist
            // yet at hosted-service startup). Tests seed catalog data directly, so remove it.
            var catalogInit = services.FirstOrDefault(
                d => d.ImplementationType == typeof(CatalogInitializationService));
            if (catalogInit is not null)
                services.Remove(catalogInit);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure the Identity + game schema exists before any test sends a request.
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        // EnsureCreated is a no-op once any tables exist in the shared SQLite in-memory
        // database, so call CreateTables directly to create ContentDbContext's schema.
        sp.GetRequiredService<ContentDbContext>().Database.GetService<IRelationalDatabaseCreator>().CreateTables();
        DatabaseSeeder.SeedApplicationDataAsync(sp).GetAwaiter().GetResult();
        DatabaseSeeder.SeedRolesAsync(sp).GetAwaiter().GetResult();

        // Initialize catalog singletons after the SQLite schema exists.
        host.Services.InitializeCatalogsAsync().GetAwaiter().GetResult();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _keepAlive.Dispose();
        base.Dispose(disposing);
    }

    // Removes both DbContextOptions<T> and IDbContextOptionsConfiguration<T> descriptors
    // so that re-adding the context with a different provider never causes a dual-provider conflict.
    private static void RemoveDbContextRegistrations<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<TContext>) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                 d.ServiceType.GenericTypeArguments[0] == typeof(TContext)))
            .ToList();

        foreach (var d in toRemove)
            services.Remove(d);
    }
}

