using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace Veldrath.Server.Hubs;

/// <summary>
/// The channel a hub method belongs to, so one channel's traffic cannot spend another channel's budget.
/// </summary>
/// <remarks>
/// The split exists because the failure modes are different. A modded client can flood the chat senders
/// without touching combat, and a busy reconnect burst of <c>Get…</c> reads must never be able to lock a
/// player out of acting. Each channel keeps its own per-account fixed-window budget.
/// </remarks>
public enum HubInvocationGroup
{
    /// <summary>Direct chat sends, including the slash-command entry point (<c>SendChatMessage</c>).</summary>
    Chat,

    /// <summary>Read-only hub methods (<c>Get…</c>) and <c>Ping</c>; they carry no game-state change.</summary>
    Query,

    /// <summary>Every method that changes game state; the safe default for a method this type does not know.</summary>
    Action,
}

/// <summary>
/// Enforces a per-account invocation budget for each <see cref="HubInvocationGroup"/>, keyed on the same
/// account rule the HTTP policies use.
/// </summary>
/// <remarks>
/// <para>
/// SignalR's <c>hub-commands</c> policy sees only the negotiate and transport HTTP requests that carry a
/// connection; the invocations themselves travel inside the upgraded WebSocket frame and are invisible to
/// it. This limiter is the gate for those invocations. It keys each bucket on
/// <see cref="Infrastructure.RateLimitPartitionKey"/>, so an account's invocation budget is its own and a
/// proxy hop or a second device does not multiply it.
/// </para>
/// <para>
/// A bucket is one account and one channel for the process lifetime. Accounts are provisioned from Better
/// Auth on first token use and are not self-created, so the map is bounded by the real player base at three
/// small entries per account. A limit configured at zero or below disables that channel rather than
/// rejecting everything.
/// </para>
/// <para>
/// The permit numbers are derived from measurement, not guesswork: see the comment on each default below.
/// </para>
/// </remarks>
public sealed class HubInvocationRateLimiter : IDisposable
{
    /// <summary>Configuration key for the per-minute chat budget of one account.</summary>
    public const string ChatLimitKey = "RateLimit:HubChatInvocationsPerMinute";

    /// <summary>Configuration key for the per-minute read budget of one account.</summary>
    public const string QueryLimitKey = "RateLimit:HubQueryInvocationsPerMinute";

    /// <summary>Configuration key for the per-minute gameplay budget of one account.</summary>
    public const string ActionLimitKey = "RateLimit:HubActionInvocationsPerMinute";

    // Measured on 2026-11-27 with a local harness (not live traffic): one account connected with the web
    // client's own start sequence and replayed the reconnect restore path from web/client/src/lib/game/
    // session.svelte.ts — SelectCharacter, GetParty, GetInventory, EnterZone, GetMapGraph, GetLocationRoster,
    // GetGroundItems, GetLocationNodes, GetLocationChests, GetLocationNpcs, plus GetChatCommands on the first
    // connect. RateLimit:Observe recorded 11 invocations in that one burst: 9 reads and 2 gameplay
    // (SelectCharacter, EnterZone). Chat was 0 — the client never sends chat automatically. A move after that
    // adds one gameplay invocation and three or four reads, so the largest legitimate burst observed is this
    // connect/reconnect one.

    /// <summary>
    /// Default chat budget: 30 sends per account per minute.
    /// </summary>
    /// <remarks>
    /// The measured automatic burst is zero — the client only sends chat when a player types. Thirty is a
    /// generous typing rate (one message every two seconds, sustained for a minute) and still refuses a
    /// flood. A fast exchange of short lines fits easily; the slash-command senders share this budget.
    /// </remarks>
    public const int DefaultChatLimit = 30;

    /// <summary>
    /// Default read budget: 300 invocations per account per minute.
    /// </summary>
    /// <remarks>
    /// The measured reconnect burst is nine reads, and one travel click triggers three or four (the location's
    /// nodes, chests and people, plus the map when it changed). Three hundred is roughly thirty reconnect
    /// cycles a minute, or seventy travels a minute, so no legitimate burst comes close; it caps a read flood
    /// at five queries a second.
    /// </remarks>
    public const int DefaultQueryLimit = 300;

    /// <summary>
    /// Default gameplay budget: 120 invocations per account per minute.
    /// </summary>
    /// <remarks>
    /// The measured reconnect burst is 2 gameplay invocations and a combat turn is one click, which a human
    /// sustains at about one a second. Two per second leaves headroom for fast clicking and movement while
    /// refusing a flood.
    /// </remarks>
    public const int DefaultActionLimit = 120;

    /// <summary>The window every channel's budget is measured over, matching the HTTP policies.</summary>
    public static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<Partition> _limiter;
    private readonly int _chatLimit;
    private readonly int _queryLimit;
    private readonly int _actionLimit;

    /// <summary>Initializes the limiter from configuration, falling back to the measured defaults.</summary>
    /// <param name="configuration">Configuration root that may override any channel's per-minute budget.</param>
    public HubInvocationRateLimiter(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _chatLimit = configuration.GetValue(ChatLimitKey, DefaultChatLimit);
        _queryLimit = configuration.GetValue(QueryLimitKey, DefaultQueryLimit);
        _actionLimit = configuration.GetValue(ActionLimitKey, DefaultActionLimit);

        _limiter = PartitionedRateLimiter.Create<Partition, string>(partition =>
            RateLimitPartition.GetFixedWindowLimiter(
                partition.Key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LimitFor(partition.Group),
                    Window = WindowLength,
                    QueueLimit = 0,
                }));
    }

    /// <summary>Maps a hub method name to the channel whose budget it spends.</summary>
    /// <remarks>
    /// The mapping follows the hub's own naming contract instead of a list that can drift: every direct chat
    /// sender starts with <c>Send</c> (and the slash-command entry point is <c>SendChatMessage</c>), every
    /// read starts with <c>Get</c> (plus <c>Ping</c>), and everything else changes game state. A method
    /// added later therefore lands in the safe default — gameplay — without editing this method.
    /// </remarks>
    /// <param name="hubMethodName">Name of the hub method being invoked.</param>
    /// <returns>The channel the invocation is charged to.</returns>
    public static HubInvocationGroup GroupOf(string hubMethodName)
    {
        ArgumentNullException.ThrowIfNull(hubMethodName);

        if (hubMethodName.StartsWith("Send", StringComparison.Ordinal))
            return HubInvocationGroup.Chat;

        if (hubMethodName.StartsWith("Get", StringComparison.Ordinal) ||
            string.Equals(hubMethodName, "Ping", StringComparison.Ordinal))
            return HubInvocationGroup.Query;

        return HubInvocationGroup.Action;
    }

    /// <summary>Spends one permit from the caller's budget for the channel.</summary>
    /// <param name="callerKey">Partition key from <see cref="Infrastructure.RateLimitPartitionKey"/>.</param>
    /// <param name="group">Channel the invocation belongs to.</param>
    /// <param name="retryAfter">How long until the channel's window resets; zero when unknown or acquired.</param>
    /// <returns><see langword="true"/> when the invocation may run (or the channel is unlimited).</returns>
    public bool TryAcquire(string callerKey, HubInvocationGroup group, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerKey);

        retryAfter = TimeSpan.Zero;
        if (LimitFor(group) <= 0)
            return true;

        using var lease = _limiter.AttemptAcquire(new Partition(callerKey, group));
        if (lease.IsAcquired)
            return true;

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata))
            retryAfter = metadata;

        return false;
    }

    /// <inheritdoc/>
    public void Dispose() => _limiter.Dispose();

    private int LimitFor(HubInvocationGroup group) => group switch
    {
        HubInvocationGroup.Chat => _chatLimit,
        HubInvocationGroup.Query => _queryLimit,
        _ => _actionLimit,
    };

    /// <summary>One account-and-channel pair; <see cref="Key"/> is the bucket identity.</summary>
    private readonly record struct Partition(string CallerKey, HubInvocationGroup Group)
    {
        public string Key => string.Concat(CallerKey, "|", Group.ToString());
    }
}
