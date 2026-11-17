using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Veldrath.Contracts.Content;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// HTTP-level tests for the content audit endpoint behind the portal's world-health page.
/// </summary>
/// <remarks>
/// The audit itself is covered by <c>ContentAuditServiceTests</c>; what matters here is that it is reachable,
/// permissioned, and runs over the catalog the write path produces rather than over nothing.
/// </remarks>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentAuditEndpointTests(WebAppFactory factory)
{
    private async Task<HttpClient> ClientAsAsync(params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync($"content-audit-{Guid.NewGuid():N}", roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Audit_Should_Require_Authentication_And_Content_View()
    {
        using var anonymous = factory.CreateClient();
        var anonymousResponse = await anonymous.GetAsync("/api/content/audit");

        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A signed-in player without content.view is refused: the issues name entities players cannot see.
        using var player = await ClientAsAsync();
        var playerResponse = await player.GetAsync("/api/content/audit");

        playerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Audit_Should_Run_Over_The_Catalog()
    {
        using var editor = await ClientAsAsync("ContentEditor");

        // A payload the write path accepted is a payload the audit has nothing to say about.
        var created = await editor.PostAsJsonAsync("/api/content/item", new
        {
            slug = "audit-trinket",
            typeKey = "trinkets",
            displayName = "Audit Trinket",
            itemType = "consumable",
            rarityWeight = 50,
            isActive = true,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await editor.GetAsync("/api/content/audit");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var issues = JsonSerializer.Deserialize<ContentAuditIssueDto[]>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        issues.Should().NotBeNull();
        issues.Should().NotContain(issue => issue.Identifier == "audit-trinket");
    }
}
