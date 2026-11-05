using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Contracts.Characters;
using Veldrath.Server.Data;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>One page of the anonymous leaderboard payload.</summary>
internal sealed record HardcoreLeaderboardPage(int Page, int PageSize, int Total, List<HardcoreBoardEntry> Items);

/// <summary>One row of the hardcore leaderboard.</summary>
internal sealed record HardcoreBoardEntry(
    Guid CharacterId,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset? DiedAt,
    bool IsOwn);

/// <summary>One row of the character memorial.</summary>
internal sealed record MemorialEntry(
    Guid CharacterId,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset? DiedAt,
    string? CurrentZoneId);

/// <summary>
/// Integration tests for the two character read surfaces that are not covered elsewhere:
/// <c>GET /api/characters/memorial</c> and <c>GET /api/leaderboard/hardcore</c> — including ownership marking and
/// the paging clamp.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class CharacterHallOfFameEndpointTests(WebAppFactory factory)
{
    private async Task<HttpClient> AuthedClientAsync(string username)
    {
        var (token, _) = await factory.CreateAccountTokenAsync(username);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateCharacterAsync(
        HttpClient client, string name, string difficulty = "hardcore")
    {
        var response = await client.PostAsJsonAsync("/api/characters",
            new { Name = name, ClassName = "Warrior", DifficultyMode = difficulty });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CharacterDto>())!.Id;
    }

    private async Task SetProgressAsync(Guid id, int level, long experience, bool fall = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var character = await db.Characters.SingleAsync(c => c.Id == id);
        character.Level = level;
        character.Experience = experience;
        if (fall)
            character.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    // ── GET /api/characters/memorial ──────────────────────────────────────────

    [Fact]
    public async Task Memorial_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/characters/memorial");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Memorial_NewAccount_ReturnsAnEmptyList()
    {
        using var client = await AuthedClientAsync("Memorial_Empty");

        var entries = await client.GetFromJsonAsync<List<MemorialEntry>>("/api/characters/memorial");

        entries.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Memorial_ListsFallenHardcoreCharacters()
    {
        using var client = await AuthedClientAsync("Memorial_Fallen");
        var id = await CreateCharacterAsync(client, "Memorial_FallenHero");
        await SetProgressAsync(id, level: 12, experience: 4800, fall: true);

        var entries = await client.GetFromJsonAsync<List<MemorialEntry>>("/api/characters/memorial");

        var hero = entries!.Should().ContainSingle().Subject;
        hero.CharacterId.Should().Be(id);
        hero.Name.Should().Be("Memorial_FallenHero");
        hero.ClassName.Should().Be("Warrior");
        hero.Level.Should().Be(12);
        hero.Experience.Should().Be(4800);
        hero.DiedAt.Should().NotBeNull("the memorial records when the character fell");
    }

    [Fact]
    public async Task Memorial_IgnoresDeletedNormalCharacters()
    {
        using var client = await AuthedClientAsync("Memorial_NormalDeleted");
        var id = await CreateCharacterAsync(client, "Memorial_DeletedNormal", difficulty: "normal");
        (await client.DeleteAsync($"/api/characters/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entries = await client.GetFromJsonAsync<List<MemorialEntry>>("/api/characters/memorial");

        entries!.Should().NotContain(e => e.CharacterId == id,
            "a deleted softcore character is not a hardcore memorial entry");
    }

    [Fact]
    public async Task Memorial_IgnoresLivingHardcoreCharacters()
    {
        using var client = await AuthedClientAsync("Memorial_Living");
        var id = await CreateCharacterAsync(client, "Memorial_StillAlive");

        var entries = await client.GetFromJsonAsync<List<MemorialEntry>>("/api/characters/memorial");

        entries!.Should().NotContain(e => e.CharacterId == id);
    }

    [Fact]
    public async Task Memorial_OnlyShowsTheCallersCharacters()
    {
        using var mine = await AuthedClientAsync("Memorial_Mine");
        using var theirs = await AuthedClientAsync("Memorial_Theirs");
        var theirHero = await CreateCharacterAsync(theirs, "Memorial_TheirHero");
        await SetProgressAsync(theirHero, level: 4, experience: 100, fall: true);

        var entries = await mine.GetFromJsonAsync<List<MemorialEntry>>("/api/characters/memorial");

        entries!.Should().NotContain(e => e.CharacterId == theirHero);
    }

    // ── GET /api/leaderboard/hardcore ─────────────────────────────────────────

    [Fact]
    public async Task Leaderboard_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/leaderboard/hardcore");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Leaderboard_ListsHardcoreCharactersHighestLevelFirst()
    {
        using var client = await AuthedClientAsync("Board_Ordering");
        var low = await CreateCharacterAsync(client, "Board_Low");
        var high = await CreateCharacterAsync(client, "Board_High");
        var middle = await CreateCharacterAsync(client, "Board_Middle");
        await SetProgressAsync(low, level: 3, experience: 900);
        await SetProgressAsync(high, level: 17, experience: 200);
        await SetProgressAsync(middle, level: 9, experience: 1500);

        var page = await client.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?pageSize=100");

        page.Should().NotBeNull();
        page!.Page.Should().Be(1);
        page.Items.Select(i => i.Level).Should().BeInDescendingOrder();
        var indexOf = page.Items.Select(i => i.CharacterId).ToList();
        indexOf.IndexOf(high).Should().BeLessThan(indexOf.IndexOf(middle));
        indexOf.IndexOf(middle).Should().BeLessThan(indexOf.IndexOf(low));
        page.Total.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Leaderboard_BreaksLevelTiesByExperience()
    {
        using var client = await AuthedClientAsync("Board_Ties");
        var poorer = await CreateCharacterAsync(client, "Board_Poorer");
        var richer = await CreateCharacterAsync(client, "Board_Richer");
        await SetProgressAsync(poorer, level: 6, experience: 10);
        await SetProgressAsync(richer, level: 6, experience: 990);

        var page = await client.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?pageSize=100");

        var ids = page!.Items.Select(i => i.CharacterId).ToList();
        ids.IndexOf(richer).Should().BeLessThan(ids.IndexOf(poorer));
    }

    [Fact]
    public async Task Leaderboard_MarksTheCallersOwnEntries()
    {
        using var mine = await AuthedClientAsync("Board_Mine");
        using var theirs = await AuthedClientAsync("Board_Theirs");
        var myHero = await CreateCharacterAsync(mine, "Board_MyHero");
        var theirHero = await CreateCharacterAsync(theirs, "Board_TheirHero");
        await SetProgressAsync(myHero, level: 8, experience: 50);
        await SetProgressAsync(theirHero, level: 8, experience: 60);

        var page = await mine.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?pageSize=100");

        page!.Items.Single(i => i.CharacterId == myHero).IsOwn.Should().BeTrue();
        page.Items.Single(i => i.CharacterId == theirHero).IsOwn.Should().BeFalse();
    }

    [Fact]
    public async Task Leaderboard_KeepsFallenCharactersAndReportsTheirDeathDate()
    {
        using var client = await AuthedClientAsync("Board_Fallen");
        var fallen = await CreateCharacterAsync(client, "Board_FallenHero");
        await SetProgressAsync(fallen, level: 11, experience: 3000, fall: true);

        var page = await client.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?pageSize=100");

        var row = page!.Items.Single(i => i.CharacterId == fallen);
        row.DiedAt.Should().NotBeNull();
        row.Level.Should().Be(11);
    }

    [Fact]
    public async Task Leaderboard_ExcludesSoftcoreCharacters()
    {
        using var client = await AuthedClientAsync("Board_Softcore");
        var softcore = await CreateCharacterAsync(client, "Board_SoftcoreHero", difficulty: "normal");

        var page = await client.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?pageSize=100");

        page!.Items.Should().NotContain(i => i.CharacterId == softcore);
    }

    [Fact]
    public async Task Leaderboard_ClampsPageSizeAndFloorsThePage()
    {
        using var client = await AuthedClientAsync("Board_Clamp");

        var page = await client.GetFromJsonAsync<HardcoreLeaderboardPage>(
            "/api/leaderboard/hardcore?page=0&pageSize=9999");

        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(100);
        page.Items.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task Leaderboard_PaginatesWithoutChangingTheTotal()
    {
        using var client = await AuthedClientAsync("Board_Paging");
        var first = await client.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?page=1&pageSize=2");
        var second = await client.GetFromJsonAsync<HardcoreLeaderboardPage>("/api/leaderboard/hardcore?page=2&pageSize=2");

        second!.Total.Should().Be(first!.Total);
        second.Items.Select(i => i.CharacterId).Should().NotIntersectWith(first.Items.Select(i => i.CharacterId));
    }
}
