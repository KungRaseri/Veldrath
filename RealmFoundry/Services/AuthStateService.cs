using Microsoft.JSInterop;
using RealmUnbound.Contracts.Auth;

namespace RealmFoundry.Services;

/// <summary>
/// Tracks the current user's authentication state across Blazor Server circuit lifetimes.
/// Tokens are stored in the browser's <c>sessionStorage</c> via <see cref="IJSRuntime"/>
/// and restored on circuit startup by <see cref="InitialiseAsync"/>, called from
/// <c>AuthInitializer</c> in the layout.
/// </summary>
public sealed class AuthStateService(
    IJSRuntime js,
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

    /// <summary>
    /// Called when the Blazor circuit initialises. Restores auth state from sessionStorage.
    /// Only call from <c>OnAfterRenderAsync(firstRender: true)</c> — JS interop is unavailable
    /// during SSR prerendering.
    /// </summary>
    public async Task InitialiseAsync()
    {
        try
        {
            var jwt = await js.InvokeAsync<string?>("sessionStorage.getItem", AccessTokenKey);
            if (string.IsNullOrEmpty(jwt)) return;

            var username  = await js.InvokeAsync<string?>("sessionStorage.getItem", UsernameKey);
            var accountId = await js.InvokeAsync<string?>("sessionStorage.getItem", AccountIdKey);
            var isCurator = await js.InvokeAsync<string?>("sessionStorage.getItem", IsCuratorKey);
            var expiryStr = await js.InvokeAsync<string?>("sessionStorage.getItem", TokenExpiryKey);

            var expiry      = DateTimeOffset.TryParse(expiryStr, out var dt) ? dt : (DateTimeOffset?)null;
            Guid? accountGuid = Guid.TryParse(accountId, out var g) ? g : null;

            Apply(jwt, username, accountGuid, isCurator is "true", expiry);
        }
        catch (JSException)
        {
            // JS runtime not yet available (e.g. called from prerender context). Stay logged-out.
        }
    }

    /// <summary>Stores a token pair received after OAuth login and applies it in-memory.</summary>
    public async Task SetTokensAsync(AuthResponse response)
    {
        await js.InvokeVoidAsync("sessionStorage.setItem", AccessTokenKey,  response.AccessToken);
        await js.InvokeVoidAsync("sessionStorage.setItem", RefreshTokenKey, response.RefreshToken);
        await js.InvokeVoidAsync("sessionStorage.setItem", UsernameKey,     response.Username);
        await js.InvokeVoidAsync("sessionStorage.setItem", AccountIdKey,    response.AccountId.ToString());
        await js.InvokeVoidAsync("sessionStorage.setItem", IsCuratorKey,    response.IsCurator.ToString().ToLowerInvariant());
        await js.InvokeVoidAsync("sessionStorage.setItem", TokenExpiryKey,  response.AccessTokenExpiry.ToString("O"));

        Apply(response.AccessToken, response.Username, response.AccountId,
              response.IsCurator, response.AccessTokenExpiry);
    }

    /// <summary>Proactively refreshes the access token using the stored refresh token.</summary>
    public async Task<bool> TryRefreshAsync()
    {
        string? refreshToken;
        try
        {
            refreshToken = await js.InvokeAsync<string?>("sessionStorage.getItem", RefreshTokenKey);
        }
        catch (JSException) { return false; }

        if (string.IsNullOrEmpty(refreshToken)) return false;

        var refreshed = await apiClient.RefreshTokenAsync(refreshToken);
        if (refreshed is null) return false;

        await SetTokensAsync(refreshed);
        return true;
    }

    public async Task LogOutAsync()
    {
        try
        {
            await js.InvokeVoidAsync("sessionStorage.removeItem", AccessTokenKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", RefreshTokenKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", UsernameKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", AccountIdKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", IsCuratorKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", TokenExpiryKey);
        }
        catch (JSException) { /* Best-effort cleanup */ }

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

