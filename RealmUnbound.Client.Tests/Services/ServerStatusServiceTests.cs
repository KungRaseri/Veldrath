using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Client.Services;

namespace Veldrath.Client.Tests.Services;

/// <summary>
/// A minimal <see cref="IHttpClientFactory"/> stub that creates <see cref="HttpClient"/>
/// instances backed by a shared <see cref="FakeHttpHandler"/> without transferring handler
/// ownership, so the handler is not disposed between requests.
/// </summary>
internal sealed class CountingHttpClientFactory(FakeHttpHandler handler) : IHttpClientFactory
{
    /// <summary>Gets the number of times <see cref="CreateClient"/> has been called.</summary>
    public int CreateCallCount { get; private set; }

    /// <inheritdoc/>
    public HttpClient CreateClient(string name)
    {
        CreateCallCount++;
        // disposeHandler: false — the factory owns the handler lifetime, not the HttpClient.
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") };
    }
}

public class ServerStatusServiceTests : TestBase
{
    private static ServerStatusService MakeSut(FakeHttpHandler handler, TimeSpan? pollInterval = null)
    {
        var factory = new CountingHttpClientFactory(handler);
        var sut = new ServerStatusService(factory, NullLogger<ServerStatusService>.Instance);
        if (pollInterval is { } interval)
        {
            sut.OnlinePollInterval  = interval;
            sut.OfflinePollInterval = interval;
        }
        return sut;
    }

    // CheckAsync

    [Fact]
    public async Task CheckAsync_SetsStatusOnline_WhenServerReturns200()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Healthy", HttpStatusCode.OK));

        await sut.CheckAsync("http://localhost:8080/");

        sut.Status.Should().Be(ServerStatus.Online);
        sut.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_SetsStatusOffline_WhenServerReturnsNonSuccess()
    {
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.ServiceUnavailable));

        await sut.CheckAsync("http://localhost:8080/");

        sut.Status.Should().Be(ServerStatus.Offline);
        sut.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_SetsStatusOffline_WhenNetworkThrows()
    {
        var sut = MakeSut(FakeHttpHandler.Throws(new HttpRequestException("connection refused")));

        await sut.CheckAsync("http://localhost:8080/");

        sut.Status.Should().Be(ServerStatus.Offline);
        sut.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_TransitionsFromOfflineToOnline_WhenServerComesBack()
    {
        // Start offline
        var sut = MakeSut(FakeHttpHandler.Text("", HttpStatusCode.ServiceUnavailable));
        await sut.CheckAsync("http://localhost:8080/");
        sut.Status.Should().Be(ServerStatus.Offline);

        // Server comes back — recreate sut pointing at a 200-handler simulates the next poll
        var sut2 = new ServerStatusService(
            new CountingHttpClientFactory(FakeHttpHandler.Text("Healthy", HttpStatusCode.OK)),
            NullLogger<ServerStatusService>.Instance);

        await sut2.CheckAsync("http://localhost:8080/");

        sut2.Status.Should().Be(ServerStatus.Online);
    }

    // StartPollingAsync

    [Fact]
    public async Task StartPollingAsync_CompletesWithoutException_WhenCancelledImmediately()
    {
        var sut = MakeSut(FakeHttpHandler.Text("Healthy", HttpStatusCode.OK));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.StartPollingAsync(() => "http://localhost:8080/", cts.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartPollingAsync_InvokesCheckRepeatedly_UntilCancelled()
    {
        var secondCheckReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var totalChecks = 0;
        var trackingHandler = new FakeHttpHandler(_ =>
        {
            if (Interlocked.Increment(ref totalChecks) >= 2)
                secondCheckReached.TrySetResult(true);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var factory = new CountingHttpClientFactory(trackingHandler);
        var sut = new ServerStatusService(factory, NullLogger<ServerStatusService>.Instance)
        {
            OnlinePollInterval  = TimeSpan.FromMilliseconds(10),
            OfflinePollInterval = TimeSpan.FromMilliseconds(10),
        };

        using var cts = new CancellationTokenSource();
        var pollingTask = sut.StartPollingAsync(() => "http://localhost:8080/", cts.Token);

        // Wait until at least 2 checks have fired (generous 5 s budget for slow CI).
        var reachedInTime = await Task.WhenAny(secondCheckReached.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        cts.Cancel();
        await pollingTask;

        reachedInTime.Should().Be(secondCheckReached.Task, "polling should invoke CheckAsync at least twice before being cancelled");
    }

    [Fact]
    public async Task StartPollingAsync_UpdatesStatus_BasedOnHealthResponse()
    {
        // The handler alternates: first call returns 503 (offline), subsequent calls 200 (online).
        var callCount = 0;
        var onlineReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeHttpHandler(_ =>
        {
            var n = Interlocked.Increment(ref callCount);
            var response = n == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
            if (n >= 2)
                onlineReached.TrySetResult(true);
            return response;
        });
        var factory = new CountingHttpClientFactory(handler);
        var sut = new ServerStatusService(factory, NullLogger<ServerStatusService>.Instance)
        {
            OnlinePollInterval  = TimeSpan.FromMilliseconds(10),
            OfflinePollInterval = TimeSpan.FromMilliseconds(10),
        };

        using var cts = new CancellationTokenSource();
        var pollingTask = sut.StartPollingAsync(() => "http://localhost:8080/", cts.Token);

        // Wait until the second check (which returns 200) has fired.
        await Task.WhenAny(onlineReached.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        cts.Cancel();
        await pollingTask;

        sut.Status.Should().Be(ServerStatus.Online);
    }
}
