using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using System.Text.Json;

namespace Veldrath.Server.Health;

/// <summary>
/// Writes a structured JSON health response compatible with standard tooling.
/// Example output:
/// <code>
/// {
///   "status": "Healthy",
///   "duration": "00:00:00.0421337",
///   "checks": {
///     "database": { "status": "Healthy", "description": "..." },
///     "game-engine": { "status": "Healthy", "description": "..." }
///   }
/// }
/// </code>
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    error = e.Value.Exception?.Message
                })
        };

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return context.Response.WriteAsync(json, Encoding.UTF8);
    }
}
