using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Integration tests for the temporary internal account bridge used while Blazor Foundry
/// still authenticates through the .NET Identity endpoints.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class InternalAccountBridgeEndpointTests(WebAppFactory factory)
{
    private const string BridgeToken = "test-internal-bridge-token";
    private const string BridgePassword = "BridgeP@ssw0rd!23";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Sync_Without_Bridge_Token_Is_Rejected()
    {
        var response = await _client.SendAsync(SyncRequest(
            new { accountId = Guid.NewGuid() },
            token: null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sync_With_Empty_AccountId_Is_Rejected()
    {
        var response = await _client.SendAsync(SyncRequest(new { accountId = Guid.Empty }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sync_Stores_Password_So_Identity_Login_Succeeds()
    {
        var accountId = Guid.NewGuid();
        var username  = UniqueName("bridge");
        var email     = $"{username}@example.com";

        var response = await _client.SendAsync(SyncRequest(new
        {
            accountId,
            username,
            email,
            emailVerified = true,
            password = BridgePassword,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccountSyncResponse>();
        payload.Should().NotBeNull();
        payload!.AccountId.Should().Be(accountId);
        payload.Username.Should().Be(username);
        payload.PasswordSynced.Should().BeTrue();

        // The whole point of the bridge: Blazor Foundry's existing login endpoint works.
        var login = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = BridgePassword,
        });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sync_Links_External_Logins_With_Canonical_Provider_Names()
    {
        var accountId   = Guid.NewGuid();
        var username    = UniqueName("oauth");
        var providerKey = $"discord-{accountId:N}";

        var response = await _client.SendAsync(SyncRequest(new
        {
            accountId,
            username,
            email = $"{username}@example.com",
            emailVerified = true,
            externalLogins = new[]
            {
                new { provider = "discord", providerKey, displayName = "Discord" },
            },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccountSyncResponse>();
        payload.Should().NotBeNull();
        payload!.LinkedLogins.Should().Contain("Discord");

        using var scope       = factory.Services.CreateScope();
        var       userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var       linked      = await userManager.FindByLoginAsync("Discord", providerKey);

        linked.Should().NotBeNull();
        linked!.Id.Should().Be(accountId);
    }

    [Fact]
    public async Task Sync_Is_Idempotent_For_Repeated_Calls()
    {
        var accountId   = Guid.NewGuid();
        var username    = UniqueName("repeat");
        var providerKey = $"google-{accountId:N}";

        object BuildBody() => new
        {
            accountId,
            username,
            email = $"{username}@example.com",
            emailVerified = true,
            externalLogins = new[]
            {
                new { provider = "google", providerKey, displayName = "Google" },
            },
        };

        var first = await _client.SendAsync(SyncRequest(BuildBody()));
        var second = await _client.SendAsync(SyncRequest(BuildBody()));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await second.Content.ReadFromJsonAsync<InternalAccountSyncResponse>();
        payload.Should().NotBeNull();
        payload!.LinkedLogins.Should().ContainSingle("Google");

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var matches = await Task.FromResult(
            userManager.Users.Count(u => u.Id == accountId));
        matches.Should().Be(1);
    }

    [Fact]
    public async Task Sync_Updates_Identity_Fields_For_Existing_Account()
    {
        var accountId = Guid.NewGuid();
        var firstName = UniqueName("before");
        var secondName = UniqueName("after");
        var firstEmail = $"{firstName}@example.com";
        var secondEmail = $"{secondName}@example.com";

        var create = await _client.SendAsync(SyncRequest(new
        {
            accountId,
            username = firstName,
            email = firstEmail,
            emailVerified = false,
        }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await _client.SendAsync(SyncRequest(new
        {
            accountId,
            username = secondName,
            email = secondEmail,
            emailVerified = true,
            displayName = "Updated Hero",
        }));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await update.Content.ReadFromJsonAsync<InternalAccountSyncResponse>();
        payload.Should().NotBeNull();
        payload!.Username.Should().Be(secondName);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userManager.FindByIdAsync(accountId.ToString());

        user.Should().NotBeNull();
        user!.UserName.Should().Be(secondName);
        user.Email.Should().Be(secondEmail);
        user.EmailConfirmed.Should().BeTrue();
        user.DisplayName.Should().Be("Updated Hero");
    }

    private HttpRequestMessage SyncRequest(object body, string? token = BridgeToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/accounts/sync")
        {
            Content = JsonContent.Create(body),
        };

        if (token is not null)
            request.Headers.Add(InternalAccountBridgeEndpoints.BridgeTokenHeader, token);

        return request;
    }

    private static string UniqueName(string prefix) =>
        $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 32)];
}
