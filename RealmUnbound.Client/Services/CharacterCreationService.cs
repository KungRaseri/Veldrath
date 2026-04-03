using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Characters;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Client abstraction for the guided character creation wizard REST API.
/// Wraps the <c>/api/character-creation/sessions</c> endpoints.
/// </summary>
public interface ICharacterCreationService
{
    /// <summary>Starts a new creation session and returns the session identifier, or <see langword="null"/> on failure.</summary>
    Task<Guid?> BeginSessionAsync();

    /// <summary>Sets the character name on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetNameAsync(Guid sessionId, string name);

    /// <summary>Sets the selected class on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetClassAsync(Guid sessionId, string className);

    /// <summary>Sets the selected species on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetSpeciesAsync(Guid sessionId, string speciesSlug);

    /// <summary>Sets the selected background on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetBackgroundAsync(Guid sessionId, string backgroundId);

    /// <summary>Sets the attribute allocations (point-buy) on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetAttributesAsync(Guid sessionId, Dictionary<string, int> allocations);

    /// <summary>Sets the equipment preferences on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetEquipmentPreferencesAsync(Guid sessionId, SetCreationEquipmentPreferencesRequest preferences);

    /// <summary>Sets the starting location on an existing session. Returns <see langword="true"/> on success.</summary>
    Task<bool> SetLocationAsync(Guid sessionId, string locationId);

    /// <summary>Finalizes the session, creates the character, and returns the resulting <see cref="CharacterDto"/> or an error.</summary>
    Task<(CharacterDto? Character, AppError? Error)> FinalizeAsync(Guid sessionId, FinalizeCreationSessionRequest request);

    /// <summary>Abandons the session (best-effort — does not throw on failure).</summary>
    Task AbandonAsync(Guid sessionId);

    /// <summary>Returns a live preview of the character being built, or <see langword="null"/> if the session has insufficient state to generate one.</summary>
    Task<CharacterPreviewDto?> GetPreviewAsync(Guid sessionId);

    /// <summary>Checks whether <paramref name="name"/> is well-formed and not already taken.</summary>
    /// <returns>A tuple where <c>Available</c> is <see langword="true"/> when the name can be used, and <c>Error</c> is a human-readable reason when it cannot.</returns>
    Task<(bool Available, string? Error)> CheckNameAvailabilityAsync(string name);
}

/// <summary>
/// HTTP implementation of <see cref="ICharacterCreationService"/> backed by the server wizard REST API.
/// </summary>
public class HttpCharacterCreationService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpCharacterCreationService> logger) : ICharacterCreationService
{
    private AuthenticationHeaderValue? BearerHeader => tokens.BearerHeader();

    private record BeginSessionResponse(Guid SessionId, bool Success);

    /// <inheritdoc />
    public async Task<Guid?> BeginSessionAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/character-creation/sessions");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadFromJsonAsync<BeginSessionResponse>();
            return body?.SessionId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to begin character creation session");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetNameAsync(Guid sessionId, string name)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/name");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new SetCreationNameRequest(name));
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set character name on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetClassAsync(Guid sessionId, string className)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/class");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new SetCreationClassRequest(className));
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set class on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(CharacterDto? Character, AppError? Error)> FinalizeAsync(
        Guid sessionId, FinalizeCreationSessionRequest request)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"api/character-creation/sessions/{sessionId}/finalize");
            req.Headers.Authorization = BearerHeader;
            req.Content = JsonContent.Create(request);
            var response = await http.SendAsync(req);

            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<CharacterDto>(), null);

            var serverMsg = await HttpResponseHelper.ExtractServerMessageAsync(response);
            return (null, new AppError(serverMsg ?? "Failed to create character."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to finalize session {SessionId}", sessionId);
            return (null, new AppError("Network error. Please check your connection."));
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetSpeciesAsync(Guid sessionId, string speciesSlug)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/species");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new SetCreationSpeciesRequest(speciesSlug));
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set species on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetBackgroundAsync(Guid sessionId, string backgroundId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/background");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new SetCreationBackgroundRequest(backgroundId));
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set background on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetAttributesAsync(Guid sessionId, Dictionary<string, int> allocations)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/attributes");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new SetCreationAttributesRequest(allocations));
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set attributes on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetEquipmentPreferencesAsync(Guid sessionId, SetCreationEquipmentPreferencesRequest preferences)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/equipment");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(preferences);
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set equipment preferences on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetLocationAsync(Guid sessionId, string locationId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch,
                $"api/character-creation/sessions/{sessionId}/location");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new SetCreationLocationRequest(locationId));
            var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set location on session {SessionId}", sessionId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task AbandonAsync(Guid sessionId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete,
                $"api/character-creation/sessions/{sessionId}");
            request.Headers.Authorization = BearerHeader;
            await http.SendAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to abandon session {SessionId}", sessionId);
        }
    }

    /// <inheritdoc />
    public async Task<CharacterPreviewDto?> GetPreviewAsync(Guid sessionId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/character-creation/sessions/{sessionId}/preview");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<CharacterPreviewDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get preview for session {SessionId}", sessionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Available, string? Error)> CheckNameAvailabilityAsync(string name)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/character-creation/sessions/check-name?name={Uri.EscapeDataString(name)}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<CheckNameAvailabilityResponse>();
            return body is null ? (true, null) : (body.Available, body.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check name availability for name {Name}", name);
            return (true, null);
        }
    }
}
