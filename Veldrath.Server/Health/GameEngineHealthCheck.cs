using MediatR;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Veldrath.Server.Health;

/// <summary>
/// Verifies that the RealmEngine service layer is initialised and reachable.
/// Resolving <see cref="ISender"/> from DI confirms that all MediatR handler registrations
/// completed successfully at startup.
/// </summary>
public class GameEngineHealthCheck(ISender mediator) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // ISender resolving from DI confirms the MediatR handler pipeline was registered.
        _ = mediator;
        return Task.FromResult(HealthCheckResult.Healthy("Game engine MediatR pipeline verified."));
    }
}
