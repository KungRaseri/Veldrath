using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Auth;

namespace RealmFoundry.Services;

/// <summary>
/// Tracks the current user's authentication state within the Blazor Server circuit.
/// Tokens are held in circuit-scoped memory only — they are never written to
/// <c>sessionStorage</c>, <c>localStorage</c>, or any other browser-side storage.
/// This eliminates the XSS-based token-theft vector entirely. The tradeoff is that
/// the user must re-authenticate after a full page reload (circuit teardown).
/// </summary>
public sealed class AuthStateService(RealmFoundryApiClient apiClient) : AuthStateServiceBase(apiClient)
{
    // ── Foundry-specific state ────────────────────────────────────────────────

    /// <summary>The refresh-token session ID for the current active session.</summary>
    public Guid? SessionId { get; private set; }

    /// <summary>True when the user holds the <c>Curator</c> role.</summary>
    public bool IsCurator { get; private set; }

    /// <summary>Effective permission set (union of role and per-user grants) for the authenticated user.</summary>
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    // ── Backward compat ───────────────────────────────────────────────────────

    /// <summary>Expiry timestamp of the current access token. Alias for <see cref="AuthStateServiceBase.AccessTokenExpiry"/>.</summary>
    public DateTimeOffset? TokenExpiry => AccessTokenExpiry;

    /// <summary>
    /// No-op kept for backward compatibility. Tokens are circuit-scoped memory only
    /// and are not persisted in browser storage, so there is nothing to restore on circuit start.
    /// </summary>
#pragma warning disable CA1822 // Instance method preserved so Razor components can call it via the injected service.
    public Task InitialiseAsync() => Task.CompletedTask;
#pragma warning restore CA1822

    // ── Derived role / permission checks ─────────────────────────────────────

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
        AccessTokenExpiry.HasValue && (AccessTokenExpiry.Value - DateTimeOffset.UtcNow) < TimeSpan.FromMinutes(2);

    // ── Overrides ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores tokens from an <see cref="AuthResponse"/> in circuit memory, setting
    /// Foundry-specific fields in addition to the base state.
    /// </summary>
    public override Task SetTokensAsync(AuthResponse response)
    {
        SessionId   = response.SessionId;
        IsCurator   = response.IsCurator;
        Permissions = response.Permissions;
        return base.SetTokensAsync(response);
    }

    /// <summary>
    /// Updates the access token from a <see cref="RenewJwtResponse"/>, setting
    /// Foundry-specific fields in addition to the base state.
    /// </summary>
    public override Task SetTokensAsync(RenewJwtResponse response, string rawRefreshToken)
    {
        SessionId   = response.SessionId;
        IsCurator   = response.IsCurator;
        Permissions = response.Permissions;
        return base.SetTokensAsync(response, rawRefreshToken);
    }

    /// <summary>Clears Foundry-specific auth fields before base fields are cleared.</summary>
    protected override void ClearState()
    {
        SessionId   = null;
        IsCurator   = false;
        Permissions = [];
        base.ClearState();
    }
}

