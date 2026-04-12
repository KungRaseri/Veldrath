using Microsoft.AspNetCore.DataProtection;
using Serilog;
using Serilog.Events;
using RealmFoundry;
using RealmFoundry.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RealmFoundry")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/realmfoundry-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("RealmFoundry starting...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // HttpClient for calling Veldrath.Server APIs.
    var serverUrl = builder.Configuration["Veldrath:ServerUrl"]
        ?? throw new InvalidOperationException("Veldrath:ServerUrl is not configured.");

    // Register as a named client so RealmFoundryApiClient can be scoped.
    // AddHttpClient<T> (typed client) makes T transient — every component injection
    // gets a fresh HttpClient with no Authorization header, even after login.
    // Registering as scoped means all components in a Blazor circuit share the one
    // instance that AuthStateService.SetTokensAsync already called SetBearerToken on.
    builder.Services.AddHttpClient("foundry", client =>
        client.BaseAddress = new Uri(serverUrl));
    builder.Services.AddScoped<RealmFoundryApiClient>(sp =>
        new RealmFoundryApiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("foundry")));

    builder.Services.AddScoped<AuthStateService>();

    // DataProtection key persistence
    // Persisting to a mounted volume prevents antiforgery token failures on
    // container restart caused by ephemeral in-memory keys.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .SetApplicationName("RealmFoundry");

    // Antiforgery
    // Use a fixed cookie name so stale cookies from before a key rotation are
    // easily identified, and set SameSite=Strict + no Secure flag for HTTP dev.
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name     = "rf-af";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
        // HTTPS redirection is handled by the reverse proxy / Docker host, not the container.
    }
    app.UseAntiforgery();

    // Security headers — applied to every response before any other middleware writes output.
    app.Use(async (ctx, next) =>
    {
        ctx.Response.Headers["X-Frame-Options"]         = "DENY";
        ctx.Response.Headers["X-Content-Type-Options"]  = "nosniff";
        ctx.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        ctx.Response.Headers["Permissions-Policy"]      = "camera=(), microphone=(), geolocation=()";
        // Blazor requires 'unsafe-inline' for its auto-generated inline scripts.
        ctx.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none';";
        await next(ctx);
    });

    // UseStaticFiles serves physical wwwroot files (e.g. _framework/, favicon.ico).
    // MapStaticAssets handles fingerprinted/compressed endpoints from the publish manifest.
    // Both are needed: in Development inside Docker, UseStaticWebAssets() auto-wires a
    // dev-time manifest pointing at source project obj/ paths that don't exist in the
    // container, so MapStaticAssets() alone can't resolve _framework/ assets.
    app.UseStaticFiles();
    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .DisableAntiforgery();  // Blazor Server's _blazor hub uses WebSockets — antiforgery doesn't apply

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RealmFoundry terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
