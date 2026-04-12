using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class PlayerEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string Token, Guid AccountId)> RegisterAndLoginAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.AccessToken, auth.AccountId);
    }

    [Fact]
    public async Task GetProfile_Should_Return_Profile_For_Existing_Account()
    {
        var (token, accountId) = await RegisterAndLoginAsync("Profile_Existing");

        var response = await _client.GetAsync($"/api/players/{accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        profile.Should().NotBeNull();
        profile!.AccountId.Should().Be(accountId);
        profile.Username.Should().Be("Profile_Existing");
    }

    [Fact]
    public async Task GetProfile_Should_Return_Zero_Level_When_No_Characters()
    {
        var (_, accountId) = await RegisterAndLoginAsync("Profile_NoChars");

        var response = await _client.GetAsync($"/api/players/{accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        profile!.Level.Should().Be(0);
        profile.CharacterClass.Should().BeNull();
        profile.Species.Should().BeNull();
        profile.CurrentZone.Should().BeNull();
    }

    [Fact]
    public async Task GetProfile_Should_Return_404_For_Unknown_Account()
    {
        var response = await _client.GetAsync($"/api/players/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProfile_Should_Be_Accessible_Without_Authentication()
    {
        var (_, accountId) = await RegisterAndLoginAsync("Profile_Public");

        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.GetAsync($"/api/players/{accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProfile_Should_Reflect_Highest_Level_Character()
    {
        var (token, accountId) = await RegisterAndLoginAsync("Profile_WithChar");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "ProfileChar_Hero", ClassName = "Warrior" });

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/api/players/{accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        profile!.Level.Should().Be(1);
        profile.CharacterClass.Should().Be("Warrior");
    }
}
