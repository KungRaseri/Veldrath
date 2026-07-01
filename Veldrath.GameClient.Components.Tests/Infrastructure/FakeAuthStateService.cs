using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Auth;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// Configurable stub for <see cref="AuthStateServiceBase"/>.
/// Default behaviour: reports the user as logged in with a sentinel access token
/// so that <c>[Authorize]</c> guards pass automatically.
/// Set <see cref="IsLoggedInOverride"/> to <see langword="false"/> to simulate
/// an unauthenticated user.
/// </summary>
public sealed class FakeAuthStateService : AuthStateServiceBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeAuthStateService"/> class
    /// with a default logged-in state.
    /// </summary>
    /// <param name="api">The auth API client stub.</param>
    public FakeAuthStateService(IVeldrathAuthApiClient api) : base(api)
    {
        _accessToken = "__test_mode__";
        _refreshToken = "__test_refresh__";
        IsAuthReady = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the service reports the user as logged in.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool IsLoggedInOverride { get; set; } = true;

    /// <summary>
    /// Gets or sets the access token returned by <see cref="AuthStateServiceBase.AccessToken"/>.
    /// </summary>
    public string? AccessTokenOverride { get; set; } = "__test_mode__";

    /// <summary>
    /// Gets or sets the result of <see cref="TryRefreshAsync"/>.
    /// </summary>
    public bool TryRefreshResult { get; set; } = true;

    /// <inheritdoc />
    public override Task SetTokensAsync(AuthResponse response)
    {
        _accessToken = response.AccessToken;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SetTokensAsync(RenewJwtResponse response, string rawRefreshToken)
    {
        _accessToken = response.AccessToken;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>Simulates a token refresh attempt.</summary>
    /// <param name="expiryThreshold">Optional expiry threshold (ignored in stub).</param>
    public new async Task<bool> TryRefreshAsync(TimeSpan? expiryThreshold = null)
        => TryRefreshResult;
}
