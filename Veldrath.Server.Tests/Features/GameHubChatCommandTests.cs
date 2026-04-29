using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Veldrath.Contracts.Chat;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Tests for <see cref="GameHub.GetChatCommands"/> and role-aware <c>/help</c> text generation.</summary>
public class GameHubChatCommandTests : IDisposable
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

    private (GameHub Hub, FakeHubCallerContext Ctx)
        CreateHub(Guid accountId, params string[] permissions)
    {
        var db = _factory.CreateContext();
        var options    = Options.Create(new VersionCompatibilitySettings());
        var modOptions = Options.Create(new ModerationOptions());
        var userManager = new Mock<UserManager<Veldrath.Server.Data.Entities.PlayerAccount>>(
            Mock.Of<IUserStore<Veldrath.Server.Data.Entities.PlayerAccount>>(), null!, null!, null!, null!, null!, null!, null!, null!).Object;

        var hub = new GameHub(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GameHub>.Instance,
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
        var ctx     = new FakeHubCallerContext("conn-chat", MakeUser(accountId, permissions));

        hub.Clients = clients;
        hub.Groups  = new FakeGroupManager();
        hub.Context = ctx;

        return (hub, ctx);
    }

    // ── GetChatCommands — no character selected ──────────────────────────────

    [Fact]
    public async Task GetChatCommands_Returns_Empty_When_No_Character_Selected()
    {
        var accountId = Guid.NewGuid();
        var (hub, _) = CreateHub(accountId);
        // No CharacterId in Context.Items

        var result = await hub.GetChatCommands();

        result.Should().BeEmpty();
    }

    // ── GetChatCommands — regular player (no permissions) ───────────────────

    [Fact]
    public async Task GetChatCommands_Returns_Only_Player_Commands_For_No_Permission_Caller()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(cmd => cmd.RequiredPermission == null);
    }

    [Fact]
    public async Task GetChatCommands_Player_Commands_Include_Help_Who_Roll_Emote()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        var commands = result.Select(c => c.Command).ToList();
        commands.Should().Contain("help")
                         .And.Contain("who")
                         .And.Contain("roll")
                         .And.Contain("emote")
                         .And.Contain("afk")
                         .And.Contain("report");
    }

    [Fact]
    public async Task GetChatCommands_Does_Not_Include_Kick_For_Player_Without_Permission()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        result.Should().NotContain(cmd => cmd.Command == "kick");
        result.Should().NotContain(cmd => cmd.Command == "mute");
        result.Should().NotContain(cmd => cmd.Command == "warn");
    }

    // ── GetChatCommands — moderator (kick_players) ───────────────────────────

    [Fact]
    public async Task GetChatCommands_Includes_Kick_Warn_Mute_For_KickPlayers_Permission()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId, Permissions.KickPlayers);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        var commands = result.Select(c => c.Command).ToList();
        commands.Should().Contain("kick")
                         .And.Contain("warn")
                         .And.Contain("mute")
                         .And.Contain("sethealth");
    }

    [Fact]
    public async Task GetChatCommands_KickPlayers_Still_Includes_All_Player_Commands()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId, Permissions.KickPlayers);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        result.Should().Contain(cmd => cmd.Command == "roll");
        result.Should().Contain(cmd => cmd.Command == "who");
    }

    // ── GetChatCommands — GameMaster (teleport + give) ───────────────────────

    [Fact]
    public async Task GetChatCommands_Includes_Tp_Summon_Give_For_Appropriate_Permissions()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId,
            Permissions.TeleportPlayers, Permissions.GiveItems);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        var commands = result.Select(c => c.Command).ToList();
        commands.Should().Contain("tp")
                         .And.Contain("summon")
                         .And.Contain("give")
                         .And.Contain("setgold");
    }

    // ── GetChatCommands — RequiredPermission field is populated correctly ─────

    [Fact]
    public async Task GetChatCommands_Kick_Entry_Has_Correct_RequiredPermission()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId, Permissions.KickPlayers);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        var kickEntry = result.Single(c => c.Command == "kick");
        kickEntry.RequiredPermission.Should().Be(Permissions.KickPlayers);
    }

    [Fact]
    public async Task GetChatCommands_Roll_Entry_Has_Null_RequiredPermission()
    {
        var accountId = Guid.NewGuid();
        var (hub, ctx) = CreateHub(accountId);
        ctx.Items["CharacterId"] = Guid.NewGuid();

        var result = await hub.GetChatCommands();

        var rollEntry = result.Single(c => c.Command == "roll");
        rollEntry.RequiredPermission.Should().BeNull();
    }
}
