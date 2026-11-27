using Microsoft.AspNetCore.SignalR;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Infrastructure;

namespace Veldrath.Server.Hubs;

/// <summary>
/// Refuses hub invocations from an account that is banned at the moment of the call.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GameHub.OnConnectedAsync"/> already refuses a new connection for an actively banned account, but a
/// connection that was established before the ban keeps working: the moderation writers only send an advisory
/// <c>OnKicked</c> to it. This filter closes that gap for the next invocation in the same server process. It reads
/// the same account snapshot the bearer path uses and applies the same expiry rule, and the writers evict that
/// snapshot after a successful update so the refusal does not wait for the cache to expire.
/// </para>
/// <para>
/// A failed or missing lookup refuses the invocation rather than admitting it. The guarantee is deliberately
/// bounded: it stops further hub work on the next call and aborts the caller's connection, but it does not cancel
/// work already in flight, close an idle socket, or propagate across processes beyond the snapshot's cache lifetime.
/// </para>
/// </remarks>
public sealed class BanGuardFilter(ILogger<BanGuardFilter> logger) : IHubFilter
{
    private const string RefusedMessage = "This account is not permitted to use the game.";

    /// <summary>Refuses an actively banned account before the hub method runs.</summary>
    /// <param name="invocationContext">The invocation being dispatched.</param>
    /// <param name="next">The next filter or the hub method itself.</param>
    /// <returns>Whatever the rest of the invocation pipeline returns.</returns>
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var accountId = RateLimitObservation.ResolveAccountId(invocationContext.Context.User);
        if (accountId is null)
            throw new HubException("Your account could not be verified.");

        AccountAccessSnapshot? snapshot;
        try
        {
            var accounts = invocationContext.ServiceProvider.GetRequiredService<IAccountAccessReader>();
            snapshot = await accounts.EnsureAccountAsync(
                accountId.Value, null, null, false, invocationContext.Context.ConnectionAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The ban guard could not read the account snapshot, so the invocation is refused.");
            throw new HubException("Your account could not be verified.");
        }

        if (snapshot is null)
        {
            logger.LogWarning("The ban guard received no account snapshot, so the invocation is refused.");
            throw new HubException("Your account could not be verified.");
        }

        if (!snapshot.IsActivelyBanned(DateTimeOffset.UtcNow))
            return await next(invocationContext);

        // The same on-the-wire shape the connect-time guard uses, so the client already knows how to leave.
        if (invocationContext.Hub is Hub hub)
            await hub.Clients.Caller.SendAsync("OnKicked", new KickedPayload(RefusedMessage));

        invocationContext.Context.Abort();
        throw new HubException(RefusedMessage);
    }
}
