using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using Veldrath.Contracts.Content;
using Veldrath.Contracts.Foundry;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Seeds one active Skill and one inactive Skill into the test database once,
/// then provides a single <see cref="HttpClient"/> shared across all browse tests.
/// </summary>
public sealed class ContentBrowseFixture : IAsyncLifetime
{
    public WebAppFactory Factory { get; }
    public HttpClient Client { get; private set; } = null!;

    public ContentBrowseFixture(WebAppFactory factory) => Factory = factory;

    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        db.Skills.Add(new Skill
        {
            Slug        = "browse-test-skill",
            TypeKey     = "active",
            DisplayName = "Browse Test Skill",
            IsActive    = true,
        });
        db.Skills.Add(new Skill
        {
            Slug        = "browse-inactive-skill",
            TypeKey     = "passive",
            DisplayName = "Inactive Skill",
            IsActive    = false,
        });

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentBrowseEndpointTests(ContentBrowseFixture fixture) : IClassFixture<ContentBrowseFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // GET /api/content/schema
    [Fact]
    public async Task GetSchema_Returns_All_20_Types()
    {
        var response = await _client.GetAsync("/api/content/schema");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.Content.ReadFromJsonAsync<List<ContentTypeInfoDto>>();
        types.Should().HaveCount(20);
    }

    [Fact]
    public async Task GetSchema_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();

        var response = await anon.GetAsync("/api/content/schema");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSchema_Contains_Expected_Type_Keys()
    {
        var response = await _client.GetAsync("/api/content/schema");
        var types    = await response.Content.ReadFromJsonAsync<List<ContentTypeInfoDto>>();
        var keys     = types!.Select(t => t.ContentType).ToHashSet();

        foreach (var expected in new[] { "ability", "skill", "weapon", "spell", "quest", "recipe", "loottable" })
            keys.Should().Contain(expected);
    }

    [Fact]
    public async Task GetSchema_Type_Entries_Have_Non_Empty_Labels()
    {
        var response = await _client.GetAsync("/api/content/schema");
        var types    = await response.Content.ReadFromJsonAsync<List<ContentTypeInfoDto>>();

        types!.Should().AllSatisfy(t =>
        {
            t.ContentType.Should().NotBeNullOrWhiteSpace();
            t.DisplayLabel.Should().NotBeNullOrWhiteSpace();
        });
    }

    // GET /api/content/browse
    [Fact]
    public async Task Browse_Returns_OK_For_Known_Type()
    {
        var response = await _client.GetAsync("/api/content/browse?type=skill");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Browse_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();

        var response = await anon.GetAsync("/api/content/browse?type=skill");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Browse_Returns_NotFound_For_Unknown_Type()
    {
        var response = await _client.GetAsync("/api/content/browse?type=doesnotexist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_Returns_Seeded_Active_Skill()
    {
        var result = await BrowseSkillsAsync();

        result!.Items.Should().Contain(s => s.Slug == "browse-test-skill");
    }

    [Fact]
    public async Task Browse_Does_Not_Return_Inactive_Entity()
    {
        var result = await BrowseSkillsAsync();

        result!.Items.Should().NotContain(s => s.Slug == "browse-inactive-skill");
    }

    [Fact]
    public async Task Browse_Defaults_To_Page_1_And_PageSize_20()
    {
        var result = await BrowseSkillsAsync();

        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Browse_Respects_Custom_PageSize()
    {
        var response = await _client.GetAsync("/api/content/browse?type=skill&page=1&pageSize=5");
        var result   = await response.Content.ReadFromJsonAsync<PagedResult<ContentSummaryDto>>();

        result!.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task Browse_Summary_Items_Carry_Correct_ContentType()
    {
        var result = await BrowseSkillsAsync();
        var seeded = result!.Items.First(s => s.Slug == "browse-test-skill");

        seeded.ContentType.Should().Be("skill");
    }

    [Fact]
    public async Task Browse_Summary_Items_Carry_Correct_Slug_And_DisplayName()
    {
        var result = await BrowseSkillsAsync();
        var seeded = result!.Items.First(s => s.Slug == "browse-test-skill");

        seeded.DisplayName.Should().Be("Browse Test Skill");
    }

    [Fact]
    public async Task Browse_TotalCount_Reflects_Active_Items_Only()
    {
        var result = await BrowseSkillsAsync();

        // Only the active skill was seeded; the inactive one must be excluded
        result!.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        result.Items.Should().NotContain(s => s.IsActive == false);
    }

    // GET /api/content/browse/{type}/{slug}
    [Fact]
    public async Task BrowseDetail_Returns_OK_And_Detail_For_Seeded_Skill()
    {
        var response = await _client.GetAsync("/api/content/browse/skill/browse-test-skill");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ContentDetailDto>();
        detail!.Summary.Slug.Should().Be("browse-test-skill");
        detail.Summary.ContentType.Should().Be("skill");
    }

    [Fact]
    public async Task BrowseDetail_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();

        var response = await anon.GetAsync("/api/content/browse/skill/browse-test-skill");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BrowseDetail_Returns_NotFound_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/browse/skill/no-such-skill-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BrowseDetail_Returns_NotFound_For_Unknown_Type()
    {
        var response = await _client.GetAsync("/api/content/browse/doesnotexist/some-slug");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BrowseDetail_Payload_Is_A_Json_Object()
    {
        var response = await _client.GetAsync("/api/content/browse/skill/browse-test-skill");
        var detail   = await response.Content.ReadFromJsonAsync<ContentDetailDto>();

        detail!.Payload.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task BrowseDetail_Does_Not_Return_Inactive_Entity()
    {
        var response = await _client.GetAsync("/api/content/browse/skill/browse-inactive-skill");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BrowseDetail_Payload_Is_CamelCase()
    {
        var response = await _client.GetAsync("/api/content/browse/skill/browse-test-skill");
        var detail   = await response.Content.ReadFromJsonAsync<ContentDetailDto>();

        // Spot-check that the known root field is camelCase
        detail!.Payload.TryGetProperty("slug", out _).Should().BeTrue();
    }

    // Helpers
    private async Task<PagedResult<ContentSummaryDto>?> BrowseSkillsAsync() =>
        await (await _client.GetAsync("/api/content/browse?type=skill"))
              .Content
              .ReadFromJsonAsync<PagedResult<ContentSummaryDto>>();
}
