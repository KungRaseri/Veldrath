using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RealmUnbound.Contracts.Zones;

namespace RealmUnbound.Client.Services;

// ── Interface ──────────────────────────────────────────────────────────────────
public interface IZoneService
{
    Task<List<ZoneDto>> GetZonesAsync();
    Task<ZoneDto?> GetZoneAsync(string zoneId);
}

// ── Implementation ─────────────────────────────────────────────────────────────
public class HttpZoneService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpZoneService> logger) : IZoneService
{
    private AuthenticationHeaderValue? BearerHeader =>
        tokens.AccessToken is { } t ? new AuthenticationHeaderValue("Bearer", t) : null;

    public async Task<List<ZoneDto>> GetZonesAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/zones");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ZoneDto>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch zones");
            return [];
        }
    }

    public async Task<ZoneDto?> GetZoneAsync(string zoneId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/zones/{zoneId}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ZoneDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch zone {ZoneId}", zoneId);
            return null;
        }
    }
}
