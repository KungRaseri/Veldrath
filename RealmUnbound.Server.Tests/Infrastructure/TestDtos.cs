namespace RealmUnbound.Server.Tests.Infrastructure;

// Shared response DTOs used across integration test classes.
// Named with "Result" suffix to avoid clashing with server-side record definitions.

internal record AuthResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiry,
    Guid AccountId,
    string Username);

internal record CharacterResult(
    Guid Id,
    int SlotIndex,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset LastPlayedAt);
