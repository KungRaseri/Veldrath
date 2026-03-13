using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using RealmEngine.Core;
using RealmEngine.Data.Services;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Auth;
using RealmUnbound.Server.Features.Characters;
using RealmUnbound.Server.Features.Zones;
using RealmUnbound.Server.Services;
using RealmUnbound.Server.Health;
using RealmUnbound.Server.Hubs;
using RealmEngine.Shared.Abstractions;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RealmUnbound.Server")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/realmunbound-server-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("RealmUnbound.Server starting...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // SignalR
    builder.Services.AddSignalR();

    // CORS — allow Avalonia client on loopback during development
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowLocalClient", policy =>
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials());
    });

    // ── Database provider (sqlite = local dev, postgres = Docker/prod) ────────
    var dbProvider       = builder.Configuration["Database:Provider"] ?? "postgres";
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        if (dbProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
            options.UseSqlite(connectionString);
        else
            options.UseNpgsql(connectionString);

        // Migrations are generated against SQLite (design-time factory).
        // When running against Postgres the model-snapshot comparison detects
        // provider-specific column-type differences that aren't real schema changes.
        // The actual SQL migrations are applied correctly by MigrateAsync(), so
        // suppress the spurious warning.
        options.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    // ── ASP.NET Core Identity ─────────────────────────────────────────────────
    builder.Services.AddIdentity<PlayerAccount, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit           = true;
            options.Password.RequiredLength         = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase       = false;
            options.User.RequireUniqueEmail         = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // ── JWT authentication ────────────────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is not configured.");

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer          = true,
                ValidIssuer             = builder.Configuration["Jwt:Issuer"],
                ValidateAudience        = true,
                ValidAudience           = builder.Configuration["Jwt:Audience"],
                ClockSkew               = TimeSpan.FromSeconds(30),
            };

            // Allow SignalR to read the JWT from the query string (hub connections).
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var token = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) &&
                        ctx.Request.Path.StartsWithSegments("/hubs"))
                        ctx.Token = token;
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // ── Auth service ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<AuthService>();

    // ── Repositories ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<IPlayerAccountRepository, PlayerAccountRepository>();
    builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    // Engine interfaces backed by the server's own ApplicationDbContext:
    builder.Services.AddScoped<ISaveGameRepository, ServerSaveGameRepository>();
    builder.Services.AddScoped<IHallOfFameRepository, ServerHallOfFameRepository>();
    builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
    builder.Services.AddScoped<IZoneSessionRepository, ZoneSessionRepository>();
    builder.Services.AddSingleton<IActiveCharacterTracker, ActiveCharacterTracker>();

    // ── RealmEngine services ─────────────────────────────────────────────────
    var jsonDataPath = builder.Configuration["RealmEngine:DataPath"] ?? "Data/Json";
    builder.Services.AddRealmEngineData(jsonDataPath);
    builder.Services.AddRealmEngineMediatR();
    builder.Services.AddRealmEngineCore(p => p.UseExternal());

    // ── Health checks ─────────────────────────────────────────────────────────
    var healthChecks = builder.Services.AddHealthChecks();
    if (dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        healthChecks.AddNpgSql(connectionString, name: "database", tags: ["db", "postgres"]);
    healthChecks.AddCheck<GameEngineHealthCheck>("game-engine", tags: ["engine"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("AllowLocalClient");
    app.UseAuthentication();
    app.UseAuthorization();

    // Run any pending EF migrations on startup (creates DB on first run, updates schema on upgrade)
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Database.MigrateAsync();
    }

    // Auth & character endpoints
    app.MapAuthEndpoints();
    app.MapCharacterEndpoints();
    app.MapZoneEndpoints();

    // Hubs
    app.MapHub<GameHub>("/hubs/game");

    // Health checks
    // /health        — overall status (Healthy / Degraded / Unhealthy)
    // /health/detail — full JSON breakdown of every check
    app.MapHealthChecks("/health", new()
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy]   = StatusCodes.Status200OK,
            [HealthStatus.Degraded]  = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });

    app.MapHealthChecks("/health/detail", new()
    {
        ResponseWriter = HealthCheckResponseWriter.WriteResponse,
        ResultStatusCodes =
        {
            [HealthStatus.Healthy]   = StatusCodes.Status200OK,
            [HealthStatus.Degraded]  = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });

    Log.Information("RealmUnbound.Server running at {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RealmUnbound.Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
