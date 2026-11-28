using Microsoft.AspNetCore.SignalR;
using Veldrath.Server.Infrastructure;

namespace Veldrath.Server.Hubs;

/// <summary>
/// Refuses a hub invocation that would exceed the caller's budget for its channel, without closing the
/// caller's connection.
/// </summary>
/// <remarks>
/// <para>
/// The HTTP policies cannot see invocations: they arrive inside the upgraded WebSocket frame, after
/// negotiate and the transport request have already been metered. This filter is the gate for them, and it
/// charges each invocation to one of <see cref="HubInvocationGroup"/>'s channels so a chat flood cannot
/// starve gameplay and a reconnect burst of reads cannot lock a player out of acting.
/// </para>
/// <para>
/// The bucket is keyed with <see cref="RateLimitPartitionKey"/>, the same authenticated-subject rule the
/// named HTTP policies use, so the budget belongs to the account rather than to the connection or the
/// address. A refusal throws <see cref="HubException"/>: the caller's invocation promise rejects with a
/// message, and the connection stays open and usable — unlike the ban guard, which aborts it.
/// </para>
/// </remarks>
public sealed class HubInvocationRateLimitFilter(HubInvocationRateLimiter limiter) : IHubFilter
{
    private const string RefusedMessage = "You are sending commands too quickly. Wait a moment and try again.";

    /// <summary>Charges the invocation to the caller's channel budget before the hub method runs.</summary>
    /// <param name="invocationContext">The invocation being dispatched.</param>
    /// <param name="next">The next filter or the hub method itself.</param>
    /// <returns>Whatever the rest of the invocation pipeline returns.</returns>
    /// <exception cref="HubException">The caller has spent the channel's budget for this window.</exception>
    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var caller = RateLimitPartitionKey.For(invocationContext.Context.User);
        var group = HubInvocationRateLimiter.GroupOf(invocationContext.HubMethodName);

        if (!limiter.TryAcquire(caller, group, out var retryAfter))
            throw new HubException(Refusal(retryAfter));

        return next(invocationContext);
    }

    private static string Refusal(TimeSpan retryAfter)
    {
        if (retryAfter <= TimeSpan.Zero)
            return RefusedMessage;

        var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
        return $"{RefusedMessage} Try again in {seconds} second(s).";
    }
}
