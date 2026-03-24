using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Client.Services;
using RealmUnbound.Contracts.Content;

namespace RealmUnbound.Client.Tests;

// ── HttpContentService tests ──────────────────────────────────────────────────
// HttpContentService delegates all work to two private helpers:
//   GetListAsync<T>  – returns List<T> on success, [] on error/exception.
//   GetSingleAsync<T>– returns T? on success, null on 4xx/5xx or exception.
// The tests below verify both paths for each content type so that path strings
// and DTO deserialization are covered end-to-end via FakeHttpHandler.

public class HttpContentServiceTests : TestBase
{
    private static HttpContentService MakeSut(FakeHttpHandler handler)
    {
        var tokens = new TokenStore();
        var http   = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new HttpContentService(http, tokens, NullLogger<HttpContentService>.Instance);
    }

    // ── GetAbilitiesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAbilitiesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<PowerDto>
        {
            new("fireball", "Fireball", "Active", "Fire", "Launch a ball of fire", 20, 3, 5, 100, false, 1, 1, "Damage"),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetAbilitiesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("fireball");
    }

    [Fact]
    public async Task GetAbilitiesAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Internal Server Error", HttpStatusCode.InternalServerError));

        var result = await sut.GetAbilitiesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAbilitiesAsync_Should_Return_Empty_List_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.GetAbilitiesAsync();

        result.Should().BeEmpty();
    }

    // ── GetAbilityAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAbilityAsync_Should_Return_Dto_On_Success()
    {
        var expected = new PowerDto("fireball", "Fireball", "Active", "Fire", "Launch a ball of fire", 20, 3, 5, 100, false, 1, 1, "Damage");
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetAbilityAsync("fireball");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("fireball");
    }

    [Fact]
    public async Task GetAbilityAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetAbilityAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAbilityAsync_Should_Return_Null_On_Exception()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("timeout")));

        var result = await sut.GetAbilityAsync("fireball");

        result.Should().BeNull();
    }

    // ── GetEnemiesAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetEnemiesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<EnemyDto>
        {
            new("goblin", "Goblin", 30, 1, "Humanoid", new Dictionary<string, int> { ["Strength"] = 8 }),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetEnemiesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("goblin");
    }

    [Fact]
    public async Task GetEnemiesAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Service Unavailable", HttpStatusCode.ServiceUnavailable));

        var result = await sut.GetEnemiesAsync();

        result.Should().BeEmpty();
    }

    // ── GetEnemyAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEnemyAsync_Should_Return_Dto_On_Success()
    {
        var expected = new EnemyDto("goblin", "Goblin", 30, 1, "Humanoid", []);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetEnemyAsync("goblin");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Goblin");
    }

    [Fact]
    public async Task GetEnemyAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetEnemyAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetNpcsAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNpcsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<NpcDto> { new("innkeeper-bob", "Bob", "Innkeeper Bob", "Vendor") };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetNpcsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("innkeeper-bob");
    }

    [Fact]
    public async Task GetNpcsAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Bad Gateway", HttpStatusCode.BadGateway));

        var result = await sut.GetNpcsAsync();

        result.Should().BeEmpty();
    }

    // ── GetNpcAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNpcAsync_Should_Return_Dto_On_Success()
    {
        var expected = new NpcDto("innkeeper-bob", "Bob", "Innkeeper Bob", "Vendor");
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetNpcAsync("innkeeper-bob");

        result.Should().NotBeNull();
        result!.Category.Should().Be("Vendor");
    }

    [Fact]
    public async Task GetNpcAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetNpcAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetQuestsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<QuestDto>
        {
            new("lost-sword", "The Lost Sword", "The Lost Sword", "Main", "Normal", 100, "Find the lost sword"),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetQuestsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("lost-sword");
    }

    [Fact]
    public async Task GetQuestsAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetQuestsAsync();

        result.Should().BeEmpty();
    }

    // ── GetQuestAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuestAsync_Should_Return_Dto_On_Success()
    {
        var expected = new QuestDto("lost-sword", "The Lost Sword", "The Lost Sword", "Main", "Normal", 100, "Find the sword");
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetQuestAsync("lost-sword");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("lost-sword");
    }

    [Fact]
    public async Task GetQuestAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetQuestAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetRecipesAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecipesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<RecipeDto>
        {
            new("iron-sword", "Iron Sword", "Weapon", 1, "Anvil",
                [new RecipeMaterialDto("iron-ore", 3)], "item:iron-sword", 1),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetRecipesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("iron-sword");
    }

    [Fact]
    public async Task GetRecipesAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetRecipesAsync();

        result.Should().BeEmpty();
    }

    // ── GetRecipeAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecipeAsync_Should_Return_Dto_On_Success()
    {
        var expected = new RecipeDto("iron-sword", "Iron Sword", "Weapon", 1, "Anvil", [], "item:iron-sword", 1);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetRecipeAsync("iron-sword");

        result.Should().NotBeNull();
        result!.RequiredStation.Should().Be("Anvil");
    }

    [Fact]
    public async Task GetRecipeAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetRecipeAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetLootTablesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetLootTablesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<LootTableDto>
        {
            new("goblin-drops", "Goblin Drops", "Combat", false, false, false,
                [new LootTableEntryDto("item", "copper-coin", 100, 1, 5, false)]),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetLootTablesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("goblin-drops");
    }

    [Fact]
    public async Task GetLootTablesAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetLootTablesAsync();

        result.Should().BeEmpty();
    }

    // ── GetLootTableAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetLootTableAsync_Should_Return_Dto_On_Success()
    {
        var expected = new LootTableDto("goblin-drops", "Goblin Drops", "Combat", false, false, false, []);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetLootTableAsync("goblin-drops");

        result.Should().NotBeNull();
        result!.Context.Should().Be("Combat");
    }

    [Fact]
    public async Task GetLootTableAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetLootTableAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetSpellsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSpellsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<PowerDto>
        {
            new("fire-bolt", "Fire Bolt", "Spell", "Fire", "A bolt of fire", 10, 0, 5, 100, false, 1, 1, "Damage"),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetSpellsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("fire-bolt");
    }

    [Fact]
    public async Task GetSpellsAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetSpellsAsync();

        result.Should().BeEmpty();
    }

    // ── GetSpellAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSpellAsync_Should_Return_Dto_On_Success()
    {
        var expected = new PowerDto("fire-bolt", "Fire Bolt", "Spell", "Fire", "A bolt of fire", 10, 0, 5, 100, false, 1, 1, "Damage");
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetSpellAsync("fire-bolt");

        result.Should().NotBeNull();
        result!.School.Should().Be("Fire");
    }

    [Fact]
    public async Task GetSpellAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetSpellAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetClassesAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetClassesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<ActorClassDto>
        {
            new("fighter", "Fighter", "@classes/warriors", 10, "Strength", 100),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetClassesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("fighter");
    }

    [Fact]
    public async Task GetClassesAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetClassesAsync();

        result.Should().BeEmpty();
    }

    // ── GetClassAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetClassAsync_Should_Return_Dto_On_Success()
    {
        var expected = new ActorClassDto("fighter", "Fighter", "@classes/warriors", 10, "Strength", 100);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetClassAsync("fighter");

        result.Should().NotBeNull();
        result!.PrimaryStat.Should().Be("Strength");
    }

    [Fact]
    public async Task GetClassAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetClassAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetSpeciesAsync (list) ────────────────────────────────────────────────

    [Fact]
    public async Task GetSpeciesAsync_List_Should_Return_List_On_Success()
    {
        var expected = new List<SpeciesDto>
        {
            new("human", "Human", "@species/common", 100),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetSpeciesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("human");
    }

    [Fact]
    public async Task GetSpeciesAsync_List_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetSpeciesAsync();

        result.Should().BeEmpty();
    }

    // ── GetSpeciesAsync (single) ──────────────────────────────────────────────

    [Fact]
    public async Task GetSpeciesAsync_Single_Should_Return_Dto_On_Success()
    {
        var expected = new SpeciesDto("human", "Human", "@species/common", 100);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetSpeciesAsync("human");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Human");
    }

    [Fact]
    public async Task GetSpeciesAsync_Single_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetSpeciesAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetBackgroundsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetBackgroundsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<BackgroundDto>
        {
            new("soldier", "Soldier", "@backgrounds/martial", 80),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetBackgroundsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("soldier");
    }

    [Fact]
    public async Task GetBackgroundsAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetBackgroundsAsync();

        result.Should().BeEmpty();
    }

    // ── GetBackgroundAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBackgroundAsync_Should_Return_Dto_On_Success()
    {
        var expected = new BackgroundDto("soldier", "Soldier", "@backgrounds/martial", 80);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetBackgroundAsync("soldier");

        result.Should().NotBeNull();
        result!.TypeKey.Should().Be("@backgrounds/martial");
    }

    [Fact]
    public async Task GetBackgroundAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetBackgroundAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetSkillsAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSkillsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<SkillDto>
        {
            new("swordsmanship", "Swordsmanship", "@skills/combat", 10, "Dexterity", 50),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetSkillsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("swordsmanship");
    }

    [Fact]
    public async Task GetSkillsAsync_Should_Return_Empty_List_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetSkillsAsync();

        result.Should().BeEmpty();
    }

    // ── GetSkillAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSkillAsync_Should_Return_Dto_On_Success()
    {
        var expected = new SkillDto("swordsmanship", "Swordsmanship", "@skills/combat", 10, "Dexterity", 50);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetSkillAsync("swordsmanship");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Swordsmanship");
    }

    [Fact]
    public async Task GetSkillAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetSkillAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetOrganizationsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetOrganizationsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<OrganizationDto>
        {
            new("merchants-guild", "Merchants Guild", "guilds", "guild", 80),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetOrganizationsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("merchants-guild");
    }

    [Fact]
    public async Task GetOrganizationsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetOrganizationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationAsync_Should_Return_Dto_On_Success()
    {
        var expected = new OrganizationDto("merchants-guild", "Merchants Guild", "guilds", "guild", 80);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetOrganizationAsync("merchants-guild");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Merchants Guild");
    }

    [Fact]
    public async Task GetOrganizationAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetOrganizationAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetWorldLocationsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetWorldLocationsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<WorldLocationDto>
        {
            new("darkwood-forest", "Darkwood Forest", "environments", "environment", 60, 5, 15),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetWorldLocationsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("darkwood-forest");
    }

    [Fact]
    public async Task GetWorldLocationsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetWorldLocationsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorldLocationAsync_Should_Return_Dto_On_Success()
    {
        var expected = new WorldLocationDto("darkwood-forest", "Darkwood Forest", "environments", "environment", 60, 5, 15);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetWorldLocationAsync("darkwood-forest");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Darkwood Forest");
    }

    [Fact]
    public async Task GetWorldLocationAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetWorldLocationAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetDialoguesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetDialoguesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<DialogueDto>
        {
            new("merchant-greeting", "Merchant Greeting", "greetings", "merchant", 50, ["Welcome, traveller!"]),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetDialoguesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("merchant-greeting");
    }

    [Fact]
    public async Task GetDialoguesAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetDialoguesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDialogueAsync_Should_Return_Dto_On_Success()
    {
        var expected = new DialogueDto("merchant-greeting", "Merchant Greeting", "greetings", "merchant", 50, ["Welcome, traveller!"]);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetDialogueAsync("merchant-greeting");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Merchant Greeting");
    }

    [Fact]
    public async Task GetDialogueAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetDialogueAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetActorInstancesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetActorInstancesAsync_Should_Return_List_On_Success()
    {
        var archetypeId = Guid.NewGuid();
        var expected = new List<ActorInstanceDto>
        {
            new("bandit-king", "Bandit King", "boss", archetypeId, 10, "bandits", 30),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetActorInstancesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("bandit-king");
    }

    [Fact]
    public async Task GetActorInstancesAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetActorInstancesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActorInstanceAsync_Should_Return_Dto_On_Success()
    {
        var archetypeId = Guid.NewGuid();
        var expected = new ActorInstanceDto("bandit-king", "Bandit King", "boss", archetypeId, 10, "bandits", 30);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetActorInstanceAsync("bandit-king");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Bandit King");
    }

    [Fact]
    public async Task GetActorInstanceAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetActorInstanceAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetMaterialPropertiesAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetMaterialPropertiesAsync_Should_Return_List_On_Success()
    {
        var expected = new List<MaterialPropertyDto>
        {
            new("iron-properties", "Iron Properties", "metals", "metal", 1.5f, 70),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetMaterialPropertiesAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("iron-properties");
    }

    [Fact]
    public async Task GetMaterialPropertiesAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetMaterialPropertiesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMaterialPropertyAsync_Should_Return_Dto_On_Success()
    {
        var expected = new MaterialPropertyDto("iron-properties", "Iron Properties", "metals", "metal", 1.5f, 70);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetMaterialPropertyAsync("iron-properties");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Iron Properties");
    }

    [Fact]
    public async Task GetMaterialPropertyAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetMaterialPropertyAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetTraitDefinitionsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetTraitDefinitionsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<TraitDefinitionDto>
        {
            new("aggressive", "bool", "Makes the entity attack on sight", "enemy"),
        };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetTraitDefinitionsAsync();

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("aggressive");
    }

    [Fact]
    public async Task GetTraitDefinitionsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.InternalServerError));

        var result = await sut.GetTraitDefinitionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTraitDefinitionAsync_Should_Return_Dto_On_Success()
    {
        var expected = new TraitDefinitionDto("aggressive", "bool", "Makes the entity attack on sight", "enemy");
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetTraitDefinitionAsync("aggressive");

        result.Should().NotBeNull();
        result!.ValueType.Should().Be("bool");
    }

    [Fact]
    public async Task GetTraitDefinitionAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetTraitDefinitionAsync("nonexistent-key");

        result.Should().BeNull();
    }

    // ── GetItemsAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<ItemDto> { new("iron-ore", "Iron Ore", "ores", 10) };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetItemsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("iron-ore");
    }

    [Fact]
    public async Task GetItemsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Internal Server Error", HttpStatusCode.InternalServerError));

        var result = await sut.GetItemsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetItemAsync_Should_Return_Dto_On_Success()
    {
        var expected = new ItemDto("iron-ore", "Iron Ore", "ores", 10);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetItemAsync("iron-ore");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("iron-ore");
    }

    [Fact]
    public async Task GetItemAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetItemAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetEnchantmentsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetEnchantmentsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<EnchantmentDto> { new("flaming", "Flaming", "elemental", 8) };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetEnchantmentsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("flaming");
    }

    [Fact]
    public async Task GetEnchantmentsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Internal Server Error", HttpStatusCode.InternalServerError));

        var result = await sut.GetEnchantmentsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEnchantmentAsync_Should_Return_Dto_On_Success()
    {
        var expected = new EnchantmentDto("flaming", "Flaming", "elemental", 8);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetEnchantmentAsync("flaming");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("flaming");
    }

    [Fact]
    public async Task GetEnchantmentAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetEnchantmentAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── GetMaterialsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMaterialsAsync_Should_Return_List_On_Success()
    {
        var expected = new List<MaterialDto> { new("iron", "Iron", "metals", 10) };
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetMaterialsAsync();

        result.Should().HaveCount(1);
        result[0].Slug.Should().Be("iron");
    }

    [Fact]
    public async Task GetMaterialsAsync_Should_Return_Empty_On_Error()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Internal Server Error", HttpStatusCode.InternalServerError));

        var result = await sut.GetMaterialsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMaterialAsync_Should_Return_Dto_On_Success()
    {
        var expected = new MaterialDto("iron", "Iron", "metals", 10);
        var sut = MakeSut(FakeHttpHandler.Json(expected));

        var result = await sut.GetMaterialAsync("iron");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("iron");
    }

    [Fact]
    public async Task GetMaterialAsync_Should_Return_Null_On_Not_Found()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Not Found", HttpStatusCode.NotFound));

        var result = await sut.GetMaterialAsync("nonexistent");

        result.Should().BeNull();
    }
}
