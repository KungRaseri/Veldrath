using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Veldrath.Contracts.Content;
using Veldrath.Contracts.Foundry;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Drives every content-type arm of the generic Foundry browser — <c>GET /api/content/browse?type=…</c> and
/// <c>GET /api/content/browse/{type}/{slug}</c>. Each theory case takes the first entry the list returns and then
/// loads its detail, so the pair proves both the switch arm and the matching detail arm without hard-coding slugs
/// that the seeded catalog could rename.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentGenericBrowseTypeCoverageTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("power")]
    [InlineData("ability")]
    [InlineData("spell")]
    [InlineData("species")]
    [InlineData("class")]
    [InlineData("archetype")]
    [InlineData("instance")]
    [InlineData("background")]
    [InlineData("skill")]
    [InlineData("item")]
    [InlineData("weapon")]
    [InlineData("armor")]
    [InlineData("material")]
    [InlineData("materialproperty")]
    [InlineData("enchantment")]
    [InlineData("quest")]
    [InlineData("recipe")]
    [InlineData("loottable")]
    [InlineData("organization")]
    [InlineData("zonelocation")]
    [InlineData("dialogue")]
    [InlineData("language")]
    public async Task Browse_And_Detail_Work_ForEveryContentType(string type)
    {
        // Act — list
        var listResponse = await _client.GetAsync($"/api/content/browse?type={type}");

        // Assert — list
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"{type} is a registered content type");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<ContentSummaryDto>>();
        page.Should().NotBeNull();
        page!.Items.Should().NotBeEmpty($"the seeded catalog has at least one active {type}");
        page.TotalCount.Should().BeGreaterThanOrEqualTo(page.Items.Count);
        page.Items.Should().OnlyContain(i => i.ContentType == type);

        var slug = page.Items[0].Slug;

        // Act — detail
        var detailResponse = await _client.GetAsync($"/api/content/browse/{type}/{slug}");

        // Assert — detail
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ContentDetailDto>();
        detail.Should().NotBeNull();
        detail!.Summary.Slug.Should().Be(slug);
        detail.Summary.ContentType.Should().Be(type);
        detail.Payload.ValueKind.Should().Be(JsonValueKind.Object);
        detail.Payload.TryGetProperty("slug", out var payloadSlug).Should().BeTrue("the payload is the camelCase entity");
        payloadSlug.GetString().Should().Be(slug);
    }

    [Fact]
    public async Task Browse_UnknownSlugDetail_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/browse/organization/definitely-not-a-slug");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Browse_PageSizeIsClampedTo100_AndPageFloorsAt1()
    {
        var response = await _client.GetAsync("/api/content/browse?type=item&page=0&pageSize=9999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ContentSummaryDto>>();
        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(100);
    }

    // The generic browser's `search` filter is built on EF.Functions.ILike, which only the Npgsql provider
    // translates; on the SQLite test host the request fails at query-translation time. Search is therefore not
    // covered here — see the class notes.

    [Fact]
    public async Task Browse_UnknownType_ReturnsNotFound_WithTheTypeName()
    {
        var response = await _client.GetAsync("/api/content/browse?type=spaceship");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("spaceship");
    }

    [Fact]
    public async Task Browse_WeaponAndArmorShareTheItemsTableWithoutLeaking()
    {
        var weapons = await _client.GetFromJsonAsync<PagedResult<ContentSummaryDto>>("/api/content/browse?type=weapon");
        var armor = await _client.GetFromJsonAsync<PagedResult<ContentSummaryDto>>("/api/content/browse?type=armor");

        weapons!.Items.Should().NotBeEmpty();
        armor!.Items.Should().NotBeEmpty();
        weapons.Items.Select(i => i.Id).Should().NotIntersectWith(armor.Items.Select(i => i.Id));
    }
}
