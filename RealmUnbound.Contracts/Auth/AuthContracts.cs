namespace RealmUnbound.Contracts.Auth;

public record RegisterRequest(string Email, string Username, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

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
    bool IsCurator = false);
