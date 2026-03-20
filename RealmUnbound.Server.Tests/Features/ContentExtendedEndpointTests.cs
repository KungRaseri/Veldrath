using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>
/// Seeds one entity of every extended typed content catalog route into a fresh
/// in-memory database once, then provides a shared <see cref="HttpClient"/> for
/// all tests in this fixture.
/// </summary>
/// <remarks>
/// <para>
/// Enemies and NPCs both use the <see cref="ActorArchetype"/> table — the same seeded
/// archetype therefore appears in both <c>/api/content/enemies</c> and
/// <c>/api/content/npcs</c> responses.
/// </para>
/// <para>
/// <c>SpellDto.School</c> is the mapped <see cref="MagicalTradition"/> string, not
/// the raw entity school. <c>School="fire"</c> maps to <c>"Primal"</c>.
/// </para>
/// <para>
/// <c>QuestDto.QuestType</c> is always an empty string because
/// <see cref="EfCoreQuestRepository"/> does not map that field; only
/// <c>QuestDto.Title</c>, <c>DisplayName</c>, and <c>RarityWeight</c> are reliable.
/// </para>
/// </remarks>
public sealed class ContentExtendedEndpointsFixture : IAsyncLifetime
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

        // ActorArchetype — appears in both /enemies AND /npcs (no hostile filter).
        db.ActorArchetypes.Add(new ActorArchetype
        {
            Slug        = "typed-bandit",
            TypeKey     = "humanoids/bandits",
            DisplayName = "Bandit",
            IsActive    = true,
            MinLevel    = 5,
            MaxLevel    = 10,
            Stats       = new ArchetypeStats
            {
                Health        = 80,
                Strength      = 12,
                Agility       = 10,
                Intelligence  = 5,
                Constitution  = 10,
                Damage        = 8,
                ExperienceReward = 50,
                GoldRewardMin = 5,
                GoldRewardMax = 15,
            },
        });

        db.Quests.Add(new Quest
        {
            Slug         = "typed-clear-the-camp",
            TypeKey      = "combat",
            DisplayName  = "Clear the Camp",
            IsActive     = true,
            RarityWeight = 60,
            MinLevel     = 5,
        });

        // Recipe with a relational ingredient — EF resolves RecipeId FK on save.
        db.Recipes.Add(new Recipe
        {
            Slug             = "typed-iron-sword",
            TypeKey          = "weapons",
            DisplayName      = "Iron Sword Recipe",
            IsActive         = true,
            OutputItemDomain = "weapons",
            OutputItemSlug   = "iron-sword",
            OutputQuantity   = 1,
            CraftingSkill    = "blacksmithing",
            CraftingLevel    = 5,
            Ingredients      =
            [
                new RecipeIngredient { ItemDomain = "materials", ItemSlug = "iron", Quantity = 2 },
            ],
        });

        // LootTable with a relational entry row.
        db.LootTables.Add(new LootTable
        {
            Slug        = "typed-wolf-drops",
            TypeKey     = "enemies",
            DisplayName = "Wolf Drops",
            IsActive    = true,
            RarityWeight = 60,
            Traits      = new LootTableTraits { Common = true },
            Entries     =
            [
                new LootTableEntry
                {
                    ItemDomain   = "materials",
                    ItemSlug     = "beast-bone",
                    DropWeight   = 80,
                    QuantityMin  = 1,
                    QuantityMax  = 3,
                },
            ],
        });

        // Spell — School "fire" maps to MagicalTradition.Primal → SpellDto.School = "Primal".
        db.Spells.Add(new Spell
        {
            Slug        = "typed-fireball",
            TypeKey     = "fire",
            DisplayName = "Fireball",
            IsActive    = true,
            School      = "fire",
            RarityWeight = 50,
            Stats       = new SpellStats { ManaCost = 25, CastTime = 1.5f, Range = 20 },
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
/// Integration tests for the extended typed catalog GET endpoints under
/// <c>/api/content</c>: enemies, NPCs, quests, recipes, loot-tables, and spells.
/// All routes are anonymous. Each section includes a list test, a by-slug test,
/// and a 404 test.
/// </summary>
[Trait("Category", "Integration")]
public class ContentExtendedEndpointTests(ContentExtendedEndpointsFixture fixture)
    : IClassFixture<ContentExtendedEndpointsFixture>
{
    private readonly HttpClient _client = fixture.Client;

    // ── GET /api/content/enemies ──────────────────────────────────────────────

    [Fact]
    public async Task GetEnemies_Returns_OK_And_ContainsSeededArchetype()
    {
        var response = await _client.GetAsync("/api/content/enemies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<EnemyDto>>();
        items.Should().Contain(e => e.Slug == "typed-bandit");
    }

    [Fact]
    public async Task GetEnemies_Does_Not_Require_Auth()
    {
        using var anon = fixture.Factory.CreateClient();
        var response = await anon.GetAsync("/api/content/enemies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEnemyBySlug_Returns_Correct_Enemy()
    {
        var response = await _client.GetAsync("/api/content/enemies/typed-bandit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<EnemyDto>();
        item!.Slug.Should().Be("typed-bandit");
        item.Name.Should().Be("Bandit");
        item.Health.Should().Be(80);
        item.Level.Should().Be(5);           // MinLevel
        item.Family.Should().Be("Bandit");   // DisplayName used as Family
    }

    [Fact]
    public async Task GetEnemyBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/enemies/no-such-enemy");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/npcs ─────────────────────────────────────────────────
    // Note: same ActorArchetype table — "typed-bandit" appears in both /enemies and /npcs.

    [Fact]
    public async Task GetNpcs_Returns_OK_And_ContainsSeededArchetype()
    {
        var response = await _client.GetAsync("/api/content/npcs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<NpcDto>>();
        items.Should().Contain(n => n.Slug == "typed-bandit");
    }

    [Fact]
    public async Task GetNpcBySlug_Returns_Correct_Npc()
    {
        var response = await _client.GetAsync("/api/content/npcs/typed-bandit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<NpcDto>();
        item!.Slug.Should().Be("typed-bandit");
        item.Name.Should().Be("Bandit");
        item.DisplayName.Should().Be("Bandit");
        item.Category.Should().Be("humanoids/bandits");   // TypeKey
    }

    [Fact]
    public async Task GetNpcBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/npcs/no-such-npc");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/quests ───────────────────────────────────────────────

    [Fact]
    public async Task GetQuests_Returns_OK_And_ContainsSeededQuest()
    {
        var response = await _client.GetAsync("/api/content/quests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<QuestDto>>();
        items.Should().Contain(q => q.Slug == "typed-clear-the-camp");
    }

    [Fact]
    public async Task GetQuestBySlug_Returns_Correct_Quest()
    {
        var response = await _client.GetAsync("/api/content/quests/typed-clear-the-camp");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<QuestDto>();
        item!.Slug.Should().Be("typed-clear-the-camp");
        item.Title.Should().Be("Clear the Camp");       // DisplayName → Title
        item.DisplayName.Should().Be("Clear the Camp");
        item.RarityWeight.Should().Be(60);
        // QuestType and Difficulty are not mapped by EfCoreQuestRepository — always empty.
    }

    [Fact]
    public async Task GetQuestBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/quests/no-such-quest");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/recipes ──────────────────────────────────────────────

    [Fact]
    public async Task GetRecipes_Returns_OK_And_ContainsSeededRecipe()
    {
        var response = await _client.GetAsync("/api/content/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<RecipeDto>>();
        items.Should().Contain(r => r.Slug == "typed-iron-sword");
    }

    [Fact]
    public async Task GetRecipeBySlug_Returns_Correct_Recipe()
    {
        var response = await _client.GetAsync("/api/content/recipes/typed-iron-sword");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<RecipeDto>();
        item!.Slug.Should().Be("typed-iron-sword");
        item.Name.Should().Be("Iron Sword Recipe");
        item.Category.Should().Be("weapons");               // TypeKey
        item.OutputItemReference.Should().Be("@weapons:iron-sword");
        item.OutputQuantity.Should().Be(1);
        item.Materials.Should().HaveCount(1);
        item.Materials[0].ItemReference.Should().Be("@materials:iron");
        item.Materials[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetRecipeBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/recipes/no-such-recipe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/loot-tables ─────────────────────────────────────────

    [Fact]
    public async Task GetLootTables_Returns_OK_And_ContainsSeededTable()
    {
        var response = await _client.GetAsync("/api/content/loot-tables");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<LootTableDto>>();
        items.Should().Contain(t => t.Slug == "typed-wolf-drops");
    }

    [Fact]
    public async Task GetLootTableBySlug_Returns_Correct_LootTable()
    {
        var response = await _client.GetAsync("/api/content/loot-tables/typed-wolf-drops");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<LootTableDto>();
        item!.Slug.Should().Be("typed-wolf-drops");
        item.Name.Should().Be("Wolf Drops");
        item.Context.Should().Be("enemies");    // TypeKey
        item.IsBoss.Should().BeFalse();
        item.IsChest.Should().BeFalse();
        item.IsHarvesting.Should().BeFalse();
        item.Entries.Should().HaveCount(1);
        item.Entries[0].ItemSlug.Should().Be("beast-bone");
        item.Entries[0].DropWeight.Should().Be(80);
        item.Entries[0].QuantityMin.Should().Be(1);
        item.Entries[0].QuantityMax.Should().Be(3);
    }

    [Fact]
    public async Task GetLootTableBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/loot-tables/no-such-table");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/content/spells ───────────────────────────────────────────────

    [Fact]
    public async Task GetSpells_Returns_OK_And_ContainsSeededSpell()
    {
        var response = await _client.GetAsync("/api/content/spells");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<SpellDto>>();
        items.Should().Contain(s => s.SpellId == "typed-fireball");
    }

    [Fact]
    public async Task GetSpellBySlug_Returns_Correct_Spell()
    {
        var response = await _client.GetAsync("/api/content/spells/typed-fireball");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<SpellDto>();
        item!.SpellId.Should().Be("typed-fireball");
        item.DisplayName.Should().Be("Fireball");
        // School "fire" → ParseTradition → MagicalTradition.Primal → "Primal"
        item.School.Should().Be("Primal");
        item.ManaCost.Should().Be(25);
        item.Rank.Should().Be(50);   // RarityWeight
    }

    [Fact]
    public async Task GetSpellBySlug_Returns_404_For_Unknown_Slug()
    {
        var response = await _client.GetAsync("/api/content/spells/no-such-spell");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
