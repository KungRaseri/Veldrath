using Veldrath.Auth;
using Veldrath.Contracts.Account;
using Veldrath.Contracts.Auth;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// Configurable stub for <see cref="IVeldrathAuthApiClient"/>.
/// Default behaviour: all operations succeed with no-op or empty results.
/// Set properties to simulate specific server behaviours.
/// </summary>
public sealed class FakeVeldrathAuthApiClient : IVeldrathAuthApiClient
{
    /// <summary>Gets or sets the auth response returned by <see cref="LoginAsync"/>.</summary>
    public AuthResponse? LoginResponse { get; set; } = new AuthResponse(
        "test-access-token",
        "test-refresh-token",
        DateTimeOffset.UtcNow.AddMinutes(15),
        Guid.NewGuid(),
        "TestUser",
        Array.Empty<string>(),
        Array.Empty<string>());

    /// <summary>Gets or sets the auth response returned by <see cref="RegisterAsync"/>.</summary>
    public AuthResponse? RegisterResponse { get; set; } = null;

    /// <summary>Gets or sets the auth response returned by <see cref="RefreshTokenAsync"/>.</summary>
    public AuthResponse? RefreshTokenResponse { get; set; } = null;

    /// <summary>Gets or sets the renew response returned by <see cref="RenewJwtAsync"/>.</summary>
    public RenewJwtResponse? RenewJwtResponse { get; set; } = new RenewJwtResponse(
        "renewed-access-token",
        DateTimeOffset.UtcNow.AddMinutes(15),
        Guid.NewGuid(),
        "TestUser",
        Array.Empty<string>(),
        Array.Empty<string>());

    /// <summary>Gets the number of times <see cref="LogoutAsync"/> was called.</summary>
    public int LogoutCallCount { get; private set; }

    /// <inheritdoc />
    public void SetBearerToken(string token) { }

    /// <inheritdoc />
    public void ClearBearerToken() { }

    /// <inheritdoc />
    public Task<bool> IsServerReachableAsync(CancellationToken ct = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<AuthResponse?> RegisterAsync(string email, string username, string password, CancellationToken ct = default)
        => Task.FromResult(RegisterResponse);

    /// <inheritdoc />
    public Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
        => Task.FromResult(LoginResponse);

    /// <inheritdoc />
    public Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult(RefreshTokenResponse);

    /// <inheritdoc />
    public Task<RenewJwtResponse?> RenewJwtAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult(RenewJwtResponse);

    /// <inheritdoc />
    public Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        LogoutCallCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AuthResponse?> ExchangeCodeAsync(string code, Guid accountId, CancellationToken ct = default)
        => Task.FromResult<AuthResponse?>(null);

    /// <inheritdoc />
    public Task<CreateExchangeCodeResponse?> CreateExchangeCodeAsync(CancellationToken ct = default)
        => Task.FromResult<CreateExchangeCodeResponse?>(null);

    /// <inheritdoc />
    public Task ForgotPasswordAsync(string email, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ConfirmEmailAsync(string userId, string token, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ResendEmailConfirmationAsync(CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<AccountProfileDto?> GetMyProfileAsync(CancellationToken ct = default)
        => Task.FromResult<AccountProfileDto?>(null);

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> UpdateProfileAsync(string? displayName, string? bio, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ChangeUsernameAsync(string newUsername, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<IReadOnlyList<LinkedProviderDto>> GetLinkedProvidersAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LinkedProviderDto>>([]);

    /// <inheritdoc />
    public Task<(bool Ok, string? Error)> UnlinkProviderAsync(string provider, string providerKey, CancellationToken ct = default)
        => Task.FromResult((true, (string?)null));

    /// <inheritdoc />
    public Task<IReadOnlyList<AccountSessionDto>> GetSessionsAsync(Guid? currentSessionId = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AccountSessionDto>>([]);

    /// <inheritdoc />
    public Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> RevokeOtherSessionsAsync(Guid currentSessionId, CancellationToken ct = default) => Task.FromResult(true);
}
