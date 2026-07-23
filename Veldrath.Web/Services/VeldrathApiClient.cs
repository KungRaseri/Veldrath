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

    /// <summary>
    /// Returns the last active session info for the authenticated account,
    /// or <see langword="null"/> if no characters exist. Used after page refresh
    /// to restore game state without manual character selection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="LastSessionDto"/> with the last character and location, or <see langword="null"/>.</returns>
    public async Task<LastSessionDto?> GetLastSessionAsync(CancellationToken ct = default)
    {
        var resp = await Http.GetAsync("/api/characters/last-session", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LastSessionDto>(ct);
    }

    /// <summary>Deletes the last session record so the routing guard won't show a resume prompt.</summary>
    public async Task DeleteLastSessionAsync(CancellationToken ct = default)
    {
        await Http.DeleteAsync("/api/characters/last-session", ct);
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

    // ── Content (classes, species, backgrounds, etc.) ────────────────────────

    /// <summary>Returns all active actor classes available for character creation.</summary>
    public async Task<List<ActorClassDto>> GetClassesAsync(CancellationToken ct = default)
    {
        var resp = await Http.GetAsync("/api/content/classes", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ActorClassDto>>(ct) ?? [];
    }

    /// <summary>Returns all available playable species for character creation.</summary>
    public async Task<List<SpeciesDto>> GetSpeciesAsync(CancellationToken ct = default)
    {
        var resp = await Http.GetAsync("/api/content/species", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<SpeciesDto>>(ct) ?? [];
    }

    /// <summary>Returns all available backgrounds for character creation.</summary>
    public async Task<List<BackgroundDto>> GetBackgroundsAsync(CancellationToken ct = default)
    {
        var resp = await Http.GetAsync("/api/content/backgrounds", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<BackgroundDto>>(ct) ?? [];
    }

    // ── Session-based character creation ───────────────────────────────────────────

    /// <summary>Begins a new character creation session and returns the session identifier.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BeginCreationSessionResponse?> BeginCreationSessionAsync(CancellationToken ct = default)
    {
        var resp = await Http.PostAsJsonAsync("/api/character-creation/sessions", new { }, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<BeginCreationSessionResponse>(ct)
            : null;
    }

    /// <summary>Returns a non-persisted preview of the character being built in the session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<CharacterPreviewDto?> GetCreationPreviewAsync(Guid sessionId, CancellationToken ct = default)
    {
        var resp = await Http.GetAsync($"/api/character-creation/sessions/{sessionId}/preview", ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<CharacterPreviewDto>(ct)
            : null;
    }

    /// <summary>Finalizes the session and creates the character.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="request">The finalization request containing the character name and difficulty mode.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<CharacterDto?> FinalizeCreationSessionAsync(Guid sessionId, FinalizeCreationSessionRequest request, CancellationToken ct = default)
    {
        var resp = await Http.PostAsJsonAsync($"/api/character-creation/sessions/{sessionId}/finalize", request, ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<CharacterDto>(ct)
            : null;
    }

    /// <summary>Abandons the creation session and releases server-side resources.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task AbandonCreationSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try { await Http.DeleteAsync($"/api/character-creation/sessions/{sessionId}", ct); }
        catch { /* best-effort — session will expire server-side regardless */ }
    }

    /// <summary>Sets the character name on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="characterName">The desired character name.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SetCreationChoiceResponse?> SetCreationNameAsync(Guid sessionId, string characterName, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/name",
            new SetCreationNameRequest(characterName), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SetCreationChoiceResponse>(ct)
            : null;
    }

    /// <summary>Sets the selected class on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="className">The class display name or slug to select.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SetCreationChoiceResponse?> SetCreationClassAsync(Guid sessionId, string className, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/class",
            new SetCreationClassRequest(className), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SetCreationChoiceResponse>(ct)
            : null;
    }

    /// <summary>Sets the selected species on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="speciesSlug">The species slug to select.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SetCreationChoiceResponse?> SetCreationSpeciesAsync(Guid sessionId, string speciesSlug, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/species",
            new SetCreationSpeciesRequest(speciesSlug), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SetCreationChoiceResponse>(ct)
            : null;
    }

    /// <summary>Sets the selected background on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="backgroundId">The background identifier to select.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SetCreationChoiceResponse?> SetCreationBackgroundAsync(Guid sessionId, string backgroundId, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/background",
            new SetCreationBackgroundRequest(backgroundId), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SetCreationChoiceResponse>(ct)
            : null;
    }

    /// <summary>Sets the attribute allocations (point-buy) on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="allocations">A mapping of attribute name to the allocated value.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AllocateCreationAttributesResponse?> SetCreationAttributesAsync(Guid sessionId, Dictionary<string, int> allocations, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/attributes",
            new SetCreationAttributesRequest(allocations), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<AllocateCreationAttributesResponse>(ct)
            : null;
    }

    /// <summary>Sets the equipment preferences on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="armorType">The preferred armor type slug, or <c>null</c> to skip.</param>
    /// <param name="weaponType">The preferred weapon type slug, or <c>null</c> to skip.</param>
    /// <param name="includeShield">Whether to include a shield in starting equipment.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SetCreationChoiceResponse?> SetCreationEquipmentPreferencesAsync(Guid sessionId, string? armorType, string? weaponType, bool includeShield, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/equipment",
            new SetCreationEquipmentPreferencesRequest(armorType, weaponType, includeShield), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SetCreationChoiceResponse>(ct)
            : null;
    }

    /// <summary>Sets the starting location on an existing creation session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="locationId">The location identifier to select.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SetCreationChoiceResponse?> SetCreationLocationAsync(Guid sessionId, string locationId, CancellationToken ct = default)
    {
        var resp = await Http.PatchAsJsonAsync($"/api/character-creation/sessions/{sessionId}/location",
            new SetCreationLocationRequest(locationId), ct);
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<SetCreationChoiceResponse>(ct)
            : null;
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

