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
    Task<(CharacterDto? Character, string? Error)> CreateCharacterAsync(string name, string className);
    Task<string?> DeleteCharacterAsync(Guid id);
}

// ── Implementation ─────────────────────────────────────────────────────────────
public class HttpCharacterService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpCharacterService> logger) : ICharacterService
{
    private AuthenticationHeaderValue? BearerHeader =>
        tokens.AccessToken is { } t ? new AuthenticationHeaderValue("Bearer", t) : null;

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

    public async Task<(CharacterDto? Character, string? Error)> CreateCharacterAsync(string name, string className)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/characters");
            request.Headers.Authorization = BearerHeader;
            request.Content = JsonContent.Create(new CreateCharacterRequest(name, className));
            var response = await http.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<CharacterDto>(), null);

            return (null, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create character");
            return (null, "Network error.");
        }
    }

    public async Task<string?> DeleteCharacterAsync(Guid id)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/characters/{id}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);

            return response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete character {Id}", id);
            return "Network error.";
        }
    }
}
