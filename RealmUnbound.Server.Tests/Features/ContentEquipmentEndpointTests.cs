using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>
/// Seeds one <see cref="Weapon"/>, one <see cref="Armor"/>, and one <see cref="Material"/>
/// into a fresh in-memory database once, then provides a shared <see cref="HttpClient"/>
/// for all tests in this fixture.
/// </summary>
public sealed class ContentEquipmentEndpointsFixture : IAsyncLifetime
{
    /// <summary>Gets the web application factory used across all tests in this fixture.</summary>
    public WebAppFactory Factory { get; } = new();

    /// <summary>Gets the shared HTTP client for sending test requests.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        db.Weapons.Add(new Weapon
        {
            Slug        = "equip-iron-sword",
            TypeKey     = "swords",
            DisplayName = "Iron Sword",
            IsActive    = true,
            RarityWeight = 80,
            WeaponType  = "sword",
            DamageType  = "physical",
            Stats       = new WeaponStats { DamageMin = 5, DamageMax = 10, Value = 25 },
        });

        db.Armors.Add(new Armor
        {
            Slug        = "equip-leather-chest",
            TypeKey     = "chest",
            DisplayName = "Leather Chest",
            IsActive    = true,
            RarityWeight = 80,
            ArmorType   = "light",
            EquipSlot   = "chest",
            Stats       = new ArmorStats { ArmorRating = 5, Value = 20 },
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
    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}

/// <summary>
/// Integration tests for the equipment catalog GET endpoints under <c>/api/content</c>:
/// weapons, armors, and materials. All routes are anonymous.
/// Each section includes a list test, a by-slug test, and a 404 test.
/// </summary>
[Trait("Category", "Integration")]
public class ContentEquipmentEndpointTests(ContentEquipmentEndpointsFixture fixture)
    : IClassFixture<ContentEquipmentEndpointsFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // ── GET /api/content/weapons ──────────────────────────────────────────────

    [Fact]
    public async Task GetWeapons_Returns_OK_And_ContainsSeededWeapon()
    {
        var response = await _client.GetAsync("/api/content/weapons");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<WeaponDto>>();
        items.Should().Contain(w => w.Slug == "equip-iron-sword");
    }

    [Fact]
    public async Task GetWeapons_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/weapons");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetWeaponBySlug_Returns_Correct_Weapon()
    {
        var response = await _client.GetAsync("/api/content/weapons/equip-iron-sword");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<WeaponDto>();
        item!.Slug.Should().Be("equip-iron-sword");
        item.DisplayName.Should().Be("Iron Sword");
        item.TypeKey.Should().Be("swords");
        item.WeaponType.Should().Be("sword");
        item.RarityWeight.Should().Be(80);
    }

    [Fact]
    public async Task GetWeaponBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/weapons/no-such-weapon");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/armors ───────────────────────────────────────────────

    [Fact]
    public async Task GetArmors_Returns_OK_And_ContainsSeededArmor()
    {
        var response = await _client.GetAsync("/api/content/armors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ArmorDto>>();
        items.Should().Contain(a => a.Slug == "equip-leather-chest");
    }

    [Fact]
    public async Task GetArmors_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/armors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetArmorBySlug_Returns_Correct_Armor()
    {
        var response = await _client.GetAsync("/api/content/armors/equip-leather-chest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<ArmorDto>();
        item!.Slug.Should().Be("equip-leather-chest");
        item.DisplayName.Should().Be("Leather Chest");
        item.TypeKey.Should().Be("chest");
        item.ArmorType.Should().Be("light");
        item.RarityWeight.Should().Be(80);
    }

    [Fact]
    public async Task GetArmorBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/armors/no-such-armor");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/materials ────────────────────────────────────────────

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
