using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// HTTP-level tests for the Foundry content write path: validation, RBAC, concurrency and soft delete.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentWriteEndpointTests(WebAppFactory factory)
{
    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

    private async Task<HttpClient> ClientAsAsync(params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync($"content-writer-{Guid.NewGuid():N}", roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object ValidItem(string slug) => new
    {
        slug,
        typeKey = "trinkets",
        displayName = "Test Trinket",
        itemType = "consumable",
        rarityWeight = 50,
        isActive = true,
    };

    // ── Authorization ──────────────────────────────────────────────────────────

    [Fact]
    public async Task An_anonymous_write_is_rejected()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsync("/api/content/item", Json(ValidItem("anon-trinket")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_caller_without_the_content_permission_is_rejected()
    {
        using var client = await ClientAsAsync();

        var response = await client.PostAsync("/api/content/item", Json(ValidItem("forbidden-trinket")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reads_stay_anonymous_while_writes_are_guarded()
    {
        using var anonymous = factory.CreateClient();

        var read = await anonymous.GetAsync("/api/content/browse?type=item");
        var write = await anonymous.PostAsync("/api/content/item", Json(ValidItem("anon-trinket-2")));

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        write.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validating_an_existing_entity_returns_its_issues_without_writing()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var response = await client.GetAsync("/api/content/item/health-potion/validate");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isValid").GetBoolean().Should().BeTrue();
        body.GetProperty("entity").GetProperty("slug").GetString().Should().Be("health-potion");
    }

    [Fact]
    public async Task A_quest_with_a_dangling_objective_is_rejected_with_the_same_rule_as_the_audit()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var response = await client.PostAsync("/api/content/quest", Json(new
        {
            slug = "b2-dangling-quest",
            typeKey = "side",
            displayName = "Dangling",
            minLevel = 1,
            objectives = new
            {
                items = new[] { new { type = "collect", target = "no-such-item", quantity = 1 } },
            },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isValid").GetBoolean().Should().BeFalse();

        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.Should().Contain(issue =>
            issue.GetProperty("rule").GetString() == "missing-quest-target"
            && issue.GetProperty("field").GetString()!.Contains("collect:no-such-item"));
    }

    [Fact]
    public async Task A_quest_with_no_objectives_is_rejected()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var response = await client.PostAsync("/api/content/quest", Json(new
        {
            slug = "b2-empty-quest",
            typeKey = "side",
            displayName = "Empty",
            minLevel = 1,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").EnumerateArray()
            .Select(issue => issue.GetProperty("rule").GetString())
            .Should().Contain("quest_without_objectives");
    }

    // ── Create / update / deactivate ───────────────────────────────────────────

    [Fact]
    public async Task A_valid_entity_is_created_and_then_visible_on_browse()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-created-{Guid.NewGuid():N}";

        var created = await client.PostAsync("/api/content/item", Json(ValidItem(slug)));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        // No `search` filter here: it uses Npgsql's ILike, which the SQLite test host cannot translate.
        var browse = await client.GetFromJsonAsync<JsonElement>("/api/content/browse?type=item&pageSize=100");
        browse.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("slug").GetString())
            .Should().Contain(slug);
    }

    [Fact]
    public async Task Creating_the_same_slug_twice_conflicts()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-duplicate-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/item", Json(ValidItem(slug)))).StatusCode
            .Should().Be(HttpStatusCode.Created);
        (await client.PostAsync("/api/content/item", Json(ValidItem(slug)))).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_update_that_changes_the_slug_is_refused()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-immutable-{Guid.NewGuid():N}";
        await client.PostAsync("/api/content/item", Json(ValidItem(slug)));

        var renamed = ValidItem($"b2-renamed-{Guid.NewGuid():N}");
        var response = await client.PutAsync($"/api/content/item/{slug}", Json(renamed));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_stale_expected_timestamp_conflicts_instead_of_overwriting()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-stale-{Guid.NewGuid():N}";
        await client.PostAsync("/api/content/item", Json(ValidItem(slug)));

        var stale = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));
        var response = await client.PutAsync($"/api/content/item/{slug}?expectedUpdatedAt={stale}", Json(ValidItem(slug)));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_valid_update_saves_and_bumps_the_version()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-updated-{Guid.NewGuid():N}";
        var created = await client.PostAsync("/api/content/item", Json(ValidItem(slug)));
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var version = createdBody.GetProperty("entity").GetProperty("version").GetInt32();

        var updated = await client.PutAsync($"/api/content/item/{slug}", Json(new
        {
            slug,
            typeKey = "trinkets",
            displayName = "Renamed Trinket",
            itemType = "consumable",
            rarityWeight = 50,
            isActive = true,
        }));

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>();
        updatedBody.GetProperty("entity").GetProperty("displayName").GetString().Should().Be("Renamed Trinket");
        updatedBody.GetProperty("entity").GetProperty("version").GetInt32().Should().Be(version + 1);
    }

    [Fact]
    public async Task Deleting_deactivates_instead_of_removing_the_row()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-deleted-{Guid.NewGuid():N}";
        await client.PostAsync("/api/content/item", Json(ValidItem(slug)));

        var deleted = await client.DeleteAsync($"/api/content/item/{slug}");

        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();

        // The row still exists — `validate` finds it, which a hard delete would not. Browse hides it because browse
        // only lists active content, which is the intended difference between "deactivated" and "gone".
        var stillValidatable = await client.GetAsync($"/api/content/item/{slug}/validate");
        stillValidatable.StatusCode.Should().Be(HttpStatusCode.OK);

        var hiddenFromBrowse = await client.GetAsync($"/api/content/browse/item/{slug}");
        hiddenFromBrowse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_content_type_is_a_404_that_lists_the_known_ones()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var response = await client.PostAsync("/api/content/spaceship", Json(new { slug = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("knownTypes").EnumerateArray()
            .Select(type => type.GetString())
            .Should().Contain(["item", "quest", "zonelocation"]);
    }

    [Fact]
    public async Task Validating_an_unknown_slug_is_a_404()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var response = await client.GetAsync("/api/content/item/no-such-item/validate");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
