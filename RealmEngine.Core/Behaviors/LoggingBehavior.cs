using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace RealmEngine.Core.Behaviors;

/// <summary>
/// Logs all commands and queries with timing information.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request by logging execution details and timing information.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Executing {RequestName}", requestName);
        logger.LogDebug("Request: {@Request}", request);

        try
        {
            var response = await next();

            stopwatch.Stop();
            logger.LogInformation("Completed {RequestName} in {ElapsedMs}ms",
                requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed {RequestName} after {ElapsedMs}ms",
                requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}