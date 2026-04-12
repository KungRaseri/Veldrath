using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Contracts.Admin;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class AdminWarnEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // Register a plain user and return their token.
    private async Task<string> RegisterAndGetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    // Register a user, assign the Moderator role, then re-login so the JWT carries
    // the warn_players permission claim (IssueTokenPairAsync resolves role claims at login time).
    private async Task<(string Token, Guid AccountId)> RegisterModeratorAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userManager.FindByNameAsync(username);
        await userManager.AddToRoleAsync(user!, "Moderator");

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.AccessToken, user!.Id);
    }

    // Look up an account's current WarnCount directly via UserManager (bypasses EF cache).
    private async Task<int> GetWarnCountAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userManager.FindByNameAsync(username);
        return user!.WarnCount;
    }

    private async Task<bool> GetIsBannedAsync(string username)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userManager.FindByNameAsync(username);
        return user!.IsBanned;
    }

    [Fact]
    public async Task WarnPlayer_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/players/warn",
            new WarnPlayerRequest(Guid.NewGuid(), "test reason"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WarnPlayer_PlainUser_ReturnsForbidden()
    {
        // Arrange
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync("Warn_PlainUser");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/players/warn",
            new WarnPlayerRequest(Guid.NewGuid(), "test reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WarnPlayer_UnknownAccount_ReturnsNotFound()
    {
        // Arrange
        var client = factory.CreateClient();
        var (token, _) = await RegisterModeratorAsync("Warn_Mod_NotFound");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/players/warn",
            new WarnPlayerRequest(Guid.NewGuid(), "griefing"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WarnPlayer_ValidRequest_IncrementsWarnCount()
    {
        // Arrange — register target first so they exist in the DB
        await RegisterAndGetTokenAsync("Warn_Target1");

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var target = await userManager.FindByNameAsync("Warn_Target1");

        var client = factory.CreateClient();
        var (token, _) = await RegisterModeratorAsync("Warn_Mod_Increment");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var beforeCount = await GetWarnCountAsync("Warn_Target1");

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/players/warn",
            new WarnPlayerRequest(target!.Id, "verbal abuse"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminActionResponse>();
        body!.Success.Should().BeTrue();

        var afterCount = await GetWarnCountAsync("Warn_Target1");
        afterCount.Should().Be(beforeCount + 1);
    }

    [Fact]
    public async Task WarnPlayer_AtThreshold_TriggersBan()
    {
        // Default threshold is 3 (appsettings.json: Moderation:AutoBanWarnThreshold).
        // Warn the same account 3 times and verify it ends up banned.

        // Arrange
        await RegisterAndGetTokenAsync("Warn_BanTarget");

        using var setupScope = factory.Services.CreateScope();
        var um = setupScope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var target = await um.FindByNameAsync("Warn_BanTarget");

        var client = factory.CreateClient();
        var (token, _) = await RegisterModeratorAsync("Warn_Mod_AutoBan");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act — issue 3 warnings to hit the threshold
        for (var i = 0; i < 3; i++)
        {
            var r = await client.PostAsJsonAsync("/api/admin/players/warn",
                new WarnPlayerRequest(target!.Id, $"warning #{i + 1}"));
            r.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Assert — account should now be banned
        var isBanned = await GetIsBannedAsync("Warn_BanTarget");
        isBanned.Should().BeTrue();
    }

    [Fact]
    public async Task WarnPlayer_AlreadyBanned_DoesNotDoubleBan()
    {
        // If the account is already banned, further warns should still succeed
        // (WarnCount increments) but must not re-ban or overwrite BanReason.

        // Arrange — register and manually ban the target
        await RegisterAndGetTokenAsync("Warn_AlreadyBanned");

        using var banScope = factory.Services.CreateScope();
        var um = banScope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var target = await um.FindByNameAsync("Warn_AlreadyBanned");
        target!.IsBanned = true;
        target.BanReason = "Manual ban for testing";
        await um.UpdateAsync(target);

        var client = factory.CreateClient();
        var (token, _) = await RegisterModeratorAsync("Warn_Mod_AlreadyBanned");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act — warn the already-banned account
        var response = await client.PostAsJsonAsync("/api/admin/players/warn",
            new WarnPlayerRequest(target.Id, "additional warning"));

        // Assert — warn succeeds, ban state unchanged (BanReason not overwritten)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var checkScope = factory.Services.CreateScope();
        var um2 = checkScope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var refreshed = await um2.FindByNameAsync("Warn_AlreadyBanned");
        refreshed!.IsBanned.Should().BeTrue();
        refreshed.BanReason.Should().Be("Manual ban for testing");
    }
}
