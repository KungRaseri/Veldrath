using System.Net;
using System.Net.Http.Json;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Integration tests for the internal email endpoints that let the SvelteKit server deliver
/// Better Auth password-reset and verification messages through the game server's SMTP sender.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class InternalEmailEndpointTests(WebAppFactory factory)
{
    private const string BridgeToken = "test-internal-bridge-token";
    private const string ResetUrl    = "https://veldrath.test/reset-password?token=reset-token";
    private const string VerifyUrl   = "https://veldrath.test/verify-email?token=verify-token";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Password_Reset_Without_Bridge_Token_Is_Rejected()
    {
        var response = await _client.SendAsync(PostRequest(
            "/api/internal/email/password-reset",
            new { to = "player@example.com", userName = "player", resetUrl = ResetUrl },
            token: null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verification_Without_Bridge_Token_Is_Rejected()
    {
        var response = await _client.SendAsync(PostRequest(
            "/api/internal/email/verification",
            new { to = "player@example.com", userName = "player", verifyUrl = VerifyUrl },
            token: null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Password_Reset_With_Invalid_Recipient_Is_Rejected()
    {
        var response = await _client.SendAsync(PostRequest(
            "/api/internal/email/password-reset",
            new { to = "not-an-email", userName = "player", resetUrl = ResetUrl }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verification_With_Relative_Url_Is_Rejected()
    {
        var response = await _client.SendAsync(PostRequest(
            "/api/internal/email/verification",
            new { to = "player@example.com", userName = "player", verifyUrl = "/verify-email?token=abc" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Password_Reset_With_Valid_Payload_Returns_Ok()
    {
        var response = await _client.SendAsync(PostRequest(
            "/api/internal/email/password-reset",
            new { to = "player@example.com", userName = "player", resetUrl = ResetUrl }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InternalEmailResponse>();
        payload.Should().NotBeNull();
        payload!.Sent.Should().BeTrue();
    }

    [Fact]
    public async Task Verification_With_Valid_Payload_Returns_Ok()
    {
        var response = await _client.SendAsync(PostRequest(
            "/api/internal/email/verification",
            new { to = "player@example.com", userName = "player", verifyUrl = VerifyUrl }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InternalEmailResponse>();
        payload.Should().NotBeNull();
        payload!.Sent.Should().BeTrue();
    }

    private static HttpRequestMessage PostRequest(string path, object body, string? token = BridgeToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };

        if (token is not null)
            request.Headers.Add(InternalAccountBridgeEndpoints.BridgeTokenHeader, token);

        return request;
    }
}
