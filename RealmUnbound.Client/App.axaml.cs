using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using RealmUnbound.Assets;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Client.Views;
using Serilog;

namespace RealmUnbound.Client;

[ExcludeFromCodeCoverage]
public partial class App : Application
{
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
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var serverBaseUrl = configuration["ServerBaseUrl"] ?? "http://localhost:8080/";

        var services = new ServiceCollection();
        ConfigureServices(services, configuration, serverBaseUrl);
        Services = services.BuildServiceProvider();

        // Restore persisted session (DPAPI-encrypted) so the user stays logged in across restarts.
        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // ProtectedData is Windows-only — guarded by OperatingSystem.IsWindows()
            var persistence = Services.GetRequiredService<TokenPersistenceService>();
            var tokenStore  = Services.GetRequiredService<TokenStore>();
            var saved = persistence.Load();
            if (saved is not null)
                tokenStore.Set(saved.AccessToken, saved.RefreshToken, saved.Username, saved.AccountId,
                               saved.AccessTokenExpiry, saved.IsCurator);

            // Mirror every token update to persistent storage, and clear on logout.
            tokenStore.WhenAnyValue(t => t.AccessToken).Subscribe(at =>
            {
                if (at is not null && tokenStore.RefreshToken is not null && tokenStore.Username is not null && tokenStore.AccountId.HasValue)
                    persistence.Save(at, tokenStore.RefreshToken, tokenStore.Username, tokenStore.AccountId.Value,
                                      tokenStore.AccessTokenExpiry ?? DateTimeOffset.UtcNow.AddMinutes(15), tokenStore.IsCurator);
                else if (at is null)
                    persistence.Clear();
            });
#pragma warning restore CA1416
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, string serverBaseUrl)
    {
        // Logging
        services.AddLogging(b => b.AddSerilog(dispose: true));

        // Token store (singleton — shared between auth service and connection service)
        services.AddSingleton<TokenStore>();

        // Persists DPAPI-encrypted tokens so sessions survive restarts.
        services.AddSingleton<TokenPersistenceService>();

        // Persists lightweight session preferences (email only — never tokens/passwords)
        services.AddSingleton<SessionStore>();

        // HTTP client for auth + character APIs
        services.AddHttpClient<IAuthService, HttpAuthService>(client =>
            client.BaseAddress = new Uri(serverBaseUrl));
        services.AddHttpClient<ICharacterService, HttpCharacterService>(client =>
            client.BaseAddress = new Uri(serverBaseUrl));
        services.AddHttpClient<IZoneService, HttpZoneService>(client =>
            client.BaseAddress = new Uri(serverBaseUrl));
        services.AddHttpClient<IContentService, HttpContentService>(client =>
            client.BaseAddress = new Uri(serverBaseUrl));

        // App services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IHubConnectionFactory, HubConnectionFactory>();
        services.AddSingleton<IServerConnectionService, ServerConnectionService>();
        services.AddSingleton<ContentCache>();
        services.AddSingleton(new ClientSettings(serverBaseUrl.TrimEnd('/')));

        // Game asset store — warms the IMemoryCache during the splash screen
        services.AddRealmUnboundAssets();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<SplashViewModel>();
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddSingleton<GameViewModel>();
        services.AddTransient<CharacterSelectViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
