using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using RealmEngine.Core;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Repositories;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Services;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Auth;
using RealmUnbound.Server.Features.Announcements;
using RealmUnbound.Server.Features.Characters;
using RealmUnbound.Server.Features.Content;
using RealmUnbound.Server.Features.Foundry;
using RealmUnbound.Server.Features.Zones;
using RealmUnbound.Server.Services;
using RealmUnbound.Server.Health;
using RealmUnbound.Server.Hubs;
using RealmUnbound.Server.Settings;
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

    // Database (PostgreSQL — design-time and runtime both target Postgres)
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Game entities (saves, hall of fame, inventory) live in their own context
    // so they can also be used by standalone clients (Avalonia, Godot) without
    // pulling in ASP.NET Core Identity scaffolding.
    builder.Services.AddDbContext<GameDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ASP.NET Core Identity
    builder.Services.AddIdentity<PlayerAccount, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.User.RequireUniqueEmail = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // JWT authentication
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is not configured.");

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ClockSkew = TimeSpan.FromSeconds(30),
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

    builder.Services.AddAuthorization(options =>
        options.AddPolicy("Curator", p => p.RequireRole("Curator")));

    var foundryWriteLimit = builder.Configuration.GetValue<int>("RateLimit:FoundryWritesPerMinute", 5);
    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddFixedWindowLimiter("foundry-writes", o =>
        {
            o.Window = TimeSpan.FromMinutes(1);
            o.PermitLimit = foundryWriteLimit;
        });
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // OAuth providers
    // Registered conditionally so the server starts cleanly when credentials are
    // absent (CI, local dev without OAuth app registrations).
    // SignInScheme must be explicitly set to IdentityConstants.ExternalScheme so
    // the OAuth middleware writes the external cookie that GetExternalLoginInfoAsync reads.
    var discordId = builder.Configuration["OAuth:Discord:ClientId"];
    var discordSecret = builder.Configuration["OAuth:Discord:ClientSecret"];
    if (!string.IsNullOrEmpty(discordId) && !string.IsNullOrEmpty(discordSecret))
        builder.Services.AddAuthentication()
            .AddDiscord(o =>
            {
                o.SignInScheme = IdentityConstants.ExternalScheme;
                o.ClientId = discordId;
                o.ClientSecret = discordSecret;
                o.Scope.Add("email");
                o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
            });

    var googleId = builder.Configuration["OAuth:Google:ClientId"];
    var googleSecret = builder.Configuration["OAuth:Google:ClientSecret"];
    if (!string.IsNullOrEmpty(googleId) && !string.IsNullOrEmpty(googleSecret))
        builder.Services.AddAuthentication()
            .AddGoogle(o =>
            {
                o.SignInScheme = IdentityConstants.ExternalScheme;
                o.ClientId = googleId;
                o.ClientSecret = googleSecret;
                o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
            });

    var msId = builder.Configuration["OAuth:Microsoft:ClientId"];
    var msSecret = builder.Configuration["OAuth:Microsoft:ClientSecret"];
    if (!string.IsNullOrEmpty(msId) && !string.IsNullOrEmpty(msSecret))
        builder.Services.AddAuthentication()
            .AddMicrosoftAccount(o =>
            {
                o.SignInScheme = IdentityConstants.ExternalScheme;
                o.ClientId = msId;
                o.ClientSecret = msSecret;
                o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
            });


    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<FoundryService>();

    // Repositories
    builder.Services.AddScoped<IPlayerAccountRepository, PlayerAccountRepository>();
    builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
    // Engine interfaces backed by the server's own ApplicationDbContext:
    builder.Services.AddScoped<ISaveGameRepository, ServerSaveGameRepository>();
    builder.Services.AddScoped<IHallOfFameRepository, ServerHallOfFameRepository>();
    builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
    builder.Services.AddScoped<IZoneSessionRepository, ZoneSessionRepository>();
    builder.Services.AddScoped<IRegionRepository, RegionRepository>();
    builder.Services.AddScoped<IWorldRepository, WorldRepository>();
    builder.Services.AddSingleton<IActiveCharacterTracker, ActiveCharacterTracker>();
    builder.Services.AddSingleton<ICharacterCreationSessionStore, InMemoryCharacterCreationSessionStore>();
    builder.Services.AddSingleton<IZoneEntityTracker, ZoneEntityTracker>();
    builder.Services.AddSingleton<RealmEngine.Shared.Abstractions.ITileMapRepository>(sp => new RealmEngine.Data.Repositories.CompositeITileMapRepository(
        Path.Combine(AppContext.BaseDirectory, "GameAssets", "tilemaps", "maps"),
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RealmEngine.Data.Repositories.CompositeITileMapRepository>>()));

    // RealmEngine services
    builder.Services.AddRealmEngineMediatR();
    builder.Services.AddRealmEngineCore(p => p.UseExternal());
    builder.Services.AddScoped<RealmUnbound.Server.Features.Characters.Combat.ActorPoolResolver>();
    // Register server-local MediatR handlers (hub commands such as GainExperienceHubCommand).
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddHostedService<CatalogInitializationService>();
    builder.Services.AddHostedService<SessionCleanupService>();
    builder.Services.AddHostedService<EnemyAiService>();

    // Content catalog repos — backed by ContentDbContext sharing the same Postgres schema.
    builder.Services.AddDbContextFactory<ContentDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddSingleton<GameConfigService, DbGameConfigService>();
    builder.Services.AddScoped<IBackgroundRepository, EfCoreBackgroundRepository>();
    builder.Services.AddScoped<ICharacterClassRepository, EfCoreCharacterClassRepository>();
    builder.Services.AddScoped<IPowerRepository, EfCorePowerRepository>();
    builder.Services.AddScoped<IEnemyRepository, EfCoreEnemyRepository>();
    builder.Services.AddScoped<INpcRepository, EfCoreNpcRepository>();
    builder.Services.AddScoped<IQuestRepository, EfCoreQuestRepository>();
    builder.Services.AddScoped<IRecipeRepository, EfCoreRecipeRepository>();
    builder.Services.AddScoped<ILootTableRepository, EfCoreLootTableRepository>();
    // ISpellRepository merged into IPowerRepository above
    builder.Services.AddScoped<ISkillRepository, EfCoreSkillRepository>();
    builder.Services.AddScoped<IMaterialRepository, EfCoreMaterialRepository>();
    builder.Services.AddScoped<IInventoryService, EfCoreInventoryService>();
    builder.Services.AddScoped<IEquipmentSetRepository, EfCoreEquipmentSetRepository>();
    builder.Services.AddScoped<INamePatternRepository, EfCoreNamePatternRepository>();
    builder.Services.AddScoped<ISpeciesRepository, EfCoreSpeciesRepository>();
    builder.Services.AddScoped<IItemRepository, EfCoreItemRepository>();
    builder.Services.AddScoped<IEnchantmentRepository, EfCoreEnchantmentRepository>();
    builder.Services.AddScoped<INodeRepository, EfCoreNodeRepository>();
    builder.Services.AddScoped<IOrganizationRepository, EfCoreOrganizationRepository>();
    builder.Services.AddScoped<IZoneLocationRepository, EfCoreZoneLocationRepository>();
    builder.Services.AddScoped<ICharacterUnlockedLocationRepository, CharacterUnlockedLocationRepository>();
    builder.Services.AddScoped<ICharacterUnlockedConnectionRepository, CharacterUnlockedConnectionRepository>();
    builder.Services.AddScoped<IDialogueRepository, EfCoreDialogueRepository>();
    builder.Services.AddScoped<IActorInstanceRepository, EfCoreActorInstanceRepository>();
    builder.Services.AddScoped<IMaterialPropertyRepository, EfCoreMaterialPropertyRepository>();
    builder.Services.AddScoped<ITraitDefinitionRepository, EfCoreTraitDefinitionRepository>();

    // Version compatibility — MinCompatibleClientVersion is stamped into appsettings.Production.json
    // at Release publish time by the MSBuild target in RealmUnbound.Server.csproj.
    // Post-deploy override (no redeploy): VersionCompatibility__MinCompatibleClientVersion env var.
    builder.Services.Configure<VersionCompatibilitySettings>(
        builder.Configuration.GetSection("VersionCompatibility"));

    // Cookie policy
    // When running on plain HTTP (Docker dev, CI), browsers reject SameSite=None
    // cookies without the Secure flag, and will not send Secure cookies over HTTP.
    // That breaks the OAuth correlation cookie and makes GetExternalLoginInfoAsync
    // return null → 401. SameAsRequest + None→Lax fallback fixes this without
    // requiring HTTPS in development.
    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
        options.Secure = CookieSecurePolicy.SameAsRequest;
        options.OnAppendCookie = ctx =>
        {
            if (!ctx.Context.Request.IsHttps && ctx.CookieOptions.SameSite == SameSiteMode.None)
                ctx.CookieOptions.SameSite = SameSiteMode.Lax;
        };
    });

    // DataProtection key persistence
    // Keys are used to encrypt OAuth correlation and external cookies.
    // Persisting to a mounted volume prevents key loss on container restart.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .SetApplicationName("RealmUnbound.Server");

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "database", tags: ["db", "postgres"])
        .AddCheck<GameEngineHealthCheck>("game-engine", tags: ["engine"]);

    var app = builder.Build();

    app.UseExceptionHandler(handler => handler.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is not null)
        {
            var exLogger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RealmUnbound.Server.UnhandledExceptions");
            exLogger.LogError(exceptionFeature.Error, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        await Results.Problem(
            title: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    }));
    app.UseSerilogRequestLogging();
    app.UseCors("AllowLocalClient");
    app.UseCookiePolicy();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // Run any pending EF migrations on startup (creates DB on first run, updates schema on upgrade)
    // Skip all migrations and seeding for non-Postgres providers (e.g. SQLite in test environments,
    // which call EnsureCreated() on the test host after startup).
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var appDb = services.GetRequiredService<ApplicationDbContext>();
        var gameDb = services.GetRequiredService<GameDbContext>();

        if (appDb.Database.ProviderName?.Contains("Npgsql") == true)
        {
            // ContentDbContext uses Npgsql-specific SQL (JSONB, etc.).
            var contentDb = services.GetRequiredService<ContentDbContext>();

            // Build the union of all migrations known to every context that shares this database.
            // RepairStaleMigrationsAsync uses this to avoid treating another context's valid
            // migrations as stale entries to be deleted.
            var allKnown = appDb.Database.GetMigrations()
                .Concat(contentDb.Database.ProviderName?.Contains("Npgsql") == true
                    ? contentDb.Database.GetMigrations()
                    : [])
                .Concat(gameDb.Database.GetMigrations())
                .ToHashSet(StringComparer.Ordinal);

            await RepairStaleMigrationsAsync(appDb, app.Environment.IsDevelopment(), allKnown);
            await appDb.Database.MigrateAsync();
            await DatabaseSeeder.SeedApplicationDataAsync(services);

            await RepairStaleMigrationsAsync(gameDb, app.Environment.IsDevelopment(), allKnown);
            await gameDb.Database.MigrateAsync();

            if (contentDb.Database.ProviderName?.Contains("Npgsql") == true)
            {
                await RepairStaleMigrationsAsync(contentDb, app.Environment.IsDevelopment(), allKnown);
                await contentDb.Database.MigrateAsync();
            }

            // Seed baseline content (idempotent — skips each table that already has rows).
            await DatabaseSeeder.SeedAsync(services);

            // Ensure the Curator identity role exists (idempotent).
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync("Curator"))
                await roleManager.CreateAsync(new IdentityRole<Guid>("Curator"));
        }
    }

    // Auth, character, zone & content catalog endpoints
    app.MapAuthEndpoints();
    app.MapExternalAuthEndpoints();
    app.MapAnnouncementEndpoints();
    app.MapFoundryEndpoints();
    app.MapCharacterEndpoints();
    app.MapCharacterCreationSessionEndpoints();
    app.MapZoneEndpoints();
    app.MapContentEndpoints();

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

// Detects migrations in __EFMigrationsHistory that no longer exist in the codebase
// (e.g., after migrations are squashed or renamed during development).  In that state,
// EF would try to re-apply the first known migration on top of existing tables and crash.
// Dev: drop the database so MigrateAsync can rebuild it cleanly.
// Prod: throw so a human can act — never silently drop production data.
static async Task RepairStaleMigrationsAsync(DbContext db, bool isDevelopment, IReadOnlySet<string>? allKnownMigrations = null)
{
    if (!await db.Database.CanConnectAsync())
        return;

    IEnumerable<string> applied;
    try
    {
        applied = await db.Database.GetAppliedMigrationsAsync();
    }
    catch
    {
        // History table doesn't exist yet — nothing to repair.
        return;
    }

    // Use the combined known set when provided so that migrations belonging to
    // another DbContext sharing the same __EFMigrationsHistory table are not
    // incorrectly treated as stale entries from this context.
    var globalKnown = allKnownMigrations ?? db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    var stale = applied.Where(m => !globalKnown.Contains(m)).ToList();

    if (stale.Count == 0)
        return;

    if (!isDevelopment)
        throw new InvalidOperationException(
            $"Database contains migration(s) that no longer exist in the codebase: {string.Join(", ", stale)}. " +
            "Reconcile manually with 'dotnet ef database update'.");

    Log.Warning(
        "Stale migration history detected — {Count} migration(s) not found in codebase: {Migrations}. " +
        "Dropping and recreating the development database...",
        stale.Count, string.Join(", ", stale));

    await db.Database.EnsureDeletedAsync();
    Log.Information("Development database dropped; migrations will be re-applied from scratch.");
}

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
