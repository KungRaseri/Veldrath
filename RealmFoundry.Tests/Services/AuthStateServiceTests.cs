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
        // Tokens must not be reachable through any public property.
        var (svc, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        // No public API should expose the raw JWT or refresh token.
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


public class AuthStateServiceTests
{
    // helpers
    private static Mock<IJSRuntime> BuildJs(Dictionary<string, string?> sessionStorage)
    {
        var js = new Mock<IJSRuntime>();

        // sessionStorage.getItem
        js.Setup(j => j.InvokeAsync<string?>(
                "sessionStorage.getItem", It.IsAny<object[]>()))
          .Returns<string, object[]>((_, args) =>
          {
              var key = (string)args[0];
              sessionStorage.TryGetValue(key, out var value);
              return ValueTask.FromResult(value);
          });

        // sessionStorage.setItem
        js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "sessionStorage.setItem", It.IsAny<object[]>()))
          .Returns<string, object[]>((_, args) =>
          {
              sessionStorage[(string)args[0]] = (string)args[1];
              return ValueTask.FromResult(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
          });

        // sessionStorage.removeItem
        js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "sessionStorage.removeItem", It.IsAny<object[]>()))
          .Returns<string, object[]>((_, args) =>
          {
              sessionStorage.Remove((string)args[0]);
              return ValueTask.FromResult(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>());
          });

        return js;
    }

    private static (AuthStateService Service, Mock<IJSRuntime> Js, Dictionary<string, string?> Storage,
                    FakeApiClient Api)
        Build(Dictionary<string, string?>? initial = null)
    {
        var storage = initial ?? [];
        var js      = BuildJs(storage);
        var api     = new FakeApiClient();
        var svc     = new AuthStateService(js.Object, api);
        return (svc, js, storage, api);
    }

    private static AuthResponse MakeResponse(string username = "alice", bool isCurator = false) =>
        new("jwt-token", "refresh-token",
            DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), username, [], [], isCurator);

    // InitialiseAsync
    [Fact]
    public async Task InitialiseAsync_DoesNothing_WhenSessionStorageIsEmpty()
    {
        var (svc, _, _, _) = Build();
        await svc.InitialiseAsync();
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task InitialiseAsync_RestoresState_WhenTokensArePresent()
    {
        var accountId = Guid.NewGuid();
        var storage = new Dictionary<string, string?>
        {
            ["rf_access"]      = "tok",
            ["rf_username"]    = "alice",
            ["rf_account_id"]  = accountId.ToString(),
            ["rf_is_curator"]  = "false",
            ["rf_token_expiry"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
        };
        var (svc, _, _, _) = Build(storage);

        await svc.InitialiseAsync();

        svc.IsLoggedIn.Should().BeTrue();
        svc.Username.Should().Be("alice");
        svc.AccountId.Should().Be(accountId);
        svc.IsCurator.Should().BeFalse();
    }

    [Fact]
    public async Task InitialiseAsync_SetsCurator_WhenFlagIsTrue()
    {
        var storage = new Dictionary<string, string?>
        {
            ["rf_access"]      = "tok",
            ["rf_username"]    = "curator",
            ["rf_account_id"]  = Guid.NewGuid().ToString(),
            ["rf_is_curator"]  = "true",
            ["rf_token_expiry"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
        };
        var (svc, _, _, _) = Build(storage);

        await svc.InitialiseAsync();

        svc.IsCurator.Should().BeTrue();
    }

    [Fact]
    public async Task InitialiseAsync_FiresOnChange_WhenTokensArePresent()
    {
        var storage = new Dictionary<string, string?>
        {
            ["rf_access"]      = "tok",
            ["rf_username"]    = "alice",
            ["rf_account_id"]  = Guid.NewGuid().ToString(),
            ["rf_is_curator"]  = "false",
            ["rf_token_expiry"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"),
        };
        var (svc, _, _, _) = Build(storage);

        var fired = false;
        svc.OnChange += () => fired = true;
        await svc.InitialiseAsync();

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task InitialiseAsync_SilentlyIgnores_JSException()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<string?>("sessionStorage.getItem", It.IsAny<object[]>()))
          .ThrowsAsync(new JSException("No JS context"));
        var svc = new AuthStateService(js.Object, new FakeApiClient());

        var act = () => svc.InitialiseAsync();

        await act.Should().NotThrowAsync();
        svc.IsLoggedIn.Should().BeFalse();
    }

    // SetTokensAsync
    [Fact]
    public async Task SetTokensAsync_SetsIsLoggedIn()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        svc.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public async Task SetTokensAsync_SetsUsername()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse("bob"));
        svc.Username.Should().Be("bob");
    }

    [Fact]
    public async Task SetTokensAsync_SetsCurator_WhenFlagIsTrue()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse(isCurator: true));
        svc.IsCurator.Should().BeTrue();
    }

    [Fact]
    public async Task SetTokensAsync_WritesAllKeysToSessionStorage()
    {
        var (svc, _, storage, _) = Build();
        await svc.SetTokensAsync(MakeResponse("charlie"));

        storage.Should().ContainKey("rf_access");
        storage.Should().ContainKey("rf_refresh");
        storage.Should().ContainKey("rf_username");
        storage.Should().ContainKey("rf_account_id");
        storage.Should().ContainKey("rf_is_curator");
        storage.Should().ContainKey("rf_token_expiry");
        storage["rf_username"].Should().Be("charlie");
    }

    [Fact]
    public async Task SetTokensAsync_FiresOnChange()
    {
        var (svc, _, _, _) = Build();
        var fired = false;
        svc.OnChange += () => fired = true;
        await svc.SetTokensAsync(MakeResponse());
        fired.Should().BeTrue();
    }

    // TokenExpiresSoon
    [Fact]
    public async Task TokenExpiresSoon_ReturnsTrue_WhenExpiryIsWithinTwoMinutes()
    {
        var response = MakeResponse() with { AccessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(60) };
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(response);
        svc.TokenExpiresSoon.Should().BeTrue();
    }

    [Fact]
    public async Task TokenExpiresSoon_ReturnsFalse_WhenExpiryIsFarAway()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        svc.TokenExpiresSoon.Should().BeFalse();
    }

    // TryRefreshAsync
    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenNoRefreshToken()
    {
        var (svc, _, _, _) = Build();
        var result = await svc.TryRefreshAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsFalse_WhenApiReturnsNull()
    {
        var storage = new Dictionary<string, string?> { ["rf_refresh"] = "old-refresh" };
        var (svc, _, _, api) = Build(storage);
        api.SetRefreshResult(null);

        var result = await svc.TryRefreshAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryRefreshAsync_ReturnsTrue_AndUpdatesState_WhenSuccessful()
    {
        var storage = new Dictionary<string, string?> { ["rf_refresh"] = "old-refresh" };
        var (svc, _, _, api) = Build(storage);
        api.SetRefreshResult(MakeResponse("refreshed-user"));

        var result = await svc.TryRefreshAsync();

        result.Should().BeTrue();
        svc.Username.Should().Be("refreshed-user");
    }

    // LogOutAsync
    [Fact]
    public async Task LogOutAsync_ClearsIsLoggedIn()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        await svc.LogOutAsync();
        svc.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task LogOutAsync_ClearsUsername()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse("alice"));
        await svc.LogOutAsync();
        svc.Username.Should().BeNull();
    }

    [Fact]
    public async Task LogOutAsync_ClearsAllSessionStorageKeys()
    {
        var (svc, _, storage, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        await svc.LogOutAsync();

        storage.Should().NotContainKey("rf_access");
        storage.Should().NotContainKey("rf_refresh");
        storage.Should().NotContainKey("rf_username");
        storage.Should().NotContainKey("rf_account_id");
        storage.Should().NotContainKey("rf_is_curator");
        storage.Should().NotContainKey("rf_token_expiry");
    }

    [Fact]
    public async Task LogOutAsync_FiresOnChange()
    {
        var (svc, _, _, _) = Build();
        await svc.SetTokensAsync(MakeResponse());
        var fired = false;
        svc.OnChange += () => fired = true;
        await svc.LogOutAsync();
        fired.Should().BeTrue();
    }

    [Fact]
    public async Task LogOutAsync_SilentlyIgnores_JSException()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                "sessionStorage.removeItem", It.IsAny<object[]>()))
          .ThrowsAsync(new JSException("No JS context"));
        var svc = new AuthStateService(js.Object, new FakeApiClient());

        var act = () => svc.LogOutAsync();

        await act.Should().NotThrowAsync();
    }
}
