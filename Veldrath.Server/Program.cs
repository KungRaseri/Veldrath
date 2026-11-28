using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using Prometheus.DotNetRuntime;
using RealmEngine.Core;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Repositories;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Abstractions;
using Serilog;
using Serilog.Events;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Features.Account;
using Veldrath.Server.Features.Admin;
using Veldrath.Server.Features.Announcements;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Chests;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Content;
using Veldrath.Server.Features.Dev;
using Veldrath.Server.Features.Editorial;
using Veldrath.Server.Features.Foundry;
using Veldrath.Server.Features.Party;
using Veldrath.Server.Features.Players;
using Veldrath.Server.Features.Reports;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Health;
using Veldrath.Server.Hubs;
using Veldrath.Server.Infrastructure;
using Veldrath.Server.Infrastructure.Email;
using Veldrath.Server.Services;
using Veldrath.Server.Settings;

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

    // Local `dotnet run` does not read .env automatically; Docker Compose injects the
    // same values directly. Development-only and never overrides existing variables.
    DotEnvConfiguration.LoadForDevelopment(Directory.GetCurrentDirectory());

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Cap request body at 512 KB to prevent unbounded memory exhaustion on large payloads.
    // SignalR frames have their own internal limits; this covers REST endpoints.
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 524_288);

    // SignalR
    builder.Services.AddSignalR(options =>
    {
        // Invocation attempts are invisible to the HTTP limiter, so an opt-in filter counts them separately.
        options.AddFilter<RateLimitObservationFilter>();
        // A connection established before a ban keeps working otherwise; this refuses the next invocation.
        options.AddFilter<BanGuardFilter>();
        // Invocations travel inside the upgraded WebSocket frame, where `hub-commands` cannot see them; this
        // charges each one to its channel's per-account budget and refuses with a HubException (the
        // connection stays open). Runs after the ban guard so a banned caller is refused for that reason.
        options.AddFilter<HubInvocationRateLimitFilter>();
    });

    // CORS — browser clients (SvelteKit dev server, Docker web, production domain).
    // Resolves the allow-list, logs it, and fails fast in Production when it is localhost-only.
    builder.AddVeldrathCors();

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

    // ASP.NET Core Identity + JWT bearer authentication + authorization policies + the optional
    // Identity (for RBAC) + JWT validation of Better Auth tokens. No OAuth providers, no password
    // login: Better Auth owns credentials. Details in Infrastructure/AuthSetup.cs.
    builder.AddVeldrathIdentityAndAuth();

    builder.Services.Configure<Veldrath.Server.Settings.ModerationOptions>(
        builder.Configuration.GetSection("Moderation"));

    // Permits are per caller, not per process. There was no traffic to size them from on 2026-11-27:
    // production held 4 accounts, 3 characters and 0 player sessions, and answered 0 of 18,778 proxy log
    // lines with 429. These are therefore stated capacity assumptions, not observations (comment 717 on
    // t_66e06bcf). The local harness measured what the previous process-wide buckets did: one account spent
    // the whole `hub-commands` allowance and a second account was rejected on its first request, and
    // malformed `foundry-writes` requests spent permits too. Each value is roughly three times the
    // legitimate client burst that harness saw. A hub connection costs two permits (negotiate plus transport
    // upgrade) and the browser reconnect cycle can spend eight more, so 30 covers connecting and reconnecting;
    // a foundry submission is deliberate and rare, so its smaller burst is still generous; admin actions are
    // burstier but still per operator. Revisit against observed rates once a deployment carries real players.
    var foundryWriteLimit = builder.Configuration.GetValue<int>("RateLimit:FoundryWritesPerMinute", 20);
    var adminActionsLimit = builder.Configuration.GetValue<int>("RateLimit:AdminActionsPerMinute", 40);
    var hubCommandsLimit = builder.Configuration.GetValue<int>("RateLimit:HubCommandsPerMinute", 30);

    // Measurement is opt-in and never changes enforcement: the three named policies below keep their permits,
    // windows, assignments and rejection behaviour whether or not anything is observing them.
    builder.Services.AddSingleton(provider => new RateLimitObservation(
        builder.Configuration,
        provider.GetRequiredService<ILogger<RateLimitObservation>>()));

    // Per-account budgets for hub invocations, which the HTTP policies cannot meter. Channels and their
    // permits are documented on HubInvocationRateLimiter; config keys override the measured defaults.
    builder.Services.AddSingleton(provider => new HubInvocationRateLimiter(builder.Configuration));

    builder.Services.AddRateLimiter(opts =>
    {
        // One bucket per caller per policy: the authenticated account subject, or the client address when a
        // request carries no usable subject (Infrastructure/RateLimitPartitionKey). The policy names and config
        // keys are unchanged, so the observation middleware and the OnRejected hook keep reporting.
        opts.AddPolicy("foundry-writes", PolicyPartition(foundryWriteLimit));
        opts.AddPolicy("admin-actions", PolicyPartition(adminActionsLimit));
        // Meters SignalR connection establishment (negotiate plus transport upgrade). Command invocations travel
        // inside the upgraded WebSocket frame and are metered separately, per account and channel, by
        // HubInvocationRateLimitFilter.
        opts.AddPolicy("hub-commands", PolicyPartition(hubCommandsLimit));
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Records which policy and route template rejected a request. A no-op unless RateLimit:Observe is on.
        opts.OnRejected = (rejected, _) =>
        {
            var observation = rejected.HttpContext.RequestServices.GetService<RateLimitObservation>();
            var policy = RateLimitObservation.PolicyFor(rejected.HttpContext);
            if (observation is not null && policy is not null)
                observation.RecordRejection(policy, RateLimitObservation.RouteLabelFor(rejected.HttpContext));

            return ValueTask.CompletedTask;
        };
    });

    builder.Services.AddScoped<AccountService>();
    // Provisions the Identity row (roles/permissions) for a Better Auth account on first token use.
    builder.Services.AddScoped<AccountProvisioningService>();
    // The moderation guards read the same snapshot through the interface, so both token shapes and the hub filter
    // consult one per-process view of the account's ban state.
    builder.Services.AddScoped<IAccountAccessReader>(provider =>
        provider.GetRequiredService<AccountProvisioningService>());
    builder.Services.AddScoped<FoundryService>();
    builder.Services.AddScoped<FoundryNotificationWriter>();
    // The content write path, shared by the editor's endpoints and Foundry's review gate.
    builder.Services.AddScoped<Veldrath.Server.Features.Content.ContentWriteService>();

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
    builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
    // Engine interfaces backed by the server's own ApplicationDbContext:
    builder.Services.AddScoped<ISaveGameRepository, ServerSaveGameRepository>();
    builder.Services.AddScoped<IHallOfFameRepository, ServerHallOfFameRepository>();
    builder.Services.AddScoped<IZoneRepository, ZoneRepository>();
    builder.Services.AddScoped<IEnemyInstanceStore, EfCoreEnemyInstanceStore>();
    builder.Services.AddScoped<ICombatSessionStore, EfCoreCombatSessionStore>();
    builder.Services.AddScoped<Veldrath.Server.Features.Zones.IZoneInstanceService,
        Veldrath.Server.Features.Zones.EfCoreZoneInstanceService>();
    // The zone's lobby rule lives in one place so the reconnect and the respawn cannot resolve a hidden safe
    // location differently.
    builder.Services.AddScoped<Veldrath.Server.Features.Zones.IZoneLobbyResolver,
        Veldrath.Server.Features.Zones.ZoneLobbyResolver>();

    // AFK is ephemeral by design: in-memory for the process lifetime, cleared on any action or reconnect.
    builder.Services.AddSingleton<IAfkTracker, AfkTracker>();
    builder.Services.AddScoped<PartyStore>();
    builder.Services.AddScoped<IPartyCombatQuery>(provider => provider.GetRequiredService<PartyStore>());

    // World tuning is validated at startup: a missing or nonsensical value fails the host rather than silently
    // falling back to a default.
    builder.Services.AddOptions<EnemyWorldOptions>()
        .Bind(builder.Configuration.GetSection(EnemyWorldOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // How long an emptied resource node stays empty; applied by the chest sweeper below.
    builder.Services.AddOptions<HarvestingOptions>()
        .Bind(builder.Configuration.GetSection(HarvestingOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Chest reopen window, roaming cadence and per-zone cap. The sweeper rides its own timer because chests
    // have to appear on schedule whether or not anyone is standing there.
    builder.Services.AddOptions<ChestOptions>()
        .Bind(builder.Configuration.GetSection(ChestOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddHostedService<ChestService>();
    builder.Services.AddHostedService<EnemyRespawnService>();
    // Registered as both: the host runs it once at startup, and the world-health endpoint runs it on demand.
    builder.Services.AddSingleton<ContentAuditService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ContentAuditService>());

    builder.Services.AddOptions<PartyOptions>()
        .Bind(builder.Configuration.GetSection(PartyOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<DeathPenaltyOptions>()
        .Bind(builder.Configuration.GetSection(DeathPenaltyOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<TutorialOptions>()
        .Bind(builder.Configuration.GetSection(TutorialOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    builder.Services.AddScoped<IPlayerSessionRepository, PlayerSessionRepository>();
    builder.Services.AddScoped<IGroundItemRepository, EfCoreGroundItemRepository>();
    builder.Services.AddScoped<IRegionRepository, RegionRepository>();
    builder.Services.AddScoped<IWorldRepository, WorldRepository>();
    builder.Services.AddSingleton<IActiveCharacterTracker, ActiveCharacterTracker>();
    builder.Services.Configure<Veldrath.Server.Settings.ActiveCharacterTrackerOptions>(
        builder.Configuration.GetSection("ActiveCharacterTracker"));
    builder.Services.AddSingleton<ICharacterCreationSessionStore, InMemoryCharacterCreationSessionStore>();

    // RealmEngine services
    builder.Services.AddRealmEngineMediatR();
    builder.Services.AddRealmEngineCore(p => p.UseExternal());
    builder.Services.AddScoped<Veldrath.Server.Features.Characters.Combat.ActorPoolResolver>();
    // One roster read for every arrival: a walk and a resume must not be able to spawn two cohorts for one room.
    builder.Services.AddScoped<Veldrath.Server.Features.Zones.LocationRosterReader>();
    builder.Services.AddScoped<Veldrath.Server.Features.Content.MapEditorService>();
    // Shop gate: the merchant standing with a character, and the stock their town offers.
    builder.Services.AddScoped<Veldrath.Server.Features.Shop.IShopMerchantResolver,
        Veldrath.Server.Features.Shop.ShopMerchantResolver>();
    builder.Services.AddScoped<Veldrath.Server.Features.Shop.IZoneStockReader,
        Veldrath.Server.Features.Shop.ZoneStockReader>();
    // Register server-local MediatR handlers (hub commands such as GainExperienceHubCommand).
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
    builder.Services.AddHostedService<CatalogInitializationService>();
    builder.Services.AddHostedService<SessionCleanupService>();

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
    builder.Services.AddScoped<IMapGraphRepository, EfCoreMapGraphRepository>();
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
    // ASP.NET Core persists keys to $HOME/.aspnet/DataProtection-Keys by default,
    // which is /root/.aspnet/DataProtection-Keys inside the container where the
    // compose volume is mounted. Set DataProtection:KeysPath to override (for example,
    // a writable location when running natively on Linux as a non-root user).
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("Veldrath.Server");
    var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
    if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

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
        ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        // API-only server: no content to render, so a strict CSP is safe.
        ctx.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        await next(ctx);
    });

    app.UseCors(CorsSetup.PolicyName);
    app.UseCookiePolicy();
    app.UseAuthentication();
    app.UseAuthorization();
    // Counts eligible HTTP attempts before the limiter decides; forwards the request unchanged either way.
    app.UseMiddleware<RateLimitObservationMiddleware>();
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

            // The seeders above stop as soon as a table has a row, so a database migrated before a prose change
            // keeps the old text forever. The sweep rewrites only fields that still carry a retired canon token,
            // which makes it a no-op on a fresh database (just seeded with the current text) and never touches
            // text an author edited later.
            var canonSweep = await WorldCanonSweep.SweepAsync(appDb, contentDb);

            if (canonSweep.FieldsChanged > 0)
            {
                app.Logger.LogWarning(
                    "World canon sweep rewrote {FieldCount} field(s) across {RowCount} row(s): {Fields}",
                    canonSweep.FieldsChanged,
                    canonSweep.RowsChanged,
                    string.Join(", ", canonSweep.ChangedFields));
            }

            if (canonSweep.UnresolvedFields.Count > 0)
            {
                app.Logger.LogWarning(
                    "World canon sweep found {FieldCount} field(s) still carrying retired lore with no canon entry: {Fields}",
                    canonSweep.UnresolvedFields.Count,
                    string.Join(", ", canonSweep.UnresolvedFields));
            }

            // A canon-covered field that differs from the shipped text without a retired token is either a reword
            // whose old value nobody recorded in WorldCanonText.KnownStaleValues, or a manual edit of a row only the
            // seeder writes. Either way it stays as it is and is named here, so a migrated database cannot silently
            // keep text the fresh seed no longer produces.
            if (canonSweep.DriftedFields.Count > 0)
            {
                app.Logger.LogWarning(
                    "World canon sweep found {FieldCount} field(s) that differ from the shipped canon but hold no retired token; left unchanged: {Fields}",
                    canonSweep.DriftedFields.Count,
                    string.Join("; ", canonSweep.DriftedFields));
            }

            // Enemy rows written before the roster scope was derived from the destination zone can stand under the
            // group of the zone the character walked out of. Nothing rewrites them, so they stay visible to a read
            // forever; the sweep deletes only rows whose group names no zone that owns their location, and leaves a
            // slug the catalog no longer holds alone rather than deleting state on absence.
            var zonesByLocationSlug = (await services.GetRequiredService<IZoneLocationRepository>().GetAllAsync())
                .GroupBy(location => location.Slug, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlySet<string>)group
                        .Select(location => location.ZoneId)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var foreignEnemyRows = await services
                .GetRequiredService<IEnemyInstanceStore>()
                .RemoveForeignZoneGroupRowsAsync(zonesByLocationSlug);

            if (foreignEnemyRows > 0)
            {
                app.Logger.LogWarning(
                    "Enemy roster scope sweep deleted {RowCount} row(s) stored under a group that names no zone owning their location",
                    foreignEnemyRows);
            }

            // Authored chests live in the application database, beside the ground items they resemble, and are
            // idempotent by chest id so a restart never refills a chest a player emptied a moment ago.
            await Veldrath.Server.Data.Seeders.LootChestsSeeder.SeedAsync(appDb);

            // The people the world's own content names. Without placement, a `talk:` objective has nobody to
            // talk to — which is how the main-story quest became unfinishable.
            await Veldrath.Server.Data.Seeders.LocationNpcsSeeder.SeedAsync(appDb);

            // Authored resource nodes. Separate from the content seeders because node state lives in
            // GameDbContext, and it is idempotent by node id so a restart never restores a depleted node.
            await Veldrath.Server.Data.Seeders.HarvestableNodesSeeder.SeedAsync(gameDb);

        }

        // Seed all identity roles and their default permission claims.
        // Skipped for non-Postgres providers (SQLite in tests): the test host calls EnsureCreated()
        // only after base.CreateHost() returns, so the schema does not exist yet at this point.
        // WebAppFactory.CreateHost() seeds roles explicitly after EnsureCreated().
        if (appDb.Database.ProviderName?.Contains("Npgsql") == true)
            await DatabaseSeeder.SeedRolesAsync(scope.ServiceProvider);
    }

    // Character, zone, content & admin endpoints. The /api/auth/* group (register/login/refresh/
    // renew-jwt/logout/exchange/create-exchange-code, external OAuth challenge+callback, link
    // confirmation, password reset, email confirmation, and /api/auth/session) was the Avalonia
    // client's native Identity sign-in; Better Auth owns all of that now, so the group is gone and
    // only the RBAC-facing endpoints below remain.
    //
    // Transactional email for Better Auth password-reset and address-verification flows. The
    // .NET server owns the SMTP configuration, so the web app relays those messages through this
    // endpoint instead of running its own mailer. (The account-mirroring bridge that used to sit
    // next to it is gone: nothing signs in through Identity passwords any more.)
    app.MapInternalEmailEndpoints();
    // The /api/auth/* group (register/login/refresh/renew-jwt/logout/exchange/create-exchange-code,
    // external OAuth challenge+callback, link confirmation, password reset, email confirmation, and
    // /api/auth/session) was the Avalonia client's native Identity sign-in. Better Auth owns all of
    // that now, so the group is gone; only the RBAC-facing endpoints below remain.
    app.MapAccountEndpoints();
    app.MapAnnouncementEndpoints();
    app.MapFoundryEndpoints();
    app.MapReportEndpoints();
    app.MapCharacterEndpoints();
    app.MapCharacterCreationSessionEndpoints();
    app.MapZoneEndpoints();
    app.MapContentEndpoints();
    app.MapMapEditorEndpoints();
    app.MapContentWriteEndpoints();
    app.MapContentAuditEndpoints();
    app.MapAdminEndpoints();
    app.MapPlayerEndpoints();
    app.MapEditorialEndpoints();
    app.MapDevEndpoints();

    // Hubs
    app.MapHub<GameHub>("/hubs/game").RequireRateLimiting("hub-commands");

    // Health checks
    // /health        — overall status (Healthy / Degraded / Unhealthy). Public on purpose: the container
    //                  healthcheck, the e2e stack and any external monitor poll it, and the default writer
    //                  returns a single status word, nothing about the server's internals.
    // /health/detail — full JSON breakdown of every check, including each failing check's exception
    //                  message (component names, and whatever a driver puts in that message). It is an
    //                  operator surface, so it requires the admin.users permission. Nothing polls it: the
    //                  compose healthchecks and the e2e stack all wait on /health.
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
    }).RequireAuthorization(Permissions.ManageUsers);

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

// Builds one named policy's partitioner: a fixed-window bucket per caller, keyed by
// Infrastructure/RateLimitPartitionKey. The window is a burst guard, not a quota, so fixed windows are enough.
static Func<HttpContext, RateLimitPartition<string>> PolicyPartition(int permitLimit) =>
    context => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPartitionKey.For(context),
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = permitLimit,
        });

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
