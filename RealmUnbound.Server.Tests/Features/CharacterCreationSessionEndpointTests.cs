using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmUnbound.Contracts.Auth;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>
/// Seeds an ActorClass and Species once, then runs integration tests against the
/// character creation wizard API endpoints.
/// </summary>
public sealed class CharacterCreationFixture : IAsyncLifetime
{
    public WebAppFactory Factory { get; } = new();
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        db.ActorClasses.Add(new ActorClass
        {
            Slug        = "warrior",
            TypeKey     = "melee",
            DisplayName = "Warrior",
            IsActive    = true,
            PrimaryStat = "strength",
            Stats       = new ActorClassStats { BaseHealth = 120, BaseMana = 20 },
        });

        db.Species.Add(new Species
        {
            Slug        = "human",
            TypeKey     = "humanoid",
            DisplayName = "Human",
            IsActive    = true,
        });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        await Task.CompletedTask;
    }
}

[Trait("Category", "Integration")]
public class CharacterCreationSessionEndpointTests(CharacterCreationFixture fixture)
    : IClassFixture<CharacterCreationFixture>
{
    private readonly WebAppFactory _factory = fixture.Factory;
    private readonly HttpClient    _client  = fixture.Client;

    private async Task<string> GetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@cctest.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "Pass1234!" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "Pass1234!" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    // ── Auth guard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Begin_NoAuth_Returns401()
    {
        using var anonClient = _factory.CreateClient();
        var response = await anonClient.PostAsJsonAsync("/api/character-creation/sessions", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchName_NoAuth_Returns401()
    {
        using var anonClient = _factory.CreateClient();
        var sessionId = Guid.NewGuid();
        var response = await anonClient.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{sessionId}/name",
            new { CharacterName = "Hero" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Begin session ───────────────────────────────────────────────────────

    [Fact]
    public async Task Begin_Authenticated_Returns201WithSessionId()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcBegin_User"));

        var response = await _client.PostAsJsonAsync("/api/character-creation/sessions", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var body = await response.Content.ReadFromJsonAsync<BeginSessionResponse>();
        body!.SessionId.Should().NotBe(Guid.Empty);
        body.Success.Should().BeTrue();
    }

    // ── GET session ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSession_AfterBegin_Returns200WithDraftStatus()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcGetSession_User"));

        var beginResp = await _client.PostAsJsonAsync("/api/character-creation/sessions", new { });
        var begin     = await beginResp.Content.ReadFromJsonAsync<BeginSessionResponse>();

        var response = await _client.GetAsync($"/api/character-creation/sessions/{begin!.SessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionStatusResponse>();
        session!.Status.Should().Be(0); // Draft = 0
    }

    [Fact]
    public async Task GetSession_UnknownId_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcGet404_User"));

        var response = await _client.GetAsync($"/api/character-creation/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH name ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchName_ValidAlphaName_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchName_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchName_EmptyString_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcNameEmpty_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchName_ContainsDigit_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcNameDigit_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "H3ro" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchName_TooLong_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcNameLong_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = new string('A', 31) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH class ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchClass_SeededWarriorSlug_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchClass_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchClass_UnknownClass_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchUnknown_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "nonexistent-class" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Finalize guards ──────────────────────────────────────────────────────

    [Fact]
    public async Task Finalize_NoClassSelected_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcFinalizeNoClass_User"));
        var begin = await BeginSessionAsync();

        // Set name but not class
        await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Gandalf" });

        var response = await _client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Abandon ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Abandon_ExistingSession_Returns204()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcAbandon_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.DeleteAsync(
            $"/api/character-creation/sessions/{begin.SessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Full wizard flow ─────────────────────────────────────────────────────

    [Fact]
    public async Task FullWizardFlow_BeginNameClassFinalize_CreatesCharacter()
    {
        using var client = _factory.CreateClient();
        var token = await GetTokenForNewClient(client, "CcFullFlow_User");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Begin
        var beginResp = await client.PostAsJsonAsync("/api/character-creation/sessions", new { });
        beginResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var begin = await beginResp.Content.ReadFromJsonAsync<BeginSessionResponse>();
        var sid   = begin!.SessionId;

        // Set name
        var nameResp = await client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{sid}/name",
            new { CharacterName = "Eriador" });
        nameResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Set class (seeded warrior)
        var classResp = await client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{sid}/class",
            new { ClassName = "warrior" });
        classResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Finalize
        var finalResp = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{sid}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        finalResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var character = await finalResp.Content.ReadFromJsonAsync<CharacterDto>();
        character!.Name.Should().Be("Eriador");
        character.ClassName.Should().Be("Warrior");
        character.DifficultyMode.Should().Be("normal");
        character.Id.Should().NotBe(Guid.Empty);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<BeginSessionResponse> BeginSessionAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/character-creation/sessions", new { });
        return (await response.Content.ReadFromJsonAsync<BeginSessionResponse>())!;
    }

    private async Task<string> GetTokenForNewClient(HttpClient client, string username)
    {
        var email = $"{username.ToLower()}@cctest.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "Pass1234!" });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "Pass1234!" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    // Local response projection types (avoid depending on internal server DTOs)
    private record BeginSessionResponse(Guid SessionId, bool Success);
    private record SessionStatusResponse(int Status);
}
