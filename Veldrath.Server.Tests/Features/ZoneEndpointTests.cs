using System.Net;
using System.Net.Http.Json;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ZoneEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetZones_Should_Return_All_Seeded_Zones()
    {
        var response = await _client.GetAsync("/api/zones");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var zones = await response.Content.ReadFromJsonAsync<ZoneDto[]>();
        zones.Should().NotBeNull();
        zones!.Length.Should().Be(18);
    }

    [Fact]
    public async Task GetZones_Should_Include_Starter_Zone()
    {
        var response = await _client.GetAsync("/api/zones");
        var zones = await response.Content.ReadFromJsonAsync<ZoneDto[]>();

        var starter = zones!.SingleOrDefault(z => z.IsStarter);
        starter.Should().NotBeNull();
        starter!.Id.Should().Be("crestfall");
    }

    [Fact]
    public async Task GetZones_Should_Return_Correct_Zone_Types()
    {
        var zones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/zones");
        zones.Should().Contain(z => z.Type == "Town");
        zones.Should().Contain(z => z.Type == "Dungeon");
        zones.Should().Contain(z => z.Type == "Wilderness");
    }

    [Fact]
    public async Task GetZones_Should_Return_Zero_Online_Players_Initially()
    {
        var zones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/zones");
        zones!.All(z => z.OnlinePlayers == 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetZoneById_Should_Return_Zone_Details()
    {
        var response = await _client.GetAsync("/api/zones/crestfall");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var zone = await response.Content.ReadFromJsonAsync<ZoneDto>();
        zone!.Id.Should().Be("crestfall");
        zone.IsStarter.Should().BeTrue();
        zone.MinLevel.Should().Be(0);
    }

    [Fact]
    public async Task GetZoneById_Should_Return_404_For_Unknown_Zone()
    {
        var response = await _client.GetAsync("/api/zones/nonexistent-zone");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetZones_Does_Not_Require_Authentication()
    {
        // Zones list should be publicly accessible (let players see server world without signing in)
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/zones");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetZoneById_Should_Return_404_For_DevOnly_Zone()
    {
        // Dev-only zones must never be reachable through the public API
        var response = await _client.GetAsync("/api/zones/playground");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetZoneLocations_Should_Return_404_For_DevOnly_Zone()
    {
        // Location list for a dev zone must also be blocked from the public API
        var response = await _client.GetAsync("/api/zones/playground/locations");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
