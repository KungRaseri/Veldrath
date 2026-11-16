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
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
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
    public async Task Reads_RequireAnAccountButNotAContentPermission()
    {
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/content/maps")).StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized, "the graph carries hidden nodes and unlock keys");

        // The portal is public to signed-in accounts: reading the world graph is not a staff capability.
        using var client = await AuthedClientAsync("MapPlainReader");
        (await client.GetAsync("/api/content/maps")).StatusCode.Should().Be(HttpStatusCode.OK);
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
    public async Task Targets_ListTheSeededReferencesForAnySignedInAccount()
    {
        await EnsureAMapAsync();

        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/content/maps/targets")).StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);

        using var client = await AuthedClientAsync("MapPlainTargets");
        var targets = await client.GetFromJsonAsync<MapTargetsDto>("/api/content/maps/targets", Json);

        targets!.Maps.Should().NotBeEmpty();
        targets.Zones.Should().NotBeEmpty();
        targets.Locations.Should().NotBeEmpty();
        targets.Regions.Should().NotBeEmpty();
        targets.Nodes.Should().NotBeEmpty();
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

    [Fact]
    public async Task Create_RequiresTheWritePermission()
    {
        using var plain = await AuthedClientAsync("MapPlainCreator");
        (await plain.PostAsJsonAsync(
                "/api/content/maps",
                new MapCreateRequest("Zone", "plain-zone", "Nope"),
                Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A Curator reviews; it does not author.
        using var curator = await AuthedClientAsync("MapCuratorCreator", "Curator");
        (await curator.PostAsJsonAsync(
                "/api/content/maps",
                new MapCreateRequest("Zone", "curator-zone", "Nope"),
                Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var editor = await AuthedClientAsync("MapEditorCreator", "ContentEditor");
        (await editor.PostAsJsonAsync(
                "/api/content/maps",
                new MapCreateRequest("Zone", "editor-zone", "Yes"),
                Json))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Review_RequiresTheReviewPermission()
    {
        var mapId = await EnsureAMapAsync();
        using var editor = await AuthedClientAsync("MapEditorReviewer", "ContentEditor");

        var response = await editor.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(mapId)}/review",
            new MapReviewRequest(true),
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Review_AcceptsAnAdminWithoutTheCuratorRole()
    {
        var (authorToken, _) = await factory.CreateAccountTokenAsync("MapAdminGate_Author", "ContentEditor");
        using var author = factory.CreateClient();
        author.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authorToken);

        var created = await author.PostAsJsonAsync(
            "/api/content/maps",
            new MapCreateRequest("Zone", "admin-gate-zone", "Admin Gate Zone"),
            Json);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await created.Content.ReadFromJsonAsync<MapGraphDto>(Json);

        var submitted = await author.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(draft!.Id)}/submit",
            new { },
            Json);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin holds content.review but is not in the Curator role; the old role policy locked it out of a
        // decision it is otherwise trusted to make.
        var (adminToken, _) = await factory.CreateAccountTokenAsync("MapAdminGate_Admin", "Admin");
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var approved = await admin.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(draft.Id)}/review",
            new MapReviewRequest(true),
            Json);

        approved.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheGate_HoldsFromDraftToApproval()
    {
        var (token, authorAccountId) = await factory.CreateAccountTokenAsync("MapGateReviewer", "Admin", "Curator");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var created = await client.PostAsJsonAsync(
            "/api/content/maps",
            new MapCreateRequest("Zone", "gate-zone", "Gate Zone"),
            Json);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var draft = await created.Content.ReadFromJsonAsync<MapGraphDto>(Json);
        draft!.IsActive.Should().BeFalse("a new map is a draft");

        var early = await client.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(draft.Id)}/review",
            new MapReviewRequest(true),
            Json);
        early.StatusCode.Should().Be(HttpStatusCode.Conflict, "nothing was submitted");

        var submitted = await client.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(draft.Id)}/submit",
            new { },
            Json);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);

        var approved = await client.PostAsJsonAsync(
            $"/api/content/maps/{Uri.EscapeDataString(draft.Id)}/review",
            new MapReviewRequest(true),
            Json);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await ReadMapAsync(client, draft.Id);
        stored!.IsActive.Should().BeTrue();
        stored.ApprovedVersion.Should().Be(stored.Version);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.FoundryNotifications.AnyAsync(notification =>
            notification.Kind == FoundryNotificationKind.MapReview &&
            notification.MapId == draft.Id &&
            notification.RecipientId == authorAccountId))
            .Should()
            .BeTrue("the author is told in the inbox they already use for submission decisions");
    }
}
