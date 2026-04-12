namespace Veldrath.Contracts.Auth;

public record RegisterRequest(string Email, string Username, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

/// <summary>Requests a password-reset email for the given address.</summary>
/// <param name="Email">The email address associated with the account.</param>
public record ForgotPasswordRequest(string Email);

/// <summary>Completes a password reset using the token sent by email.</summary>
/// <param name="Email">The email address identifying the account.</param>
/// <param name="Token">The ASP.NET Identity password-reset token from the email link.</param>
/// <param name="NewPassword">The new password to set on the account.</param>
public record ResetPasswordRequest(string Email, string Token, string NewPassword);

/// <summary>Returned by the server when a logged-in client requests an exchange code for SSO handoff.</summary>
/// <param name="Code">Opaque 64-character hex code valid for 60 seconds.</param>
/// <param name="AccountId">Account identifier of the authenticated user the code was issued for.</param>
public record CreateExchangeCodeResponse(string Code, Guid AccountId);

/// <summary>
/// Sent by the Foundry callback page to redeem a single-use exchange code for a full
/// <see cref="AuthResponse"/>. The code is valid for 60 seconds and can only be used once.
/// </summary>
/// <param name="Code">Opaque 64-character hex exchange code issued by the server OAuth callback.</param>
/// <param name="AccountId">Account identifier of the user the code was issued for.</param>
public record ExchangeCodeRequest(string Code, Guid AccountId);

/// <summary>
/// Returned after successful authentication. Contains the JWT access token, a refresh token,
/// and the full set of roles and permissions the account currently holds.
/// </summary>
/// <param name="AccessToken">Short-lived JWT bearer token.</param>
/// <param name="RefreshToken">Opaque long-lived refresh token for silent renewal.</param>
/// <param name="AccessTokenExpiry">UTC timestamp when the access token expires.</param>
/// <param name="AccountId">Unique account identifier.</param>
/// <param name="Username">Display username.</param>
/// <param name="Roles">All role names currently assigned to this account.</param>
/// <param name="Permissions">Effective permission set (union of role and per-user grants).</param>
/// <param name="IsCurator">
/// Legacy convenience flag — <c>true</c> when <see cref="Roles"/> contains <c>"Curator"</c>.
/// Retained for backward compatibility with existing clients.
/// </param>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry,
    Guid AccountId,
    string Username,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsCurator = false,
    Guid? SessionId = null);
