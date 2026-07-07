using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealmFoundry.Health;

/// <summary>Health check that verifies connectivity to the Veldrath game server API.</summary>
public class ServerConnectivityHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerConnectivityHealthCheck> _logger;

    /// <summary>Initializes a new instance of the <see cref="ServerConnectivityHealthCheck"/> class.</summary>
    /// <param name="httpClient">An HttpClient configured with the server's base URL.</param>
    /// <param name="logger">Logger instance.</param>
    public ServerConnectivityHealthCheck(HttpClient httpClient, ILogger<ServerConnectivityHealthCheck> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Checks if the Veldrath server API is reachable.</summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A health check result indicating whether the server is reachable.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Server connectivity health check passed");
                return HealthCheckResult.Healthy("Server API is reachable");
            }
            _logger.LogWarning("Server connectivity health check failed: {StatusCode}", response.StatusCode);
            return HealthCheckResult.Unhealthy($"Server API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Server connectivity health check failed");
            return HealthCheckResult.Unhealthy("Server API is unreachable", ex);
        }
    }
}
