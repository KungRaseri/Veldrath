using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

[Trait("Category", "Integration")]
public class CharacterEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Helper — register + login and return a bearer token.
    private async Task<string> GetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "Pass1234!" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "Pass1234!" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task ListCharacters_Should_Return_Empty_For_New_Account()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_List_User"));

        var response = await _client.GetAsync("/api/characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var characters = await response.Content.ReadFromJsonAsync<CharacterDto[]>();
        characters.Should().BeEmpty();
    }

    [Fact]
    public async Task ListCharacters_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/characters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCharacter_Should_Return_201_With_Slot_1()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Create_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "Aragorn_Create", ClassName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var character = await response.Content.ReadFromJsonAsync<CharacterDto>();
        character!.Name.Should().Be("Aragorn_Create");
        character.SlotIndex.Should().Be(1);
        character.Level.Should().Be(1);
    }

    [Fact]
    public async Task CreateCharacter_Should_Assign_Sequential_Slots()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Slots_User"));

        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "Char_Slot1", ClassName = "Warrior" });
        var r2 = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "Char_Slot2", ClassName = "Mage" });

        var c2 = await r2.Content.ReadFromJsonAsync<CharacterDto>();
        c2!.SlotIndex.Should().Be(2);
    }

    [Fact]
    public async Task CreateCharacter_Should_Reject_Duplicate_Name()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_DupeA_User"));
        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "GlobalDupeName", ClassName = "Warrior" });

        // Different account tries to claim the same name.
        using var client2 = factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_DupeB_User"));
        var response = await client2.PostAsJsonAsync("/api/characters",
            new { Name = "GlobalDupeName", ClassName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCharacter_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/api/characters",
            new { Name = "ShouldFail", ClassName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteCharacter_Should_Return_204_And_Remove_From_List()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Delete_User"));

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "ToBeDeleted", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();

        var del = await _client.DeleteAsync($"/api/characters/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await _client.GetAsync("/api/characters");
        var characters = await list.Content.ReadFromJsonAsync<CharacterDto[]>();
        characters!.Should().NotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task DeleteCharacter_Should_Return_404_For_Already_Deleted()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Del404_User"));

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "DeleteTwice", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();

        await _client.DeleteAsync($"/api/characters/{created!.Id}");
        var second = await _client.DeleteAsync($"/api/characters/{created.Id}");
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCharacter_Should_Return_Forbidden_For_Another_Account()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Owner_User"));
        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "ProtectedChar", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();

        using var client2 = factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Thief_User"));
        var del = await client2.DeleteAsync($"/api/characters/{created!.Id}");

        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCharacter_Should_Return_404_For_Unknown_Id()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Del_Unk_User"));

        var del = await _client.DeleteAsync($"/api/characters/{Guid.NewGuid()}");
        del.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCharacter_Should_Return_400_When_Slot_Limit_Reached()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_SlotLimit_User"));

        // Fill all 5 slots
        for (int i = 1; i <= 5; i++)
        {
            var r = await _client.PostAsJsonAsync("/api/characters",
                new { Name = $"SlotChar_{i}", ClassName = "Warrior" });
            r.StatusCode.Should().Be(HttpStatusCode.Created, $"slot {i} creation should succeed");
        }

        // Sixth character must be rejected
        var overflow = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotChar_6", ClassName = "Warrior" });

        overflow.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCharacter_Should_Return_Default_Level_1()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Level_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "LevelOneChar", ClassName = "Warrior" });

        var character = await response.Content.ReadFromJsonAsync<CharacterDto>();
        character!.Level.Should().Be(1);
        character.Experience.Should().Be(0);
    }

    [Fact]
    public async Task CreateCharacter_Should_Set_Default_Starting_Zone()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Zone_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "StarterZoneChar", ClassName = "Mage" });

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("currentZoneId").GetString().Should().Be("fenwick-crossing");
    }

    [Fact]
    public async Task DeleteCharacter_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var del = await unauthClient.DeleteAsync($"/api/characters/{Guid.NewGuid()}");
        del.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedCharacter_Should_Not_Appear_In_List()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_SoftDel_User"));

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SoftDeletedChar", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();

        await _client.DeleteAsync($"/api/characters/{created!.Id}");

        var list = await _client.GetFromJsonAsync<CharacterDto[]>("/api/characters");
        list.Should().NotContain(c => c.Name == "SoftDeletedChar");
    }

    [Fact]
    public async Task CreateCharacter_Should_Refill_Lowest_Available_Slot()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_SlotGap_User"));

        // Create two characters → slots 1 and 2
        var r1 = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotGap_Char1", ClassName = "Warrior" });
        var c1 = await r1.Content.ReadFromJsonAsync<CharacterDto>();
        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotGap_Char2", ClassName = "Warrior" });

        // Delete the first character (slot 1)
        await _client.DeleteAsync($"/api/characters/{c1!.Id}");

        // Create a new character → should reclaim slot 1
        var r3 = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotGap_Char3", ClassName = "Warrior" });
        var c3 = await r3.Content.ReadFromJsonAsync<CharacterDto>();

        c3!.SlotIndex.Should().Be(1);
    }

    [Fact]
    public async Task ListCharacters_Should_Only_Return_Calling_Accounts_Characters()
    {
        // Account A creates a character
        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_IsolA_User"));
        await clientA.PostAsJsonAsync("/api/characters",
            new { Name = "IsolA_Char", ClassName = "Warrior" });

        // Account B should not see Account A's character
        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_IsolB_User"));

        var list = await clientB.GetFromJsonAsync<CharacterDto[]>("/api/characters");
        list.Should().NotContain(c => c.Name == "IsolA_Char");
    }

    // ── Lifecycle: create → delete → recreate ─────────────────────────────────

    [Fact]
    public async Task CreateCharacter_AfterDeletingSameNamedChar_Succeeds()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Reclaim_User"));

        // Create, then delete
        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "Recyclable", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();
        await _client.DeleteAsync($"/api/characters/{created!.Id}");

        // Recreate with the exact same name — must return 201, not 409
        var recreate = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "Recyclable", ClassName = "Warrior" });

        recreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var reborn = await recreate.Content.ReadFromJsonAsync<CharacterDto>();
        reborn!.Name.Should().Be("Recyclable");
    }

    [Fact]
    public async Task CreateCharacter_AfterDeletingSameNamedChar_AppearsInList()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_ReclaimList_User"));

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "ListReclaim", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();
        await _client.DeleteAsync($"/api/characters/{created!.Id}");

        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "ListReclaim", ClassName = "Warrior" });

        var list = await _client.GetFromJsonAsync<CharacterDto[]>("/api/characters");
        list.Should().ContainSingle(c => c.Name == "ListReclaim");
    }

    [Fact]
    public async Task CreateCharacter_AfterDeletedByDifferentAccount_ClaimsName()
    {
        // Account A creates and deletes "FreeAgent"
        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_FreeA_User"));
        var create = await clientA.PostAsJsonAsync("/api/characters",
            new { Name = "FreeAgent", ClassName = "Warrior" });
        var created = await create.Content.ReadFromJsonAsync<CharacterDto>();
        await clientA.DeleteAsync($"/api/characters/{created!.Id}");

        // Account B should now be able to claim "FreeAgent"
        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_FreeB_User"));
        var claim = await clientB.PostAsJsonAsync("/api/characters",
            new { Name = "FreeAgent", ClassName = "Warrior" });

        claim.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DeleteMultipleCharacters_ThenRecreate_AllNamesAvailable()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_MultiDel_User"));

        // Create three characters
        var names = new[] { "MultiDel_A", "MultiDel_B", "MultiDel_C" };
        var ids   = new List<Guid>();
        foreach (var n in names)
        {
            var r = await _client.PostAsJsonAsync("/api/characters",
                new { Name = n, ClassName = "Warrior" });
            var c = await r.Content.ReadFromJsonAsync<CharacterDto>();
            ids.Add(c!.Id);
        }

        // Delete all three
        foreach (var id in ids)
            await _client.DeleteAsync($"/api/characters/{id}");

        // Recreate each — none should conflict
        foreach (var n in names)
        {
            var r = await _client.PostAsJsonAsync("/api/characters",
                new { Name = n, ClassName = "Warrior" });
            r.StatusCode.Should().Be(HttpStatusCode.Created, $"recreating {n} should succeed");
        }
    }

    [Fact]
    public async Task SlotLimit_DoesNotCountDeletedCharacters()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_SlotDel_User"));

        // Fill all 5 slots
        var toDelete = new List<Guid>();
        for (int i = 1; i <= 5; i++)
        {
            var r = await _client.PostAsJsonAsync("/api/characters",
                new { Name = $"SlotDel_{i}", ClassName = "Warrior" });
            var c = await r.Content.ReadFromJsonAsync<CharacterDto>();
            toDelete.Add(c!.Id);
        }

        // Delete one to free a slot
        await _client.DeleteAsync($"/api/characters/{toDelete[0]}");

        // Sixth character should succeed now that a slot is free
        var sixth = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotDel_New", ClassName = "Warrior" });

        sixth.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateCharacter_WithEmptyName_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_EmptyName_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "", ClassName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCharacter_WithWhitespaceName_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_WsName_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "   ", ClassName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
