using Microsoft.AspNetCore.SignalR;
using Veldrath.Server.Infrastructure;

namespace Veldrath.Server.Hubs;

/// <summary>
/// Counts SignalR hub invocations for <see cref="RateLimitObservation"/> without altering how they run.
/// </summary>
/// <remarks>
/// SignalR's own HTTP limiter (<c>hub-commands</c>) meters the negotiate and transport requests that carry the
/// connection, not the invocations inside an upgraded WebSocket. This filter is the only place an individual
/// invocation is visible, so it records the handler name and the authenticated account and then forwards exactly
/// once. It never short-circuits, throws or reorders an invocation, and when measurement is off it is a pure
/// pass-through.
/// </remarks>
public sealed class RateLimitObservationFilter(RateLimitObservation observation) : IHubFilter
{
    /// <summary>Records the invocation attempt when measurement is enabled, then invokes the hub method.</summary>
    /// <param name="invocationContext">The invocation being dispatched.</param>
    /// <param name="next">The next filter or the hub method itself.</param>
    /// <returns>Whatever the rest of the invocation pipeline returns.</returns>
    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (observation.Enabled)
        {
            observation.RecordInvocation(
                RateLimitObservation.ResolveAccountId(invocationContext.Context.User),
                invocationContext.HubMethodName);
        }

        return next(invocationContext);
    }
}
