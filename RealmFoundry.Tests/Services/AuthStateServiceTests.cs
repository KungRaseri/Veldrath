using RealmFoundry.Tests.Infrastructure;
using Veldrath.Contracts.Auth;

namespace RealmFoundry.Tests.Services;

public class AuthStateServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static (AuthStateService Service, FakeApiClient Api) Build()
    {
        var api = new FakeApiClient();
        var svc = new AuthStateService(api);
        return (svc, api);
    }

    private static AuthResponse MakeResponse(string username = "alice", bool isCurator = false) =>
        new("jwt-token", "refresh-token",
            DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), username, [], [], isCurator);

    // ── InitialiseAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task InitialiseAsync_IsNoOp_UserRemainsLoggedOut()
    {
        var (svc, _) = Build();
        await svc.InitialiseAsync();
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task InitialiseAsync_DoesNotThrow()
    {
        var (svc, _) = Build();
        var act = () => svc.InitialiseAsync();
        await act.Should().NotThrowAsync();
    }

    // ── SetTokensAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetTokensAsync_SetsIsLoggedIn()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        svc.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public async Task SetTokensAsync_SetsUsername()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse("bob"));
        svc.Username.Should().Be("bob");
    }

    [Fact]
    public async Task SetTokensAsync_SetsCurator_WhenFlagIsTrue()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse(isCurator: true));
        svc.IsCurator.Should().BeTrue();
    }

    [Fact]
    public async Task SetTokensAsync_SetsAccountId()
    {
        var accountId = Guid.NewGuid();
        var response  = MakeResponse() with { AccountId = accountId };
        var (svc, _) = Build();
        await svc.SetTokensAsync(response);
        svc.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task SetTokensAsync_TokensNotExposedViaPublicProperties()
    {
        // Neither the access token nor the refresh token must be reachable through
        // any public property. Tokens are circuit-scoped memory only and must not
        // be accessible to Razor component templates or other callers.
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        var publicProps = typeof(AuthStateService)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.GetValue(svc)?.ToString() ?? "");
        publicProps.Should().NotContain("jwt-token");
        publicProps.Should().NotContain("refresh-token");
    }

    [Fact]
    public async Task SetTokensAsync_FiresOnChange()
    {
        var (svc, _) = Build();
        var fired = false;
        svc.OnChange += () => fired = true;
        await svc.SetTokensAsync(MakeResponse());
        fired.Should().BeTrue();
    }

    // ── TokenExpiresSoon ──────────────────────────────────────────────────────

    [Fact]
    public async Task TokenExpiresSoon_ReturnsTrue_WhenExpiryIsWithinTwoMinutes()
    {
        var response = MakeResponse() with { AccessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(60) };
        var (svc, _) = Build();
        await svc.SetTokensAsync(response);
        svc.TokenExpiresSoon.Should().BeTrue();
    }

    [Fact]
    public async Task TokenExpiresSoon_ReturnsFalse_WhenExpiryIsFarAway()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        svc.TokenExpiresSoon.Should().BeFalse();
    }

    // ── TryRefreshAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenNoRefreshToken()
    {
        // Fresh service with no prior SetTokensAsync — no refresh token in memory.
        var (svc, _) = Build();
        var result = await svc.TryRefreshAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenApiReturnsNull()
    {
        var (svc, api) = Build();
        await svc.SetTokensAsync(MakeResponse());
        api.SetRefreshResult(null);

        var result = await svc.TryRefreshAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsTrue_AndUpdatesState_WhenSuccessful()
    {
        var (svc, api) = Build();
        await svc.SetTokensAsync(MakeResponse());
        api.SetRefreshResult(MakeResponse("refreshed-user"));

        var result = await svc.TryRefreshAsync();

        result.Should().BeTrue();
        svc.Username.Should().Be("refreshed-user");
    }

    // ── LogOutAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogOutAsync_ClearsIsLoggedIn()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        await svc.LogOutAsync();
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task LogOutAsync_ClearsUsername()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse("alice"));
        await svc.LogOutAsync();
        svc.Username.Should().BeNull();
    }

    [Fact]
    public async Task LogOutAsync_ClearsRolesAndPermissions()
    {
        var response = MakeResponse() with
        {
            Roles = ["Admin"],
            Permissions = ["ban_players"]
        };
        var (svc, _) = Build();
        await svc.SetTokensAsync(response);
        await svc.LogOutAsync();
        svc.Roles.Should().BeEmpty();
        svc.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task LogOutAsync_FiresOnChange()
    {
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        var fired = false;
        svc.OnChange += () => fired = true;
        await svc.LogOutAsync();
        fired.Should().BeTrue();
    }

    [Fact]
    public async Task LogOutAsync_DoesNotThrow_WhenCalledWithoutLogin()
    {
        var (svc, _) = Build();
        var act = () => svc.LogOutAsync();
        await act.Should().NotThrowAsync();
    }
}

