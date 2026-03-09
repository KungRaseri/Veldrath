using Serilog;
using Serilog.Events;
using RealmUnbound.Server.Hubs;

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

    // RealmEngine services
    // builder.Services.AddRealmEngineCore(); // TODO: wire up when ready

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors("AllowLocalClient");

    // Hubs
    app.MapHub<GameHub>("/hubs/game");

    // Health probe
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", app = "RealmUnbound.Server" }));

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
