using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using RealmUnbound.Contracts.Auth;
using RealmUnbound.Contracts.Foundry;

namespace RealmFoundry.Tests.Services;

public class RealmFoundryApiClientTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (RealmFoundryApiClient Client, Mock<HttpMessageHandler> Handler) Build()
    {
        var handler = new Mock<HttpMessageHandler>();
        var http    = new HttpClient(handler.Object) { BaseAddress = new Uri("https://localhost") };
        return (new RealmFoundryApiClient(http), handler);
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

    private static void SetupResponse(Mock<HttpMessageHandler> handler, HttpStatusCode status, string rawBody)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(rawBody) };
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
        var client = new RealmFoundryApiClient(http);
        client.SetBearerToken("test-token");
        http.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        http.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        http.DefaultRequestHeaders.Authorization!.Parameter.Should().Be("test-token");
    }

    [Fact]
    public void Constructor_LeavesAuthorizationHeader_Unset()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new RealmFoundryApiClient(http);
        http.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Fact]
    public void ClearBearerToken_RemovesAuthorizationHeader()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new RealmFoundryApiClient(http);
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

    // ── RefreshTokenAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ReturnsAuthResponse_WhenSuccessful()
    {
        var (client, handler) = Build();
        var expected = new AuthResponse("new-jwt", "new-refresh", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "alice", true);
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

    // ── GetSubmissionsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSubmissionsAsync_ReturnsPage_WhenSuccessful()
    {
        var (client, handler) = Build();
        var summary = new FoundrySubmissionSummaryDto(Guid.NewGuid(), "Item", "Sword", "Pending",
            "alice", 5, DateTimeOffset.UtcNow);
        var page = new PagedResult<FoundrySubmissionSummaryDto>([summary], 1, 1, 20);
        SetupResponse(handler, HttpStatusCode.OK, page);

        var result = await client.GetSubmissionsAsync();

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(s => s.Title == "Sword");
    }

    [Fact]
    public async Task GetSubmissionsAsync_ReturnsEmptyPage_WhenServerFails()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.InternalServerError);

        var result = await client.GetSubmissionsAsync();

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ── GetSubmissionAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubmissionAsync_ReturnsDto_WhenSuccessful()
    {
        var (client, handler) = Build();
        var id  = Guid.NewGuid();
        var dto = new FoundrySubmissionDto(id, "Spell", "Fireball", "Big boom", "{}",
            "Approved", "bob", Guid.NewGuid(), "curator", "Looks good", 10,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        SetupResponse(handler, HttpStatusCode.OK, dto);

        var result = await client.GetSubmissionAsync(id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Fireball");
    }

    [Fact]
    public async Task GetSubmissionAsync_ReturnsNull_WhenNotFound()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.NotFound);

        var result = await client.GetSubmissionAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── CreateSubmissionAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubmissionAsync_ReturnsDto_WhenSuccessful()
    {
        var (client, handler) = Build();
        var id  = Guid.NewGuid();
        var dto = new FoundrySubmissionDto(id, "Item", "Shield", null, "{}",
            "Pending", "alice", Guid.NewGuid(), null, null, 0,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        SetupResponse(handler, HttpStatusCode.Created, dto);

        var (result, error) = await client.CreateSubmissionAsync(
            new CreateSubmissionRequest("Item", "Shield", "{}"));

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        error.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubmissionAsync_ReturnsError_WhenBadRequest()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.BadRequest, "Title is required.");

        var (result, error) = await client.CreateSubmissionAsync(
            new CreateSubmissionRequest("Item", "", "{}"));

        result.Should().BeNull();
        error.Should().Be("Title is required.");
    }

    // ── VoteAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task VoteAsync_ReturnsSummary_WhenSuccessful()
    {
        var (client, handler) = Build();
        var id      = Guid.NewGuid();
        var summary = new FoundrySubmissionSummaryDto(id, "Item", "Shield", "Pending", "alice", 1, DateTimeOffset.UtcNow);
        SetupResponse(handler, HttpStatusCode.OK, summary);

        var (result, error) = await client.VoteAsync(id, 1);

        result.Should().NotBeNull();
        result!.VoteScore.Should().Be(1);
        error.Should().BeNull();
    }

    [Fact]
    public async Task VoteAsync_ReturnsError_WhenUnauthorized()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.Unauthorized, "Login required.");

        var (result, error) = await client.VoteAsync(Guid.NewGuid(), 1);

        result.Should().BeNull();
        error.Should().Be("Login required.");
    }

    // ── ReviewAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_ReturnsDto_WhenApproved()
    {
        var (client, handler) = Build();
        var id  = Guid.NewGuid();
        var dto = new FoundrySubmissionDto(id, "Item", "Shield", null, "{}",
            "Approved", "alice", Guid.NewGuid(), "curator", "LGTM", 2,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        SetupResponse(handler, HttpStatusCode.OK, dto);

        var (result, error) = await client.ReviewAsync(id, new ReviewRequest(true, "LGTM"));

        result.Should().NotBeNull();
        result!.Status.Should().Be("Approved");
        error.Should().BeNull();
    }

    [Fact]
    public async Task ReviewAsync_ReturnsError_WhenForbidden()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.Forbidden, "Curators only.");

        var (result, error) = await client.ReviewAsync(Guid.NewGuid(), new ReviewRequest(true));

        result.Should().BeNull();
        error.Should().Be("Curators only.");
    }

    // ── GetNotificationsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsList_WhenSuccessful()
    {
        var (client, handler) = Build();
        var notification = new FoundryNotificationDto(Guid.NewGuid(), Guid.NewGuid(),
            "Shield", "Your submission was approved.", false, DateTimeOffset.UtcNow);
        SetupResponse(handler, HttpStatusCode.OK, new List<FoundryNotificationDto> { notification });

        var result = await client.GetNotificationsAsync();

        result.Should().ContainSingle(n => n.Message == "Your submission was approved.");
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsEmpty_WhenServerFails()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.InternalServerError);

        var result = await client.GetNotificationsAsync();

        result.Should().BeEmpty();
    }

    // ── MarkNotificationReadAsync ─────────────────────────────────────────────

    [Fact]
    public async Task MarkNotificationReadAsync_ReturnsTrue_WhenSuccessful()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.OK);

        var result = await client.MarkNotificationReadAsync(Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkNotificationReadAsync_ReturnsFalse_WhenNotFound()
    {
        var (client, handler) = Build();
        SetupResponse(handler, HttpStatusCode.NotFound);

        var result = await client.MarkNotificationReadAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }
}
