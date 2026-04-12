using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using Veldrath.Contracts.Content;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Seeds two weapon <see cref="Item"/> rows, two armor <see cref="Item"/> rows, and one <see cref="Material"/>
/// into a fresh in-memory database once, then provides a shared <see cref="HttpClient"/>
/// for all tests in this fixture.
/// </summary>
public sealed class ContentEquipmentEndpointsFixture : IAsyncLifetime
{
    /// <summary>Gets the web application factory used across all tests in this fixture.</summary>
    public WebAppFactory Factory { get; }

    /// <summary>Gets the shared HTTP client for sending test requests.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Initializes a new instance of <see cref="ContentEquipmentEndpointsFixture"/> with the shared collection factory.</summary>
    public ContentEquipmentEndpointsFixture(WebAppFactory factory) => Factory = factory;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        db.Items.AddRange(
            new Item
            {
                Slug         = "equip-iron-sword",
                TypeKey      = "heavy-blades",
                ItemType     = "weapon",
                DisplayName  = "Iron Sword",
                IsActive     = true,
                RarityWeight = 80,
                WeaponType   = "sword",
                DamageType   = "physical",
                HandsRequired = 1,
                Stats        = new() { DamageMin = 5, DamageMax = 10, Value = 25 },
            },
            new Item
            {
                Slug         = "equip-leather-chest",
                TypeKey      = "light",
                ItemType     = "armor",
                DisplayName  = "Leather Chest",
                IsActive     = true,
                RarityWeight = 80,
                ArmorType    = "light",
                EquipSlot    = "chest",
                Stats        = new() { ArmorRating = 5, Value = 20 },
            });

        db.Materials.Add(new Material
        {
            Slug           = "equip-iron",
            TypeKey        = "metals",
            DisplayName    = "Iron",
            IsActive       = true,
            RarityWeight   = 90,
            MaterialFamily = "metal",
            Stats          = new MaterialStats { Hardness = 7.5f },
        });

        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Integration tests for the equipment catalog GET endpoints under <c>/api/content/items</c>
/// (filtered by <c>?type=weapon</c> or <c>?type=armor</c>) and <c>/api/content/materials</c>.
/// All routes are anonymous.
/// Each section includes a list test, a by-slug test, and a 404 test.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentEquipmentEndpointTests(ContentEquipmentEndpointsFixture fixture)
    : IClassFixture<ContentEquipmentEndpointsFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // GET /api/content/items?type=weapon
    [Fact]
    public async Task GetWeaponItems_Returns_OK_And_ContainsSeededWeapon()
    {
        var response = await _client.GetAsync("/api/content/items?type=weapon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ItemDto>>();
        items.Should().Contain(w => w.Slug == "equip-iron-sword");
    }

    [Fact]
    public async Task GetWeaponItems_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/items?type=weapon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetItemBySlug_Returns_Correct_WeaponItem()
    {
        var response = await _client.GetAsync("/api/content/items/equip-iron-sword");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<ItemDto>();
        item!.Slug.Should().Be("equip-iron-sword");
        item.DisplayName.Should().Be("Iron Sword");
        item.TypeKey.Should().Be("heavy-blades");
        item.ItemType.Should().Be("weapon");
        item.WeaponType.Should().Be("sword");
        item.RarityWeight.Should().Be(80);
    }

    [Fact]
    public async Task GetItemBySlug_Returns_404_For_Unknown_WeaponSlug()
    {
        var response = await _client.GetAsync("/api/content/items/no-such-weapon");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/items?type=armor
    [Fact]
    public async Task GetArmorItems_Returns_OK_And_ContainsSeededArmor()
    {
        var response = await _client.GetAsync("/api/content/items?type=armor");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ItemDto>>();
        items.Should().Contain(a => a.Slug == "equip-leather-chest");
    }

    [Fact]
    public async Task GetArmorItems_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/items?type=armor");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetItemBySlug_Returns_Correct_ArmorItem()
    {
        var response = await _client.GetAsync("/api/content/items/equip-leather-chest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<ItemDto>();
        item!.Slug.Should().Be("equip-leather-chest");
        item.DisplayName.Should().Be("Leather Chest");
        item.TypeKey.Should().Be("light");
        item.ItemType.Should().Be("armor");
        item.ArmorType.Should().Be("light");
        item.RarityWeight.Should().Be(80);
    }

    [Fact]
    public async Task GetItemBySlug_Returns_404_For_Unknown_ArmorSlug()
    {
        var response = await _client.GetAsync("/api/content/items/no-such-armor");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET /api/content/materials
    [Fact]
    public async Task GetMaterials_Returns_OK_And_ContainsSeededMaterial()
    {
        var response = await _client.GetAsync("/api/content/materials");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<MaterialDto>>();
        items.Should().Contain(m => m.Slug == "equip-iron");
    }

    [Fact]
    public async Task GetMaterials_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/materials");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMaterialBySlug_Returns_Correct_Material()
    {
        var response = await _client.GetAsync("/api/content/materials/equip-iron");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<MaterialDto>();
        item!.Slug.Should().Be("equip-iron");
        item.DisplayName.Should().Be("Iron");
        item.MaterialFamily.Should().Be("metal");
        item.RarityWeight.Should().Be(90);
    }

    [Fact]
    public async Task GetMaterialBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/materials/no-such-material");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
