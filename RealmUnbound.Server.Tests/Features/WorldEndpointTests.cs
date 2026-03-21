using System.Net;
using System.Net.Http.Json;
using RealmUnbound.Contracts.Zones;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

[Trait("Category", "Integration")]
public class WorldEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetWorlds_Should_Return_All_Seeded_Worlds()
    {
        var response = await _client.GetAsync("/api/worlds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var worlds = await response.Content.ReadFromJsonAsync<WorldDto[]>();
        worlds.Should().NotBeNull();
        worlds!.Length.Should().Be(1);
    }

    [Fact]
    public async Task GetWorlds_Should_Return_Draveth()
    {
        var worlds = await _client.GetFromJsonAsync<WorldDto[]>("/api/worlds");

        worlds!.Should().ContainSingle(w => w.Id == "draveth");
        worlds![0].Name.Should().Be("Draveth");
        worlds![0].Era.Should().Be("The Age of Embers");
    }

    [Fact]
    public async Task GetWorldById_Should_Return_World_Details()
    {
        var response = await _client.GetAsync("/api/worlds/draveth");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var world = await response.Content.ReadFromJsonAsync<WorldDto>();
        world!.Id.Should().Be("draveth");
        world.Name.Should().Be("Draveth");
        world.Era.Should().Be("The Age of Embers");
        world.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetWorldById_Should_Return_404_For_Unknown_World()
    {
        var response = await _client.GetAsync("/api/worlds/nonexistent-world");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWorlds_Does_Not_Require_Authentication()
    {
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/worlds");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
