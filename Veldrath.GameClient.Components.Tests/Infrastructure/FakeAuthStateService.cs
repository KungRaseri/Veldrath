using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Auth;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// Configurable stub for <see cref="AuthStateServiceBase"/>.
/// Default behaviour: reports the user as logged in with a sentinel access token
/// so that <c>[Authorize]</c> guards pass automatically.
/// Set <see cref="IsLoggedInOverride"/> to <see langword="false"/> to simulate
/// an unauthenticated user.
/// </summary>
public sealed class FakeAuthStateService : AuthStateServiceBase
{
    private bool _isLoggedInOverride = true;
    private string? _accessTokenOverride = "__test_mode__";

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeAuthStateService"/> class
    /// with a default logged-in state.
    /// </summary>
    /// <param name="api">The auth API client stub.</param>
    public FakeAuthStateService(IVeldrathAuthApiClient api) : base(api)
    {
        _accessToken = "__test_mode__";
        _refreshToken = "__test_refresh__";
        IsAuthReady = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the service reports the user as logged in.
    /// Setting to <see langword="false"/> clears the access token so that
    /// <see cref="AuthStateServiceBase.IsLoggedIn"/> returns <see langword="false"/>.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool IsLoggedInOverride
    {
        get => _isLoggedInOverride;
        set
        {
            _isLoggedInOverride = value;
            SyncAccessToken();
        }
    }

    /// <summary>
    /// Gets or sets the access token returned by <see cref="AuthStateServiceBase.AccessToken"/>.
    /// Default is <c>"__test_mode__"</c>.
    /// </summary>
    public string? AccessTokenOverride
    {
        get => _accessTokenOverride;
        set
        {
            _accessTokenOverride = value;
            SyncAccessToken();
        }
    }

    /// <summary>
    /// Gets or sets the result of <see cref="TryRefreshAsync"/>.
    /// </summary>
    public bool TryRefreshResult { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether auth initialisation has completed.
    /// Shadows the base <see cref="AuthStateServiceBase.IsAuthReady"/> with a public setter
    /// so tests can simulate the auth-ready transition.
    /// </summary>
    public new bool IsAuthReady
    {
        get => base.IsAuthReady;
        set => base.IsAuthReady = value;
    }

    /// <summary>Public wrapper for the protected <see cref="AuthStateServiceBase.NotifyStateChanged"/>
    /// so tests can simulate auth state transitions.</summary>
    public new void NotifyStateChanged() => base.NotifyStateChanged();

    /// <inheritdoc />
    public override Task SetTokensAsync(AuthResponse response)
    {
        _accessToken = response.AccessToken;
        _accessTokenOverride = response.AccessToken;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SetTokensAsync(RenewJwtResponse response, string rawRefreshToken)
    {
        _accessToken = response.AccessToken;
        _accessTokenOverride = response.AccessToken;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>Simulates a token refresh attempt.</summary>
    /// <param name="expiryThreshold">Optional expiry threshold (ignored in stub).</param>
    public new async Task<bool> TryRefreshAsync(TimeSpan? expiryThreshold = null)
        => TryRefreshResult;

    /// <summary>Synchronises the base <c>_accessToken</c> field with the override values.</summary>
    private void SyncAccessToken()
    {
        _accessToken = _isLoggedInOverride ? _accessTokenOverride : null;
    }
}
