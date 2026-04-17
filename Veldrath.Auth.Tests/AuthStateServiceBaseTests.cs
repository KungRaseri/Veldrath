using FluentAssertions;
using Moq;
using Veldrath.Auth;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Auth;
using Xunit;

namespace Veldrath.Auth.Tests;

// Minimal concrete subclass to allow testing the abstract base.
internal sealed class TestAuthStateService(IVeldrathAuthApiClient api) : AuthStateServiceBase(api)
{
    public string? ExposedAccessToken  => _accessToken;
    public string? ExposedRefreshToken => _refreshToken;
}

// Extended subclass to verify ClearState override chain.
internal sealed class ExtendedAuthStateService(IVeldrathAuthApiClient api) : AuthStateServiceBase(api)
{
    public bool ExtraFieldCleared { get; private set; }
    public string? ExtraField { get; private set; } = "value";

    protected override void ClearState()
    {
        ExtraField        = null;
        ExtraFieldCleared = true;
        base.ClearState();
    }
}

public class AuthStateServiceBaseTests
{
    private static (TestAuthStateService Service, Mock<IVeldrathAuthApiClient> Api) CreateService()
    {
        var api = new Mock<IVeldrathAuthApiClient>();
        return (new TestAuthStateService(api.Object), api);
    }

    private static AuthResponse MakeAuthResponse(string access = "access-token", string refresh = "refresh-token") =>
        new(access, refresh, DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", ["Player"], [], false, Guid.NewGuid());

    private static RenewJwtResponse MakeRenewResponse(string access = "renewed-token") =>
        new(access, DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", ["Player"], [], false, null);

    // ── SetTokensAsync(AuthResponse) ─────────────────────────────────────────

    [Fact]
    public async Task SetTokensAsync_AuthResponse_SetsAllFields()
    {
        var (svc, api) = CreateService();
        var response   = new AuthResponse(
            "access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", ["Player"], ["perm:edit"], true, Guid.NewGuid());

        await svc.SetTokensAsync(response);

        svc.IsLoggedIn.Should().BeTrue();
        svc.Username.Should().Be(response.Username);
        svc.AccountId.Should().Be(response.AccountId);
        svc.Roles.Should().BeEquivalentTo(response.Roles);
        svc.Permissions.Should().BeEquivalentTo(response.Permissions);
        svc.IsCurator.Should().Be(response.IsCurator);
        svc.SessionId.Should().Be(response.SessionId);
        svc.AccessTokenExpiry.Should().Be(response.AccessTokenExpiry);
        svc.ExposedAccessToken.Should().Be(response.AccessToken);
        svc.ExposedRefreshToken.Should().Be(response.RefreshToken);
        api.Verify(a => a.SetBearerToken(response.AccessToken), Times.Once);
    }

    [Fact]
    public async Task SetTokensAsync_AuthResponse_FiresOnChange()
    {
        var (svc, _) = CreateService();
        var fired    = false;
        svc.OnChange += () => fired = true;

        await svc.SetTokensAsync(MakeAuthResponse());

        fired.Should().BeTrue();
    }

    // ── SetTokensAsync(RenewJwtResponse, string) ─────────────────────────────

    [Fact]
    public async Task SetTokensAsync_RenewResponse_UpdatesAllMappedFields()
    {
        var (svc, api) = CreateService();
        await svc.SetTokensAsync(MakeAuthResponse("old-access", "my-refresh"));

        var sessionId = Guid.NewGuid();
        var renewed = new RenewJwtResponse(
            "new-access", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "renewed-user", ["Admin"], ["perm:admin"], true, sessionId);
        await svc.SetTokensAsync(renewed, "my-refresh");

        svc.ExposedAccessToken.Should().Be("new-access");
        svc.ExposedRefreshToken.Should().Be("my-refresh");
        svc.Username.Should().Be("renewed-user");
        svc.Roles.Should().BeEquivalentTo(["Admin"]);
        svc.Permissions.Should().BeEquivalentTo(["perm:admin"]);
        svc.IsCurator.Should().BeTrue();
        svc.SessionId.Should().Be(sessionId);
        api.Verify(a => a.SetBearerToken("new-access"), Times.Once);
    }

    // ── IsLoggedIn ────────────────────────────────────────────────────────────

    [Fact]
    public void IsLoggedIn_FalseByDefault()
    {
        var (svc, _) = CreateService();
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task IsLoggedIn_TrueAfterSetTokens()
    {
        var (svc, _) = CreateService();
        await svc.SetTokensAsync(MakeAuthResponse());
        svc.IsLoggedIn.Should().BeTrue();
    }

    // ── MarkReady ─────────────────────────────────────────────────────────────

    [Fact]
    public void MarkReady_SetsIsAuthReady()
    {
        var (svc, _) = CreateService();
        svc.IsAuthReady.Should().BeFalse();
        svc.MarkReady();
        svc.IsAuthReady.Should().BeTrue();
    }

    [Fact]
    public void MarkReady_FiresOnChange()
    {
        var (svc, _) = CreateService();
        var fired    = false;
        svc.OnChange += () => fired = true;
        svc.MarkReady();
        fired.Should().BeTrue();
    }

    // ── LogOutAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogOutAsync_ClearsState()
    {
        var (svc, _) = CreateService();
        await svc.SetTokensAsync(MakeAuthResponse());

        await svc.LogOutAsync();

        svc.IsLoggedIn.Should().BeFalse();
        svc.Username.Should().BeNull();
        svc.AccountId.Should().BeNull();
        svc.Permissions.Should().BeEmpty();
        svc.IsCurator.Should().BeFalse();
        svc.SessionId.Should().BeNull();
        svc.ExposedAccessToken.Should().BeNull();
        svc.ExposedRefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task LogOutAsync_CallsServerLogout_WhenRefreshTokenPresent()
    {
        var (svc, api) = CreateService();
        await svc.SetTokensAsync(MakeAuthResponse("access", "rt-123"));

        await svc.LogOutAsync();

        api.Verify(a => a.LogoutAsync("rt-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogOutAsync_DoesNotCallServerLogout_WhenNotLoggedIn()
    {
        var (svc, api) = CreateService();
        await svc.LogOutAsync();
        api.Verify(a => a.LogoutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogOutAsync_FiresOnChange()
    {
        var (svc, _) = CreateService();
        await svc.SetTokensAsync(MakeAuthResponse());
        var fired = false;
        svc.OnChange += () => fired = true;

        await svc.LogOutAsync();
        fired.Should().BeTrue();
    }

    // ── TryRefreshAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenNoRefreshToken()
    {
        var (svc, _) = CreateService();
        var result   = await svc.TryRefreshAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsTrueWithoutApiCall_WhenTokenStillFresh()
    {
        var (svc, api) = CreateService();
        var response   = new AuthResponse(
            "access", "refresh", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", [], [], false, null);
        await svc.SetTokensAsync(response);

        var result = await svc.TryRefreshAsync(TimeSpan.FromMinutes(2));

        result.Should().BeTrue();
        api.Verify(a => a.RenewJwtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRefreshAsync_CallsRenewJwt_WhenTokenNearExpiry()
    {
        var (svc, api) = CreateService();
        // Token expires in 30 seconds — inside the 2-minute threshold
        var response = new AuthResponse(
            "access", "refresh", DateTimeOffset.UtcNow.AddSeconds(30),
            Guid.NewGuid(), "user", [], [], false, null);
        await svc.SetTokensAsync(response);

        var renewed = MakeRenewResponse("new-access");
        api.Setup(a => a.RenewJwtAsync("refresh", It.IsAny<CancellationToken>()))
           .ReturnsAsync(renewed);

        var result = await svc.TryRefreshAsync();

        result.Should().BeTrue();
        svc.ExposedAccessToken.Should().Be("new-access");
    }

    [Fact]
    public async Task TryRefreshAsync_LogsOutAndReturnsFalse_WhenRenewFails()
    {
        var (svc, api) = CreateService();
        var response = new AuthResponse(
            "access", "refresh", DateTimeOffset.UtcNow.AddSeconds(30),
            Guid.NewGuid(), "user", [], [], false, null);
        await svc.SetTokensAsync(response);

        api.Setup(a => a.RenewJwtAsync("refresh", It.IsAny<CancellationToken>()))
           .ReturnsAsync((RenewJwtResponse?)null);
        api.Setup(a => a.LogoutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);

        var result = await svc.TryRefreshAsync();

        result.Should().BeFalse();
        svc.IsLoggedIn.Should().BeFalse();
    }

    // ── ClearState override chain ─────────────────────────────────────────────

    [Fact]
    public async Task LogOutAsync_InvokesDerivedClearState_BeforeNotifyingChange()
    {
        var api = new Mock<IVeldrathAuthApiClient>();
        api.Setup(a => a.LogoutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        var svc = new ExtendedAuthStateService(api.Object);

        await svc.SetTokensAsync(MakeAuthResponse());
        await svc.LogOutAsync();

        svc.ExtraFieldCleared.Should().BeTrue();
        svc.ExtraField.Should().BeNull();
    }
}
