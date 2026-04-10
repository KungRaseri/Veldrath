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
    private const string RolesKey        = "rf_roles";
    private const string PermissionsKey  = "rf_permissions";

    public string?         Username     { get; private set; }
    public Guid?           AccountId    { get; private set; }
    public bool            IsCurator    { get; private set; }
    public DateTimeOffset? TokenExpiry  { get; private set; }
    public bool            IsLoggedIn   => Username is not null;

    /// <summary>All role names currently held by the authenticated user.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>Effective permission set (union of role and per-user grants) for the authenticated user.</summary>
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    /// <summary>True when the user holds the <c>Admin</c> role.</summary>
    public bool IsAdmin => Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the user holds the <c>Moderator</c> or <c>Admin</c> role.</summary>
    public bool IsModerator => Roles.Any(r =>
        r.Equals("Moderator", StringComparison.OrdinalIgnoreCase) ||
        r.Equals("Admin",     StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns <c>true</c> when the user holds the specified permission.</summary>
    /// <param name="permission">Permission string constant (e.g. <c>"ban_players"</c>).</param>
    public bool HasPermission(string permission)
        => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

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

            var username    = await js.InvokeAsync<string?>("sessionStorage.getItem", UsernameKey);
            var accountId   = await js.InvokeAsync<string?>("sessionStorage.getItem", AccountIdKey);
            var isCurator   = await js.InvokeAsync<string?>("sessionStorage.getItem", IsCuratorKey);
            var expiryStr   = await js.InvokeAsync<string?>("sessionStorage.getItem", TokenExpiryKey);
            var rolesJson   = await js.InvokeAsync<string?>("sessionStorage.getItem", RolesKey);
            var permsJson   = await js.InvokeAsync<string?>("sessionStorage.getItem", PermissionsKey);

            var expiry      = DateTimeOffset.TryParse(expiryStr, out var dt) ? dt : (DateTimeOffset?)null;
            Guid? accountGuid = Guid.TryParse(accountId, out var g) ? g : null;
            var roles       = ParseJsonStringArray(rolesJson);
            var permissions = ParseJsonStringArray(permsJson);

            Apply(jwt, username, accountGuid, isCurator is "true", expiry, roles, permissions);
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
        await js.InvokeVoidAsync("sessionStorage.setItem", RolesKey,
            System.Text.Json.JsonSerializer.Serialize(response.Roles));
        await js.InvokeVoidAsync("sessionStorage.setItem", PermissionsKey,
            System.Text.Json.JsonSerializer.Serialize(response.Permissions));

        Apply(response.AccessToken, response.Username, response.AccountId,
              response.IsCurator, response.AccessTokenExpiry, response.Roles, response.Permissions);
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

    /// <summary>Clears all auth state and removes tokens from sessionStorage.</summary>
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
            await js.InvokeVoidAsync("sessionStorage.removeItem", RolesKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", PermissionsKey);
        }
        catch (JSException) { /* Best-effort cleanup */ }

        Username    = null;
        AccountId   = null;
        IsCurator   = false;
        TokenExpiry = null;
        Roles       = [];
        Permissions = [];
        apiClient.ClearBearerToken();
        OnChange?.Invoke();
    }

    private void Apply(
        string jwt, string? username, Guid? accountId, bool isCurator,
        DateTimeOffset? expiry, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        Username    = username;
        AccountId   = accountId;
        IsCurator   = isCurator;
        TokenExpiry = expiry;
        Roles       = roles;
        Permissions = permissions;
        apiClient.SetBearerToken(jwt);
        OnChange?.Invoke();
    }

    private static IReadOnlyList<string> ParseJsonStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
