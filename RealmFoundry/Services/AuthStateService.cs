using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RealmUnbound.Contracts.Auth;

namespace RealmFoundry.Services;

/// <summary>
/// Tracks the current user's authentication state across Blazor Server circuit lifetimes.
/// Tokens are stored in <see cref="ProtectedSessionStorage"/> — encrypted, server-side,
/// and scoped to the browser session so they survive page refreshes but not tab close.
/// </summary>
public sealed class AuthStateService(
    ProtectedSessionStorage storage,
    RealmFoundryApiClient apiClient)
{
    private const string AccessTokenKey  = "rf_access";
    private const string RefreshTokenKey = "rf_refresh";
    private const string UsernameKey     = "rf_username";
    private const string AccountIdKey    = "rf_account_id";
    private const string IsCuratorKey    = "rf_is_curator";
    private const string TokenExpiryKey  = "rf_token_expiry";

    public string?         Username     { get; private set; }
    public Guid?           AccountId    { get; private set; }
    public bool            IsCurator    { get; private set; }
    public DateTimeOffset? TokenExpiry  { get; private set; }
    public bool            IsLoggedIn   => Username is not null;

    /// <summary>True when the access token expires within two minutes.</summary>
    public bool TokenExpiresSoon =>
        TokenExpiry.HasValue && (TokenExpiry.Value - DateTimeOffset.UtcNow) < TimeSpan.FromMinutes(2);

    public event Action? OnChange;

    /// <summary>Called when the Blazor circuit initialises. Restores any saved token.</summary>
    public async Task InitialiseAsync()
    {
        var result = await storage.GetAsync<string>(AccessTokenKey);
        if (!result.Success || string.IsNullOrEmpty(result.Value))
            return;

        var username   = await storage.GetAsync<string>(UsernameKey);
        var accountId  = await storage.GetAsync<string>(AccountIdKey);
        var isCurator  = await storage.GetAsync<bool>(IsCuratorKey);
        var expiryStr  = await storage.GetAsync<string>(TokenExpiryKey);

        var expiry = expiryStr.Success && DateTimeOffset.TryParse(expiryStr.Value, out var dt) ? dt : (DateTimeOffset?)null;

        Apply(result.Value, username.Value, accountId.Value is null ? null : Guid.Parse(accountId.Value!),
              isCurator.Success && isCurator.Value, expiry);
    }

    /// <summary>Stores a token pair received after OAuth login.</summary>
    public async Task SetTokensAsync(AuthResponse response)
    {
        await storage.SetAsync(AccessTokenKey,  response.AccessToken);
        await storage.SetAsync(RefreshTokenKey, response.RefreshToken);
        await storage.SetAsync(UsernameKey,     response.Username);
        await storage.SetAsync(AccountIdKey,    response.AccountId.ToString());
        await storage.SetAsync(IsCuratorKey,    response.IsCurator);
        await storage.SetAsync(TokenExpiryKey,  response.AccessTokenExpiry.ToString("O"));

        Apply(response.AccessToken, response.Username, response.AccountId,
              response.IsCurator, response.AccessTokenExpiry);
    }

    /// <summary>Proactively refreshes the access token using the stored refresh token.</summary>
    public async Task<bool> TryRefreshAsync()
    {
        var stored = await storage.GetAsync<string>(RefreshTokenKey);
        if (!stored.Success || string.IsNullOrEmpty(stored.Value)) return false;

        var refreshed = await apiClient.RefreshTokenAsync(stored.Value);
        if (refreshed is null) return false;

        await SetTokensAsync(refreshed);
        return true;
    }

    public async Task LogOutAsync()
    {
        await storage.DeleteAsync(AccessTokenKey);
        await storage.DeleteAsync(RefreshTokenKey);
        await storage.DeleteAsync(UsernameKey);
        await storage.DeleteAsync(AccountIdKey);
        await storage.DeleteAsync(IsCuratorKey);
        await storage.DeleteAsync(TokenExpiryKey);

        Username    = null;
        AccountId   = null;
        IsCurator   = false;
        TokenExpiry = null;
        apiClient.ClearBearerToken();
        OnChange?.Invoke();
    }

    private void Apply(string jwt, string? username, Guid? accountId, bool isCurator, DateTimeOffset? expiry)
    {
        Username    = username;
        AccountId   = accountId;
        IsCurator   = isCurator;
        TokenExpiry = expiry;
        apiClient.SetBearerToken(jwt);
        OnChange?.Invoke();
    }
}
