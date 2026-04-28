using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Veldrath.Contracts.Content;
using Veldrath.Server.Features.Dev;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class DevEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // Helper — register + login, return bearer token.
    private async Task<string> GetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    // Helper — register, login, create a character, return (token, characterId).
    private async Task<(string Token, Guid CharacterId)> RegisterWithCharacterAsync(string username)
    {
        var token = await GetTokenAsync(username);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = $"Dev_{username}", ClassName = "Warrior" });

        var character = await create.Content.ReadFromJsonAsync<CharacterDto>();
        return (token, character!.Id);
    }

    // ─── GET /api/dev/zones ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDevZones_WithoutAuth_Returns401()
    {
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/dev/zones");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDevZones_WithAuth_IncludesPlaygroundZone()
    {
        var token = await GetTokenAsync("DevZones_User1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var zones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/dev/zones");

        zones.Should().NotBeNull();
        zones!.Should().Contain(z => z.Id == "playground");
    }

    [Fact]
    public async Task GetDevZones_WithAuth_ContainsAllPublicZonesPlusPlayground()
    {
        var token = await GetTokenAsync("DevZones_User2");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var devZones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/dev/zones");
        var publicZones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/zones");

        // Dev list should have all public zones plus at least the playground.
        devZones!.Length.Should().BeGreaterThan(publicZones!.Length);
        devZones.Should().Contain(z => z.Id == "playground");
    }

    // ─── GET /api/zones — public list must never expose playground ───────────

    [Fact]
    public async Task GetPublicZones_NeverReturnsPlaygroundZone()
    {
        var zones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/zones");

        zones.Should().NotContain(z => z.Id == "playground");
    }

    // ─── POST /api/dev/teleport ──────────────────────────────────────────────

    [Fact]
    public async Task Teleport_WithoutAuth_Returns401()
    {
        using var anonClient = factory.CreateClient();
        var response = await anonClient.PostAsJsonAsync("/api/dev/teleport",
            new { CharacterId = Guid.NewGuid(), ZoneId = "playground" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Teleport_UnknownCharacter_Returns404()
    {
        var token = await GetTokenAsync("DevTeleport_404Char");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/dev/teleport",
            new { CharacterId = Guid.NewGuid(), ZoneId = "playground" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Teleport_UnknownZone_Returns404()
    {
        var (_, characterId) = await RegisterWithCharacterAsync("DevTeleport_404Zone");

        var response = await _client.PostAsJsonAsync("/api/dev/teleport",
            new { CharacterId = characterId, ZoneId = "zone-that-does-not-exist" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Teleport_ValidRequest_Returns200WithUpdatedZone()
    {
        var (token, characterId) = await RegisterWithCharacterAsync("DevTeleport_Happy");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/dev/teleport",
            new { CharacterId = characterId, ZoneId = "playground" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TeleportResult>();
        result!.CharacterId.Should().Be(characterId);
        result.ZoneId.Should().Be("playground");
        result.ZoneName.Should().Be("Dev Playground");
    }

    [Fact]
    public async Task Teleport_ValidRequest_PersistsNewZoneOnCharacter()
    {
        var (token, characterId) = await RegisterWithCharacterAsync("DevTeleport_Persist");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/dev/teleport",
            new { CharacterId = characterId, ZoneId = "playground" });

        // Verify the character's CurrentZoneId was updated by fetching the character list.
        var characters = await _client.GetFromJsonAsync<CharacterDto[]>("/api/characters");
        var character = characters!.Single(c => c.Id == characterId);
        character.CurrentZoneId.Should().Be("playground");
    }

    // ─── GET /api/dev/zones/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetDevZone_WithoutAuth_Returns401()
    {
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/dev/zones/playground");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDevZone_WithAuth_ReturnsZoneDetails()
    {
        var token = await GetTokenAsync("DevZone_Detail_User1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/dev/zones/playground");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var zone = await response.Content.ReadFromJsonAsync<ZoneDto>();
        zone!.Id.Should().Be("playground");
        zone.Name.Should().Be("Dev Playground");
    }

    [Fact]
    public async Task GetDevZone_UnknownZone_Returns404()
    {
        var token = await GetTokenAsync("DevZone_Detail_404");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/dev/zones/zone-that-does-not-exist");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── GET /api/dev/zones/{id}/locations ──────────────────────────────────

    [Fact]
    public async Task GetDevZoneLocations_WithoutAuth_Returns401()
    {
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/dev/zones/playground/locations");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDevZoneLocations_WithAuth_ReturnsAllLocations()
    {
        var token = await GetTokenAsync("DevZoneLoc_All_User1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var locations = await _client.GetFromJsonAsync<ZoneLocationDto[]>("/api/dev/zones/playground/locations");

        // 13 seeded playground locations (8 visible + 5 hidden)
        locations.Should().NotBeNull();
        locations!.Length.Should().Be(13);
    }

    [Fact]
    public async Task GetDevZoneLocations_IncludesHiddenLocations()
    {
        var token = await GetTokenAsync("DevZoneLoc_Hidden_User1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var locations = await _client.GetFromJsonAsync<ZoneLocationDto[]>("/api/dev/zones/playground/locations");

        locations!.Should().Contain(l => l.IsHidden);
        locations.Count(l => l.IsHidden).Should().Be(5);
    }

    // ─── POST /api/dev/teleport with LocationSlug ────────────────────────────

    [Fact]
    public async Task Teleport_WithLocationSlug_SetsLocationOnCharacter()
    {
        var (token, characterId) = await RegisterWithCharacterAsync("DevTeleport_Slug");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/dev/teleport",
            new { CharacterId = characterId, ZoneId = "playground", LocationSlug = "playground-clearing" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TeleportResult>();
        result!.ZoneId.Should().Be("playground");
        result.LocationSlug.Should().Be("playground-clearing");

        // Verify persistence
        var characters = await _client.GetFromJsonAsync<CharacterDto[]>("/api/characters");
        characters!.Single(c => c.Id == characterId).CurrentZoneLocationSlug.Should().Be("playground-clearing");
    }
}
