using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;
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

    // HttpClient for calling Veldrath.Server APIs.
    var serverUrl = builder.Configuration["Veldrath:ServerUrl"]
        ?? throw new InvalidOperationException("Veldrath:ServerUrl is not configured.");

    // Scoped named client so all Blazor circuit components share the one instance
    // that AuthStateService.SetTokensAsync already called SetBearerToken on.
    builder.Services.AddHttpClient("veldrath-web", client =>
        client.BaseAddress = new Uri(serverUrl));
    builder.Services.AddScoped<VeldrathApiClient>(sp =>
        new VeldrathApiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("veldrath-web")));

    builder.Services.AddScoped<AuthStateService>();
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

    var app = builder.Build();

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
    app.UseAntiforgery();

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

    app.UseStaticFiles();
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
        .AddInteractiveServerRenderMode()
        .DisableAntiforgery();

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
