using Veldrath.Auth;
using Veldrath.Contracts.Auth;

namespace Veldrath.Auth.Blazor;

/// <summary>
/// Base class for Blazor Server circuit-scoped authentication state services.
/// Holds the JWT and refresh token in server-side circuit memory only — tokens are
/// never exposed to browser storage or JavaScript, preventing XSS-based token theft.
/// </summary>
/// <remarks>
/// Derive from this class and pass the <see cref="IVeldrathAuthApiClient"/> instance to
/// the base constructor. Override <see cref="SetTokensAsync(AuthResponse)"/>,
/// <see cref="SetTokensAsync(RenewJwtResponse, string)"/>, and <see cref="ClearState"/>
/// to extend with application-specific state (e.g. roles, permissions, session IDs).
/// Call <c>base.SetTokensAsync</c> / <c>base.ClearState</c> from overrides to ensure
/// base fields are correctly updated before <see cref="OnChange"/> fires.
/// </remarks>
public abstract class AuthStateServiceBase(IVeldrathAuthApiClient api)
{
    /// <summary>Raw access token held in circuit memory.</summary>
    protected string? _accessToken;

    /// <summary>Raw refresh token held in circuit memory.</summary>
    protected string? _refreshToken;

    /// <summary>Raised whenever the authentication state changes.</summary>
    public event Action? OnChange;

    /// <summary>Gets a value indicating whether there is an active authenticated session.</summary>
    public bool IsLoggedIn => _accessToken is not null;

    /// <summary>
    /// Gets a value indicating whether the auth initialisation pass has completed.
    /// Components should render a placeholder until this is <c>true</c> to avoid
    /// flashing a "must sign in" message during the SSR prerender → circuit handoff.
    /// </summary>
    public bool IsAuthReady { get; protected set; }

    /// <summary>Gets the display name of the authenticated user, or <see langword="null"/> if not logged in.</summary>
    public string? Username { get; protected set; }

    /// <summary>Gets the account identifier of the authenticated user, or <see langword="null"/> if not logged in.</summary>
    public Guid? AccountId { get; protected set; }

    /// <summary>Gets the role names held by the authenticated user.</summary>
    public IReadOnlyList<string> Roles { get; protected set; } = [];

    /// <summary>Gets the effective permission set (union of role and per-user grants) for the authenticated user.</summary>
    public IReadOnlyList<string> Permissions { get; protected set; } = [];

    /// <summary>Gets a value indicating whether the authenticated user holds the <c>Curator</c> role.</summary>
    public bool IsCurator { get; protected set; }

    /// <summary>Gets the session identifier for the current active refresh-token session, or <see langword="null"/> if not available.</summary>
    public Guid? SessionId { get; protected set; }

    /// <summary>Gets the UTC expiry of the current access token, or <see langword="null"/> if not authenticated.</summary>
    public DateTimeOffset? AccessTokenExpiry { get; protected set; }

    /// <summary>
    /// Stores tokens and user info from an <see cref="AuthResponse"/> in circuit memory
    /// and propagates the bearer token to the API client.
    /// </summary>
    public virtual Task SetTokensAsync(AuthResponse response)
    {
        _accessToken      = response.AccessToken;
        _refreshToken     = response.RefreshToken;
        Username          = response.Username;
        AccountId         = response.AccountId;
        Roles             = response.Roles;
        Permissions       = response.Permissions;
        IsCurator         = response.IsCurator;
        SessionId         = response.SessionId;
        AccessTokenExpiry = response.AccessTokenExpiry;
        api.SetBearerToken(response.AccessToken);
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the access token and user info from a <see cref="RenewJwtResponse"/> without
    /// rotating the refresh token, and propagates the new bearer token to the API client.
    /// </summary>
    public virtual Task SetTokensAsync(RenewJwtResponse response, string rawRefreshToken)
    {
        _accessToken      = response.AccessToken;
        _refreshToken     = rawRefreshToken;
        Username          = response.Username;
        AccountId         = response.AccountId;
        Roles             = response.Roles;
        Permissions       = response.Permissions;
        IsCurator         = response.IsCurator;
        SessionId         = response.SessionId;
        AccessTokenExpiry = response.AccessTokenExpiry;
        api.SetBearerToken(response.AccessToken);
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Revokes the current session on the server (best-effort) and clears all auth state
    /// from circuit memory.
    /// </summary>
    public async Task LogOutAsync()
    {
        if (_refreshToken is not null)
        {
            try { await api.LogoutAsync(_refreshToken); }
            catch { /* best-effort — local sign-out always succeeds */ }
        }

        ClearState();
        api.ClearBearerToken();
        NotifyStateChanged();
    }

    /// <summary>
    /// Proactively renews the access token using the in-memory refresh token without rotating it.
    /// Skips the API call when the token is still fresh within <paramref name="expiryThreshold"/>.
    /// Automatically calls <see cref="LogOutAsync"/> when the server rejects the refresh token.
    /// </summary>
    /// <param name="expiryThreshold">
    /// How close to expiry the token must be before a renewal attempt is made.
    /// Defaults to two minutes when <see langword="null"/>.
    /// </param>
    /// <returns><c>true</c> if the token is valid (either still fresh or successfully renewed).</returns>
    public async Task<bool> TryRefreshAsync(TimeSpan? expiryThreshold = null)
    {
        if (_refreshToken is null) return false;

        var threshold = expiryThreshold ?? TimeSpan.FromMinutes(2);
        if (AccessTokenExpiry.HasValue && AccessTokenExpiry.Value - DateTimeOffset.UtcNow > threshold)
            return true;

        var renewed = await api.RenewJwtAsync(_refreshToken);
        if (renewed is null)
        {
            // The refresh token was rejected (revoked or expired) — clear stale auth state
            // so the circuit reflects reality and the user is directed to re-login.
            await LogOutAsync();
            return false;
        }

        await SetTokensAsync(renewed, _refreshToken);
        return true;
    }

    /// <summary>
    /// Marks the auth initialisation pass as complete and fires <see cref="OnChange"/>.
    /// Call this when the startup check finishes, whether or not a valid session was found.
    /// </summary>
    public void MarkReady()
    {
        IsAuthReady = true;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all base authentication fields. Override to also clear derived-class state,
    /// calling <c>base.ClearState()</c> to ensure base fields are wiped before
    /// <see cref="OnChange"/> fires.
    /// </summary>
    protected virtual void ClearState()
    {
        _accessToken      = null;
        _refreshToken     = null;
        Username          = null;
        AccountId         = null;
        Roles             = [];
        Permissions       = [];
        IsCurator         = false;
        SessionId         = null;
        AccessTokenExpiry = null;
    }

    /// <summary>Fires <see cref="OnChange"/> to notify subscribers of a state change.</summary>
    protected void NotifyStateChanged() => OnChange?.Invoke();
}
