using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using Veldrath.Contracts.Auth;
using Veldrath.Contracts.Characters;
using Veldrath.Server.Tests.Infrastructure;
namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Seeds an ActorClass and Species once, then runs integration tests against the
/// character creation wizard API endpoints.
/// </summary>
public sealed class CharacterCreationFixture : IAsyncLifetime
{
    public WebAppFactory Factory { get; }
    public HttpClient Client { get; private set; } = null!;

    public CharacterCreationFixture(WebAppFactory factory) => Factory = factory;

    public async Task InitializeAsync()
    {
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        // Guard against re-seeding when the global WebAppFactory seeder already inserted these rows.
        if (!await db.ActorClasses.AnyAsync(c => c.Slug == "warrior"))
            db.ActorClasses.Add(new ActorClass
            {
                Slug        = "warrior",
                TypeKey     = "melee",
                DisplayName = "Warrior",
                IsActive    = true,
                PrimaryStat = "strength",
                Stats       = new ActorClassStats { BaseHealth = 120, BaseMana = 20 },
            });

        if (!await db.Species.AnyAsync(s => s.Slug == "human"))
            db.Species.Add(new Species
            {
                Slug               = "human",
                TypeKey            = "humanoid",
                DisplayName        = "Human",
                IsActive           = true,
                IsPlayerSelectable = true,
            });

        if (!await db.Backgrounds.AnyAsync(b => b.Slug == "soldier"))
            db.Backgrounds.Add(new Background
            {
                Slug        = "soldier",
                TypeKey     = "combat",
                DisplayName = "Soldier",
                IsActive    = true,
            });

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

[Collection("Integration")]
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
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
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

    [Fact]
    public async Task Abandon_UnknownSession_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcAbandon404_User"));

        var response = await _client.DeleteAsync(
            $"/api/character-creation/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH species ────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchSpecies_SeededHumanSlug_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchSpecies_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "human" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchSpecies_UnknownSlug_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchSpeciesUnk_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "nonexistent-species" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchSpecies_UnknownSession_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchSpecies404_User"));

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{Guid.NewGuid()}/species",
            new { SpeciesSlug = "human" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH background ─────────────────────────────────────────────────────

    [Fact]
    public async Task PatchBackground_SeededSoldierSlug_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchBg_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/background",
            new { BackgroundId = "soldier" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchBackground_UnknownSlug_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchBgUnk_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/background",
            new { BackgroundId = "nonexistent-bg" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH attributes ─────────────────────────────────────────────────────

    [Fact]
    public async Task PatchAttributes_ValidPointBuy_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchAttr_User"));
        var begin = await BeginSessionAsync();

        // Standard point-buy that sums correctly
        var allocations = new
        {
            Allocations = new Dictionary<string, int>
            {
                ["Strength"]     = 14,
                ["Dexterity"]    = 12,
                ["Constitution"] = 13,
                ["Intelligence"] = 8,
                ["Wisdom"]       = 10,
                ["Charisma"]     = 8,
            }
        };
        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/attributes",
            allocations);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchAttributes_UnknownSession_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchAttr404_User"));

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{Guid.NewGuid()}/attributes",
            new { Strength = 10, Dexterity = 10, Constitution = 10, Intelligence = 10, Wisdom = 10, Charisma = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH equipment ──────────────────────────────────────────────────────

    [Fact]
    public async Task PatchEquipment_ValidPreferences_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchEquip_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/equipment",
            new { PreferredArmorType = "heavy", PreferredWeaponType = "sword", IncludeShield = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchEquipment_UnknownSession_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPatchEquip404_User"));

        var response = await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{Guid.NewGuid()}/equipment",
            new { PreferredArmorType = "heavy", PreferredWeaponType = "sword", IncludeShield = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET preview ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_AfterBegin_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPreviewEmpty_User"));
        var begin = await BeginSessionAsync();

        var response = await _client.GetAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPreview_AfterClassSelected_ReturnsPreviewWithClassName()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPreviewClass_User"));
        var begin = await BeginSessionAsync();

        await _client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });

        var response = await _client.GetAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("warrior");
    }

    [Fact]
    public async Task GetPreview_UnknownSession_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("CcPreview404_User"));

        var response = await _client.GetAsync(
            $"/api/character-creation/sessions/{Guid.NewGuid()}/preview");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Finalize — additional branches ───────────────────────────────────────

    [Fact]
    public async Task Finalize_NoSpeciesSelected_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcFinalizeNoSpecies_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Aldric" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });
        // Intentionally skip species

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finalize_NoBackgroundSelected_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcFinalizeNoBg_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Bryndis" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "human" });
        // Intentionally skip background

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finalize_NoNameProvided_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcFinalizeNoName_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "human" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/background",
            new { BackgroundId = "soldier" });
        // No name set and no CharacterName in finalize body

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finalize_InvalidDifficultyMode_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcFinalizeInvalidMode_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Calder" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "human" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/background",
            new { BackgroundId = "soldier" });

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "extreme" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finalize_HardcoreMode_Returns201WithHardcoreFlag()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcFinalizeHardcore_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Darveth" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "human" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/background",
            new { BackgroundId = "soldier" });

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "hardcore" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var character = await response.Content.ReadFromJsonAsync<CharacterDto>();
        character!.IsHardcore.Should().BeTrue();
        character.DifficultyMode.Should().Be("hardcore");
    }

    [Fact]
    public async Task Finalize_Sets_StartingLocationSlug_To_CrestfallSquare()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcFinalizeLocation_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Elara" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class",
            new { ClassName = "warrior" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/species",
            new { SpeciesSlug = "human" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/background",
            new { BackgroundId = "soldier" });

        var response = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var character = await response.Content.ReadFromJsonAsync<CharacterDto>();
        character!.CurrentZoneLocationSlug.Should().Be("crestfall-square");
    }

    [Fact]
    public async Task Finalize_DuplicateName_Returns409()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        var tokenA = await GetTokenForNewClient(clientA, "CcDupeName_UserA");
        var tokenB = await GetTokenForNewClient(clientB, "CcDupeName_UserB");

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // First character with this name succeeds
        var beginA = await BeginSessionWithClientAsync(clientA);
        await clientA.PatchAsJsonAsync($"/api/character-creation/sessions/{beginA.SessionId}/name",  new { CharacterName = "Fernwick" });
        await clientA.PatchAsJsonAsync($"/api/character-creation/sessions/{beginA.SessionId}/class", new { ClassName = "warrior" });
        await clientA.PatchAsJsonAsync($"/api/character-creation/sessions/{beginA.SessionId}/species", new { SpeciesSlug = "human" });
        await clientA.PatchAsJsonAsync($"/api/character-creation/sessions/{beginA.SessionId}/background", new { BackgroundId = "soldier" });
        var firstResp = await clientA.PostAsJsonAsync(
            $"/api/character-creation/sessions/{beginA.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });
        firstResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second character with identical name should conflict
        var beginB = await BeginSessionWithClientAsync(clientB);
        await clientB.PatchAsJsonAsync($"/api/character-creation/sessions/{beginB.SessionId}/class", new { ClassName = "warrior" });
        await clientB.PatchAsJsonAsync($"/api/character-creation/sessions/{beginB.SessionId}/species", new { SpeciesSlug = "human" });
        await clientB.PatchAsJsonAsync($"/api/character-creation/sessions/{beginB.SessionId}/background", new { BackgroundId = "soldier" });
        // Pass the duplicate name directly in the finalize body (PatchName would return 400 since name is taken)
        var conflictResp = await clientB.PostAsJsonAsync(
            $"/api/character-creation/sessions/{beginB.SessionId}/finalize",
            new { CharacterName = "Fernwick", DifficultyMode = "normal" });

        conflictResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Cross-session ownership ───────────────────────────────────────────────

    [Fact]
    public async Task PatchName_OtherUsersSession_Returns403()
    {
        using var ownerClient = _factory.CreateClient();
        using var otherClient = _factory.CreateClient();

        var ownerToken = await GetTokenForNewClient(ownerClient, "CcOwner_User");
        var otherToken = await GetTokenForNewClient(otherClient, "CcOther_User");

        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var begin = await BeginSessionWithClientAsync(ownerClient);

        // Other user tries to modify the session
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var response = await otherClient.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/name",
            new { CharacterName = "Intruder" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Finalize_OtherUsersSession_Returns403()
    {
        using var ownerClient = _factory.CreateClient();
        using var otherClient = _factory.CreateClient();

        var ownerToken = await GetTokenForNewClient(ownerClient, "CcFinalizeOwner_User");
        var otherToken = await GetTokenForNewClient(otherClient, "CcFinalizeOther_User");

        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var begin = await BeginSessionWithClientAsync(ownerClient);

        await ownerClient.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",  new { CharacterName = "Grimnir" });
        await ownerClient.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class", new { ClassName = "warrior" });

        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var response = await otherClient.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Double-finalize guard ─────────────────────────────────────────────────

    [Fact]
    public async Task Finalize_AlreadyFinalizedSession_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenForNewClient(client, "CcDoubleFinalize_User"));
        var begin = await BeginSessionWithClientAsync(client);

        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/name",  new { CharacterName = "Halvard" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/class", new { ClassName = "warrior" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/species", new { SpeciesSlug = "human" });
        await client.PatchAsJsonAsync($"/api/character-creation/sessions/{begin.SessionId}/background", new { BackgroundId = "soldier" });

        // First finalize — should succeed
        var first = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second finalize — should fail
        var second = await client.PostAsJsonAsync(
            $"/api/character-creation/sessions/{begin.SessionId}/finalize",
            new { CharacterName = (string?)null, DifficultyMode = "normal" });
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

        // Set species (seeded human)
        var speciesResp = await client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{sid}/species",
            new { SpeciesSlug = "human" });
        speciesResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Set background (seeded soldier)
        var backgroundResp = await client.PatchAsJsonAsync(
            $"/api/character-creation/sessions/{sid}/background",
            new { BackgroundId = "soldier" });
        backgroundResp.StatusCode.Should().Be(HttpStatusCode.OK);

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

    private static async Task<BeginSessionResponse> BeginSessionWithClientAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/character-creation/sessions", new { });
        return (await response.Content.ReadFromJsonAsync<BeginSessionResponse>())!;
    }

    private async Task<string> GetTokenForNewClient(HttpClient client, string username)
    {
        var email = $"{username.ToLower()}@cctest.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    // Local response projection types (avoid depending on internal server DTOs)
    private record BeginSessionResponse(Guid SessionId, bool Success);
    private record SessionStatusResponse(int Status);
}
