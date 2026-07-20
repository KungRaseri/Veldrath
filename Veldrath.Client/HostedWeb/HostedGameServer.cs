using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
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
using Veldrath.Client.Services;
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
    private readonly TokenStore _tokenStore;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostedGameServer"/> class.
    /// </summary>
    /// <param name="remoteServerUrl">Base URL of the remote Veldrath.Server (e.g. <c>http://localhost:9000</c>).</param>
    /// <param name="loggerFactory">Logger factory for the embedded server pipeline.</param>
    /// <param name="tokenStore">The desktop client's token store for forwarding the real JWT to the embedded services.</param>
    public HostedGameServer(string remoteServerUrl, ILoggerFactory loggerFactory, TokenStore tokenStore)
    {
        _remoteServerUrl = remoteServerUrl.TrimEnd('/');
        _loggerFactory = loggerFactory;
        _tokenStore = tokenStore;
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

        // Register the desktop client's token store so the embedded services can
        // forward the real JWT to the remote Veldrath.Server.
        builder.Services.AddSingleton(_tokenStore);

        // Register an auth API client that forwards the desktop JWT.
        builder.Services.AddSingleton<IVeldrathAuthApiClient>(sp =>
        {
            var tokens = sp.GetRequiredService<TokenStore>();
            return new EmbeddedAuthApiClient(tokens);
        });

        // Register an auth state service that reflects the desktop's real auth state.
        builder.Services.AddScoped<AuthStateServiceBase, EmbeddedAuthStateService>();

        // Register a typed HttpClient for the remote game server API, used by IGameApiClient.
        // The Bearer token is added per-request by EmbeddedGameApiClient from the TokenStore.
        builder.Services.AddHttpClient("embedded-game", client =>
            client.BaseAddress = new Uri(_remoteServerUrl));

        // Map the typed client to the RCL's IGameApiClient interface,
        // forwarding the desktop JWT on every API call.
        builder.Services.AddScoped<IGameApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("embedded-game");
            var tokens = sp.GetRequiredService<TokenStore>();
            return new EmbeddedGameApiClient(client, tokens);
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
/// Embedded <see cref="IVeldrathAuthApiClient"/> that satisfies the DI contract for the
/// embedded server context. Uses the desktop client's <see cref="TokenStore"/> for the
/// real JWT so <c>AuthStateServiceBase</c> can report accurate auth state.
/// Auth mutations (login/register/logout) are no-ops — the desktop client handles those.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmbeddedAuthApiClient : IVeldrathAuthApiClient
{
    private readonly TokenStore _tokens;
    private string? _bearerToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedAuthApiClient"/> class.
    /// </summary>
    /// <param name="tokens">The desktop client's token store.</param>
    public EmbeddedAuthApiClient(TokenStore tokens)
    {
        _tokens = tokens;
    }

    /// <inheritdoc />
    public void SetBearerToken(string token) => _bearerToken = token;

    /// <inheritdoc />
    public void ClearBearerToken() => _bearerToken = null;

    /// <summary>Gets the current bearer token: the explicitly-set one, or the desktop token store's JWT.</summary>
    private string? EffectiveToken => _bearerToken ?? _tokens.AccessToken;

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
/// Uses the desktop client's <see cref="TokenStore"/> for real auth state so the
/// RCL's <c>[Authorize]</c> guard and <c>IsLoggedIn</c> binding reflect the
/// actual authenticated user.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmbeddedAuthStateService : AuthStateServiceBase
{
    private readonly TokenStore _tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedAuthStateService"/> class.
    /// Reads the current access token from the desktop's token store so the base class
    /// reports <c>IsLoggedIn</c> accurately.
    /// </summary>
    /// <param name="api">Embedded auth API client.</param>
    /// <param name="tokens">The desktop client's token store.</param>
    public EmbeddedAuthStateService(IVeldrathAuthApiClient api, TokenStore tokens) : base(api)
    {
        _tokens = tokens;
        _accessToken = _tokens.AccessToken ?? "__embedded_mode__";
        IsAuthReady = true;
    }
}

/// <summary>
/// Minimal <see cref="IGameApiClient"/> implementation for the embedded server.
/// Delegates HTTP calls to the remote Veldrath.Server API, forwarding the auth JWT
/// from the desktop client's <see cref="TokenStore"/> via the <c>Authorization</c> header.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EmbeddedGameApiClient : IGameApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStore _tokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedGameApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The pre-configured HTTP client targeting the remote server.</param>
    /// <param name="tokens">The desktop client's token store for forwarding the JWT.</param>
    public EmbeddedGameApiClient(HttpClient httpClient, TokenStore tokens)
    {
        _httpClient = httpClient;
        _tokens = tokens;
    }

    /// <summary>Adds the desktop JWT as a Bearer token to an HTTP request if available.</summary>
    private void ApplyAuth(HttpRequestMessage request)
    {
        if (_tokens.AccessToken is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <inheritdoc />
    public async Task<List<Veldrath.Contracts.Characters.CharacterDto>> GetCharactersAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/characters");
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Veldrath.Contracts.Characters.CharacterDto>>(cancellationToken: ct)
               ?? [];
    }

    /// <inheritdoc />
    public async Task<Veldrath.Contracts.Characters.CharacterDto?> CreateCharacterAsync(
        string name, string className, string difficultyMode = "normal", CancellationToken ct = default)
    {
        var payload = new { name, className, difficultyMode };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/characters")
        {
            Content = JsonContent.Create(payload),
        };
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Veldrath.Contracts.Characters.CharacterDto>(cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<Veldrath.Contracts.Characters.CheckNameAvailabilityResponse?> CheckCharacterNameAsync(
        string name, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(name);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/character-creation/sessions/check-name?name={encoded}");
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Veldrath.Contracts.Characters.CheckNameAvailabilityResponse>(cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task<List<Veldrath.Contracts.Content.ActorClassDto>> GetClassesAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/classes");
        ApplyAuth(request);
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Veldrath.Contracts.Content.ActorClassDto>>(cancellationToken: ct)
               ?? [];
    }

    // ── Session-based character creation ───────────────────────────────────────────

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.BeginCreationSessionResponse?> BeginCreationSessionAsync(CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.CharacterPreviewDto?> GetCreationPreviewAsync(Guid sessionId, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.CharacterDto?> FinalizeCreationSessionAsync(Guid sessionId, Veldrath.Contracts.Characters.FinalizeCreationSessionRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task AbandonCreationSessionAsync(Guid sessionId, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.SetCreationChoiceResponse?> SetCreationNameAsync(Guid sessionId, string characterName, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.SetCreationChoiceResponse?> SetCreationClassAsync(Guid sessionId, string className, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.SetCreationChoiceResponse?> SetCreationSpeciesAsync(Guid sessionId, string speciesSlug, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.SetCreationChoiceResponse?> SetCreationBackgroundAsync(Guid sessionId, string backgroundId, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.AllocateCreationAttributesResponse?> SetCreationAttributesAsync(Guid sessionId, Dictionary<string, int> allocations, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.SetCreationChoiceResponse?> SetCreationEquipmentPreferencesAsync(Guid sessionId, string? armorType, string? weaponType, bool includeShield, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    /// <inheritdoc />
    public Task<Veldrath.Contracts.Characters.SetCreationChoiceResponse?> SetCreationLocationAsync(Guid sessionId, string locationId, CancellationToken ct = default)
        => throw new NotSupportedException("Session-based creation is not available in the embedded client.");

    // ── Content lookups ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<List<Veldrath.Contracts.Content.SpeciesDto>> GetSpeciesAsync(CancellationToken ct = default)
        => throw new NotSupportedException("Content lookups are not available in the embedded client.");

    /// <inheritdoc />
    public Task<List<Veldrath.Contracts.Content.BackgroundDto>> GetBackgroundsAsync(CancellationToken ct = default)
        => throw new NotSupportedException("Content lookups are not available in the embedded client.");
}
