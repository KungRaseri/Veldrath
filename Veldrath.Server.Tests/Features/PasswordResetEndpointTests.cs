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
/// <c>POST /api/auth/forgot-password</c> and
/// <c>POST /api/auth/reset-password</c>.</summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class PasswordResetEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AuthResponse> RegisterAsync(string username)
    {
        var email = $"{username.ToLowerInvariant()}@pwresettest.com";
        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userMgr.FindByEmailAsync(email);
        return await userMgr.GeneratePasswordResetTokenAsync(user!);
    }

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private void ClearBearer() =>
        _client.DefaultRequestHeaders.Authorization = null;

    // ── POST /api/auth/forgot-password ────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ValidEmail_Returns200()
    {
        await RegisterAsync("PwReset_ForgotValid");

        var resp = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("pwreset_forgotvalid@pwresettest.com"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturns200()
    {
        // Must not leak whether the email is registered.
        var resp = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@doesnotexist.invalid"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/auth/reset-password ─────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidToken_Returns200()
    {
        await RegisterAsync("PwReset_ResetValid");
        var email = "pwreset_resetvalid@pwresettest.com";
        var token = await GeneratePasswordResetTokenAsync(email);

        var resp = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, token, "NewP@ssword456!"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        await RegisterAsync("PwReset_BadToken");
        var email = "pwreset_badtoken@pwresettest.com";

        var resp = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(email, "this-is-not-a-valid-token", "NewP@ssword456!"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithUnknownEmail_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest("ghost@doesnotexist.invalid", "sometoken", "NewP@ssword456!"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
