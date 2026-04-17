using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
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
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Admin;
using Veldrath.Server.Features.Account;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Features.Announcements;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Editorial;
using Veldrath.Server.Features.Content;
using Veldrath.Server.Features.Foundry;
using Veldrath.Server.Features.Reports;
using Veldrath.Server.Features.Players;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Infrastructure.Email;
using Veldrath.Server.Services;
using Veldrath.Server.Health;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using RealmEngine.Shared.Abstractions;
using System.Text;
using Prometheus;
using Prometheus.DotNetRuntime;

// Read Seq URL early so it can be wired into the bootstrap logger before the host is built.
var bootstrapConfig = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .Build();

var seqServerUrl = bootstrapConfig["Seq:ServerUrl"];

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Veldrath.Server")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/veldrath-server-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7);

if (!string.IsNullOrEmpty(seqServerUrl))
    loggerConfig = loggerConfig.WriteTo.Seq(seqServerUrl);

Log.Logger = loggerConfig.CreateLogger();

try
{
    Log.Information("Veldrath.Server starting...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Cap request body at 512 KB to prevent unbounded memory exhaustion on large payloads.
    // SignalR frames have their own internal limits; this covers REST endpoints.
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 524_288);

    // SignalR
    builder.Services.AddSignalR();

    // CORS — allow Avalonia client on loopback during development
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowLocalClient", policy =>
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
                  // Explicit allow-list prevents TRACE/CONNECT and limits header exposure.
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                  .WithHeaders("Authorization", "Content-Type", "X-Requested-With")
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

    // Editorial content: patch notes, lore articles, and site announcements.
    builder.Services.AddDbContext<EditorialDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ASP.NET Core Identity
    builder.Services.AddIdentity<PlayerAccount, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireNonAlphanumeric = true;
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
    {
        // Legacy Curator role policy (kept for backward compat with existing Foundry endpoints).
        options.AddPolicy("Curator", p => p.RequireRole(Roles.Curator));

        // Fine-grained permission policies — each permission string maps to a JWT claim.
        foreach (var permission in Permissions.All)
            options.AddPolicy(permission, p => p.RequireClaim("permission", permission));
    });

    builder.Services.Configure<Veldrath.Server.Settings.ModerationOptions>(
        builder.Configuration.GetSection("Moderation"));

    // AuthExchangeCodeService uses an internal ConcurrentDictionary — no IMemoryCache dependency.
    builder.Services.AddSingleton<AuthExchangeCodeService>();

    var foundryWriteLimit  = builder.Configuration.GetValue<int>("RateLimit:FoundryWritesPerMinute", 5);
    var adminActionsLimit  = builder.Configuration.GetValue<int>("RateLimit:AdminActionsPerMinute",  20);
    var authAttemptsLimit  = builder.Configuration.GetValue<int>("RateLimit:AuthAttemptsPerMinute",  10);
    var hubCommandsLimit   = builder.Configuration.GetValue<int>("RateLimit:HubCommandsPerMinute",   120);
    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddFixedWindowLimiter("foundry-writes", o =>
        {
            o.Window = TimeSpan.FromMinutes(1);
            o.PermitLimit = foundryWriteLimit;
        });
        opts.AddFixedWindowLimiter("admin-actions", o =>
        {
            o.Window = TimeSpan.FromMinutes(1);
            o.PermitLimit = adminActionsLimit;
        });
        // Protects login, register, and refresh from brute-force and credential stuffing.
        opts.AddFixedWindowLimiter("auth-attempts", o =>
        {
            o.Window = TimeSpan.FromMinutes(1);
            o.PermitLimit = authAttemptsLimit;
        });
        // Throttles authenticated SignalR hub commands to prevent spam from modded clients.
        opts.AddFixedWindowLimiter("hub-commands", o =>
        {
            o.Window = TimeSpan.FromMinutes(1);
            o.PermitLimit = hubCommandsLimit;
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
                // Correlation cookie must not require Secure on plain HTTP (Docker dev).
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.CorrelationCookie.SameSite   = SameSiteMode.Lax;
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
                // Correlation cookie must not require Secure on plain HTTP (Docker dev).
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.CorrelationCookie.SameSite   = SameSiteMode.Lax;
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
                // /consumers/ targets personal Microsoft accounts (Xbox/Live/Outlook) only,
                // which avoids the userAudience mismatch that /common/ produces with a
                // single-tenant app registration.
                o.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
                o.TokenEndpoint         = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
                o.Events.OnTicketReceived = ExternalAuthEndpoints.HandleOAuthTicket;
                // Correlation cookie must not require Secure on plain HTTP (Docker dev).
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.CorrelationCookie.SameSite   = SameSiteMode.Lax;
            });


    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<AccountService>();
    builder.Services.AddScoped<FoundryService>();
    builder.Services.AddScoped<AccountLinkService>();

    // IEmailSender: use NullEmailSender when SmtpHost is not configured (dev / CI).
    // Switch to SmtpEmailSender by setting Email:SmtpHost in the environment or user-secrets.
    var smtpHost = builder.Configuration["Email:SmtpHost"];
    if (string.IsNullOrWhiteSpace(smtpHost))
        builder.Services.AddScoped<IEmailSender, NullEmailSender>();
    else
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

    // Repositories
    builder.Services.AddScoped<IPlayerAccountRepository, PlayerAccountRepository>();
    builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
    builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
    builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
    builder.Services.AddScoped<IPendingLinkRepository, EfCorePendingLinkRepository>();
    // Engine interfaces backed by the server's own ApplicationDbContext:
    builder.Services.AddScoped<ISaveGameRepository, ServerSaveGameRepository>();
    builder.Services.AddScoped<IHallOfFameRepository, ServerHallOfFameRepository>();
    builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
    builder.Services.AddScoped<IPlayerSessionRepository, PlayerSessionRepository>();
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
    builder.Services.AddScoped<Veldrath.Server.Features.Characters.Combat.ActorPoolResolver>();
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
    builder.Services.AddScoped<IDialogueRepository, EfCoreDialogueRepository>();
    builder.Services.AddScoped<IActorInstanceRepository, EfCoreActorInstanceRepository>();
    builder.Services.AddScoped<IMaterialPropertyRepository, EfCoreMaterialPropertyRepository>();
    builder.Services.AddScoped<ITraitDefinitionRepository, EfCoreTraitDefinitionRepository>();
    builder.Services.AddScoped<ILanguageRepository, EfCoreLanguageRepository>();

    // Version compatibility — MinCompatibleClientVersion is stamped into appsettings.Production.json
    // at Release publish time by the MSBuild target in Veldrath.Server.csproj.
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
        .SetApplicationName("Veldrath.Server");

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "database", tags: ["db", "postgres"])
        .AddCheck<GameEngineHealthCheck>("game-engine", tags: ["engine"]);

    var app = builder.Build();

    // Collect .NET runtime metrics (GC, thread pool, exceptions, contention) for Grafana.
    // Skip in test environment: StartCollecting() registers global event-source listeners and
    // throws if called a second time while a previous collector is still alive.  Parallel
    // WebApplicationFactory instances in integration tests would otherwise collide here.
    if (!app.Environment.IsEnvironment("Test"))
    {
        var runtimeCollector = DotNetRuntimeStatsBuilder.Default().StartCollecting();
        app.Lifetime.ApplicationStopping.Register(runtimeCollector.Dispose);
    }

    // Trust the Caddy reverse-proxy's forwarded headers so Request.Scheme is https
    // and Request.Host is the public hostname.  Required for correct OAuth redirect
    // URL construction.  Safe to clear KnownNetworks because only Caddy reaches the
    // container in production (no direct port exposure).
    var fwdOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    fwdOptions.KnownIPNetworks.Clear();
    fwdOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(fwdOptions);

    app.UseExceptionHandler(handler => handler.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature?.Error is not null)
        {
            var exLogger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Veldrath.Server.UnhandledExceptions");
            exLogger.LogError(exceptionFeature.Error, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        await Results.Problem(
            title: "An unexpected error occurred.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    }));
    app.UseSerilogRequestLogging();
    app.UseHttpMetrics();

    // Security headers on every response.
    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
        ctx.Response.Headers["X-Frame-Options"]        = "DENY";
        ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
        // API-only server: no content to render, so a strict CSP is safe.
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        await next(ctx);
    });

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
        var editorialDb = services.GetRequiredService<EditorialDbContext>();

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
                .Concat(editorialDb.Database.GetMigrations())
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

            await RepairStaleMigrationsAsync(editorialDb, app.Environment.IsDevelopment(), allKnown);
            await editorialDb.Database.MigrateAsync();

            // Seed baseline content (idempotent — skips each table that already has rows).
            await DatabaseSeeder.SeedAsync(services);

        }

        // Seed all identity roles and their default permission claims.
        // Skipped for non-Postgres providers (SQLite in tests): the test host calls EnsureCreated()
        // only after base.CreateHost() returns, so the schema does not exist yet at this point.
        // WebAppFactory.CreateHost() seeds roles explicitly after EnsureCreated().
        if (appDb.Database.ProviderName?.Contains("Npgsql") == true)
            await DatabaseSeeder.SeedRolesAsync(scope.ServiceProvider);
    }

    // Auth, character, zone, content & admin endpoints
    app.MapAuthEndpoints();
    app.MapPasswordResetEndpoints();
    app.MapEmailConfirmationEndpoints();
    app.MapExternalAuthEndpoints();
    app.MapPendingLinkEndpoints();
    app.MapSessionEndpoints();
    app.MapAccountEndpoints();
    app.MapAnnouncementEndpoints();
    app.MapFoundryEndpoints();
    app.MapReportEndpoints();
    app.MapCharacterEndpoints();
    app.MapCharacterCreationSessionEndpoints();
    app.MapZoneEndpoints();
    app.MapContentEndpoints();
    app.MapAdminEndpoints();
    app.MapPlayerEndpoints();
    app.MapEditorialEndpoints();

    // Hubs
    app.MapHub<GameHub>("/hubs/game").RequireRateLimiting("hub-commands");

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

    // Prometheus metrics — expose /metrics for scraping.
    // In production, restrict access to this endpoint at the network/proxy level.
    app.MapMetrics();

    Log.Information("Veldrath.Server running at {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Veldrath.Server terminated unexpectedly");
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
