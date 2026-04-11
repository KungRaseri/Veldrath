using Veldrath.Contracts.Zones;
using System.Net.Http.Json;

namespace Veldrath.Discord.Services;

/// <summary>
/// Retrieves live zone data from the public Veldrath.Server API.
/// </summary>
public sealed class ServerStatusService(HttpClient http)
{
    /// <summary>
    /// Fetches all zones and their current online player counts.
    /// Returns <see langword="null"/> when the server is unreachable.
    /// </summary>
    public async Task<List<ZoneDto>?> GetZonesAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<List<ZoneDto>>("/api/zones");
        }
        catch
        {
            return null;
        }
    }
}
