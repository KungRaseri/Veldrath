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

    public string? Username  { get; private set; }
    public Guid?   AccountId { get; private set; }
    public bool    IsLoggedIn => Username is not null;

    public event Action? OnChange;

    /// <summary>Called when the Blazor circuit initialises. Restores any saved token.</summary>
    public async Task InitialiseAsync()
    {
        var result = await storage.GetAsync<string>(AccessTokenKey);
        if (!result.Success || string.IsNullOrEmpty(result.Value))
            return;

        var username  = await storage.GetAsync<string>(UsernameKey);
        var accountId = await storage.GetAsync<string>(AccountIdKey);

        Apply(result.Value, username.Value, accountId.Value is null ? null : Guid.Parse(accountId.Value!));
    }

    /// <summary>Stores a token pair received after OAuth login.</summary>
    public async Task SetTokensAsync(AuthResponse response)
    {
        await storage.SetAsync(AccessTokenKey,  response.AccessToken);
        await storage.SetAsync(RefreshTokenKey, response.RefreshToken);
        await storage.SetAsync(UsernameKey,     response.Username);
        await storage.SetAsync(AccountIdKey,    response.AccountId.ToString());

        Apply(response.AccessToken, response.Username, response.AccountId);
    }

    public async Task LogOutAsync()
    {
        await storage.DeleteAsync(AccessTokenKey);
        await storage.DeleteAsync(RefreshTokenKey);
        await storage.DeleteAsync(UsernameKey);
        await storage.DeleteAsync(AccountIdKey);

        Username  = null;
        AccountId = null;
        apiClient.ClearBearerToken();
        OnChange?.Invoke();
    }

    private void Apply(string jwt, string? username, Guid? accountId)
    {
        Username  = username;
        AccountId = accountId;
        apiClient.SetBearerToken(jwt);
        OnChange?.Invoke();
    }
}
