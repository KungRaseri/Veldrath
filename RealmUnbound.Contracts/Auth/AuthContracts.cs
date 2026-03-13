namespace RealmUnbound.Contracts.Auth;

public record RegisterRequest(string Email, string Username, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry,
    Guid AccountId,
    string Username);
