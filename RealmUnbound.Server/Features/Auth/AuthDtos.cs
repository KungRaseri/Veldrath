namespace RealmUnbound.Server.Features.Auth;

/// <summary>Request body for account registration.</summary>
public record RegisterRequest(string Email, string Username, string Password);

/// <summary>Request body for login.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Request body for refreshing an access token.</summary>
public record RefreshRequest(string RefreshToken);

/// <summary>Request body for revoking a refresh token (logout).</summary>
public record LogoutRequest(string RefreshToken);

/// <summary>Returned by register, login, and refresh endpoints.</summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry,
    Guid AccountId,
    string Username);
