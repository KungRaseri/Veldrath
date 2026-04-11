using System.Net;
using System.Net.Http.Json;
using Veldrath.Contracts.Zones;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Trait("Category", "Integration")]
public class RegionEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetRegions_Should_Return_All_Seeded_Regions()
    {
        var response = await _client.GetAsync("/api/regions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var regions = await response.Content.ReadFromJsonAsync<RegionDto[]>();
        regions.Should().NotBeNull();
        regions!.Length.Should().Be(4);
    }

    [Fact]
    public async Task GetRegions_Should_Return_Regions_Ordered_By_MinLevel()
    {
        var regions = await _client.GetFromJsonAsync<RegionDto[]>("/api/regions");

        regions.Should().NotBeNull();
        regions!.Select(r => r.MinLevel)
               .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetRegions_Should_Include_Starter_Region()
    {
        var regions = await _client.GetFromJsonAsync<RegionDto[]>("/api/regions");

        var starter = regions!.SingleOrDefault(r => r.IsStarter);
        starter.Should().NotBeNull();
        starter!.Id.Should().Be("varenmark");
    }

    [Fact]
    public async Task GetRegions_Should_Return_Correct_Region_Types()
    {
        var regions = await _client.GetFromJsonAsync<RegionDto[]>("/api/regions");

        regions!.Should().Contain(r => r.Type == "Countryside");
        regions!.Should().Contain(r => r.Type == "Highland");
        regions!.Should().Contain(r => r.Type == "Coastal");
        regions!.Should().Contain(r => r.Type == "Volcanic");
    }

    [Fact]
    public async Task GetRegions_Should_All_Belong_To_Veldrath_World()
    {
        var regions = await _client.GetFromJsonAsync<RegionDto[]>("/api/regions");

        regions!.Should().OnlyContain(r => r.WorldId == "veldrath");
    }

    [Fact]
    public async Task GetRegionById_Should_Return_Region_Details()
    {
        var response = await _client.GetAsync("/api/regions/greymoor");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var region = await response.Content.ReadFromJsonAsync<RegionDto>();
        region!.Id.Should().Be("greymoor");
        region.Type.Should().Be("Highland");
        region.MinLevel.Should().Be(5);
        region.MaxLevel.Should().Be(14);
    }

    [Fact]
    public async Task GetRegionById_Should_Return_404_For_Unknown_Region()
    {
        var response = await _client.GetAsync("/api/regions/nonexistent-region");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRegionConnections_Should_Return_Adjacent_Regions()
    {
        // greymoor connects to: varenmark, saltcliff, cinderplain
        var response = await _client.GetAsync("/api/regions/greymoor/connections");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var connected = await response.Content.ReadFromJsonAsync<RegionDto[]>();
        connected.Should().NotBeNull();
        connected!.Length.Should().Be(3);
        connected.Should().Contain(r => r.Id == "varenmark");
        connected.Should().Contain(r => r.Id == "saltcliff");
        connected.Should().Contain(r => r.Id == "cinderplain");
    }

    [Fact]
    public async Task GetRegionConnections_Should_Return_Empty_Array_For_Unknown_Region()
    {
        var response = await _client.GetAsync("/api/regions/nonexistent-region/connections");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var connected = await response.Content.ReadFromJsonAsync<RegionDto[]>();
        connected.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetRegions_Does_Not_Require_Authentication()
    {
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/regions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetZonesByRegion_Should_Return_Zones_For_Varenmark()
    {
        var response = await _client.GetAsync("/api/zones/by-region/varenmark");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var zones = await response.Content.ReadFromJsonAsync<ZoneDto[]>();
        zones.Should().NotBeNull();
        zones!.Length.Should().Be(6);
        zones.Should().OnlyContain(z => z.RegionId == "varenmark");
    }

    [Fact]
    public async Task GetZonesByRegion_Should_Return_Empty_For_Unknown_Region()
    {
        var response = await _client.GetAsync("/api/zones/by-region/nonexistent-region");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var zones = await response.Content.ReadFromJsonAsync<ZoneDto[]>();
        zones.Should().NotBeNull().And.BeEmpty();
    }
}
