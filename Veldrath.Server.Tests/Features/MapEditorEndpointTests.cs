using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using Veldrath.Contracts.Map;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// HTTP-level tests for the map authoring endpoints.
/// </summary>
/// <remarks>
/// The group used to require <c>content.manage</c> for reads as well, which made the read-only Foundry view
/// unreachable for a Reviewer. That split, and the validate-only endpoint that must never write, are the two
/// things a service-level test cannot prove.
/// </remarks>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class MapEditorEndpointTests(WebAppFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<HttpClient> AuthedClientAsync(string username, params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username, roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Seeds the authored graphs when the content database is empty and returns one with nodes.</summary>
    private async Task<string> EnsureAMapAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        if (!await db.Maps.AnyAsync())
        {
            await MapGraphSeeder.SeedAsync(db);
            await ZoneLocationsSeeder.SeedAsync(db);
        }

        return await db.Maps
            .Where(map => map.Nodes.Count > 1)
            .Select(map => map.Id)
            .FirstAsync();
    }

    private static Task<MapGraphDto?> ReadMapAsync(HttpClient client, string mapId) =>
        client.GetFromJsonAsync<MapGraphDto>(
            $"/api/content/maps/{Uri.EscapeDataString(mapId)}",
            Json);

    [Fact]
    public async Task Reads_RequireContentReadAccess()
    {
        using var client = await AuthedClientAsync("MapPlainReader");

        var response = await client.GetAsync("/api/content/maps");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reads_AreAllowedForAReviewerWithoutTheWritePermission()
    {
        await EnsureAMapAsync();
        using var client = await AuthedClientAsync("MapCuratorReader", "Curator");

        var response = await client.GetAsync("/api/content/maps");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Targets_RequireReadAccessAndListTheSeededReferences()
    {
        await EnsureAMapAsync();

        using var plain = await AuthedClientAsync("MapPlainTargets");
        (await plain.GetAsync("/api/content/maps/targets")).StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        using var client = await AuthedClientAsync("MapTargetReader", "Curator");
        var targets = await client.GetFromJsonAsync<MapTargetsDto>("/api/content/maps/targets", Json);

        targets!.Maps.Should().NotBeEmpty();
        targets.Zones.Should().NotBeEmpty();
        targets.Locations.Should().NotBeEmpty();
        targets.Regions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_ReturnsNotFoundForAnUnknownMap()
    {
        using var client = await AuthedClientAsync("MapEditorReader", "ContentEditor");

        var response = await client.GetAsync("/api/content/maps/zone:nowhere");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validate_RequiresTheWritePermission()
    {
        var mapId = await EnsureAMapAsync();
        using var client = await AuthedClientAsync("MapCuratorValidator", "Curator");
        var graph = await ReadMapAsync(client, mapId);

        var response = await client.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(mapId)}/validate",
            new MapGraphValidationRequest(graph!),
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Validate_ReportsAnExitToANodeTheTargetMapDoesNotHave_AndWritesNothing()
    {
        var mapId = await EnsureAMapAsync();
        using var client = await AuthedClientAsync("MapEditorValidator", "ContentEditor");
        var graph = (await ReadMapAsync(client, mapId))!;
        var exit = graph.Nodes.First(node => !string.IsNullOrWhiteSpace(node.TargetMapId));

        var broken = graph with
        {
            Nodes = graph.Nodes
                .Select(node => node.Id == exit.Id ? node with { TargetNodeId = "ghost-node" } : node)
                .ToList(),
        };

        var response = await client.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(mapId)}/validate",
            new MapGraphValidationRequest(broken),
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MapGraphValidationResponse>(Json);
        result!.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "target_node_unknown");

        var stored = await ReadMapAsync(client, mapId);
        stored!.Version.Should().Be(graph.Version, "a validate pass must not write");
        stored.Nodes.Single(node => node.Id == exit.Id).TargetNodeId.Should().Be(exit.TargetNodeId);
    }

    [Fact]
    public async Task Save_RefusesAStaleVersion()
    {
        var mapId = await EnsureAMapAsync();
        using var client = await AuthedClientAsync("MapEditorSaver", "ContentEditor");
        var graph = (await ReadMapAsync(client, mapId))!;

        var response = await client.PutAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(mapId)}",
            new MapGraphWriteRequest(graph, graph.Version + 42),
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
