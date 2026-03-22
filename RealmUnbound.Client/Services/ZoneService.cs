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
    Task<List<ZoneDto>> GetZonesByRegionAsync(string regionId);

    Task<List<RegionDto>> GetRegionsAsync();
    Task<RegionDto?> GetRegionAsync(string regionId);
    Task<List<RegionDto>> GetRegionConnectionsAsync(string regionId);

    Task<List<WorldDto>> GetWorldsAsync();
    Task<WorldDto?> GetWorldAsync(string worldId);
}

// ── Implementation ─────────────────────────────────────────────────────────────
public class HttpZoneService(
    HttpClient http,
    TokenStore tokens,
    ILogger<HttpZoneService> logger) : IZoneService
{
    private AuthenticationHeaderValue? BearerHeader => tokens.BearerHeader();

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

    public async Task<List<ZoneDto>> GetZonesByRegionAsync(string regionId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/zones/by-region/{regionId}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ZoneDto>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch zones for region {RegionId}", regionId);
            return [];
        }
    }

    public async Task<List<RegionDto>> GetRegionsAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/regions");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<RegionDto>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch regions");
            return [];
        }
    }

    public async Task<RegionDto?> GetRegionAsync(string regionId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/regions/{regionId}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<RegionDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch region {RegionId}", regionId);
            return null;
        }
    }

    public async Task<List<RegionDto>> GetRegionConnectionsAsync(string regionId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/regions/{regionId}/connections");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<RegionDto>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch connections for region {RegionId}", regionId);
            return [];
        }
    }

    public async Task<List<WorldDto>> GetWorldsAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/worlds");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<WorldDto>>() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch worlds");
            return [];
        }
    }

    public async Task<WorldDto?> GetWorldAsync(string worldId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/worlds/{worldId}");
            request.Headers.Authorization = BearerHeader;
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<WorldDto>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch world {WorldId}", worldId);
            return null;
        }
    }
}
