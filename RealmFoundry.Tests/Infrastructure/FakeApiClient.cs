using RealmUnbound.Contracts.Auth;

namespace RealmFoundry.Tests.Infrastructure;

/// <summary>
/// Stub for <see cref="RealmFoundryApiClient"/> used in tests that need to
/// control the result of <see cref="RefreshTokenAsync"/> without hitting the network.
/// </summary>
internal sealed class FakeApiClient : RealmFoundryApiClient
{
    private AuthResponse? _refreshResult;

    public FakeApiClient() : base(new System.Net.Http.HttpClient
    {
        BaseAddress = new Uri("https://localhost")
    }) { }

    public void SetRefreshResult(AuthResponse? response) => _refreshResult = response;

    public override Task<AuthResponse?> RefreshTokenAsync(
        string refreshToken, CancellationToken ct = default)
        => Task.FromResult(_refreshResult);
}
