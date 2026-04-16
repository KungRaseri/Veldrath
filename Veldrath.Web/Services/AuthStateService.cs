using Veldrath.Contracts.Auth;

namespace Veldrath.Web.Services;

/// <summary>
/// Scoped service that holds the authenticated player's JWT in circuit memory.
/// Tokens never touch browser storage — they are only held for the lifetime of the
/// Blazor Server WebSocket circuit, preventing XSS-based token theft.
/// </summary>
public class AuthStateService
{
    private string? _accessToken;
    private string? _refreshToken;

    /// <summary>Raised whenever the authentication state changes.</summary>
    public event Action? OnChange;

    /// <summary>Gets a value indicating whether there is an active authenticated session.</summary>
    public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken);

    /// <summary>Gets a value indicating whether the auth initialisation pass has completed.</summary>
    public bool IsAuthReady { get; private set; }

    /// <summary>Gets the display name of the authenticated player, or <see langword="null"/> if not logged in.</summary>
    public string? Username { get; private set; }

    /// <summary>Gets the account identifier of the authenticated player, or <see langword="null"/> if not logged in.</summary>
    public Guid? AccountId { get; private set; }

    /// <summary>Gets the roles held by the authenticated player.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>Gets the current raw access token, or <see langword="null"/> if not authenticated.</summary>
    public string? AccessToken => _accessToken;

    /// <summary>Gets the current refresh token, or <see langword="null"/> if not authenticated.</summary>
    public string? RefreshToken => _refreshToken;

    /// <summary>Gets the UTC expiry of the current access token.</summary>
    public DateTimeOffset? AccessTokenExpiry { get; private set; }

    /// <summary>
    /// Populates auth state from a <see cref="RenewJwtResponse"/> returned during the SSR
    /// prerender pass and serialised into the component state for the circuit to restore.
    /// </summary>
    public Task SetTokensAsync(AuthResponse response)
    {
        _accessToken   = response.AccessToken;
        _refreshToken  = response.RefreshToken;
        Username       = response.Username;
        AccountId      = response.AccountId;
        Roles          = response.Roles;
        AccessTokenExpiry = response.AccessTokenExpiry;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Populates auth state from a <see cref="RenewJwtResponse"/> (no refresh-token rotation).
    /// </summary>
    public Task SetTokensAsync(RenewJwtResponse response, string rawRefreshToken)
    {
        _accessToken   = response.AccessToken;
        _refreshToken  = rawRefreshToken;
        Username       = response.Username;
        AccountId      = response.AccountId;
        Roles          = response.Roles;
        AccessTokenExpiry = response.AccessTokenExpiry;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>Clears all auth state and notifies listeners.</summary>
    public Task LogOutAsync()
    {
        _accessToken  = null;
        _refreshToken = null;
        Username      = null;
        AccountId     = null;
        Roles         = [];
        AccessTokenExpiry = null;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Proactively refreshes the JWT if it will expire within the supplied threshold.
    /// Uses the in-memory refresh token; does not update the HttpOnly cookie.
    /// </summary>
    public async Task<bool> TryRefreshAsync(VeldrathApiClient api, TimeSpan? expiryThreshold = null)
    {
        if (_refreshToken is null) return false;

        var threshold = expiryThreshold ?? TimeSpan.FromMinutes(2);
        if (AccessTokenExpiry.HasValue && AccessTokenExpiry.Value - DateTimeOffset.UtcNow > threshold)
            return true;

        var renewed = await api.RenewJwtAsync(_refreshToken);
        if (renewed is null) return false;

        await SetTokensAsync(renewed, _refreshToken);
        api.SetBearerToken(renewed.AccessToken);
        return true;
    }

    /// <summary>Marks the auth state as initialised after the SSR prerender pass completes.</summary>
    public void MarkReady()
    {
        IsAuthReady = true;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
