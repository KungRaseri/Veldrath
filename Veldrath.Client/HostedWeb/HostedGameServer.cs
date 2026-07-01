using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration.Memory;
using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Veldrath.GameClient.Components.Components.Layout;

namespace Veldrath.Client.HostedWeb;

/// <summary>
/// Creates and manages a minimal ASP.NET Core web application on a random localhost port.
/// The server hosts the <c>Veldrath.GameClient.Components</c> RCL as Interactive Server
/// Blazor, providing the game UI for the embedded WebView2 control in <c>Veldrath.Client</c>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class HostedGameServer : IHostedGameServer, IAsyncDisposable
{
    private WebApplication? _app;
    private readonly ILogger<HostedGameServer> _logger;
    private readonly string _remoteServerUrl;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostedGameServer"/> class.
    /// </summary>
    /// <param name="remoteServerUrl">Base URL of the remote Veldrath.Server (e.g. <c>http://localhost:9000</c>).</param>
    /// <param name="loggerFactory">Logger factory for the embedded server pipeline.</param>
    public HostedGameServer(string remoteServerUrl, ILoggerFactory loggerFactory)
    {
        _remoteServerUrl = remoteServerUrl.TrimEnd('/');
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<HostedGameServer>();
    }

    /// <inheritdoc />
    public int Port { get; private set; }

    /// <inheritdoc />
    public bool IsRunning => _app is not null;

    /// <inheritdoc />
    public string? BaseUrl => IsRunning ? $"http://localhost:{Port}" : null;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_app is not null)
        {
            _logger.LogWarning("HostedGameServer is already running on port {Port}.", Port);
            return;
        }

        _logger.LogInformation("Starting embedded game server...");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Use the same logger factory so the embedded server logs to the same sinks.
        builder.Services.AddSingleton(_loggerFactory);
        builder.Services.AddSingleton<ILogger<HostedGameServer>>(_logger);

        // Configure Kestrel to bind to 127.0.0.1 only with a random OS-assigned port.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Loopback, 0); // port 0 = OS assigns
        });

        // Register Blazor Server interactive SSR.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Register GameClient.Core services as scoped (one per Blazor circuit).
        builder.Services.AddScoped<IGameHubConnectionService, GameHubConnectionService>();
        builder.Services.AddScoped<IGameStateService, GameStateService>();

        // Register a stub auth API client for the embedded server.
        builder.Services.AddSingleton<IVeldrathAuthApiClient>(_ => new EmbeddedAuthApiClient());

        // Register a placeholder AuthStateService for the embedded server.
        // The desktop client handles auth via its own TokenStore and ServerConnectionService.
        builder.Services.AddScoped<AuthStateServiceBase, EmbeddedAuthStateService>();

        // Register a typed HttpClient for the remote game server API, used by IGameApiClient.
        builder.Services.AddHttpClient("embedded-game", client =>
            client.BaseAddress = new Uri(_remoteServerUrl));

        // Map the typed client to the RCL's IGameApiClient interface.
        builder.Services.AddScoped<IGameApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("embedded-game");
            return new EmbeddedGameApiClient(client);
        });

        // Register configuration for the embedded server.
        builder.Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(_ =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["Veldrath:ServerUrl"] = _remoteServerUrl,
            };
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
                {
                    InitialData = dict,
                })
                .Build();
            return config;
        });

        var app = builder.Build();

        // Only error handling in non-development (but we're always in Development for embedded).
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.MapStaticAssets();
        app.UseAntiforgery();

        // Map the Razor components, using our embedded App component as the root.
        app.MapRazorComponents<Veldrath.GameClient.Components.Hosted.EmbeddedApp>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(GameLayout).Assembly)
            .DisableAntiforgery();

        // Health endpoint used by the bridge for status checks.
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        _app = app;

        // Start the server.  After StartAsync completes, _app.Urls[0] contains the assigned URL.
        await _app.StartAsync(ct);

        // Capture the assigned port from the first URL.
        var urls = _app.Urls;
        if (urls.Count > 0)
        {
            var uri = new Uri(urls.First());
            Port = uri.Port;
            _logger.LogInformation(
                "HostedGameServer started on {BaseUrl} (remote server: {RemoteUrl}).",
                BaseUrl, _remoteServerUrl);
        }
        else
        {
            _logger.LogError("HostedGameServer started but no bound address was assigned.");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is null)
            return;

        _logger.LogInformation("Stopping embedded game server...");

        try
        {
            await _app.StopAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping HostedGameServer.");
        }

        await _app.DisposeAsync();
        _app = null;
        Port = 0;

        _logger.LogInformation("HostedGameServer stopped.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Stub <see cref="IVeldrathAuthApiClient"/> that satisfies the DI contract for the
/// embedded server context. The desktop client manages authentication natively.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmbeddedAuthApiClient : IVeldrathAuthApiClient
{
    /// <inheritdoc />
    public void SetBearerToken(string token) { }
    /// <inheritdoc />
    public void ClearBearerToken() { }
    /// <inheritdoc />
    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) => Task.FromResult(true);
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Auth.AuthResponse?> RegisterAsync(string email, string username, string password, CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Auth.AuthResponse?>(null);
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Auth.AuthResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Auth.AuthResponse?>(null);
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Auth.AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Auth.AuthResponse?>(null);
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Auth.RenewJwtResponse?> RenewJwtAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Auth.RenewJwtResponse?>(null);
    /// <inheritdoc />
    public Task LogoutAsync(string refreshToken, CancellationToken ct = default) => Task.CompletedTask;
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Auth.AuthResponse?> ExchangeCodeAsync(string code, Guid accountId, CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Auth.AuthResponse?>(null);
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Auth.CreateExchangeCodeResponse?> CreateExchangeCodeAsync(CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Auth.CreateExchangeCodeResponse?>(null);
    /// <inheritdoc />
    public Task ForgotPasswordAsync(string email, CancellationToken ct = default) => Task.CompletedTask;
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ResendEmailConfirmationAsync(CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<Veldrath.Contracts.Account.AccountProfileDto?> GetMyProfileAsync(CancellationToken ct = default)
        => Task.FromResult<Veldrath.Contracts.Account.AccountProfileDto?>(null);
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> UpdateProfileAsync(string? displayName, string? bio, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ChangeUsernameAsync(string newUsername, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<IReadOnlyList<Veldrath.Contracts.Account.LinkedProviderDto>> GetLinkedProvidersAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Veldrath.Contracts.Account.LinkedProviderDto>>([]);
    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> UnlinkProviderAsync(string provider, string providerKey, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));
    /// <inheritdoc />
    public Task<IReadOnlyList<Veldrath.Contracts.Account.AccountSessionDto>> GetSessionsAsync(Guid? currentSessionId = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Veldrath.Contracts.Account.AccountSessionDto>>([]);
    /// <inheritdoc />
    public Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) => Task.FromResult(true);
    /// <inheritdoc />
    public Task<bool> RevokeOtherSessionsAsync(Guid currentSessionId, CancellationToken ct = default) => Task.FromResult(true);
}

/// <summary>
/// Minimal <see cref="AuthStateServiceBase"/> for the embedded server context.
/// The desktop client manages authentication natively; this stub satisfies the RCL's DI
/// requirement for <c>AuthStateServiceBase</c> without performing real auth operations.
/// Sets the internal access token to a sentinel value so <see cref="AuthStateServiceBase.IsLoggedIn"/>
/// returns <c>true</c> and the RCL's <c>[Authorize]</c> guard passes automatically.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmbeddedAuthStateService : AuthStateServiceBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedAuthStateService"/> class.
    /// Sets the internal access token to a sentinel value so the base class reports
    /// the user as logged in.
    /// </summary>
    /// <param name="api">Stub auth API client.</param>
    public EmbeddedAuthStateService(IVeldrathAuthApiClient api) : base(api)
    {
        // Set a sentinel token to satisfy the base class IsLoggedIn check.
        // The desktop client handles real authentication natively.
        _accessToken = "__embedded_mode__";
        IsAuthReady = true;
    }
}

/// <summary>
/// Minimal <see cref="IGameApiClient"/> implementation for the embedded server.
/// Delegates HTTP calls to the remote Veldrath.Server API, forwarding the auth token
/// from the desktop client's <see cref="TokenStore"/> via a header set by the caller.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmbeddedGameApiClient : IGameApiClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedGameApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The pre-configured HTTP client targeting the remote server.</param>
    public EmbeddedGameApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<List<Veldrath.Contracts.Characters.CharacterDto>> GetCharactersAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/characters", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Veldrath.Contracts.Characters.CharacterDto>>(cancellationToken: ct)
               ?? [];
    }

    /// <inheritdoc />
    public async Task<Veldrath.Contracts.Characters.CharacterDto?> CreateCharacterAsync(
        string name, string className, string difficultyMode = "normal", CancellationToken ct = default)
    {
        var payload = new { name, className, difficultyMode };
        var response = await _httpClient.PostAsJsonAsync("/api/characters", payload, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Veldrath.Contracts.Characters.CharacterDto>(cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<Veldrath.Contracts.Characters.CheckNameAvailabilityResponse?> CheckCharacterNameAsync(
        string name, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(name);
        var response = await _httpClient.GetAsync($"/api/characters/check-name?name={encoded}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Veldrath.Contracts.Characters.CheckNameAvailabilityResponse>(cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<List<Veldrath.Contracts.Content.ActorClassDto>> GetClassesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/classes", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Veldrath.Contracts.Content.ActorClassDto>>(cancellationToken: ct)
               ?? [];
    }
}
