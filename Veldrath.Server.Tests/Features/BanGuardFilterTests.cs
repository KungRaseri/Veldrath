using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Hubs;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Behaviour tests for <see cref="BanGuardFilter"/>: an account banned after its connection was established must be
/// refused before the next handler runs, and an unbanned or expired ban must pass straight through.
/// </summary>
public class BanGuardFilterTests
{
    private static readonly Guid AccountId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    [Fact]
    public async Task Invocation_RefusesAnAccountBannedAfterConnect()
    {
        // The connection predates the ban: connect-time checking never saw it.
        var reader = new FakeAccountAccessReader(Banned());
        var (filter, invocation, clients, context) = Build(reader);
        var invoked = 0;

        var act = () => filter.InvokeMethodAsync(invocation, _ =>
        {
            invoked++;
            return ValueTask.FromResult<object?>(null);
        }).AsTask();

        await act.Should().ThrowAsync<HubException>("a banned account must not reach the handler");
        invoked.Should().Be(0, "the filter refuses before calling the next delegate");

        context.Aborted.Should().BeTrue("the caller's connection is aborted, not left idle");
        clients.Caller.SentMessages.Should().ContainSingle()
            .Which.Method.Should().Be("OnKicked");
        clients.Caller.SentMessages.Single().Args[0].Should().BeOfType<KickedPayload>()
            .Which.Reason.Should().Be("This account is not permitted to use the game.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invocation_AllowsUnbannedAndExpiredBans(bool expired)
    {
        var snapshot = expired
            ? Banned(DateTimeOffset.UtcNow.AddMinutes(-1))
            : new AccountAccessSnapshot(AccountId, "player", false, null, [], []);
        var (filter, invocation, clients, context) = Build(new FakeAccountAccessReader(snapshot));
        var invoked = 0;
        var sentinel = new object();

        var result = await filter.InvokeMethodAsync(invocation, _ =>
        {
            invoked++;
            return ValueTask.FromResult<object?>(sentinel);
        });

        invoked.Should().Be(1);
        result.Should().BeSameAs(sentinel);
        context.Aborted.Should().BeFalse();
        clients.EveryMessage.Should().BeEmpty("an allowed invocation sends no kick");
    }

    [Fact]
    public async Task Invocation_RefusesWhenTheAccountCannotBeRead()
    {
        // A guard that cannot establish the state must not admit the call.
        var (filter, invocation, _, context) = Build(new FakeAccountAccessReader(null));
        var invoked = 0;

        var act = () => filter.InvokeMethodAsync(invocation, _ =>
        {
            invoked++;
            return ValueTask.FromResult<object?>(null);
        }).AsTask();

        await act.Should().ThrowAsync<HubException>();
        invoked.Should().Be(0);
        context.Aborted.Should().BeFalse("a read failure denies the call without assuming a ban");
    }

    private static AccountAccessSnapshot Banned(DateTimeOffset? until = null) =>
        new(AccountId, "player", true, until, [], []);

    private static (
        BanGuardFilter Filter,
        HubInvocationContext Invocation,
        RecordingHubClients Clients,
        RecordingHubCallerContext Context) Build(IAccountAccessReader reader)
    {
        var hub = new BanGuardTestHub();
        var clients = new RecordingHubClients();
        var context = new RecordingHubCallerContext(AccountId);
        hub.Clients = clients;
        hub.Context = context;

        var services = new ServiceCollection()
            .AddSingleton(reader)
            .BuildServiceProvider();

        var invocation = new HubInvocationContext(
            context,
            services,
            hub,
            typeof(BanGuardTestHub).GetMethod(nameof(BanGuardTestHub.Handle))!,
            []);

        return (new BanGuardFilter(NullLogger<BanGuardFilter>.Instance), invocation, clients, context);
    }

    /// <summary>Minimal hub so a <see cref="HubInvocationContext"/> can name a real method.</summary>
    private sealed class BanGuardTestHub : Hub
    {
        /// <summary>The method a refused invocation must never reach.</summary>
        /// <returns>A completed task.</returns>
        public Task Handle() => Task.CompletedTask;
    }

    /// <summary>Caller context that remembers the abort the guard must request.</summary>
    private sealed class RecordingHubCallerContext : HubCallerContext
    {
        private readonly Dictionary<object, object?> _items = new();

        public RecordingHubCallerContext(Guid accountId)
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", accountId.ToString())], "Bearer"));
        }

        public bool Aborted { get; private set; }

        public override string ConnectionId => "conn-ban-guard";

        public override string? UserIdentifier => AccountId.ToString();

        public override ClaimsPrincipal? User { get; }

        public override IDictionary<object, object?> Items => _items;

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => Aborted = true;
    }

    /// <summary>Account store returning a fixed snapshot; <see langword="null"/> means the read failed.</summary>
    private sealed class FakeAccountAccessReader(AccountAccessSnapshot? snapshot) : IAccountAccessReader
    {
        public Task<AccountAccessSnapshot?> EnsureAccountAsync(
            Guid accountId,
            string? username,
            string? email,
            bool emailVerified,
            CancellationToken ct = default) => Task.FromResult(snapshot);

        public Task EvictAsync(Guid accountId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
