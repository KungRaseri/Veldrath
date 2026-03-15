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

    // Typed HttpClient for calling RealmUnbound.Server APIs.
    var serverUrl = builder.Configuration["RealmUnbound:ServerUrl"]
        ?? throw new InvalidOperationException("RealmUnbound:ServerUrl is not configured.");

    builder.Services.AddHttpClient<RealmFoundryApiClient>(client =>
        client.BaseAddress = new Uri(serverUrl));

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

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
