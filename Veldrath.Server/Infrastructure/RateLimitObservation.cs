using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Veldrath.Server.Infrastructure;

/// <summary>
/// A point-in-time, identifier-free summary of one rate-limit observation window.
/// </summary>
/// <remarks>
/// The snapshot deliberately carries no account identifiers, addresses or message content: per-account counts are
/// reduced to the number of distinct accounts and the peak account's share, so a snapshot can be logged or attached
/// to a report without exporting anything that identifies a player.
/// </remarks>
/// <param name="StartedAt">UTC start of the window the counts belong to.</param>
/// <param name="Window">Length of the window.</param>
/// <param name="RejectionTotal">Number of requests the limiter rejected with 429 in the window.</param>
/// <param name="RejectionsByPolicy">Rejections per named rate-limit policy. The policy is the reporting unit; the caller's partition key is never recorded.</param>
/// <param name="RejectionsByRoute">Rejections per route template, with hub negotiate and transport split.</param>
/// <param name="RequestTotal">Eligible HTTP requests seen in the window, rejected ones included.</param>
/// <param name="RequestsByPolicy">Eligible HTTP requests per named rate-limit policy.</param>
/// <param name="RequestingAccounts">Distinct authenticated accounts that made an eligible request.</param>
/// <param name="PeakAccountRequests">Largest number of eligible requests made by a single account.</param>
/// <param name="PeakAccountRequestShare">That largest count as a share of <paramref name="RequestTotal"/>.</param>
/// <param name="InvocationTotal">Hub method invocations attempted in the window.</param>
/// <param name="InvocationsByHandler">Invocation attempts per hub method name.</param>
/// <param name="InvokingAccounts">Distinct authenticated accounts that invoked a hub method.</param>
/// <param name="PeakAccountInvocations">Largest number of invocations made by a single account.</param>
/// <param name="PeakAccountInvocationShare">That largest count as a share of <paramref name="InvocationTotal"/>.</param>
/// <param name="OverflowRequests">Requests from accounts left uncounted because the per-window account bound was reached.</param>
public sealed record RateLimitObservationSnapshot(
    DateTimeOffset StartedAt,
    TimeSpan Window,
    long RejectionTotal,
    IReadOnlyDictionary<string, long> RejectionsByPolicy,
    IReadOnlyDictionary<string, long> RejectionsByRoute,
    long RequestTotal,
    IReadOnlyDictionary<string, long> RequestsByPolicy,
    int RequestingAccounts,
    long PeakAccountRequests,
    double PeakAccountRequestShare,
    long InvocationTotal,
    IReadOnlyDictionary<string, long> InvocationsByHandler,
    int InvokingAccounts,
    long PeakAccountInvocations,
    double PeakAccountInvocationShare,
    long OverflowRequests);

/// <summary>
/// Opt-in, bounded measurement of the server's named rate-limit policies and of SignalR hub invocations.
/// </summary>
/// <remarks>
/// <para>
/// The limiter itself is untouched: this type only counts what already happened. It is disabled unless
/// <c>RateLimit:Observe</c> is <see langword="true"/>, so shipping it changes no permit, window, policy
/// assignment, partition or rejection.
/// </para>
/// <para>
/// Two different things are measured, and they must not be conflated. The named policies
/// (<c>foundry-writes</c>, <c>admin-actions</c>, <c>hub-commands</c>) are HTTP middleware, and each is
/// partitioned per caller by <see cref="RateLimitPartitionKey"/>, so the permits one account spends are its
/// own. <c>hub-commands</c> meters negotiate and transport HTTP requests — including a long-polling or SSE
/// fallback — but not individual SignalR invocations, which travel inside an upgraded WebSocket frame.
/// Invocation attempts are therefore counted separately, per account and handler name.
/// </para>
/// <para>
/// Counts are aggregated per fixed window and per-account maps are bounded, so a busy window cannot grow memory
/// without limit. Account identifiers never leave the process: the snapshot reduces them to distinct counts and a
/// peak share.
/// </para>
/// </remarks>
public sealed class RateLimitObservation
{
    /// <summary>Configuration key that turns measurement on. Off by default.</summary>
    public const string ConfigurationKey = "RateLimit:Observe";

    /// <summary>Most distinct accounts tracked per window before further accounts are only counted in the totals.</summary>
    public const int MaxAccountsPerWindow = 2048;

    private readonly ILogger<RateLimitObservation> _logger;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    private Window _window = new();
    private DateTimeOffset _windowStartedAt;

    /// <summary>Initializes a new instance of the <see cref="RateLimitObservation"/> class.</summary>
    /// <param name="configuration">Configuration root; <c>RateLimit:Observe</c> turns measurement on.</param>
    /// <param name="logger">Logger that receives one structured summary per completed window.</param>
    /// <param name="time">Clock used to roll windows; the system clock when omitted.</param>
    public RateLimitObservation(
        IConfiguration configuration,
        ILogger<RateLimitObservation> logger,
        TimeProvider? time = null)
    {
        Enabled = configuration.GetValue(ConfigurationKey, false);
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _windowStartedAt = _time.GetUtcNow();
    }

    /// <summary>Gets a value indicating whether measurement is enabled for this process.</summary>
    public bool Enabled { get; }

    /// <summary>Gets the named policy the request's endpoint is limited by, or <see langword="null"/>.</summary>
    /// <param name="context">Request being handled.</param>
    /// <returns>The policy name.</returns>
    public static string? PolicyFor(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

    /// <summary>Gets a stable, non-identifying label for the request's route.</summary>
    /// <remarks>
    /// Endpoint route templates are used rather than the concrete path so a value like a character id never reaches
    /// a log line. The two SignalR endpoints share one policy, so they are labelled separately: negotiate is a
    /// one-off HTTP call per connection and transport requests are the fallback channel, and folding them together
    /// would hide which one the bucket is spending its permits on.
    /// </remarks>
    /// <param name="context">Request being handled.</param>
    /// <returns>A route template, or <c>unmatched</c> outside the endpoint pipeline.</returns>
    public static string RouteLabelFor(HttpContext context)
    {
        var template = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (string.IsNullOrEmpty(template))
            return "unmatched";

        if (!template.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
            return template;

        return context.Request.Path.Value?.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase) == true
            ? "/hubs/{hub}/negotiate"
            : "/hubs/{hub}";
    }

    /// <summary>
    /// Resolves the account subject the JWT bearer handler accepted, if the caller carries one.
    /// </summary>
    /// <param name="user">Authenticated principal, or <see langword="null"/>.</param>
    /// <returns>The account identifier, or <see langword="null"/> when the principal carries no GUID subject.</returns>
    internal static Guid? ResolveAccountId(ClaimsPrincipal? user)
    {
        var subject = user?.FindFirst("sub")?.Value
                   ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out var accountId) ? accountId : null;
    }

    /// <summary>Records a request the limiter rejected.</summary>
    /// <param name="policy">Name of the policy that rejected it.</param>
    /// <param name="route">Route template label from <see cref="RouteLabelFor"/>.</param>
    public void RecordRejection(string policy, string route) => Record(window =>
    {
        window.RejectionTotal++;
        Increment(window.RejectionsByPolicy, policy);
        Increment(window.RejectionsByRoute, route);
    });

    /// <summary>Records an eligible HTTP request that reached a named rate-limit policy.</summary>
    /// <param name="policy">Name of the policy the endpoint is limited by.</param>
    /// <param name="accountId">Authenticated account, or <see langword="null"/> for an anonymous request.</param>
    public void RecordRequest(string policy, Guid? accountId) => Record(window =>
    {
        window.RequestTotal++;
        Increment(window.RequestsByPolicy, policy);
        CountAccount(window.RequestsByAccount, window, accountId);
    });

    /// <summary>Records an attempted hub method invocation.</summary>
    /// <param name="accountId">Authenticated account, or <see langword="null"/> when the caller has no subject.</param>
    /// <param name="handler">Hub method name.</param>
    public void RecordInvocation(Guid? accountId, string handler) => Record(window =>
    {
        window.InvocationTotal++;
        Increment(window.InvocationsByHandler, handler);
        CountAccount(window.InvocationsByAccount, window, accountId);
    });

    /// <summary>Returns the current window's summary, rolling the window first when it has elapsed.</summary>
    /// <remarks>
    /// The returned snapshot belongs to the window that starts at <see cref="RateLimitObservationSnapshot.StartedAt"/>.
    /// Once a window rolls, an enabled instance logs one structured summary per completed window, which is how an
    /// operator harvests the measurement without an endpoint or a database.
    /// </remarks>
    /// <returns>The summary of the current (possibly just-rolled) window.</returns>
    public RateLimitObservationSnapshot Snapshot()
    {
        RateLimitObservationSnapshot? completed;
        RateLimitObservationSnapshot current;
        lock (_gate)
        {
            completed = RollIfDue();
            current = _window.ToSnapshot(_windowStartedAt);
        }

        if (completed is not null)
            Log(completed);

        return current;
    }

    private void Record(Action<Window> record)
    {
        if (!Enabled)
            return;

        RateLimitObservationSnapshot? completed;
        lock (_gate)
        {
            completed = RollIfDue();
            record(_window);
        }

        if (completed is not null)
            Log(completed);
    }

    private RateLimitObservationSnapshot? RollIfDue()
    {
        var now = _time.GetUtcNow();
        if (now - _windowStartedAt < WindowLength)
            return null;

        var completed = _window.ToSnapshot(_windowStartedAt);
        _window = new Window();
        _windowStartedAt = now;
        return completed;
    }

    private void Log(RateLimitObservationSnapshot snapshot)
    {
        // Structured, identifier-free fields only: policy names, route templates, handler names and aggregates.
        _logger.LogInformation(
            "Rate-limit observation window {WindowStart:o} ({WindowSeconds}s): " +
            "{RejectionTotal} rejections by policy {RejectionsByPolicy} on routes {RejectionsByRoute}; " +
            "{RequestTotal} eligible HTTP requests {RequestsByPolicy} from {RequestingAccounts} account(s), " +
            "peak account share {PeakAccountRequestShare:P1}; " +
            "{InvocationTotal} hub invocations by handler {InvocationsByHandler} from {InvokingAccounts} account(s), " +
            "peak account share {PeakAccountInvocationShare:P1}; " +
            "{OverflowRequests} request(s) came from accounts beyond the bounded per-window map.",
            snapshot.StartedAt,
            snapshot.Window.TotalSeconds,
            snapshot.RejectionTotal,
            snapshot.RejectionsByPolicy,
            snapshot.RejectionsByRoute,
            snapshot.RequestTotal,
            snapshot.RequestsByPolicy,
            snapshot.RequestingAccounts,
            snapshot.PeakAccountRequestShare,
            snapshot.InvocationTotal,
            snapshot.InvocationsByHandler,
            snapshot.InvokingAccounts,
            snapshot.PeakAccountInvocationShare,
            snapshot.OverflowRequests);
    }

    private static TimeSpan WindowLength => TimeSpan.FromMinutes(1);

    private static void Increment(Dictionary<string, long> counts, string key)
    {
        counts.TryGetValue(key, out var seen);
        counts[key] = seen + 1;
    }

    private static void CountAccount(Dictionary<Guid, long> counts, Window window, Guid? accountId)
    {
        if (accountId is not { } id || id == Guid.Empty)
            return;

        counts.TryGetValue(id, out var seen);
        if (seen > 0 || counts.Count < MaxAccountsPerWindow)
        {
            counts[id] = seen + 1;
            return;
        }

        window.OverflowRequests++;
    }

    /// <summary>One fixed window of counts. Only mutated while the owner's lock is held.</summary>
    private sealed class Window
    {
        public Dictionary<string, long> RejectionsByPolicy { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> RejectionsByRoute { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> RequestsByPolicy { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> InvocationsByHandler { get; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, long> RequestsByAccount { get; } = new();
        public Dictionary<Guid, long> InvocationsByAccount { get; } = new();
        public long RejectionTotal { get; set; }
        public long RequestTotal { get; set; }
        public long InvocationTotal { get; set; }
        public long OverflowRequests { get; set; }

        public RateLimitObservationSnapshot ToSnapshot(DateTimeOffset startedAt)
        {
            var peakRequests = RequestsByAccount.Count == 0 ? 0 : RequestsByAccount.Values.Max();
            var peakInvocations = InvocationsByAccount.Count == 0 ? 0 : InvocationsByAccount.Values.Max();

            return new RateLimitObservationSnapshot(
                startedAt,
                WindowLength,
                RejectionTotal,
                new Dictionary<string, long>(RejectionsByPolicy, StringComparer.Ordinal),
                new Dictionary<string, long>(RejectionsByRoute, StringComparer.Ordinal),
                RequestTotal,
                new Dictionary<string, long>(RequestsByPolicy, StringComparer.Ordinal),
                RequestsByAccount.Count,
                peakRequests,
                RequestTotal == 0 ? 0 : (double)peakRequests / RequestTotal,
                InvocationTotal,
                new Dictionary<string, long>(InvocationsByHandler, StringComparer.Ordinal),
                InvocationsByAccount.Count,
                peakInvocations,
                InvocationTotal == 0 ? 0 : (double)peakInvocations / InvocationTotal,
                OverflowRequests);
        }
    }
}

/// <summary>
/// Builds the rate-limit partition key for one request, so each caller gets its own permit bucket.
/// </summary>
/// <remarks>
/// <para>
/// An authenticated caller is keyed on its account subject. One account therefore cannot spend another
/// account's permits, and a proxy hop or a second device does not multiply that account's allowance. A request
/// without a usable subject falls back to the client address — <c>X-Forwarded-For</c> first, then the
/// connection's remote address — because one bucket shared by every anonymous caller would let one caller
/// starve the rest.
/// </para>
/// <para>
/// The forwarded header is read from the right: a proxy appends the address it observed, so the right-most
/// parseable entry is the hop that actually reached this process, while entries to its left are client-supplied
/// and can be spoofed.
/// </para>
/// <para>
/// The value is a partition key and is never logged or exported. It is one of <c>account:{guid}</c> or
/// <c>address:{ip}</c>, both bounded in length, so a request cannot inflate the key.
/// </para>
/// </remarks>
public static class RateLimitPartitionKey
{
    /// <summary>Prefix that keeps account buckets from ever colliding with address buckets.</summary>
    public const string AccountPrefix = "account:";

    /// <summary>Prefix for a caller identified by address rather than by account.</summary>
    public const string AddressPrefix = "address:";

    /// <summary>Header through which the reverse proxy reports the client address.</summary>
    public const string ForwardedForHeader = "X-Forwarded-For";

    /// <summary>Gets the partition key for a request.</summary>
    /// <param name="context">Request being partitioned.</param>
    /// <returns>The account key when the principal carries a GUID subject, otherwise the address key.</returns>
    public static string For(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (AccountKey(context.User) is { } account)
            return account;

        var address = ForwardedAddress(context) ?? Normalize(context.Connection.RemoteIpAddress);
        return string.Concat(AddressPrefix, address?.ToString() ?? "unknown");
    }

    /// <summary>Gets the partition key for a principal when no HTTP request of its own is being handled.</summary>
    /// <remarks>
    /// <para>
    /// A hub invocation has no request: the frame carrying it was read from the connection's upgraded
    /// transport, and only the connection's principal is available at that point. That principal carries the
    /// same GUID subject (<c>sub</c> or the mapped <see cref="ClaimTypes.NameIdentifier"/>) the HTTP path
    /// uses, so this overload applies the account rule once instead of restating it in the hub.
    /// </para>
    /// <para>
    /// A principal without a GUID subject falls back to the shared <c>address:unknown</c> key rather than a
    /// per-connection identity: a per-connection bucket would let a caller refresh its allowance by
    /// reconnecting, which is the opposite of what a per-account budget is for. The hub's authorize gate and
    /// the ban guard both refuse such a caller before it can act.
    /// </para>
    /// </remarks>
    /// <param name="user">Principal of the connection, or <see langword="null"/>.</param>
    /// <returns>The account key when the principal carries a GUID subject, otherwise the shared anonymous key.</returns>
    public static string For(ClaimsPrincipal? user) =>
        AccountKey(user) ?? string.Concat(AddressPrefix, "unknown");

    private static string? AccountKey(ClaimsPrincipal? user) =>
        RateLimitObservation.ResolveAccountId(user) is { } accountId
            ? string.Concat(AccountPrefix, accountId.ToString("D"))
            : null;

    private static IPAddress? ForwardedAddress(HttpContext context)
    {
        var values = context.Request.Headers[ForwardedForHeader];
        for (var line = values.Count - 1; line >= 0; line--)
        {
            var value = values[line];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var entries = value.Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var entry = entries.Length - 1; entry >= 0; entry--)
            {
                if (IPAddress.TryParse(entries[entry], out var address))
                    return Normalize(address);
            }
        }

        return null;
    }

    private static IPAddress? Normalize(IPAddress? address)
    {
        if (address is null)
            return null;

        // Drop any scope id, and fold an IPv4-mapped IPv6 address back to IPv4, so the same caller never gets
        // two buckets depending on how the address was formatted.
        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : new IPAddress(address.GetAddressBytes());
    }
}

/// <summary>
/// Records the eligible HTTP requests that reach a named rate-limit policy, before the limiter decides on them.
/// </summary>
/// <remarks>
/// Registered in front of <c>UseRateLimiter</c>. It never short-circuits, rewrites or delays the request: when
/// measurement is off it forwards immediately, and when it is on it adds one dictionary update per eligible request.
/// </remarks>
public sealed class RateLimitObservationMiddleware(RequestDelegate next)
{
    /// <summary>Counts the request when measurement is enabled, then forwards it unchanged.</summary>
    /// <param name="context">Request being handled.</param>
    /// <param name="observation">Process-wide observation store.</param>
    /// <returns>A task that completes when the rest of the pipeline has.</returns>
    public async Task InvokeAsync(HttpContext context, RateLimitObservation observation)
    {
        if (observation.Enabled)
        {
            var policy = RateLimitObservation.PolicyFor(context);
            if (policy is not null)
                observation.RecordRequest(policy, RateLimitObservation.ResolveAccountId(context.User));
        }

        await next(context);
    }
}
