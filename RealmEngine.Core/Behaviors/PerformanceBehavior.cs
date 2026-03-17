using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace RealmEngine.Core.Behaviors;

/// <summary>
/// Logs warnings for slow commands/queries (>500ms).
/// </summary>
public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int SlowThresholdMs = 500;

    /// <summary>
    /// Handles the request and logs a warning if execution exceeds the slow threshold.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowThresholdMs)
        {
            logger.LogWarning("Slow request detected: {RequestName} took {ElapsedMs}ms",
                typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}