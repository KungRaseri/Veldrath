using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Hubs;
using Veldrath.Server.Infrastructure;
using Veldrath.Server.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Proves that hub invocations are budgeted per account and per channel: one account spending its chat
/// allowance cannot refuse another account, and cannot refuse itself on a different channel.
/// </summary>
/// <remarks>
/// The named HTTP policies meter only the negotiate and transport requests that carry a connection; the
/// invocations themselves arrive inside the upgraded frame. Before <c>HubInvocationRateLimitFilter</c>, a
/// modded client could call <c>SendChatMessage</c> without limit. These tests connect two real SignalR
/// clients to a host whose invocation budgets are tiny, so one test can exhaust a channel.
/// </remarks>
[Collection(HubInvocationRateLimitCollection.Name)]
public sealed class HubInvocationRateLimitTests
{
    private readonly HubInvocationLimitFactory _factory;
    private readonly ITestOutputHelper _output;

    /// <summary>Creates the test class over the low-budget host shared by this collection.</summary>
    /// <param name="factory">Host whose hub invocation budgets are small and known.</param>
    /// <param name="output">Captures the observed refusals in the test log.</param>
    public HubInvocationRateLimitTests(HubInvocationLimitFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>
    /// Exhausts one account's chat allowance and asserts a second account's chat invocation still runs.
    /// </summary>
    /// <remarks>
    /// This is the regression the card asks for. With a single shared invocation bucket, A's refusals would
    /// land on B's first send; with a per-account channel bucket, only A is refused, and A's connection stays
    /// open — the refusal rejects one invocation rather than aborting the session.
    /// </remarks>
    [Fact]
    public async Task ChatInvocations_OneAccountExhaustingItsAllowanceDoesNotRefuseAnotherAccount()
    {
        var (tokenA, _) = await _factory.CreateAccountTokenAsync("invocation-alpha");
        var (tokenB, _) = await _factory.CreateAccountTokenAsync("invocation-beta");

        await using var connectionA = await ConnectAsync(tokenA);
        await using var connectionB = await ConnectAsync(tokenB);

        for (var i = 0; i < HubInvocationLimitFactory.ChatInvocationsPerMinute; i++)
            await connectionA.InvokeAsync("SendChatMessage", new ChatMessageHubRequest("hello"));

        var refusal = await Assert.ThrowsAsync<HubException>(
            () => connectionA.InvokeAsync("SendChatMessage", new ChatMessageHubRequest("one too many")));
        _output.WriteLine($"account A refusal: {refusal.Message}");

        refusal.Message.Should().Contain("too quickly", "the limiter, not the chat handler, refused the call");

        await connectionB.InvokeAsync("SendChatMessage", new ChatMessageHubRequest("hello"));

        connectionA.State.Should().Be(HubConnectionState.Connected,
            "a refusal rejects one invocation; it must not disconnect the caller");
    }

    /// <summary>
    /// Exhausts one account's chat allowance and asserts its own read invocation still runs.
    /// </summary>
    /// <remarks>
    /// A chatty chat handler is the finding's example of a caller that could starve gameplay. Channels keep
    /// separate buckets, so spending the entire chat budget leaves the read and gameplay budgets untouched.
    /// </remarks>
    [Fact]
    public async Task ChatInvocations_ExhaustingChatLeavesTheOtherChannelsAvailable()
    {
        var (token, _) = await _factory.CreateAccountTokenAsync("invocation-gamma");

        await using var connection = await ConnectAsync(token);

        for (var i = 0; i < HubInvocationLimitFactory.ChatInvocationsPerMinute; i++)
            await connection.InvokeAsync("SendChatMessage", new ChatMessageHubRequest("hello"));

        await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("SendChatMessage", new ChatMessageHubRequest("one too many")));

        // Ping is a read and has its own bucket, so the chat exhaustion cannot appear here.
        var ping = await connection.InvokeAsync<long>("Ping");
        ping.Should().BeGreaterThan(0, "the read channel kept its own budget");
    }

    /// <summary>
    /// Exhausts one account's read allowance and asserts a second account's read invocation still runs.
    /// </summary>
    /// <remarks>
    /// Mirrors the chat assertion on the channel a reconnect burst spends, so the per-account rule is proven
    /// for reads as well as for chat.
    /// </remarks>
    [Fact]
    public async Task QueryInvocations_OneAccountExhaustingItsAllowanceDoesNotRefuseAnotherAccount()
    {
        var (tokenA, _) = await _factory.CreateAccountTokenAsync("invocation-delta");
        var (tokenB, _) = await _factory.CreateAccountTokenAsync("invocation-epsilon");

        await using var connectionA = await ConnectAsync(tokenA);
        await using var connectionB = await ConnectAsync(tokenB);

        for (var i = 0; i < HubInvocationLimitFactory.QueryInvocationsPerMinute; i++)
            await connectionA.InvokeAsync<long>("Ping");

        await Assert.ThrowsAsync<HubException>(() => connectionA.InvokeAsync<long>("Ping"));

        var pong = await connectionB.InvokeAsync<long>("Ping");
        pong.Should().BeGreaterThan(0, "B's read budget is its own");
    }

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/game"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                // Long polling is the one transport the in-memory test server carries end to end.
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}

/// <summary>
/// Focused tests for the invocation limiter itself: which channel a method is charged to, and that each
/// channel and each account gets its own bucket.
/// </summary>
public sealed class HubInvocationRateLimiterTests
{
    /// <summary>A name that starts with <c>Send</c> is chat; reads and <c>Ping</c> are queries.</summary>
    /// <param name="method">Hub method name.</param>
    /// <param name="expected">Channel the limiter must charge it to.</param>
    [Theory]
    [InlineData("SendChatMessage", HubInvocationGroup.Chat)]
    [InlineData("SendZoneMessage", HubInvocationGroup.Chat)]
    [InlineData("SendGlobalMessage", HubInvocationGroup.Chat)]
    [InlineData("SendWhisper", HubInvocationGroup.Chat)]
    [InlineData("GetInventory", HubInvocationGroup.Query)]
    [InlineData("GetMapGraph", HubInvocationGroup.Query)]
    [InlineData("Ping", HubInvocationGroup.Query)]
    [InlineData("SelectCharacter", HubInvocationGroup.Action)]
    [InlineData("AttackEnemy", HubInvocationGroup.Action)]
    [InlineData("TravelToNode", HubInvocationGroup.Action)]
    [InlineData("SomeFutureHubMethod", HubInvocationGroup.Action)]
    public void GroupOf_ChargesEachMethodToItsChannel(string method, HubInvocationGroup expected) =>
        HubInvocationRateLimiter.GroupOf(method).Should().Be(expected);

    /// <summary>Each channel and each account holds a separate bucket.</summary>
    [Fact]
    public void TryAcquire_SeparatesChannelsAndAccounts()
    {
        using var limiter = new HubInvocationRateLimiter(Configuration(chat: 1, query: 1, action: 1));

        limiter.TryAcquire("account:first", HubInvocationGroup.Chat, out _).Should().BeTrue();
        limiter.TryAcquire("account:first", HubInvocationGroup.Chat, out var retryAfter).Should().BeFalse();
        retryAfter.Should().BeGreaterThan(TimeSpan.Zero, "a refusal tells the caller when the window resets");

        limiter.TryAcquire("account:first", HubInvocationGroup.Query, out _)
            .Should().BeTrue("a spent chat budget leaves the read budget alone");
        limiter.TryAcquire("account:first", HubInvocationGroup.Action, out _)
            .Should().BeTrue("a spent chat budget leaves the gameplay budget alone");
        limiter.TryAcquire("account:second", HubInvocationGroup.Chat, out _)
            .Should().BeTrue("the bucket belongs to the account, not to the process");
    }

    /// <summary>A channel configured at zero or below is unlimited instead of a reject-everything gate.</summary>
    [Fact]
    public void TryAcquire_TreatsANonPositiveLimitAsUnlimited()
    {
        using var limiter = new HubInvocationRateLimiter(Configuration(chat: 0, query: 0, action: 0));

        for (var i = 0; i < 5; i++)
            limiter.TryAcquire("account:any", HubInvocationGroup.Chat, out _).Should().BeTrue();
    }

    private static IConfiguration Configuration(int chat, int query, int action) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HubInvocationRateLimiter.ChatLimitKey] = chat.ToString(),
                [HubInvocationRateLimiter.QueryLimitKey] = query.ToString(),
                [HubInvocationRateLimiter.ActionLimitKey] = action.ToString(),
            })
            .Build();
}

/// <summary>
/// Host whose hub invocation budgets are small, known values so one test can exhaust a channel, and whose
/// observation is on so the counting path stays exercised alongside the limiter.
/// </summary>
/// <remarks>
/// The settings land after the base factory's deliberately huge limits, so these win. Every other setting,
/// the in-memory database and the real startup path are inherited unchanged.
/// </remarks>
public sealed class HubInvocationLimitFactory : WebAppFactory
{
    /// <summary>Chat invocations per account per minute, low enough to exhaust in a few calls.</summary>
    public const int ChatInvocationsPerMinute = 2;

    /// <summary>Read invocations per account per minute, low enough to exhaust in a few calls.</summary>
    public const int QueryInvocationsPerMinute = 3;

    /// <summary>Gameplay invocations per account per minute; no test exhausts it over the wire.</summary>
    public const int ActionInvocationsPerMinute = 2;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting(HubInvocationRateLimiter.ChatLimitKey, ChatInvocationsPerMinute.ToString());
        builder.UseSetting(HubInvocationRateLimiter.QueryLimitKey, QueryInvocationsPerMinute.ToString());
        builder.UseSetting(HubInvocationRateLimiter.ActionLimitKey, ActionInvocationsPerMinute.ToString());
        builder.UseSetting(RateLimitObservation.ConfigurationKey, "true");
    }
}

/// <summary>Shares one low-budget host across the invocation assertions and stops them racing on it.</summary>
public static class HubInvocationRateLimitCollection
{
    /// <summary>Name of the collection the invocation-rate-limit tests belong to.</summary>
    public const string Name = "hub-invocation-rate-limit";
}

/// <summary>Binds <see cref="HubInvocationLimitFactory"/> to the invocation-rate-limit collection.</summary>
[CollectionDefinition(HubInvocationRateLimitCollection.Name, DisableParallelization = true)]
public sealed class HubInvocationRateLimitCollectionDefinition : ICollectionFixture<HubInvocationLimitFactory>;
