using Veldrath.Contracts.Auth;

namespace RealmFoundry.Services;

/// <summary>
/// Tracks the current user's authentication state within the Blazor Server circuit.
/// Tokens are held in circuit-scoped memory only — they are never written to
/// <c>sessionStorage</c>, <c>localStorage</c>, or any other browser-side storage.
/// This eliminates the XSS-based token-theft vector entirely. The tradeoff is that
/// the user must re-authenticate after a full page reload (circuit teardown).
/// </summary>
public sealed class AuthStateService(RealmFoundryApiClient apiClient)
{
    // Tokens stored only in circuit memory — no JS interop, no DOM exposure.
    private string? _accessToken;
    private string? _refreshToken;

    /// <summary>The refresh-token session ID for the current active session.</summary>
    public Guid? SessionId { get; private set; }

    /// <summary>The authenticated user's display name.</summary>
    public string? Username { get; private set; }

    /// <summary>The authenticated user's account identifier.</summary>
    public Guid? AccountId { get; private set; }

    /// <summary>True when the user holds the <c>Curator</c> role.</summary>
    public bool IsCurator { get; private set; }

    /// <summary>Expiry timestamp of the current access token.</summary>
    public DateTimeOffset? TokenExpiry { get; private set; }

    /// <summary>True when an access token is present in circuit memory.</summary>
    public bool IsLoggedIn => _accessToken is not null;

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

    /// <summary>
    /// True once the startup auth check has completed — either auth was successfully
    /// restored or the check confirmed there is no active session.
    /// Components should render a blank placeholder until this is <c>true</c> to
    /// avoid flashing a "must sign in" message while the async check is still running.
    /// </summary>
    public bool IsAuthReady { get; private set; }

    /// <summary>Raised whenever auth state changes.</summary>
    public event Action? OnChange;

    /// <summary>
    /// Marks the startup auth check as complete without setting a logged-in session.
    /// Call this when the check finishes and no valid session was found.
    /// Fires <see cref="OnChange"/> so subscriber components can re-render.
    /// </summary>
    public void MarkReady()
    {
        IsAuthReady = true;
        OnChange?.Invoke();
    }

    /// <summary>
    /// No-op kept for backward compatibility. Tokens are circuit-scoped memory only
    /// and are not persisted in browser storage, so there is nothing to restore on circuit start.
    /// </summary>
    public Task InitialiseAsync() => Task.CompletedTask;

    /// <summary>Stores a token pair in circuit memory and applies it as the active auth state.</summary>
    /// <param name="response">The <see cref="AuthResponse"/> returned from the server.</param>
    public Task SetTokensAsync(AuthResponse response)
    {
        _accessToken  = response.AccessToken;
        _refreshToken = response.RefreshToken;
        SessionId     = response.SessionId;
        Apply(response.Username, response.AccountId, response.IsCurator,
              response.AccessTokenExpiry, response.Roles, response.Permissions);
        return Task.CompletedTask;
    }

    /// <summary>Proactively renews the access token using the in-memory refresh token without rotating it.</summary>
    /// <remarks>
    /// Calls <c>POST /api/auth/renew-jwt</c> so the HttpOnly cookie refresh token never needs to
    /// change — the browser cookie and the circuit's in-memory token stay permanently in sync.
    /// </remarks>
    /// <returns><c>true</c> if the renewal succeeded and state was updated.</returns>
    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        var renewed = await apiClient.RenewJwtAsync(_refreshToken);
        if (renewed is null) return false;

        _accessToken = renewed.AccessToken;
        Apply(renewed.Username, renewed.AccountId, renewed.IsCurator,
              renewed.AccessTokenExpiry, renewed.Roles, renewed.Permissions);
        return true;
    }

    /// <summary>Revokes the current session on the server and clears all auth state from circuit memory.</summary>
    public async Task LogOutAsync()
    {
        if (_refreshToken is not null)
            await apiClient.LogoutAsync(_refreshToken);

        _accessToken  = null;
        _refreshToken = null;
        SessionId   = null;
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
        string? username, Guid? accountId, bool isCurator,
        DateTimeOffset? expiry, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        Username    = username;
        AccountId   = accountId;
        IsCurator   = isCurator;
        TokenExpiry = expiry;
        Roles       = roles;
        Permissions = permissions;
        IsAuthReady = true;
        apiClient.SetBearerToken(_accessToken!);
        OnChange?.Invoke();
    }
}

