using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Contracts.Auth;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Integration tests for
/// <c>GET /api/auth/confirm-email</c> and
/// <c>POST /api/auth/resend-confirmation</c>.</summary>
[Trait("Category", "Integration")]
public class EmailConfirmationEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AuthResponse> RegisterAsync(string username)
    {
        var email = $"{username.ToLowerInvariant()}@emailconftest.com";
        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<(string UserId, string Token)> GenerateEmailConfirmationTokenAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userMgr.FindByEmailAsync(email);
        var token = await userMgr.GenerateEmailConfirmationTokenAsync(user!);
        return (user!.Id.ToString(), token);
    }

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private void ClearBearer() =>
        _client.DefaultRequestHeaders.Authorization = null;

    // ── GET /api/auth/confirm-email ───────────────────────────────────────────

    [Fact]
    public async Task ConfirmEmail_ValidToken_Returns200()
    {
        await RegisterAsync("EmailConf_Valid");
        var email = "emailconf_valid@emailconftest.com";
        var (userId, token) = await GenerateEmailConfirmationTokenAsync(email);

        var resp = await _client.GetAsync(
            $"/api/auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_Returns400()
    {
        var auth = await RegisterAsync("EmailConf_BadToken");
        var userId = auth.AccountId.ToString();

        var resp = await _client.GetAsync(
            $"/api/auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token=this-is-not-valid");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmEmail_MissingParams_Returns400()
    {
        var resp = await _client.GetAsync("/api/auth/confirm-email");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auth/resend-confirmation ────────────────────────────────────

    [Fact]
    public async Task ResendConfirmation_Authenticated_Returns200()
    {
        var auth = await RegisterAsync("EmailConf_Resend");
        SetBearer(auth.AccessToken);

        var resp = await _client.PostAsync("/api/auth/resend-confirmation", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResendConfirmation_Unauthenticated_Returns401()
    {
        ClearBearer();

        var resp = await _client.PostAsync("/api/auth/resend-confirmation", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
