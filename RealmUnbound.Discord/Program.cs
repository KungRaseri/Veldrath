using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using RealmEngine.Data;
using RealmEngine.Core;
using RealmUnbound.Discord;
using RealmUnbound.Discord.Services;
using RealmUnbound.Discord.Settings;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RealmUnbound.Discord")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/realmunbound-discord-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("RealmUnbound.Discord starting...");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));

    // Wire up the RealmEngine — data → core → MediatR must be registered in this order
    var jsonDataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Json");
    builder.Services.AddRealmEngineData(jsonDataPath);
    builder.Services.AddRealmEngineCore();
    builder.Services.AddRealmEngineMediatR();

    // Typed HttpClient for server status; base URL is configurable in Discord settings
    builder.Services.AddHttpClient<ServerStatusService>((sp, client) =>
    {
        var settings = sp.GetRequiredService<IOptions<DiscordSettings>>().Value;
        client.BaseAddress = new Uri(settings.ServerBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    });

    builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages,
        LogLevel       = LogSeverity.Debug,
    }));

    builder.Services.AddSingleton(provider =>
        new InteractionService(provider.GetRequiredService<DiscordSocketClient>()));

    builder.Services.AddSingleton<InteractionHandlingService>();
    builder.Services.AddHostedService<BotWorker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RealmUnbound.Discord terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
