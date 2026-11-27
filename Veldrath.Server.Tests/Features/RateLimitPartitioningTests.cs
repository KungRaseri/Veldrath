using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Veldrath.Server.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Proves that a named rate-limit policy gives each caller its own bucket, so one account cannot spend
/// another account's permits.
/// </summary>
/// <remarks>
/// The three named policies were fixed-window limiters whose partition was the policy name, which made every
/// caller share one process-wide bucket. With limits pinned low, account A exhausting its allowance rejected
/// account B on B's first request. These tests fail against that code and pass once the policy is partitioned
/// on the authenticated subject (or the forwarded client address for anonymous callers).
/// </remarks>
[Collection(RateLimitPartitioningCollection.Name)]
public sealed class RateLimitPartitioningTests
{
    private readonly PartitionLimitFactory _factory;
    private readonly ITestOutputHelper _output;

    /// <summary>Creates the test class over the low-limit host shared by this collection.</summary>
    /// <param name="factory">Host whose three named policies carry small, known permit limits.</param>
    /// <param name="output">Captures the observed statuses in the test log.</param>
    public RateLimitPartitioningTests(PartitionLimitFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>
    /// Exhausts one account's <c>hub-commands</c> permits and asserts a second account still negotiates.
    /// </summary>
    /// <remarks>
    /// This is the regression the card asks for: a per-policy bucket lets account A reject account B even though
    /// B has sent nothing. A partitioned limiter rejects only A.
    /// </remarks>
    [Fact]
    public async Task HubCommands_OneAccountExhaustingItsPermitsDoesNotRejectAnotherAccount()
    {
        var (tokenA, _) = await _factory.CreateAccountTokenAsync("partition-alpha");
        var (tokenB, _) = await _factory.CreateAccountTokenAsync("partition-beta");

        using var clientA = ClientFor(tokenA, "203.0.113.10");
        using var clientB = ClientFor(tokenB, "198.51.100.20");

        var statusesA = await NegotiateAsync(clientA, PartitionLimitFactory.HubCommandsPerMinute + 1);
        var statusesB = await NegotiateAsync(clientB, 2);

        _output.WriteLine($"account A negotiate statuses: {string.Join(", ", statusesA)}");
        _output.WriteLine($"account B negotiate statuses: {string.Join(", ", statusesB)}");

        statusesA.Take(PartitionLimitFactory.HubCommandsPerMinute)
            .Should().OnlyContain(status => status == HttpStatusCode.OK);
        statusesA.Should().Contain(HttpStatusCode.TooManyRequests, "A spent its own allowance");
        statusesB.Should().OnlyContain(status => status == HttpStatusCode.OK,
            "B's permits are its own, so A's exhaustion cannot appear on B's requests");
    }

    /// <summary>
    /// Asserts one account keeps one bucket even when its requests arrive from different forwarded addresses.
    /// </summary>
    /// <remarks>
    /// Routing through a proxy pool must not multiply an account's allowance, so the subject wins over the address.
    /// </remarks>
    [Fact]
    public async Task HubCommands_SameAccountKeepsOneBucketAcrossForwardedAddresses()
    {
        var (token, _) = await _factory.CreateAccountTokenAsync("partition-gamma");

        using var firstClient = ClientFor(token, "203.0.113.30");
        using var secondClient = ClientFor(token, "203.0.113.31");

        var firstAddress = await NegotiateAsync(firstClient, 2);
        var secondAddress = await NegotiateAsync(secondClient, 2);

        _output.WriteLine($"same account, address one: {string.Join(", ", firstAddress)}");
        _output.WriteLine($"same account, address two: {string.Join(", ", secondAddress)}");

        var allStatuses = firstAddress.Concat(secondAddress).ToList();
        allStatuses.Take(PartitionLimitFactory.HubCommandsPerMinute)
            .Should().OnlyContain(status => status == HttpStatusCode.OK);
        allStatuses.Should().Contain(HttpStatusCode.TooManyRequests,
            "the subject is the partition key, so a new address inherits the same spent allowance");
    }

    /// <summary>
    /// Exhausts one account's <c>foundry-writes</c> permits with malformed bodies and asserts a second account
    /// still reaches the endpoint.
    /// </summary>
    /// <remarks>
    /// A rejected request spends its permit before the handler validates the body, which made a malformed write
    /// from one account a way to lock another account out of the write endpoints entirely.
    /// </remarks>
    [Fact]
    public async Task FoundryWrites_OneAccountExhaustingItsPermitsDoesNotRejectAnotherAccount()
    {
        var (tokenA, _) = await _factory.CreateAccountTokenAsync("partition-delta");
        var (tokenB, _) = await _factory.CreateAccountTokenAsync("partition-epsilon");

        using var clientA = ClientFor(tokenA, "203.0.113.60");
        using var clientB = ClientFor(tokenB, "198.51.100.70");

        var statusesA = new List<HttpStatusCode>();
        for (var i = 0; i < PartitionLimitFactory.FoundryWritesPerMinute + 1; i++)
            statusesA.Add(await PostMalformedSubmissionAsync(clientA));

        var statusesB = new List<HttpStatusCode>
        {
            await PostMalformedSubmissionAsync(clientB),
        };

        _output.WriteLine($"account A foundry statuses: {string.Join(", ", statusesA)}");
        _output.WriteLine($"account B foundry statuses: {string.Join(", ", statusesB)}");

        statusesA.Should().Contain(HttpStatusCode.TooManyRequests, "A spent its own allowance");
        statusesB.Should().NotContain(HttpStatusCode.TooManyRequests,
            "B still has its full allowance; the body is malformed but the limiter must not answer for A");
    }

    private HttpClient ClientFor(string token, string forwardedFor)
    {
        var client = AnonymousClientFor(forwardedFor);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient AnonymousClientFor(string forwardedFor)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static async Task<List<HttpStatusCode>> NegotiateAsync(HttpClient client, int attempts)
    {
        var statuses = new List<HttpStatusCode>(attempts);
        for (var i = 0; i < attempts; i++)
        {
            using var response = await client.PostAsync("/hubs/game/negotiate?negotiateVersion=1", null);
            statuses.Add(response.StatusCode);
        }

        return statuses;
    }

    private static async Task<HttpStatusCode> PostMalformedSubmissionAsync(HttpClient client)
    {
        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/foundry/submissions", content);
        return response.StatusCode;
    }
}

/// <summary>
/// Host whose three named rate-limit policies carry small, known permit limits so one test request can
/// exhaust a bucket.
/// </summary>
/// <remarks>
/// The settings land after the base factory's deliberately huge limits, so these win. Every other setting, the
/// in-memory database and the real startup path are inherited unchanged.
/// </remarks>
public sealed class PartitionLimitFactory : WebAppFactory
{
    /// <summary>Permits per account for <c>hub-commands</c>, low enough to exhaust in a few requests.</summary>
    public const int HubCommandsPerMinute = 3;

    /// <summary>Permits per account for <c>foundry-writes</c>, low enough to exhaust in a few requests.</summary>
    public const int FoundryWritesPerMinute = 2;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimit:HubCommandsPerMinute", HubCommandsPerMinute.ToString());
        builder.UseSetting("RateLimit:FoundryWritesPerMinute", FoundryWritesPerMinute.ToString());
        builder.UseSetting("RateLimit:AdminActionsPerMinute", "2");
    }
}

/// <summary>Shares one low-limit host across the partition assertions and stops them racing on it.</summary>
public static class RateLimitPartitioningCollection
{
    /// <summary>Name of the collection the partitioning tests belong to.</summary>
    public const string Name = "ratelimit-partitioning";
}

/// <summary>Binds <see cref="PartitionLimitFactory"/> to the partitioning collection.</summary>
[CollectionDefinition(RateLimitPartitioningCollection.Name, DisableParallelization = true)]
public sealed class RateLimitPartitioningCollectionDefinition : ICollectionFixture<PartitionLimitFactory>;
