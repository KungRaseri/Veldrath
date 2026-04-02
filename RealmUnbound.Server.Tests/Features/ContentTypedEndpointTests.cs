using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>
/// Seeds one entity of every typed content catalog route into a fresh in-memory
/// database once, then provides a shared <see cref="HttpClient"/> for all tests.
/// </summary>
public sealed class ContentTypedEndpointsFixture : IAsyncLifetime
{
    public WebAppFactory Factory { get; } = new();
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        db.ActorClasses.Add(new ActorClass
        {
            Slug        = "typed-warrior",
            TypeKey     = "melee",
            DisplayName = "Typed Warrior",
            IsActive    = true,
            PrimaryStat = "strength",
        });

        db.Species.Add(new Species
        {
            Slug               = "typed-human",
            TypeKey            = "humanoid",
            DisplayName        = "Typed Human",
            IsActive           = true,
            IsPlayerSelectable = true,
        });

        db.Backgrounds.Add(new Background
        {
            Slug        = "typed-soldier",
            TypeKey     = "combat",
            DisplayName = "Typed Soldier",
            IsActive    = true,
        });

        db.Skills.Add(new Skill
        {
            Slug        = "typed-mining",
            TypeKey     = "gathering",
            DisplayName = "Typed Mining",
            IsActive    = true,
        });

        // Two enchantments with different TargetSlot values to test slot filtering.
        db.Enchantments.Add(new Enchantment
        {
            Slug        = "typed-flame",
            TypeKey     = "elemental",
            DisplayName = "Flame Enchant",
            IsActive    = true,
            TargetSlot  = "weapon",
        });
        db.Enchantments.Add(new Enchantment
        {
            Slug        = "typed-frost",
            TypeKey     = "elemental",
            DisplayName = "Frost Enchant",
            IsActive    = true,
            TargetSlot  = "armor",
        });

        db.Powers.Add(new Power
        {
            Slug        = "typed-strike",
            TypeKey     = "offensive",
            DisplayName = "Typed Strike",
            IsActive    = true,
            PowerType   = "active",
        });

        // Item entity: TypeKey = category; ItemType = sub-type used for filtering.
        db.Items.Add(new Item
        {
            Slug        = "typed-gem",
            TypeKey     = "gems",
            DisplayName = "Typed Gem",
            IsActive    = true,
            ItemType    = "gem",
        });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}

/// <summary>
/// Integration tests for the typed catalog GET endpoints under <c>/api/content</c>.
/// Covers classes, species, backgrounds, skills, enchantments (with slot filter),
/// abilities, and items (with type filter). All routes are anonymous.
/// </summary>
[Trait("Category", "Integration")]
public class ContentTypedEndpointTests(ContentTypedEndpointsFixture fixture)
    : IClassFixture<ContentTypedEndpointsFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // GET /api/content/classes
    [Fact]
    public async Task GetClasses_Returns_OK_And_ContainsSeededClass()
    {
        var response = await _client.GetAsync("/api/content/classes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ActorClassDto>>();
        items.Should().Contain(c => c.Slug == "typed-warrior");
    }

    [Fact]
    public async Task GetClasses_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/classes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetClassBySlug_Returns_Correct_Class()
    {
        var response = await _client.GetAsync("/api/content/classes/typed-warrior");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<ActorClassDto>();
        item!.Slug.Should().Be("typed-warrior");
        item.DisplayName.Should().Be("Typed Warrior");
        item.TypeKey.Should().Be("melee");
        item.PrimaryStat.Should().Be("strength");
    }

    [Fact]
    public async Task GetClassBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/classes/no-such-class");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/species
    [Fact]
    public async Task GetSpecies_Returns_OK_And_ContainsSeededSpecies()
    {
        var response = await _client.GetAsync("/api/content/species");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<SpeciesDto>>();
        items.Should().Contain(s => s.Slug == "typed-human");
    }

    [Fact]
    public async Task GetSpeciesBySlug_Returns_Correct_Species()
    {
        var response = await _client.GetAsync("/api/content/species/typed-human");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<SpeciesDto>();
        item!.Slug.Should().Be("typed-human");
        item.DisplayName.Should().Be("Typed Human");
        item.TypeKey.Should().Be("humanoid");
    }

    [Fact]
    public async Task GetSpeciesBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/species/no-such-species");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/backgrounds
    [Fact]
    public async Task GetBackgrounds_Returns_OK_And_ContainsSeededBackground()
    {
        var response = await _client.GetAsync("/api/content/backgrounds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<BackgroundDto>>();
        items.Should().Contain(b => b.Slug == "typed-soldier");
    }

    [Fact]
    public async Task GetBackgroundBySlug_Returns_Correct_Background()
    {
        var response = await _client.GetAsync("/api/content/backgrounds/typed-soldier");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<BackgroundDto>();
        item!.Slug.Should().Be("typed-soldier");
        item.DisplayName.Should().Be("Typed Soldier");
        item.TypeKey.Should().Be("combat");
    }

    [Fact]
    public async Task GetBackgroundBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/backgrounds/no-such-background");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/skills
    [Fact]
    public async Task GetSkills_Returns_OK_And_ContainsSeededSkill()
    {
        var response = await _client.GetAsync("/api/content/skills");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<SkillDto>>();
        items.Should().Contain(s => s.Slug == "typed-mining");
    }

    [Fact]
    public async Task GetSkillBySlug_Returns_Correct_Skill()
    {
        var response = await _client.GetAsync("/api/content/skills/typed-mining");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<SkillDto>();
        item!.Slug.Should().Be("typed-mining");
        item.DisplayName.Should().Be("Typed Mining");
        item.TypeKey.Should().Be("gathering");
    }

    [Fact]
    public async Task GetSkillBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/skills/no-such-skill");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/enchantments
    [Fact]
    public async Task GetEnchantments_Returns_OK_And_All_Active_Enchantments()
    {
        var response = await _client.GetAsync("/api/content/enchantments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<EnchantmentDto>>();
        items.Should().HaveCountGreaterThanOrEqualTo(2)
             .And.Contain(e => e.Slug == "typed-flame")
             .And.Contain(e => e.Slug == "typed-frost");
    }

    [Fact]
    public async Task GetEnchantments_FilterByTargetSlot_Returns_Matching_Only()
    {
        var response = await _client.GetAsync("/api/content/enchantments?targetSlot=weapon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<EnchantmentDto>>();
        items.Should().Contain(e => e.Slug == "typed-flame")
             .And.NotContain(e => e.Slug == "typed-frost");
    }

    [Fact]
    public async Task GetEnchantmentBySlug_Returns_Correct_Enchantment()
    {
        var response = await _client.GetAsync("/api/content/enchantments/typed-flame");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<EnchantmentDto>();
        item!.Slug.Should().Be("typed-flame");
        item.DisplayName.Should().Be("Flame Enchant");
        item.TypeKey.Should().Be("elemental");
    }

    [Fact]
    public async Task GetEnchantmentBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/enchantments/no-such-enchantment");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/powers
    [Fact]
    public async Task GetAbilities_Returns_OK_And_ContainsSeededAbility()
    {
        var response = await _client.GetAsync("/api/content/powers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<PowerDto>>();
        items.Should().Contain(a => a.Slug == "typed-strike");
    }

    [Fact]
    public async Task GetAbilityBySlug_Returns_Correct_Ability()
    {
        var response = await _client.GetAsync("/api/content/powers/typed-strike");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<PowerDto>();
        item!.Slug.Should().Be("typed-strike");
        item.DisplayName.Should().Be("Typed Strike");
    }

    [Fact]
    public async Task GetAbilityBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/powers/no-such-ability");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/items
    [Fact]
    public async Task GetItems_Returns_OK_And_ContainsSeededItem()
    {
        var response = await _client.GetAsync("/api/content/items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ItemDto>>();
        items.Should().Contain(i => i.Slug == "typed-gem");
    }

    [Fact]
    public async Task GetItems_FilterByType_Returns_Matching_Only()
    {
        var response = await _client.GetAsync("/api/content/items?type=gem");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ItemDto>>();
        items.Should().Contain(i => i.Slug == "typed-gem");
    }

    [Fact]
    public async Task GetItemBySlug_Returns_Correct_Item()
    {
        var response = await _client.GetAsync("/api/content/items/typed-gem");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<ItemDto>();
        item!.Slug.Should().Be("typed-gem");
        item.DisplayName.Should().Be("Typed Gem");
        item.TypeKey.Should().Be("gems");
    }

    [Fact]
    public async Task GetItemBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/items/no-such-item");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
