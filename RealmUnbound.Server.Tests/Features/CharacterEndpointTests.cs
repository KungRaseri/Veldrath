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
        var auth = await login.Content.ReadFromJsonAsync<AuthResult>();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task ListCharacters_Should_Return_Empty_For_New_Account()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_List_User"));

        var response = await _client.GetAsync("/api/characters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var characters = await response.Content.ReadFromJsonAsync<CharacterResult[]>();
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
            new { Name = "Aragorn_Create", ClassName = "@classes/warriors:fighter" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var character = await response.Content.ReadFromJsonAsync<CharacterResult>();
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
            new { Name = "Char_Slot1", ClassName = "@classes/warriors:fighter" });
        var r2 = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "Char_Slot2", ClassName = "@classes/mages:wizard" });

        var c2 = await r2.Content.ReadFromJsonAsync<CharacterResult>();
        c2!.SlotIndex.Should().Be(2);
    }

    [Fact]
    public async Task CreateCharacter_Should_Reject_Duplicate_Name()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_DupeA_User"));
        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "GlobalDupeName", ClassName = "@classes/warriors:fighter" });

        // Different account tries to claim the same name.
        using var client2 = factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_DupeB_User"));
        var response = await client2.PostAsJsonAsync("/api/characters",
            new { Name = "GlobalDupeName", ClassName = "@classes/warriors:fighter" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCharacter_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/api/characters",
            new { Name = "ShouldFail", ClassName = "@classes/warriors:fighter" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteCharacter_Should_Return_204_And_Remove_From_List()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Delete_User"));

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "ToBeDeleted", ClassName = "@classes/warriors:fighter" });
        var created = await create.Content.ReadFromJsonAsync<CharacterResult>();

        var del = await _client.DeleteAsync($"/api/characters/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await _client.GetAsync("/api/characters");
        var characters = await list.Content.ReadFromJsonAsync<CharacterResult[]>();
        characters!.Should().NotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task DeleteCharacter_Should_Return_404_For_Already_Deleted()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Del404_User"));

        var create = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "DeleteTwice", ClassName = "@classes/warriors:fighter" });
        var created = await create.Content.ReadFromJsonAsync<CharacterResult>();

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
            new { Name = "ProtectedChar", ClassName = "@classes/warriors:fighter" });
        var created = await create.Content.ReadFromJsonAsync<CharacterResult>();

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
                new { Name = $"SlotChar_{i}", ClassName = "@classes/warriors:fighter" });
            r.StatusCode.Should().Be(HttpStatusCode.Created, $"slot {i} creation should succeed");
        }

        // Sixth character must be rejected
        var overflow = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotChar_6", ClassName = "@classes/warriors:fighter" });

        overflow.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCharacter_Should_Return_Default_Level_1()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Level_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "LevelOneChar", ClassName = "@classes/warriors:fighter" });

        var character = await response.Content.ReadFromJsonAsync<CharacterResult>();
        character!.Level.Should().Be(1);
        character.Experience.Should().Be(0);
    }

    [Fact]
    public async Task CreateCharacter_Should_Set_Default_Starting_Zone()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_Zone_User"));

        var response = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "StarterZoneChar", ClassName = "@classes/mages:wizard" });

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("currentZoneId").GetString().Should().Be("starting-zone");
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
            new { Name = "SoftDeletedChar", ClassName = "@classes/warriors:fighter" });
        var created = await create.Content.ReadFromJsonAsync<CharacterResult>();

        await _client.DeleteAsync($"/api/characters/{created!.Id}");

        var list = await _client.GetFromJsonAsync<CharacterResult[]>("/api/characters");
        list.Should().NotContain(c => c.Name == "SoftDeletedChar");
    }

    [Fact]
    public async Task CreateCharacter_Should_Refill_Lowest_Available_Slot()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_SlotGap_User"));

        // Create two characters → slots 1 and 2
        var r1 = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotGap_Char1", ClassName = "@classes/warriors:fighter" });
        var c1 = await r1.Content.ReadFromJsonAsync<CharacterResult>();
        await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotGap_Char2", ClassName = "@classes/warriors:fighter" });

        // Delete the first character (slot 1)
        await _client.DeleteAsync($"/api/characters/{c1!.Id}");

        // Create a new character → should reclaim slot 1
        var r3 = await _client.PostAsJsonAsync("/api/characters",
            new { Name = "SlotGap_Char3", ClassName = "@classes/rogues:rogue" });
        var c3 = await r3.Content.ReadFromJsonAsync<CharacterResult>();

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
            new { Name = "IsolA_Char", ClassName = "@classes/warriors:fighter" });

        // Account B should not see Account A's character
        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("Char_IsolB_User"));

        var list = await clientB.GetFromJsonAsync<CharacterResult[]>("/api/characters");
        list.Should().NotContain(c => c.Name == "IsolA_Char");
    }
}
