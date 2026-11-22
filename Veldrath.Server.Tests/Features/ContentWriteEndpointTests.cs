using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;
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

    /// <summary>
    /// Reads a language back through a fresh context, so the assertion sees the stored row rather than the response
    /// the write path built from the payload it was handed.
    /// </summary>
    private async Task<Language> StoredLanguageAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        return await db.Languages.AsNoTracking().SingleAsync(language => language.Slug == slug);
    }

    /// <summary>Reads an item back through a fresh context, so the assertion sees the stored row.</summary>
    private async Task<Item> StoredItemAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        return await db.Items.AsNoTracking().SingleAsync(item => item.Slug == slug);
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
        var response = await client.PutAsync($"/api/content/item/{slug}?expectedUpdatedAt={stale}", Json(new
        {
            slug,
            typeKey = "trinkets",
            displayName = "Test Trinket",
            itemType = "consumable",
            rarityWeight = 50,
            isActive = true,
            stats = new { value = 999 },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // The refused write must not have landed, owned JSON included.
        (await StoredItemAsync(slug)).Stats.Value.Should().BeNull();
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
    public async Task An_owned_json_edit_on_a_language_is_persisted()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-owned-language-{Guid.NewGuid():N}";

        static object LanguagePayload(string slug, string formalExample) => new
        {
            slug,
            typeKey = "imperial",
            displayName = "Owned JSON Language",
            rarityWeight = 60,
            isActive = true,
            phonology = new
            {
                junctionRules = new[]
                {
                    new { cluster = "–l + vr–", rule = "The l drops before vr", formalExample },
                },
            },
        };

        var created = await client.PostAsync(
            "/api/content/language",
            Json(LanguagePayload(slug, "vaeld + raeth → vaelraeth")));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var updated = await client.PutAsync(
            $"/api/content/language/{slug}",
            Json(LanguagePayload(slug, "vaeld + raeth → Vaelraeth")));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        // The response is built from the submitted payload, so only the stored row can prove the edit landed.
        var stored = await StoredLanguageAsync(slug);
        stored.Phonology.JunctionRules.Should().ContainSingle();
        stored.Phonology.JunctionRules[0].FormalExample.Should().Be("vaeld + raeth → Vaelraeth");
        stored.Version.Should().Be(2);
    }

    [Fact]
    public async Task An_owned_json_edit_on_an_item_is_persisted()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-owned-item-{Guid.NewGuid():N}";

        static object WeaponPayload(string slug, int damageMin) => new
        {
            slug,
            typeKey = "heavy-blades",
            displayName = "Owned JSON Sword",
            itemType = "weapon",
            weaponType = "sword",
            damageType = "physical",
            handsRequired = 1,
            rarityWeight = 80,
            isActive = true,
            stats = new
            {
                weight = 3.5f,
                value = 25,
                damageMin,
                damageMax = 8,
                attackSpeed = 1.0f,
                durability = 100,
            },
            traits = new { versatile = true },
        };

        var created = await client.PostAsync("/api/content/item", Json(WeaponPayload(slug, damageMin: 4)));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var updated = await client.PutAsync($"/api/content/item/{slug}", Json(WeaponPayload(slug, damageMin: 7)));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await StoredItemAsync(slug);
        stored.Stats.DamageMin.Should().Be(7);
        stored.Stats.DamageMax.Should().Be(8);
        stored.Version.Should().Be(2);
    }

    [Fact]
    public async Task Removing_an_entry_from_an_owned_json_collection_is_persisted()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-owned-collection-{Guid.NewGuid():N}";

        static object LanguagePayload(string slug, params string[] rules) => new
        {
            slug,
            typeKey = "imperial",
            displayName = "Owned JSON Collection",
            rarityWeight = 60,
            isActive = true,
            phonology = new
            {
                junctionRules = rules.Select(rule => new { cluster = "–l + vr–", rule, formalExample = rule }).ToArray(),
            },
        };

        var created = await client.PostAsync(
            "/api/content/language",
            Json(LanguagePayload(slug, "First rule", "Second rule")));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var updated = await client.PutAsync(
            $"/api/content/language/{slug}",
            Json(LanguagePayload(slug, "First rule")));
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await StoredLanguageAsync(slug);
        stored.Phonology.JunctionRules.Select(rule => rule.Rule).Should().Equal("First rule");
    }

    /// <summary>Reads a recipe and its ingredients back through a fresh context, so the assertion sees the stored rows.</summary>
    private async Task<Recipe> StoredRecipeAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        return await db.Recipes.AsNoTracking()
            .Include(recipe => recipe.Ingredients)
            .SingleAsync(recipe => recipe.Slug == slug);
    }

    /// <summary>Reads a loot table and its entries back through a fresh context.</summary>
    private async Task<LootTable> StoredLootTableAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        return await db.LootTables.AsNoTracking()
            .Include(table => table.Entries)
            .SingleAsync(table => table.Slug == slug);
    }

    /// <summary>
    /// A recipe whose ingredients are child rows, not owned JSON. The seeder's own materials are used so the references
    /// resolve; the slug is unique per test because the integration database is shared.
    /// </summary>
    private static object RecipePayload(string slug, params (string Slug, int Quantity)[] ingredients) => new
    {
        slug,
        typeKey = "blacksmithing",
        displayName = "Child Row Recipe",
        rarityWeight = 70,
        isActive = true,
        outputItemDomain = "items/general",
        outputItemSlug = "iron-sword",
        outputQuantity = 1,
        craftingSkill = "blacksmithing",
        craftingLevel = 1,
        traits = new { requiresStation = true },
        ingredients = ingredients
            .Select(ingredient => new
            {
                itemDomain = "items/materials",
                itemSlug = ingredient.Slug,
                quantity = ingredient.Quantity,
            })
            .ToArray(),
    };

    /// <summary>The same recipe without an <c>ingredients</c> property at all — what a schema-shaped form sends.</summary>
    private static object RecipePayloadWithoutIngredients(string slug) => new
    {
        slug,
        typeKey = "blacksmithing",
        displayName = "Child Row Recipe",
        rarityWeight = 70,
        isActive = true,
        outputItemDomain = "items/general",
        outputItemSlug = "iron-sword",
        outputQuantity = 1,
        craftingSkill = "blacksmithing",
        craftingLevel = 1,
        traits = new { requiresStation = true },
    };

    private static object LootTablePayload(string slug, params (string Slug, int Weight)[] entries) => new
    {
        slug,
        typeKey = "enemies",
        displayName = "Child Row Loot",
        rarityWeight = 60,
        isActive = true,
        traits = new { common = true },
        entries = entries
            .Select(entry => new
            {
                itemDomain = "items/general",
                itemSlug = entry.Slug,
                dropWeight = entry.Weight,
                quantityMin = 1,
                quantityMax = 1,
                isGuaranteed = false,
            })
            .ToArray(),
    };

    [Fact]
    public async Task An_ingredient_quantity_edit_through_put_is_persisted()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-{Guid.NewGuid():N}";

        var created = await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1))));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var updated = await client.PutAsync(
            $"/api/content/recipe/{slug}",
            Json(RecipePayload(slug, ("iron", 9), ("pine", 1))));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        // Ingredients are rows in their own table; only the stored rows can prove the edit landed.
        var stored = await StoredRecipeAsync(slug);
        stored.Version.Should().Be(2);
        stored.Ingredients.Should().HaveCount(2);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "iron").Quantity.Should().Be(9);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "pine").Quantity.Should().Be(1);
    }

    [Fact]
    public async Task Adding_and_removing_ingredients_through_put_stores_exactly_the_authored_set()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-set-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // iron stays, pine is dropped and oak is added — one reconcile has to do all three.
        var updated = await client.PutAsync(
            $"/api/content/recipe/{slug}",
            Json(RecipePayload(slug, ("iron", 3), ("oak", 2))));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredRecipeAsync(slug);
        stored.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "oak");
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "oak").Quantity.Should().Be(2);
    }

    [Fact]
    public async Task A_put_that_omits_ingredients_leaves_the_stored_children_alone()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-omitted-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // A payload that never names `ingredients` is not a request to delete them; the editor always sends the full
        // entity, so an omitted collection means the caller never saw it.
        var updated = await client.PutAsync(
            $"/api/content/recipe/{slug}",
            Json(RecipePayloadWithoutIngredients(slug)));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredRecipeAsync(slug);
        stored.Ingredients.Should().HaveCount(2);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "iron").Quantity.Should().Be(3);
    }

    [Fact]
    public async Task A_loot_entry_edit_through_put_is_persisted()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-loot-{Guid.NewGuid():N}";

        var created = await client.PostAsync(
            "/api/content/loottable",
            Json(LootTablePayload(slug, ("health-potion", 60), ("iron-ingot", 40))));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var updated = await client.PutAsync(
            $"/api/content/loottable/{slug}",
            Json(LootTablePayload(slug, ("health-potion", 15), ("iron-ingot", 40))));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredLootTableAsync(slug);
        stored.Version.Should().Be(2);
        stored.Entries.Should().HaveCount(2);
        stored.Entries.Single(entry => entry.ItemSlug == "health-potion").DropWeight.Should().Be(15);
        stored.Entries.Single(entry => entry.ItemSlug == "iron-ingot").DropWeight.Should().Be(40);
    }

    [Fact]
    public async Task Adding_and_removing_loot_entries_through_put_stores_exactly_the_authored_set()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-loot-set-{Guid.NewGuid():N}";

        (await client.PostAsync(
            "/api/content/loottable",
            Json(LootTablePayload(slug, ("health-potion", 60), ("iron-ingot", 40)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var updated = await client.PutAsync(
            $"/api/content/loottable/{slug}",
            Json(LootTablePayload(slug, ("iron-ingot", 40), ("iron-sword", 20))));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredLootTableAsync(slug);
        stored.Entries.Select(entry => entry.ItemSlug).Should().BeEquivalentTo("iron-ingot", "iron-sword");
    }

    /// <summary>The error entries of a validation response body.</summary>
    private static JsonElement Errors(JsonElement body) => body.GetProperty("errors");

    /// <summary>
    /// An explicit JSON <c>null</c> is neither "the property was omitted" (leave the stored rows alone) nor "the author
    /// wrote an empty list" (clear them). It used to reach the reconcile as an empty authored set; the reconcile would
    /// have deleted every stored ingredient on a 200.
    /// </summary>
    [Fact]
    public async Task A_put_naming_ingredients_as_null_is_refused_and_keeps_the_stored_rows()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-null-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "recipe", slug);
        payload["ingredients"] = null;

        var refused = await client.PutAsync($"/api/content/recipe/{slug}", JsonObjectContent(payload));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Errors(body).EnumerateArray().Should().Contain(issue =>
            issue.GetProperty("rule").GetString() == ContentRules.ChildCollectionNull
            && issue.GetProperty("field").GetString() == "ingredients");

        // Refused means nothing was written: same version, same rows.
        var stored = await StoredRecipeAsync(slug);
        stored.Version.Should().Be(1);
        stored.Ingredients.Select(ingredient => ingredient.ItemSlug).Should().BeEquivalentTo("iron", "pine");
    }

    /// <summary>The loot-table half: a null <c>entries</c> names the field instead of reaching the reconcile as empty.</summary>
    [Fact]
    public async Task A_put_naming_entries_as_null_is_refused_and_keeps_the_stored_rows()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-loot-null-{Guid.NewGuid():N}";

        (await client.PostAsync(
            "/api/content/loottable",
            Json(LootTablePayload(slug, ("health-potion", 60), ("iron-ingot", 40)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "loottable", slug);
        payload["entries"] = null;

        var refused = await client.PutAsync($"/api/content/loottable/{slug}", JsonObjectContent(payload));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Errors(body).EnumerateArray().Should().Contain(issue =>
            issue.GetProperty("rule").GetString() == ContentRules.ChildCollectionNull
            && issue.GetProperty("field").GetString() == "entries");

        var stored = await StoredLootTableAsync(slug);
        stored.Version.Should().Be(1);
        stored.Entries.Select(entry => entry.ItemSlug).Should().BeEquivalentTo("health-potion", "iron-ingot");
    }

    /// <summary>
    /// A family whose validator never reads its child collections (species only runs the shared structural rules) is the
    /// dangerous case: before the payload-shape rule, a null <c>powerPool</c> validated clean and the reconcile cleared
    /// the pool with a 200 — a silent delete, not the 500 the recipe path produced.
    /// </summary>
    [Fact]
    public async Task A_put_naming_a_species_power_pool_as_null_is_refused_and_keeps_it()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var payload = await DetailPayloadAsync(client, "species", "wolf");
        var storedPool = payload["powerPool"]!.AsArray().Count;
        storedPool.Should().BeGreaterThan(0, "the seeded wolf has an innate power pool to lose");

        payload["powerPool"] = null;

        var refused = await client.PutAsync("/api/content/species/wolf", JsonObjectContent(payload));
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, await refused.Content.ReadAsStringAsync());

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Errors(body).EnumerateArray().Should().Contain(issue =>
            issue.GetProperty("rule").GetString() == ContentRules.ChildCollectionNull
            && issue.GetProperty("field").GetString() == "powerPool");

        var stored = await DetailPayloadAsync(client, "species", "wolf");
        stored["powerPool"]!.AsArray().Count.Should().Be(storedPool);
    }

    /// <summary>
    /// The dry run takes the same payload, so it has to report the issue rather than answer 500 with a null dereference
    /// from inside a validator.
    /// </summary>
    [Fact]
    public async Task Validating_a_payload_with_a_null_collection_reports_the_issue_instead_of_failing()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-validate-null-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "recipe", slug);
        payload["ingredients"] = null;

        var validated = await client.PostAsync($"/api/content/recipe/{slug}/validate", JsonObjectContent(payload));
        validated.StatusCode.Should().Be(HttpStatusCode.OK, await validated.Content.ReadAsStringAsync());

        var body = await validated.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isValid").GetBoolean().Should().BeFalse();
        Errors(body).EnumerateArray().Should().Contain(issue =>
            issue.GetProperty("rule").GetString() == ContentRules.ChildCollectionNull);
    }

    /// <summary>Reads the exact payload the Foundry detail endpoint hands the editor.</summary>
    private async Task<JsonObject> DetailPayloadAsync(HttpClient client, string type, string slug)
    {
        var response = await client.GetAsync($"/api/content/browse/{type}/{slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("payload").Deserialize<JsonObject>()!;
    }

    /// <summary>Sends a JSON object as an <c>application/json</c> body.</summary>
    private static StringContent JsonObjectContent(JsonObject payload) =>
        new(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json");

    /// <summary>
    /// The detail payload is what the editor starts from, so it has to carry the stored child rows. It did not: the read
    /// used <c>AsNoTracking</c> without an include, every child collection serialised as <c>[]</c>, and the next save
    /// named that empty list — which the presence rule reads as "replace wholesale".
    /// </summary>
    [Fact]
    public async Task The_recipe_detail_payload_carries_the_stored_ingredients()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-detail-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "recipe", slug);

        var ingredients = payload["ingredients"]!.AsArray();
        ingredients.Should().HaveCount(2);
        ingredients.Select(ingredient => ingredient!["itemSlug"]!.GetValue<string>())
            .Should().BeEquivalentTo("iron", "pine");
        ingredients.Single(ingredient => ingredient!["itemSlug"]!.GetValue<string>() == "iron")!["quantity"]!
            .GetValue<int>().Should().Be(3);
    }

    /// <summary>
    /// A find → change one scalar → save round trip through the detail payload, which is the sequence the Foundry editor
    /// performs. Renaming a stored collection in the payload is incidental; the stored rows have to survive.
    /// </summary>
    [Fact]
    public async Task A_recipe_saved_from_its_detail_payload_keeps_its_ingredients()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-recipe-roundtrip-{Guid.NewGuid():N}";

        (await client.PostAsync("/api/content/recipe", Json(RecipePayload(slug, ("iron", 3), ("pine", 1)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "recipe", slug);
        payload["craftingLevel"] = 4;

        var updated = await client.PutAsync($"/api/content/recipe/{slug}", JsonObjectContent(payload));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredRecipeAsync(slug);
        stored.Version.Should().Be(2);
        stored.CraftingLevel.Should().Be(4);
        stored.Ingredients.Should().HaveCount(2);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "iron").Quantity.Should().Be(3);
        stored.Ingredients.Single(ingredient => ingredient.ItemSlug == "pine").Quantity.Should().Be(1);
    }

    /// <summary>The loot-table half of the detail round trip: an unrelated scalar edit must not clear the entries.</summary>
    [Fact]
    public async Task A_loot_table_saved_from_its_detail_payload_keeps_its_entries()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-loot-roundtrip-{Guid.NewGuid():N}";

        (await client.PostAsync(
            "/api/content/loottable",
            Json(LootTablePayload(slug, ("health-potion", 60), ("iron-ingot", 40)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "loottable", slug);
        payload["rarityWeight"] = 65;

        var updated = await client.PutAsync($"/api/content/loottable/{slug}", JsonObjectContent(payload));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredLootTableAsync(slug);
        stored.Version.Should().Be(2);
        stored.RarityWeight.Should().Be(65);
        stored.Entries.Should().HaveCount(2);
        stored.Entries.Single(entry => entry.ItemSlug == "health-potion").DropWeight.Should().Be(60);
        stored.Entries.Single(entry => entry.ItemSlug == "iron-ingot").DropWeight.Should().Be(40);
    }

    /// <summary>
    /// A loot entry edited through the detail payload — the only shape the editor can send, because the row it renders
    /// carries the stored surrogate id. An entry has no authorable key, so the row is replaced rather than mutated;
    /// what matters is that exactly the authored entry survives with the authored weight.
    /// </summary>
    [Fact]
    public async Task A_loot_entry_edited_through_the_detail_payload_is_persisted()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);
        var slug = $"b2-child-loot-entry-{Guid.NewGuid():N}";

        (await client.PostAsync(
            "/api/content/loottable",
            Json(LootTablePayload(slug, ("health-potion", 60), ("iron-ingot", 40)))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await DetailPayloadAsync(client, "loottable", slug);
        var entries = payload["entries"]!.AsArray();
        entries.Single(entry => entry!["itemSlug"]!.GetValue<string>() == "health-potion")!["dropWeight"] = 15;

        var updated = await client.PutAsync($"/api/content/loottable/{slug}", JsonObjectContent(payload));
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());

        var stored = await StoredLootTableAsync(slug);
        stored.Entries.Should().HaveCount(2);
        stored.Entries.Single(entry => entry.ItemSlug == "health-potion").DropWeight.Should().Be(15);
        stored.Entries.Single(entry => entry.ItemSlug == "iron-ingot").DropWeight.Should().Be(40);
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

/// <summary>
/// The per-type schema endpoint and the unsaved-payload dry run — the two things the Foundry edit form calls.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ContentEditorEndpointTests(WebAppFactory factory)
{
    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

    private async Task<HttpClient> ClientAsAsync(params string[] roles)
    {
        var (token, _) = await factory.CreateAccountTokenAsync($"content-editor-{Guid.NewGuid():N}", roles);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task The_per_type_schema_is_public_and_lists_rendereable_fields()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/content/item/schema");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("contentType").GetString().Should().Be("item");
        body.GetProperty("groups").EnumerateArray()
            .SelectMany(group => group.GetProperty("fields").EnumerateArray())
            .Select(field => field.GetProperty("name").GetString())
            .Should().Contain(["slug", "displayName", "rarityWeight"]);
    }

    [Fact]
    public async Task An_unknown_type_has_no_schema()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/content/spaceship/schema");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validating_an_unsaved_payload_reports_issues_without_writing()
    {
        using var client = await ClientAsAsync(Roles.ContentEditor);

        var response = await client.PostAsync("/api/content/quest/the-lost-shipment/validate", Json(new
        {
            slug = "the-lost-shipment",
            typeKey = "side",
            displayName = "The Lost Shipment",
            minLevel = 1,
            objectives = new
            {
                items = new[] { new { type = "collect", target = "still-missing", quantity = 1 } },
            },
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isValid").GetBoolean().Should().BeFalse();
        body.GetProperty("errors").EnumerateArray()
            .Select(issue => issue.GetProperty("rule").GetString())
            .Should().Contain("missing-quest-target");
    }

    [Fact]
    public async Task An_unsaved_payload_dry_run_needs_the_write_permission()
    {
        using var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsync("/api/content/quest/the-lost-shipment/validate", Json(new { slug = "x" }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
