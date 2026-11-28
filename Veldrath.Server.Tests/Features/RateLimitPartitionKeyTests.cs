using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Veldrath.Server.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Host-free checks of the rate-limit partition key: which claim wins, and what a request without a usable
/// subject falls back to.
/// </summary>
/// <remarks>
/// The address branch cannot be reached end to end through the three named policies today: every endpoint that
/// carries one requires authentication, and authorization rejects an anonymous request before the limiter runs,
/// while a token whose subject is not a GUID fails authentication. It is still asserted here because the
/// partitioner is the thing that must not silently collapse back to one shared bucket.
/// </remarks>
public class RateLimitPartitionKeyTests
{
    [Fact]
    public void For_AuthenticatedCallerUsesTheSubjectClaim()
    {
        var accountId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var context = Context(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", accountId.ToString())], "Bearer")));

        RateLimitPartitionKey.For(context).Should().Be($"account:{accountId:D}");
    }

    [Fact]
    public void For_AuthenticatedCallerAcceptsTheMappedNameIdentifierClaim()
    {
        var accountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var context = Context(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "Bearer")));

        RateLimitPartitionKey.For(context).Should().Be($"account:{accountId:D}");
    }

    [Fact]
    public void For_AuthenticatedCallerIgnoresTheForwardedAddress()
    {
        var accountId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var context = Context(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", accountId.ToString())], "Bearer")),
            forwardedFor: "203.0.113.7",
            remoteAddress: IPAddress.Parse("192.0.2.9"));

        RateLimitPartitionKey.For(context).Should().Be($"account:{accountId:D}",
            "the subject is the partition key, so a proxy hop cannot multiply one account's allowance");
    }

    [Fact]
    public void For_AnonymousCallerUsesTheForwardedAddress()
    {
        var context = Context(
            new ClaimsPrincipal(new ClaimsIdentity()),
            forwardedFor: "203.0.113.7",
            remoteAddress: IPAddress.Parse("127.0.0.1"));

        RateLimitPartitionKey.For(context).Should().Be("address:203.0.113.7");
    }

    [Fact]
    public void For_AnonymousCallerUsesTheAddressTheNearestProxyAppended()
    {
        // Anything left of the right-most entry is a client-supplied claim and can be spoofed.
        var context = Context(
            new ClaimsPrincipal(new ClaimsIdentity()),
            forwardedFor: "1.1.1.1, 203.0.113.8");

        RateLimitPartitionKey.For(context).Should().Be("address:203.0.113.8");
    }

    [Fact]
    public void For_AnonymousCallerWithoutForwardedAddressFallsBackToTheConnectionAddress()
    {
        var context = Context(new ClaimsPrincipal(new ClaimsIdentity()),
            remoteAddress: IPAddress.Parse("198.51.100.9"));

        RateLimitPartitionKey.For(context).Should().Be("address:198.51.100.9");
    }

    [Fact]
    public void For_NormalizesAnIpv4MappedAddress()
    {
        var context = Context(new ClaimsPrincipal(new ClaimsIdentity()),
            remoteAddress: IPAddress.Parse("::ffff:198.51.100.9"));

        RateLimitPartitionKey.For(context).Should().Be("address:198.51.100.9",
            "the same caller must not get two buckets depending on how the address was formatted");
    }

    [Fact]
    public void For_AnonymousCallerWithNoAddressSharesOneFallbackBucket()
    {
        var context = Context(new ClaimsPrincipal(new ClaimsIdentity()));

        RateLimitPartitionKey.For(context).Should().Be("address:unknown");
    }

    [Fact]
    public void ForPrincipal_UsesTheSameAccountRuleWithoutARequest()
    {
        var accountId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", accountId.ToString())], "Bearer"));

        RateLimitPartitionKey.For(user).Should().Be($"account:{accountId:D}",
            "a hub invocation has no request, and its budget must belong to the same account the HTTP policies meter");
    }

    [Fact]
    public void ForPrincipal_WithoutAGuidSubjectFallsBackToTheSharedAnonymousKey()
    {
        // A per-connection bucket would let a caller refresh its allowance by reconnecting, so the fallback is
        // deliberately shared: the hub's authorize gate and the ban guard refuse such a caller first.
        RateLimitPartitionKey.For(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "not-a-guid")], "Bearer")))
            .Should().Be("address:unknown");
    }

    private static DefaultHttpContext Context(
        ClaimsPrincipal user,
        string? forwardedFor = null,
        IPAddress? remoteAddress = null)
    {
        var context = new DefaultHttpContext { User = user };
        if (forwardedFor is not null)
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Connection.RemoteIpAddress = remoteAddress;
        return context;
    }
}
