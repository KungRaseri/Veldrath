using System.Net.Http.Json;
using Veldrath.Auth;
using Veldrath.Contracts.Characters;
using Veldrath.Contracts.Content;
using Veldrath.Contracts.Editorial;
using Veldrath.Contracts.Foundry;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.Web.Services;

/// <summary>
/// Typed HTTP client facade for calling the Veldrath.Server REST API from Veldrath.Web.
/// Authentication endpoints are provided by <see cref="VeldrathAuthApiClient"/>.
/// Configure the base address via <c>Veldrath:ServerUrl</c> at startup.
/// Call <see cref="VeldrathAuthApiClient.SetBearerToken"/> after login to authorise authenticated requests.
/// </summary>
public class VeldrathApiClient(HttpClient http) : VeldrathAuthApiClient(http), Veldrath.GameClient.Core.Abstractions.IGameApiClient
{
    // ── Characters ───────────────────────────────────────────────────────────

    /// <summary>Returns all characters belonging to the authenticated account.</summary>
    public async Task<List<CharacterDto>> GetCharactersAsync(CancellationToken ct = default)
    {
        var resp = await Http.GetAsync("/api/characters", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CharacterDto>>(ct) ?? [];
    }

    /// <summary>Creates a new character for the authenticated account.</summary>
    /// <param name="name">The character's display name.</param>
    /// <param name="className">The class display name (e.g. "Warrior", "Mage").</param>
    /// <param name="difficultyMode">The difficulty mode: "normal" or "hardcore".</param>
    /// <returns>The created character DTO, or <c>null</c> if the request was rejected.</returns>
    public async Task<CharacterDto?> CreateCharacterAsync(string name, string className, string difficultyMode = "normal", CancellationToken ct = default)
    {
        var request = new CreateCharacterRequest(name, className, difficultyMode);
        var resp = await Http.PostAsJsonAsync("/api/characters", request, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<CharacterDto>(ct)
            : null;
    }

    /// <summary>Checks whether a character name is available.</summary>
    /// <param name="name">The desired character name.</param>
    /// <returns>A response indicating availability, or <c>null</c> if the response body is empty.</returns>
    /// <exception cref="HttpRequestException">Thrown when the server returns a non-success status code.</exception>
    public async Task<CheckNameAvailabilityResponse?> CheckCharacterNameAsync(string name, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/character-creation/sessions/check-name?name={Uri.EscapeDataString(name)}", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CheckNameAvailabilityResponse>(ct);
    }

    // ── Content (classes, species, etc.) ─────────────────────────────────────

    /// <summary>Returns all active actor classes available for character creation.</summary>
    public async Task<List<ActorClassDto>> GetClassesAsync(CancellationToken ct = default)
    {
        var resp = await Http.GetAsync("/api/content/classes", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ActorClassDto>>(ct) ?? [];
    }

    // ── Editorial (public, no auth required) ─────────────────────────────────

    /// <summary>Returns a paged list of published patch notes.</summary>
    public async Task<PagedResult<PatchNoteSummaryDto>?> GetPatchNotesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/patch-notes?page={page}&pageSize={pageSize}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<PatchNoteSummaryDto>>(ct)
            : null;
    }

    /// <summary>Returns a single published patch note by slug.</summary>
    public async Task<PatchNoteDto?> GetPatchNoteAsync(string slug, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/patch-notes/{Uri.EscapeDataString(slug)}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PatchNoteDto>(ct)
            : null;
    }

    /// <summary>Returns a paged list of published lore articles, optionally filtered by category.</summary>
    public async Task<PagedResult<LoreArticleSummaryDto>?> GetLoreArticlesAsync(int page = 1, int pageSize = 20, string? category = null, CancellationToken ct = default)
    {
        var url = $"/api/editorial/lore?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category))
            url += $"&category={Uri.EscapeDataString(category)}";
        var resp = await Http.GetAsync(url, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<LoreArticleSummaryDto>>(ct)
            : null;
    }

    /// <summary>Returns a single published lore article by slug.</summary>
    public async Task<LoreArticleDto?> GetLoreArticleAsync(string slug, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/lore/{Uri.EscapeDataString(slug)}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<LoreArticleDto>(ct)
            : null;
    }

    /// <summary>Returns a paged list of published editorial announcements.</summary>
    public async Task<PagedResult<EditorialAnnouncementDto>?> GetAnnouncementsAsync(int page = 1, int pageSize = 5, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/editorial/announcements?page={page}&pageSize={pageSize}", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<PagedResult<EditorialAnnouncementDto>>(ct)
            : null;
    }
}

