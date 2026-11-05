using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Contracts.Characters;
using Veldrath.Server.Data;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for <c>GET /api/characters/last-session</c>, <c>DELETE /api/characters/last-session</c> and the
/// difficulty-mode branches of character creation — the "resume where you were" contract the web client depends on.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class CharacterLastSessionEndpointTests(WebAppFactory factory)
{
    private async Task<HttpClient> AuthedClientAsync(string username)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Creates a character through the real endpoint and returns its id.</summary>
    private static async Task<Guid> CreateCharacterAsync(HttpClient client, string name, string difficulty = "normal")
    {
        var response = await client.PostAsJsonAsync("/api/characters",
            new { Name = name, ClassName = "Warrior", DifficultyMode = difficulty });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<CharacterDto>();
        return dto!.Id;
    }

    private async Task UpdateCharacterAsync(Guid id, Action<Veldrath.Server.Data.Entities.Character> mutate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var character = await db.Characters.SingleAsync(c => c.Id == id);
        mutate(character);
        await db.SaveChangesAsync();
    }

    private async Task<Veldrath.Server.Data.Entities.Character> LoadCharacterAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Characters.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    // ── GET /api/characters/last-session ──────────────────────────────────────

    [Fact]
    public async Task LastSession_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/characters/last-session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LastSession_AccountWithoutCharacters_ReturnsNoContent()
    {
        using var client = await AuthedClientAsync("CharLast_Empty");

        var response = await client.GetAsync("/api/characters/last-session");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LastSession_ReturnsTheMostRecentlyPlayedCharacterWithItsRegion()
    {
        using var client = await AuthedClientAsync("CharLast_Resume");
        var older = await CreateCharacterAsync(client, "LastSession_Older");
        var newer = await CreateCharacterAsync(client, "LastSession_Newer");
        await UpdateCharacterAsync(older, c => c.LastPlayedAt = DateTimeOffset.UtcNow.AddDays(-2));
        await UpdateCharacterAsync(newer, c =>
        {
            c.LastPlayedAt = DateTimeOffset.UtcNow;
            c.CurrentZoneId = "crestfall";
            c.CurrentMapId = "zone-map:crestfall";
            c.CurrentNodeId = "crestfall-square";
        });

        var response = await client.GetAsync("/api/characters/last-session");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LastSessionDto>();
        dto!.CharacterId.Should().Be(newer);
        dto.CharacterName.Should().Be("LastSession_Newer");
        dto.ZoneId.Should().Be("crestfall");
        dto.RegionId.Should().Be("varenmark", "the region is resolved from the zone catalog");
        dto.CurrentMapId.Should().Be("zone-map:crestfall");
        dto.CurrentNodeId.Should().Be("crestfall-square");
    }

    [Fact]
    public async Task LastSession_CharacterWithoutZone_ReturnsNullRegion()
    {
        using var client = await AuthedClientAsync("CharLast_NoZone");
        var id = await CreateCharacterAsync(client, "LastSession_NoZone");
        await UpdateCharacterAsync(id, c => c.CurrentZoneId = string.Empty);

        var dto = await client.GetFromJsonAsync<LastSessionDto>("/api/characters/last-session");

        dto!.CharacterId.Should().Be(id);
        dto.ZoneId.Should().BeNullOrEmpty();
        dto.RegionId.Should().BeNull();
    }

    [Fact]
    public async Task LastSession_CharacterInAnUnknownZone_ReturnsNullRegion()
    {
        using var client = await AuthedClientAsync("CharLast_UnknownZone");
        var id = await CreateCharacterAsync(client, "LastSession_UnknownZone");
        await UpdateCharacterAsync(id, c => c.CurrentZoneId = "zone-that-does-not-exist");

        var dto = await client.GetFromJsonAsync<LastSessionDto>("/api/characters/last-session");

        dto!.ZoneId.Should().Be("zone-that-does-not-exist");
        dto.RegionId.Should().BeNull("a dangling zone id must not invent a region");
    }

    [Fact]
    public async Task LastSession_IgnoresDeletedCharacters()
    {
        using var client = await AuthedClientAsync("CharLast_Deleted");
        var id = await CreateCharacterAsync(client, "LastSession_Deleted");
        (await client.DeleteAsync($"/api/characters/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetAsync("/api/characters/last-session");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── DELETE /api/characters/last-session ───────────────────────────────────

    [Fact]
    public async Task DeleteLastSession_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/characters/last-session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteLastSession_ClearsLastPlayedForEveryCharacterOnTheAccount()
    {
        using var client = await AuthedClientAsync("CharLast_ClearAll");
        var first = await CreateCharacterAsync(client, "LastSession_ClearOne");
        var second = await CreateCharacterAsync(client, "LastSession_ClearTwo");

        var response = await client.DeleteAsync("/api/characters/last-session");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoadCharacterAsync(first)).LastPlayedAt.Should().Be(DateTimeOffset.MinValue);
        (await LoadCharacterAsync(second)).LastPlayedAt.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task DeleteLastSession_DoesNotTouchAnotherAccountsCharacters()
    {
        using var mine = await AuthedClientAsync("CharLast_ClearMine");
        using var theirs = await AuthedClientAsync("CharLast_ClearTheirs");
        var theirCharacter = await CreateCharacterAsync(theirs, "LastSession_Untouched");

        (await mine.DeleteAsync("/api/characters/last-session")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await LoadCharacterAsync(theirCharacter)).LastPlayedAt.Should().NotBe(DateTimeOffset.MinValue);
    }

    // ── POST /api/characters — difficulty mode ────────────────────────────────

    [Fact]
    public async Task Create_UnknownDifficultyMode_ReturnsBadRequest()
    {
        using var client = await AuthedClientAsync("CharDiff_Bogus");

        var response = await client.PostAsJsonAsync("/api/characters",
            new { Name = "Diff_Bogus", ClassName = "Warrior", DifficultyMode = "legendary" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("DifficultyMode");
    }

    [Fact]
    public async Task Create_HardcoreMode_IsStoredAndFlagged()
    {
        using var client = await AuthedClientAsync("CharDiff_Hardcore");

        var response = await client.PostAsJsonAsync("/api/characters",
            new { Name = "Diff_Hardcore", ClassName = "Warrior", DifficultyMode = "hardcore" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<CharacterDto>();
        dto!.DifficultyMode.Should().Be("hardcore");
        dto.IsHardcore.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DifficultyModeIsCaseInsensitive()
    {
        using var client = await AuthedClientAsync("CharDiff_UpperCase");

        var response = await client.PostAsJsonAsync("/api/characters",
            new { Name = "Diff_UpperCase", ClassName = "Warrior", DifficultyMode = "HARDCORE" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<CharacterDto>();
        dto!.DifficultyMode.Should().Be("hardcore", "the stored mode is normalized");
    }

    [Fact]
    public async Task Create_OmittedDifficultyMode_DefaultsToNormal()
    {
        using var client = await AuthedClientAsync("CharDiff_Default");

        var response = await client.PostAsJsonAsync("/api/characters",
            new { Name = "Diff_Default", ClassName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<CharacterDto>();
        dto!.DifficultyMode.Should().Be("normal");
        dto.IsHardcore.Should().BeFalse();
    }
}
