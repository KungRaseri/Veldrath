using System.Net;
using System.Net.Http.Json;
using Veldrath.Contracts.Content;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for the catalog GET endpoints that had no coverage:
/// <c>/api/content/organizations</c>, <c>/zone-locations</c>, <c>/dialogues</c>, <c>actor-instances</c>,
/// <c>material-properties</c> and <c>traits</c>, including each type's optional filter, the by-slug lookup and the
/// 404 for an unknown key. All routes are anonymous and read the seeded catalog.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentOrganizationAndWorldCatalogEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Organizations ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizations_ReturnsSeededOrganizations()
    {
        var response = await _client.GetAsync("/api/content/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<OrganizationDto>>();
        items.Should().Contain(o => o.Slug == "iron-anvil-guild" && o.OrgType == "guild");
        items.Should().Contain(o => o.Slug == "goblin-warband");
    }

    [Fact]
    public async Task Organizations_FilterByOrgType_ReturnsOnlyThatType()
    {
        var response = await _client.GetAsync("/api/content/organizations?orgType=faction");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<OrganizationDto>>();
        items.Should().NotBeEmpty();
        items!.Should().OnlyContain(o => o.OrgType == "faction");
        items.Should().Contain(o => o.Slug == "shadowhand-syndicate");
    }

    [Fact]
    public async Task OrganizationBySlug_ReturnsTheMappedFields()
    {
        var item = await _client.GetFromJsonAsync<OrganizationDto>("/api/content/organizations/iron-anvil-guild");

        item.Should().NotBeNull();
        item!.Slug.Should().Be("iron-anvil-guild");
        item.DisplayName.Should().Be("Iron Anvil Guild");
        item.TypeKey.Should().Be("guilds");
        item.OrgType.Should().Be("guild");
        item.RarityWeight.Should().Be(60);
    }

    [Fact]
    public async Task OrganizationBySlug_UnknownSlug_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/organizations/no-such-organization");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Organizations_DoesNotRequireAuth()
    {
        using var anon = factory.CreateClient();

        var response = await anon.GetAsync("/api/content/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Zone locations ────────────────────────────────────────────────────────

    [Fact]
    public async Task ZoneLocations_ReturnsSeededLocations()
    {
        var response = await _client.GetAsync("/api/content/zone-locations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ZoneLocationDto>>();
        items.Should().Contain(w => w.Slug == "crestfall-square" && w.ZoneId == "crestfall");
    }

    [Fact]
    public async Task ZoneLocations_FilterByTypeKey_ReturnsOnlyThatType()
    {
        var response = await _client.GetAsync("/api/content/zone-locations?typeKey=dungeons");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ZoneLocationDto>>();
        items.Should().NotBeEmpty("the seeded catalog has dungeon locations");
        items!.Should().OnlyContain(w => w.TypeKey == "dungeons");
    }

    [Fact]
    public async Task ZoneLocationBySlug_ReturnsTheMappedFields()
    {
        var item = await _client.GetFromJsonAsync<ZoneLocationDto>("/api/content/zone-locations/crestfall-square");

        item.Should().NotBeNull();
        item!.Slug.Should().Be("crestfall-square");
        item.DisplayName.Should().Be("The Crestfall Square");
        item.ZoneId.Should().Be("crestfall");
    }

    [Fact]
    public async Task ZoneLocationBySlug_UnknownSlug_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/zone-locations/no-such-location");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Dialogues ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dialogues_ReturnsSeededDialoguesWithTheirLines()
    {
        var response = await _client.GetAsync("/api/content/dialogues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<DialogueDto>>();
        var blacksmith = items.Should().ContainSingle(d => d.Slug == "blacksmith-greeting").Subject;
        blacksmith.Speaker.Should().Be("village-blacksmith");
        blacksmith.Lines.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Dialogues_FilterBySpeaker_ReturnsOnlyThatSpeaker()
    {
        var response = await _client.GetAsync("/api/content/dialogues?speaker=village-blacksmith");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<DialogueDto>>();
        items.Should().NotBeEmpty();
        items!.Should().OnlyContain(d => d.Speaker == "village-blacksmith");
    }

    [Fact]
    public async Task DialogueBySlug_UnknownSlug_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/dialogues/no-such-dialogue");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Actor instances ───────────────────────────────────────────────────────

    [Fact]
    public async Task ActorInstances_ReturnsSeededInstances()
    {
        var response = await _client.GetAsync("/api/content/actor-instances");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ActorInstanceDto>>();
        items.Should().Contain(a => a.Slug == "elder-goblin-chief" && a.TypeKey == "boss");
    }

    [Fact]
    public async Task ActorInstances_FilterByTypeKey_ReturnsOnlyThatType()
    {
        var response = await _client.GetAsync("/api/content/actor-instances?typeKey=boss");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ActorInstanceDto>>();
        items.Should().NotBeEmpty();
        items!.Should().OnlyContain(a => a.TypeKey == "boss");
    }

    [Fact]
    public async Task ActorInstanceBySlug_CarriesTheOverrides()
    {
        var item = await _client.GetFromJsonAsync<ActorInstanceDto>("/api/content/actor-instances/elder-goblin-chief");

        item.Should().NotBeNull();
        item!.DisplayName.Should().Be("Elder Goblin Chief");
        item.LevelOverride.Should().Be(5);
        item.FactionOverride.Should().Be("goblin-warband");
    }

    [Fact]
    public async Task ActorInstanceBySlug_UnknownSlug_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/actor-instances/no-such-instance");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Material properties ───────────────────────────────────────────────────

    [Fact]
    public async Task MaterialProperties_ReturnsSeededProperties()
    {
        var response = await _client.GetAsync("/api/content/material-properties");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<MaterialPropertyDto>>();
        items.Should().Contain(m => m.Slug == "iron" && m.MaterialFamily == "metal");
    }

    [Fact]
    public async Task MaterialProperties_FilterByFamily_ReturnsOnlyThatFamily()
    {
        var response = await _client.GetAsync("/api/content/material-properties?family=wood");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<MaterialPropertyDto>>();
        items.Should().NotBeEmpty();
        items!.Should().OnlyContain(m => m.MaterialFamily == "wood");
        items.Should().Contain(m => m.Slug == "oak");
    }

    [Fact]
    public async Task MaterialPropertyBySlug_ReturnsTheCostScale()
    {
        var item = await _client.GetFromJsonAsync<MaterialPropertyDto>("/api/content/material-properties/steel");

        item.Should().NotBeNull();
        item!.DisplayName.Should().Be("Steel");
        item.CostScale.Should().BeApproximately(1.8f, 0.001f);
    }

    [Fact]
    public async Task MaterialPropertyBySlug_UnknownSlug_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/material-properties/no-such-property");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Trait definitions ─────────────────────────────────────────────────────

    [Fact]
    public async Task Traits_ReturnsTheSeededVocabulary()
    {
        var response = await _client.GetAsync("/api/content/traits");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<TraitDefinitionDto>>();
        items.Should().Contain(t => t.Key == "hostile" && t.ValueType == "bool");
    }

    [Fact]
    public async Task Traits_FilterByAppliesTo_ReturnsOnlyTraitsForThatEntityKind()
    {
        var response = await _client.GetAsync("/api/content/traits?appliesTo=enchantments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<TraitDefinitionDto>>();
        items.Should().NotBeEmpty();
        items!.Should().Contain(t => t.Key == "cursed")
            .And.NotContain(t => t.Key == "hostile", "actor traits do not apply to enchantments");
        items.Should().OnlyContain(t => t.AppliesTo!.Contains("enchantments") || t.AppliesTo == "*");
    }

    [Fact]
    public async Task TraitByKey_ReturnsTheDefinition()
    {
        var item = await _client.GetFromJsonAsync<TraitDefinitionDto>("/api/content/traits/fireImmune");

        item.Should().NotBeNull();
        item!.Key.Should().Be("fireImmune");
        item.ValueType.Should().Be("bool");
        item.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TraitByKey_UnknownKey_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/content/traits/no-such-trait");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
