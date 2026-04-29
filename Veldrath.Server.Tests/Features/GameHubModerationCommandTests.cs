using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veldrath.Contracts.Connection;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests that moderation slash-commands return a permission-denied <c>SystemMessage</c>
/// to the caller when the invoking account lacks the required permission.
/// </summary>
public class GameHubModerationCommandTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static ClaimsPrincipal MakeUser(Guid accountId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,        accountId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, "TestUser"),
        };
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    /// <summary>
    /// Creates a <see cref="GameHub"/> wired with fake clients, a pre-populated character in
    /// <c>Context.Items</c>, and the zone group set so that <see cref="GameHub.SendChatMessage"/>
    /// does not short-circuit before reaching <c>HandleChatCommandAsync</c>.
    /// </summary>
    private (GameHub Hub, FakeHubCallerClients Clients, FakeHubCallerContext Ctx)
        CreateHub(Guid accountId, params string[] permissions)
    {
        var db         = _factory.CreateContext();
        var options    = Options.Create(new VersionCompatibilitySettings());
        var modOptions = Options.Create(new ModerationOptions());
        var userManager = new Mock<UserManager<Veldrath.Server.Data.Entities.PlayerAccount>>(
            Mock.Of<IUserStore<Veldrath.Server.Data.Entities.PlayerAccount>>(),
            null!, null!, null!, null!, null!, null!, null!, null!).Object;

        var hub = new GameHub(
            NullLogger<GameHub>.Instance,
            new Veldrath.Server.Data.Repositories.CharacterRepository(db),
            new Veldrath.Server.Data.Repositories.ZoneRepository(db),
            new Veldrath.Server.Data.Repositories.RegionRepository(db),
            new Veldrath.Server.Data.Repositories.PlayerSessionRepository(db),
            new Veldrath.Server.Services.ActiveCharacterTracker(),
            Mock.Of<MediatR.ISender>(),
            new Veldrath.Server.Services.ZoneEntityTracker(),
            Mock.Of<RealmEngine.Shared.Abstractions.ITileMapRepository>(),
            Mock.Of<RealmEngine.Shared.Abstractions.IEnemyRepository>(),
            options,
            userManager,
            modOptions,
            db);

        var clients = new FakeHubCallerClients();
        var ctx     = new FakeHubCallerContext("conn-mod", MakeUser(accountId, permissions));

        // Put the hub in a state where SendChatMessage won't short-circuit
        // before reaching HandleChatCommandAsync.
        ctx.Items["CharacterId"]           = Guid.NewGuid();
        ctx.Items["CharacterName"]         = "TestCharacter";
        ctx.Items["CurrentZoneGroupName"]  = "zone:test-zone";

        hub.Clients = clients;
        hub.Groups  = new FakeGroupManager();
        hub.Context = ctx;

        return (hub, clients, ctx);
    }

    // Helper: assert that the caller received a SystemMessage containing "permission".
    private static void AssertPermissionDenied(FakeHubCallerClients clients) =>
        clients.CallerProxy.SentMessages
               .Should().Contain(m =>
                   m.Method == "SystemMessage" &&
                   m.Args.Length == 1 &&
                   ((string)m.Args[0]!).Contains("permission", StringComparison.OrdinalIgnoreCase));

    // ── /kick ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Kick_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid()); // no permissions

        await hub.SendChatMessage(new ChatMessageHubRequest($"/kick {Guid.NewGuid()}"));

        AssertPermissionDenied(clients);
    }

    // ── /warn ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Warn_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/warn {Guid.NewGuid()} bad behaviour"));

        AssertPermissionDenied(clients);
    }

    // ── /mute ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Mute_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/mute {Guid.NewGuid()} 30 spamming"));

        AssertPermissionDenied(clients);
    }

    // ── /ban ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Ban_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/ban {Guid.NewGuid()} exploiting"));

        AssertPermissionDenied(clients);
    }

    // ── /suspend ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Suspend_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/suspend {Guid.NewGuid()} 24 rule violation"));

        AssertPermissionDenied(clients);
    }

    // ── /tp ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Tp_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/tp {Guid.NewGuid()} zone-crestfall"));

        AssertPermissionDenied(clients);
    }

    // ── /announce ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Announce_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/announce Server going down!"));

        AssertPermissionDenied(clients);
    }

    // ── /give ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Give_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest($"/give {Guid.NewGuid()} iron-sword 1"));

        AssertPermissionDenied(clients);
    }

    // ── /lookup ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChatMessage_Lookup_Without_Permission_Returns_PermissionDenied()
    {
        var (hub, clients, _) = CreateHub(Guid.NewGuid());

        await hub.SendChatMessage(new ChatMessageHubRequest("/lookup SomePlayer"));

        AssertPermissionDenied(clients);
    }
}
