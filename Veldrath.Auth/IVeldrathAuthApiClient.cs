using Veldrath.Contracts.Account;
using Veldrath.Contracts.Auth;

namespace Veldrath.Auth;

/// <summary>
/// Defines the client-side contract for communicating with Veldrath.Server's authentication API.
/// </summary>
public interface IVeldrathAuthApiClient
{
    /// <summary>Sets the <c>Authorization: Bearer</c> header on all subsequent requests.</summary>
    void SetBearerToken(string token);

    /// <summary>Clears the <c>Authorization</c> header.</summary>
    void ClearBearerToken();

    /// <summary>Returns <c>true</c> when the server's <c>/health</c> endpoint is reachable.</summary>
    Task<bool> IsServerReachableAsync(CancellationToken ct = default);

    /// <summary>Registers a new account. Returns <see langword="null"/> on failure.</summary>
    Task<AuthResponse?> RegisterAsync(string email, string username, string password, CancellationToken ct = default);

    /// <summary>Authenticates with email and password. Returns <see langword="null"/> on failure.</summary>
    Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Rotates the refresh token and returns a new token pair. Returns <see langword="null"/> on failure.</summary>
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Issues a fresh JWT without rotating the refresh token.
    /// Returns <see langword="null"/> if the refresh token is revoked or expired.
    /// </summary>
    Task<RenewJwtResponse?> RenewJwtAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes the given refresh token on the server, ending the session.
    /// Best-effort — failures are silently swallowed so the local sign-out always completes.
    /// </summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Redeems a single-use exchange code for a full token pair. Returns <see langword="null"/> on failure.</summary>
    Task<AuthResponse?> ExchangeCodeAsync(string code, Guid accountId, CancellationToken ct = default);

    /// <summary>Creates a single-use exchange code for the currently authenticated account.</summary>
    Task<CreateExchangeCodeResponse?> CreateExchangeCodeAsync(CancellationToken ct = default);

    /// <summary>Requests a password-reset email. Always returns successfully to prevent account enumeration.</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);

    /// <summary>Completes a password reset using a token received by email.</summary>
    Task<(bool Ok, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);

    /// <summary>Confirms an email address using a <paramref name="userId"/> and <paramref name="token"/> from the confirmation link.</summary>
    Task<(bool Ok, string? Error)> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default);

    /// <summary>Requests the server to resend the email-confirmation message for the authenticated account.</summary>
    Task<(bool Ok, string? Error)> ResendEmailConfirmationAsync(CancellationToken ct = default);

    // ── Self-service account management ──────────────────────────────────────

    /// <summary>Returns the authenticated account's own full profile including linked providers.</summary>
    Task<AccountProfileDto?> GetMyProfileAsync(CancellationToken ct = default);

    /// <summary>Updates the authenticated account's optional public display name and biography.</summary>
    Task<(bool Ok, string? Error)> UpdateProfileAsync(string? displayName, string? bio, CancellationToken ct = default);

    /// <summary>Changes the authenticated account's username.</summary>
    Task<(bool Ok, string? Error)> ChangeUsernameAsync(string newUsername, CancellationToken ct = default);

    /// <summary>Changes the authenticated account's password.</summary>
    Task<(bool Ok, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Returns all OAuth providers currently linked to the authenticated account.</summary>
    Task<IReadOnlyList<LinkedProviderDto>> GetLinkedProvidersAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes the specified OAuth provider login from the authenticated account.
    /// Fails if the provider is the account's only login method and no password is set.
    /// </summary>
    Task<(bool Ok, string? Error)> UnlinkProviderAsync(string provider, string providerKey, CancellationToken ct = default);
}
