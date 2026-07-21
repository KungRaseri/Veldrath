using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using Veldrath.Auth.Blazor;
using Veldrath.GameClient.Components.Components.Layout;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Veldrath.Web;
using Veldrath.Web.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Veldrath.Web")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/veldrath-web-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Veldrath.Web starting...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();

    // HttpClient for calling Veldrath.Server APIs.
    var serverUrl = builder.Configuration["Veldrath:ServerUrl"];
    if (string.IsNullOrWhiteSpace(serverUrl))
        throw new InvalidOperationException(
            "Veldrath:ServerUrl is not configured. Set it in appsettings.json, " +
            "appsettings.{Environment}.json, or the Veldrath__ServerUrl environment variable.");

    // Scoped handler that intercepts 401 responses for automatic JWT renewal.
    // Registered as scoped so the SemaphoreSlim serialises refresh per-circuit.
    builder.Services.AddScoped<AuthDelegatingHandler>();

    // Primary authenticated client — all game API calls go through this pipeline.
    // AuthDelegatingHandler intercepts 401 responses for automatic token refresh.
    builder.Services.AddHttpClient("veldrath-web", client =>
        client.BaseAddress = new Uri(serverUrl))
        .AddHttpMessageHandler<AuthDelegatingHandler>();

    // Raw client with NO auth handler — used exclusively by AuthDelegatingHandler
    // for the renew-jwt call, avoiding circular interception.
    builder.Services.AddHttpClient("veldrath-web-raw", client =>
        client.BaseAddress = new Uri(serverUrl));

    builder.Services.AddScoped<VeldrathApiClient>(sp =>
        new VeldrathApiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("veldrath-web")));

    // Register the API client under its interface so RCL components can inject IGameApiClient.
    builder.Services.AddScoped<IGameApiClient>(sp =>
        sp.GetRequiredService<VeldrathApiClient>());

    builder.Services.AddScoped<AuthStateService>();

    // Register the base class so RCL components can inject AuthStateServiceBase.
    builder.Services.AddScoped<AuthStateServiceBase>(sp =>
        sp.GetRequiredService<AuthStateService>());

    // Register AuthStateService as the Blazor AuthenticationStateProvider so
    // [Authorize] attributes on pages and components are backed by real auth state.
    builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
        sp.GetRequiredService<AuthStateService>());

    // Enable cascading authentication state so the Blazor framework can discover
    // the AuthenticationStateProvider via the component hierarchy.
    builder.Services.AddCascadingAuthenticationState();

    // Register authentication services so the AuthorizationMiddleware can call
    // HttpContext.ChallengeAsync() without throwing. The actual auth state is
    // driven by AuthStateService (AuthenticationStateProvider); cookie auth is
    // configured here only as middleware infrastructure for Blazor Server.
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "vw-auth";
            options.LoginPath = "/login";
            options.LogoutPath = "/auth/sign-out";
            // 🔴 CRITICAL: Suppress HTTP-level redirects — Blazor handles auth internally.
            // The cookie handler is registered only to satisfy ASP.NET Core's middleware
            // infrastructure (IAuthenticationService, Challenge/Forbid). Without this,
            // the cookie handler would redirect unauthenticated SSR requests to /login,
            // causing a redirect loop since there is no vw-auth cookie. Instead, return
            // 401/403 silently and let Blazor's AuthorizeRouteView handle navigation.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        // HTTP-level authorization passes through — Blazor's component-level auth
        // (AuthorizeRouteView/AuthorizeView) handles the actual access control
        // using the AuthenticationStateProvider (AuthStateService with JWT).
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });

    // Game client services — registered as interfaces for abstraction.
    // The factory registration passes a dynamic token provider so SignalR automatic
    // reconnection gets the current JWT on each retry, not the token captured at
    // initial connection time.
    builder.Services.AddScoped<IGameHubConnectionService>(sp =>
    {
        var authState = sp.GetRequiredService<AuthStateServiceBase>();
        return new GameHubConnectionService(
            sp,
            sp.GetRequiredService<ILogger<GameHubConnectionService>>(),
            () => Task.FromResult(authState.AccessToken));
    });
    builder.Services.AddScoped<IGameStateService, GameStateService>();
    builder.Services.AddHttpContextAccessor();

    // DataProtection key persistence — prevents antiforgery token failures on container restart.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .SetApplicationName("Veldrath.Web");

    // Antiforgery with a fixed cookie name and Strict same-site policy.
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name     = "vw-af";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    // Singleton cache that bridges auth state from SSR to circuit without relying on
    // PersistentComponentState's complex-type serialization (which can fail silently
    // for IReadOnlyList, DateTimeOffset, Guid?, etc.).
    builder.Services.AddSingleton<AuthStateCache>();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.MapHealthChecks("/health");

    // Trust the Caddy reverse-proxy's forwarded headers so Request.Scheme is https
    // and Nav.BaseUri reflects the public HTTPS origin.  Required for correct OAuth
    // returnUrl construction.  Safe to clear KnownNetworks inside the Docker network.
    var fwdOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    fwdOptions.KnownIPNetworks.Clear();
    fwdOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(fwdOptions);

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Security headers applied to every response.
    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Frame-Options"]         = "DENY";
        ctx.Response.Headers["X-Content-Type-Options"]  = "nosniff";
        ctx.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        ctx.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
        ctx.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data:; frame-ancestors 'none';";
        await next(ctx);
    });

    // Routing must be explicit to ensure correct middleware ordering.
    app.UseRouting();

    // Authentication and Authorization middleware must be registered so
    // EndpointMiddleware doesn't throw on [Authorize]-decorated endpoints.
    // The cookie handler is configured to never redirect (see AddCookie above),
    // so Blazor's AuthorizeRouteView handles auth at the component level.
    app.UseAuthentication();
    app.UseAuthorization();

    // Static assets must be mapped after routing.
    app.MapStaticAssets();

    // Revokes the refresh token stored in the cookie (server-side), deletes the cookie,
    // then redirects to /.  Called via forceLoad navigation so the circuit is torn down
    // and no stale auth state lingers in memory.
    app.MapGet("/auth/sign-out", async (HttpContext ctx, VeldrathApiClient api, IConfiguration config) =>
    {
        var rt = ctx.Request.Cookies["rt"];
        if (rt is not null)
        {
            await api.LogoutAsync(rt);
            var cookieDomain  = config["Auth:CookieDomain"];
            var deleteOptions = new CookieOptions { Path = "/" };
            if (!string.IsNullOrWhiteSpace(cookieDomain))
                deleteOptions.Domain = cookieDomain;
            ctx.Response.Cookies.Delete("rt", deleteOptions);
        }
        return Results.Redirect("/");
    });

    app.MapRazorComponents<App>()
        .AddAdditionalAssemblies(typeof(GameLayout).Assembly)
        .AddInteractiveServerRenderMode()
        .DisableAntiforgery();

    // Antiforgery must be positioned after endpoint mappings so it can see endpoint metadata.
    app.UseAntiforgery();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Veldrath.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
