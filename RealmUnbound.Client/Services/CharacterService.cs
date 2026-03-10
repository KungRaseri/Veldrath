using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace RealmUnbound.Client.Services;

// ── DTOs (mirror server CharacterDtos) ─────────────────────────────────────────
public record CharacterDto(
    Guid Id,
    int SlotIndex,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    DateTimeOffset? LastPlayedAt,
    string CurrentZoneId = "starting-zone");

public record CreateCharacterRequest(string Name, string ClassName);

// ── Interface ──────────────────────────────────────────────────────────────────
public interface ICharacterService
{
    Task<List<CharacterDto>> GetCharactersAsync();
    Task<(CharacterDto? Character, AppError? Error)> CreateCharacterAsync(string name, string className);
    Task<AppError?> DeleteCharacterAsync(Guid id);
}

// ── Implementation ─────────────────────────────────────────────────────────────
public class HttpCharacterService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpCharacterService> logger) : ICharacterService
{
    private AuthenticationHeaderValue? BearerHeader =>
        tokens.AccessToken is { } t ? new AuthenticationHeaderValue("Bearer", t) : null;
    private static async Task<AppError> ReadErrorAsync(HttpResponseMessage response, string context = "")
    {
        string? serverMessage = null;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            if (body.TryGetProperty("error", out var e))
                serverMessage = e.GetString();
        }
        catch { }

        if (serverMessage is null)
        {
            try { serverMessage = await response.Content.ReadAsStringAsync(); }
            catch { }
            if (string.IsNullOrWhiteSpace(serverMessage))
                serverMessage = null;
        }

        var statusCode = (int)response.StatusCode;
        var friendly = (context, statusCode) switch
        {
            ("create", 409)                         => "That character name is already taken. Please choose a different name.",
            ("create", 400) when serverMessage is not null => serverMessage,
            ("create", 401) or ("create", 403)     => "Your session has expired. Please log in again.",
            ("delete", 403)                         => "You do not have permission to delete this character.",
            ("delete", 404)                         => "Character not found.",
            (_,        500) or (_, 503)             => "The server encountered an error. Please try again later.",
            _                                       => serverMessage ?? "An error occurred. Please try again."
        };

        var details = serverMessage != null && serverMessage != friendly ? serverMessage : null;
        return new AppError(friendly, details);
    }
    public async Task<List<CharacterDto>> GetCharactersAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/characters");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CharacterDto>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch characters");
            return [];
        }
    }

    public async Task<(CharacterDto? Character, AppError? Error)> CreateCharacterAsync(string name, string className)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/characters");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new CreateCharacterRequest(name, className));
            var response = await http.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<CharacterDto>(), null);

            return (null, await ReadErrorAsync(response, "create"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create character");
            return (null, new AppError("Network error. Please check your connection."));
        }
    }

    public async Task<AppError?> DeleteCharacterAsync(Guid id)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/characters/{id}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);

            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, "delete");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete character {Id}", id);
            return new AppError("Network error. Please check your connection.");
        }
    }
}
