using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Hubs;
using Veldrath.Server.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Focused tests for the opt-in rate-limit observation: what it measures, that policy rejections and hub
/// invocations stay separate, and that switching it off leaves a request and an invocation untouched.
/// </summary>
public class RateLimitObservationTests
{
    private static readonly Guid AccountA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid AccountB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private static RateLimitObservation Observation(bool enabled, TestTimeProvider? time = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [RateLimitObservation.ConfigurationKey] = enabled ? "true" : "false",
            })
            .Build();

        return new RateLimitObservation(
            configuration, NullLogger<RateLimitObservation>.Instance, time ?? new TestTimeProvider());
    }

    [Fact]
    public void Observation_SeparatesPolicyRejectionsFromHubInvocations()
    {
        var time = new TestTimeProvider();
        var observation = Observation(enabled: true, time);

        // The HTTP side: two negotiate rejections and one admin write rejection, plus the requests that reached
        // the policies (rejections are a subset of attempts).
        observation.RecordRejection("hub-commands", "/hubs/{hub}/negotiate");
        observation.RecordRejection("hub-commands", "/hubs/{hub}/negotiate");
        observation.RecordRejection("admin-actions", "/api/admin/players/ban");
        observation.RecordRequest("hub-commands", AccountA);
        observation.RecordRequest("hub-commands", AccountA);
        observation.RecordRequest("hub-commands", AccountA);
        observation.RecordRequest("hub-commands", AccountB);
        observation.RecordRequest("admin-actions", AccountB);

        // The invocation side: the same two accounts calling hub methods, which no HTTP policy sees.
        for (var i = 0; i < 5; i++)
            observation.RecordInvocation(AccountA, "SendChatMessage");
        observation.RecordInvocation(AccountA, "Ping");
        observation.RecordInvocation(AccountB, "SendChatMessage");

        var snapshot = observation.Snapshot();

        snapshot.RejectionTotal.Should().Be(3, "one rejection per limiter refusal");
        snapshot.RejectionsByPolicy.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["hub-commands"] = 2,
            ["admin-actions"] = 1,
        });
        snapshot.RejectionsByRoute.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["/hubs/{hub}/negotiate"] = 2,
            ["/api/admin/players/ban"] = 1,
        }, "negotiate is labelled apart from the transport endpoint that shares its policy");

        snapshot.RequestTotal.Should().Be(5, "requests counted include the ones the limiter rejected");
        snapshot.RequestsByPolicy.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["hub-commands"] = 4,
            ["admin-actions"] = 1,
        });
        snapshot.RequestingAccounts.Should().Be(2);
        snapshot.PeakAccountRequests.Should().Be(3);
        snapshot.PeakAccountRequestShare.Should().BeApproximately(3d / 5d, 0.0001);

        snapshot.InvocationTotal.Should().Be(7, "invocations are counted separately from HTTP transport");
        snapshot.InvocationsByHandler.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["SendChatMessage"] = 6,
            ["Ping"] = 1,
        });
        snapshot.InvokingAccounts.Should().Be(2);
        snapshot.PeakAccountInvocations.Should().Be(6);
        snapshot.PeakAccountInvocationShare.Should().BeApproximately(6d / 7d, 0.0001);

        snapshot.StartedAt.Should().Be(time.Now, "the snapshot names the window it belongs to");
        snapshot.Window.Should().Be(TimeSpan.FromMinutes(1));

        var exported = JsonSerializer.Serialize(snapshot);
        exported.Should().NotContain(AccountA.ToString(), "no raw account identifier may be exported")
            .And.NotContain(AccountB.ToString());
    }

    [Fact]
    public async Task Observation_WhenDisabledLeavesTheRequestAndInvocationUnchanged()
    {
        var observation = Observation(enabled: false);
        var recordedBefore = observation.Snapshot();
        recordedBefore.RequestTotal.Should().Be(0);

        // The request path: the middleware must forward exactly once and touch nothing on the response.
        var statusAtNext = 0;
        var middleware = new RateLimitObservationMiddleware(context =>
        {
            statusAtNext = context.Response.StatusCode;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Response.StatusCode = StatusCodes.Status418ImATeapot;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", AccountA.ToString())], "Bearer"));
        httpContext.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/admin/players/ban"),
            0,
            new EndpointMetadataCollection(
                new Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute("admin-actions")),
            "admin ban"));

        await middleware.InvokeAsync(httpContext, observation);

        statusAtNext.Should().Be(StatusCodes.Status418ImATeapot, "the handler runs and the response is untouched");

        // The invocation path: the filter must call the next delegate once with the same context.
        var invoked = 0;
        var filter = new RateLimitObservationFilter(observation);
        var invocation = new HubInvocationContext(
            new FakeHubCallerContext("conn-1", new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", AccountA.ToString())], "Bearer"))),
            new ServiceCollection().BuildServiceProvider(),
            new ObservingTestHub(),
            typeof(ObservingTestHub).GetMethod(nameof(ObservingTestHub.Count))!,
            []);
        var sentinel = new object();

        var result = await filter.InvokeMethodAsync(invocation, _ =>
        {
            invoked++;
            return ValueTask.FromResult<object?>(sentinel);
        });

        invoked.Should().Be(1, "a disabled filter is a pass-through, not a gate");
        result.Should().BeSameAs(sentinel);

        var recordedAfter = observation.Snapshot();
        recordedAfter.RequestTotal.Should().Be(0, "a disabled observer records no HTTP attempt");
        recordedAfter.InvocationTotal.Should().Be(0, "a disabled observer records no invocation");
        recordedAfter.RejectionTotal.Should().Be(0);
    }

    /// <summary>A hub with a callable method, so a <see cref="HubInvocationContext"/> can be built.</summary>
    private sealed class ObservingTestHub : Hub
    {
        /// <summary>Counts nothing; it only has to be the method the context names.</summary>
        /// <returns>A completed task.</returns>
        public Task Count() => Task.CompletedTask;
    }

    /// <summary>A clock the test moves by hand so window boundaries are deterministic.</summary>
    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
