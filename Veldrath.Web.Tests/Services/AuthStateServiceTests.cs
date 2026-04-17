using Moq;
using Veldrath.Contracts.Auth;

namespace Veldrath.Web.Tests.Services;

public class AuthStateServiceTests
{
    private static AuthResponse MakeAuthResponse(string token = "jwt", string refresh = "rt") =>
        new(token, refresh, DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "alice", ["Player"], []);

    private static RenewJwtResponse MakeRenewResponse(string token = "new-jwt") =>
        new(token, DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "alice", ["Player"], []);

    // Creates a loose VeldrathApiClient mock — non-virtual methods (SetBearerToken / ClearBearerToken)
    // call through to the real implementation via CallBase = true, which is harmless on a throw-away HttpClient.
    private static VeldrathApiClient MakeApi() =>
        new Mock<VeldrathApiClient>(new HttpClient()) { CallBase = true }.Object;

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsNotLoggedIn()
    {
        var svc = new AuthStateService(MakeApi());
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public void InitialState_IsNotAuthReady()
    {
        var svc = new AuthStateService(MakeApi());
        svc.IsAuthReady.Should().BeFalse();
    }

    [Fact]
    public void InitialState_TokensAreNull()
    {
        var svc = new AuthStateService(MakeApi());
        svc.AccessToken.Should().BeNull();
        svc.RefreshToken.Should().BeNull();
    }

    // ── SetTokensAsync (AuthResponse) ─────────────────────────────────────────

    [Fact]
    public async Task SetTokensAsync_AuthResponse_SetsLoggedIn()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeAuthResponse());
        svc.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public async Task SetTokensAsync_AuthResponse_SetsUsername()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeAuthResponse());
        svc.Username.Should().Be("alice");
    }

    [Fact]
    public async Task SetTokensAsync_AuthResponse_RaisesOnChange()
    {
        var svc = new AuthStateService(MakeApi());
        var raised = false;
        svc.OnChange += () => raised = true;

        await svc.SetTokensAsync(MakeAuthResponse());

        raised.Should().BeTrue();
    }

    // ── SetTokensAsync (RenewJwtResponse) ────────────────────────────────────

    [Fact]
    public async Task SetTokensAsync_RenewJwtResponse_SetsNewAccessToken()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeRenewResponse("fresh-token"), "rt-value");
        svc.AccessToken.Should().Be("fresh-token");
        svc.RefreshToken.Should().Be("rt-value");
    }

    // ── LogOutAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogOutAsync_ClearsLoggedInState()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeAuthResponse());
        await svc.LogOutAsync();
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task LogOutAsync_ClearsUsername()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeAuthResponse());
        await svc.LogOutAsync();
        svc.Username.Should().BeNull();
    }

    [Fact]
    public async Task LogOutAsync_RaisesOnChange()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeAuthResponse());
        var raised = false;
        svc.OnChange += () => raised = true;

        await svc.LogOutAsync();

        raised.Should().BeTrue();
    }

    // ── MarkReady ─────────────────────────────────────────────────────────────

    [Fact]
    public void MarkReady_SetsIsAuthReady()
    {
        var svc = new AuthStateService(MakeApi());
        svc.MarkReady();
        svc.IsAuthReady.Should().BeTrue();
    }

    [Fact]
    public void MarkReady_RaisesOnChange()
    {
        var svc = new AuthStateService(MakeApi());
        var raised = false;
        svc.OnChange += () => raised = true;

        svc.MarkReady();

        raised.Should().BeTrue();
    }

    // ── TryRefreshAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenNoRefreshToken()
    {
        var svc = new AuthStateService(MakeApi());

        var result = await svc.TryRefreshAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsTrue_WhenTokenIsStillFresh()
    {
        var svc = new AuthStateService(MakeApi());
        await svc.SetTokensAsync(MakeAuthResponse());

        // Token expires in 1 hour, threshold is 2 minutes — still fresh
        var result = await svc.TryRefreshAsync(TimeSpan.FromMinutes(2));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryRefreshAsync_CallsRenewJwt_WhenTokenIsExpiringSoon()
    {
        var mock = new Mock<VeldrathApiClient>(new HttpClient()) { CallBase = true };
        mock.Setup(a => a.RenewJwtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRenewResponse("refreshed-jwt"));

        var svc = new AuthStateService(mock.Object);

        // Set a token that expires in 30 seconds — below default 2-minute threshold
        var resp = new AuthResponse("jwt", "rt", DateTimeOffset.UtcNow.AddSeconds(30),
            Guid.NewGuid(), "alice", ["Player"], []);
        await svc.SetTokensAsync(resp);

        var result = await svc.TryRefreshAsync();

        result.Should().BeTrue();
        mock.Verify(a => a.RenewJwtAsync("rt", It.IsAny<CancellationToken>()), Times.Once);
        svc.AccessToken.Should().Be("refreshed-jwt");
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenRenewFails()
    {
        var mock = new Mock<VeldrathApiClient>(new HttpClient()) { CallBase = true };
        mock.Setup(a => a.RenewJwtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RenewJwtResponse?)null);

        var svc = new AuthStateService(mock.Object);

        var resp = new AuthResponse("jwt", "rt", DateTimeOffset.UtcNow.AddSeconds(10),
            Guid.NewGuid(), "alice", [], []);
        await svc.SetTokensAsync(resp);

        var result = await svc.TryRefreshAsync();

        result.Should().BeFalse();
        // A rejected renewal means the refresh token is revoked/expired — the circuit
        // must be logged out so the user is redirected to sign in again.
        svc.IsLoggedIn.Should().BeFalse();
    }
}
