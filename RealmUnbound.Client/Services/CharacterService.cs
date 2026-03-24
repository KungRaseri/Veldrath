using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Characters;

namespace RealmUnbound.Client.Services;

// Interface
public interface ICharacterService
{
    Task<List<CharacterDto>> GetCharactersAsync();
    Task<(CharacterDto? Character, AppError? Error)> CreateCharacterAsync(CreateCharacterRequest request);
    Task<AppError?> DeleteCharacterAsync(Guid id);
}

// Implementation
public class HttpCharacterService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpCharacterService> logger) : ICharacterService
{
    private AuthenticationHeaderValue? BearerHeader => tokens.BearerHeader();
    private static async Task<AppError> ReadErrorAsync(HttpResponseMessage response, string context = "")
    {
        var serverMessage = await HttpResponseHelper.ExtractServerMessageAsync(response);

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

    public async Task<(CharacterDto? Character, AppError? Error)> CreateCharacterAsync(CreateCharacterRequest request)
    {
        try
        {
            using var request2 = new HttpRequestMessage(HttpMethod.Post, "api/characters");
            request2.Headers.Authorization = BearerHeader;
            request2.Content = JsonContent.Create(request);
            var response = await http.SendAsync(request2);

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
