using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealmUnbound.Server.Health;

/// <summary>
/// Verifies that the RealmEngine service layer is initialised and reachable.
/// Expand this check as services are wired up (e.g. catalog cache warm, mediator ping).
/// </summary>
public class GameEngineHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // TODO: validate catalog cache warm, mediator handlers registered, etc.
        return Task.FromResult(HealthCheckResult.Healthy("Game engine is ready."));
    }
}
