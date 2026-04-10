using RealmUnbound.Contracts.Auth;

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

    /// <summary>Raised whenever auth state changes.</summary>
    public event Action? OnChange;

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
        Apply(response.Username, response.AccountId, response.IsCurator,
              response.AccessTokenExpiry, response.Roles, response.Permissions);
        return Task.CompletedTask;
    }

    /// <summary>Proactively refreshes the access token using the in-memory refresh token.</summary>
    /// <returns><c>true</c> if the refresh succeeded and state was updated.</returns>
    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        var refreshed = await apiClient.RefreshTokenAsync(_refreshToken);
        if (refreshed is null) return false;

        await SetTokensAsync(refreshed);
        return true;
    }

    /// <summary>Clears all auth state from circuit memory.</summary>
    public Task LogOutAsync()
    {
        _accessToken  = null;
        _refreshToken = null;
        Username    = null;
        AccountId   = null;
        IsCurator   = false;
        TokenExpiry = null;
        Roles       = [];
        Permissions = [];
        apiClient.ClearBearerToken();
        OnChange?.Invoke();
        return Task.CompletedTask;
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
        apiClient.SetBearerToken(_accessToken!);
        OnChange?.Invoke();
    }
}

