using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmForge.Services;
using RealmForge.ViewModels;
using RealmForge.Views;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace RealmForge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureLogging();

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Unhandled exception. IsTerminating: {IsTerminating}", e.IsTerminating);
            else
                Log.Fatal("Unhandled non-exception error: {Error}", e.ExceptionObject);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureLogging()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RealmForge", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "RealmForge")
            .WriteTo.File(Path.Combine(logDir, "realmforge-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information("RealmForge starting...");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSerilog(dispose: false));

        // Load configuration from realmforge.json (connection strings, etc.)
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var contentConnStr = config.GetConnectionString("ContentDb");
        if (!string.IsNullOrWhiteSpace(contentConnStr))
        {
            services.AddDbContext<ContentDbContext>(options =>
                options.UseNpgsql(contentConnStr));
        }

        services.AddSingleton<EditorSettingsService>();
        services.AddSingleton<FileManagementService>();
        services.AddScoped<ModelValidationService>();
        services.AddSingleton<ReferenceResolverService>();

        services.AddSingleton<JsonEditorViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
