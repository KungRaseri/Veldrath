using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using RealmFoundry.Tests.Infrastructure;
using Veldrath.Contracts.Auth;

namespace RealmFoundry.Tests.Services;

public class AuthRefreshHandlerTests
{
    // helpers
    private static (HttpClient Client, Mock<HttpMessageHandler> InnerHandler, AuthStateService Auth)
        Build(bool isLoggedIn, bool tokenExpiresSoon)
    {
        var innerHandler = new Mock<HttpMessageHandler>();
        innerHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var api = new FakeApiClient();
        if (tokenExpiresSoon)
            api.SetRefreshResult(new AuthResponse(
                "new-tok", "new-refresh", DateTimeOffset.UtcNow.AddHours(1),
                Guid.NewGuid(), "alice", [], [], false));

        var auth = new AuthStateService(api);

        if (isLoggedIn)
        {
            var expiry = tokenExpiresSoon
                ? DateTimeOffset.UtcNow.AddSeconds(30)
                : DateTimeOffset.UtcNow.AddHours(1);
            auth.SetTokensAsync(new AuthResponse(
                "tok", "refresh-tok", expiry, Guid.NewGuid(), "alice", [], [], false))
                .GetAwaiter().GetResult();
        }

        var services = new ServiceCollection();
        services.AddSingleton(auth);
        var sp = services.BuildServiceProvider();

        var handler = new AuthRefreshHandler(sp) { InnerHandler = innerHandler.Object };
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        return (client, innerHandler, auth);
    }

    // tests
    [Fact]
    public async Task SendAsync_ForwardsRequest_WhenUserIsNotLoggedIn()
    {
        var (client, inner, _) = Build(isLoggedIn: false, tokenExpiresSoon: false);

        var response = await client.GetAsync("/api/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Protected().Verify("SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ForwardsRequest_WhenTokenIsNotExpiringSoon()
    {
        var (client, inner, _) = Build(isLoggedIn: true, tokenExpiresSoon: false);

        await client.GetAsync("/api/submissions");

        inner.Protected().Verify("SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_RefreshesToken_WhenTokenExpiresSoon()
    {
        var (client, _, auth) = Build(isLoggedIn: true, tokenExpiresSoon: true);

        await client.GetAsync("/api/submissions");

        // After the refresh the new token should be set
        auth.IsLoggedIn.Should().BeTrue();
        auth.TokenExpiresSoon.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_SkipsRefresh_ForAuthPaths()
    {
        // Even if the token is expiring soon, /api/auth/ paths must not trigger refresh
        // (that would cause infinite recursion)
        var (client, inner, _) = Build(isLoggedIn: true, tokenExpiresSoon: true);

        await client.GetAsync("/api/auth/refresh");

        // Inner handler called exactly once — no extra calls from refresh attempt
        inner.Protected().Verify("SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/auth/login")]
    [InlineData("/API/AUTH/LOGOUT")]
    public async Task SendAsync_SkipsRefresh_ForAllAuthPaths(string path)
    {
        var (client, inner, _) = Build(isLoggedIn: true, tokenExpiresSoon: true);

        await client.GetAsync(path);

        inner.Protected().Verify("SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
