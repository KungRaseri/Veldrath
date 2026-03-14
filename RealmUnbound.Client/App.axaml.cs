using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Client.Views;
using Serilog;

namespace RealmUnbound.Client;

[ExcludeFromCodeCoverage]
public partial class App : Application
{
    // Server base URL — override via config or env in production
    private const string ServerBaseUrl = "http://localhost:8080/";

    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File("logs/client-.txt", rollingInterval: Serilog.RollingInterval.Day)
            .CreateLogger();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(b => b.AddSerilog(dispose: true));

        // Token store (singleton — shared between auth service and connection service)
        services.AddSingleton<TokenStore>();

        // Persists lightweight session preferences (email only — never tokens/passwords)
        services.AddSingleton<SessionStore>();

        // HTTP client for auth + character APIs
        services.AddHttpClient<IAuthService, HttpAuthService>(client =>
            client.BaseAddress = new Uri(ServerBaseUrl));
        services.AddHttpClient<ICharacterService, HttpCharacterService>(client =>
            client.BaseAddress = new Uri(ServerBaseUrl));
        services.AddHttpClient<IZoneService, HttpZoneService>(client =>
            client.BaseAddress = new Uri(ServerBaseUrl));
        services.AddHttpClient<IContentService, HttpContentService>(client =>
            client.BaseAddress = new Uri(ServerBaseUrl));

        // App services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IHubConnectionFactory, HubConnectionFactory>();
        services.AddSingleton<IServerConnectionService, ServerConnectionService>();
        services.AddSingleton<ContentCache>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<SplashViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddSingleton<GameViewModel>();
        services.AddTransient(sp =>
        {
            var vm = ActivatorUtilities.CreateInstance<CharacterSelectViewModel>(sp);
            vm.ServerUrl = ServerBaseUrl.TrimEnd('/');
            return vm;
        });
    }
}
