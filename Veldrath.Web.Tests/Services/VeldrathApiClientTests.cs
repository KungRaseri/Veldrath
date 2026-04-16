using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using Veldrath.Contracts.Auth;
using Veldrath.Contracts.Editorial;
using Veldrath.Contracts.Foundry;

namespace Veldrath.Web.Tests.Services;

public class VeldrathApiClientTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (VeldrathApiClient Client, Mock<HttpMessageHandler> Handler) Build()
    {
        var handler = new Mock<HttpMessageHandler>();
        var http    = new HttpClient(handler.Object) { BaseAddress = new Uri("https://localhost") };
        return (new VeldrathApiClient(http), handler);
    }

    private static void SetupResponse(Mock<HttpMessageHandler> handler, HttpStatusCode status, object? body = null)
    {
        HttpContent content = body is null ? new StringContent("") : JsonContent.Create(body);
        var response = new HttpResponseMessage(status) { Content = content };
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(response);
    }

    // ── SetBearerToken / ClearBearerToken ─────────────────────────────────────

    [Fact]
    public void SetBearerToken_SetsAuthorizationHeader()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new VeldrathApiClient(http);
        client.SetBearerToken("test-token");
        http.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        http.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        http.DefaultRequestHeaders.Authorization!.Parameter.Should().Be("test-token");
    }

    [Fact]
    public void Constructor_LeavesAuthorizationHeader_Unset()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new VeldrathApiClient(http);
        http.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Fact]
    public void ClearBearerToken_RemovesAuthorizationHeader()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new VeldrathApiClient(http);
        client.SetBearerToken("tok");
        client.ClearBearerToken();
        http.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    // ── IsServerReachableAsync ────────────────────────────────────────────────

    [Fact]
    public async Task IsServerReachableAsync_ReturnsTrue_WhenHealthEndpointSucceeds()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.OK);
        var result = await client.IsServerReachableAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsServerReachableAsync_ReturnsFalse_WhenHealthEndpointFails()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.ServiceUnavailable);
        var result = await client.IsServerReachableAsync();
        result.Should().BeFalse();
    }

    // ── RenewJwtAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RenewJwtAsync_ReturnsResponse_WhenSuccessful()
    {
        var (client, handler) = Build();
        var expected = new RenewJwtResponse(
            "new-jwt", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "alice", [], []);
        SetupResponse(handler, HttpStatusCode.OK, expected);

        var result = await client.RenewJwtAsync("refresh-tok");

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("new-jwt");
        result.Username.Should().Be("alice");
    }

    [Fact]
    public async Task RenewJwtAsync_ReturnsNull_WhenServerRejects()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.Unauthorized);

        var result = await client.RenewJwtAsync("bad-token");

        result.Should().BeNull();
    }

    // ── RefreshTokenAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ReturnsAuthResponse_WhenSuccessful()
    {
        var (client, handler) = Build();
        var expected = new AuthResponse(
            "new-jwt", "new-refresh", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "alice", [], []);
        SetupResponse(handler, HttpStatusCode.OK, expected);

        var result = await client.RefreshTokenAsync("old-refresh");

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("new-jwt");
        result.Username.Should().Be("alice");
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNull_WhenServerRejects()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.Unauthorized);

        var result = await client.RefreshTokenAsync("bad-token");

        result.Should().BeNull();
    }

    // ── LogoutAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_CompletesWithoutThrowing_WhenServerSucceeds()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.OK);

        var act = async () => await client.LogoutAsync("rt");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogoutAsync_CompletesWithoutThrowing_WhenServerErrors()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.InternalServerError);

        var act = async () => await client.LogoutAsync("rt");
        await act.Should().NotThrowAsync();
    }

    // ── GetPatchNotesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPatchNotesAsync_ReturnsPage_WhenSuccessful()
    {
        var (client, handler) = Build();
        var dto = new PatchNoteSummaryDto(Guid.NewGuid(), "v1.0", "Version 1.0", "Summary", "1.0", "Published", DateTimeOffset.UtcNow);
        var page = new PagedResult<PatchNoteSummaryDto>([dto], 1, 1, 20);
        SetupResponse(handler, HttpStatusCode.OK, page);

        var result = await client.GetPatchNotesAsync();

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(p => p.Title == "Version 1.0");
    }

    [Fact]
    public async Task GetPatchNotesAsync_ReturnsNull_WhenServerErrors()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.InternalServerError);

        var result = await client.GetPatchNotesAsync();

        result.Should().BeNull();
    }

    // ── GetAnnouncementsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAnnouncementsAsync_ReturnsPage_WhenSuccessful()
    {
        var (client, handler) = Build();
        var dto  = new EditorialAnnouncementDto(Guid.NewGuid(), "Announcement 1", "Body text", "Published", DateTimeOffset.UtcNow);
        var page = new PagedResult<EditorialAnnouncementDto>([dto], 1, 1, 5);
        SetupResponse(handler, HttpStatusCode.OK, page);

        var result = await client.GetAnnouncementsAsync();

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(a => a.Title == "Announcement 1");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_ReturnsNull_WhenServerErrors()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.ServiceUnavailable);

        var result = await client.GetAnnouncementsAsync();

        result.Should().BeNull();
    }
}
