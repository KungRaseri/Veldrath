using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

[Trait("Category", "Integration")]
public class CharacterEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Helper — register + login and return an authenticated client.
    private async Task<string> GetTokenAsync(string username)
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = username, Password = "Pass1234!" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = username, Password = "Pass1234!" });
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
}
