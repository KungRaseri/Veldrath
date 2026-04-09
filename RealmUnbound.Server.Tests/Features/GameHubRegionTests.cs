using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmEngine.Core.Generators.Modern;
using RealmUnbound.Server.Features.Characters.Combat;
using RealmUnbound.Server.Hubs;
using RealmUnbound.Server.Services;
using RealmUnbound.Server.Settings;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Integration tests for region-map methods on <see cref="GameHub"/>.</summary>
public class GameHubRegionTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private static ClaimsPrincipal MakeUser(Guid accountId, string username = "TestUser") =>
        new(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub,        accountId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
        ]));

    private static async Task<Guid> SeedAccountAsync(ApplicationDbContext db)
    {
        var name    = $"User_{Guid.NewGuid():N}";
        var account = new PlayerAccount { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, Guid accountId)
    {
        var c = new Character
        {
            AccountId = accountId,
            Name      = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private (GameHub Hub, FakeHubCallerClients Clients, FakeGroupManager Groups, FakeHubCallerContext Ctx)
        CreateHub(ApplicationDbContext db, Guid accountId, string connId = "conn-1", ISender? mediator = null)
    {
        var options = Options.Create(new VersionCompatibilitySettings());
        var hub     = new GameHub(
            NullLogger<GameHub>.Instance,
            new CharacterRepository(db),
            new ZoneRepository(db),
            new RegionRepository(db),
            new PlayerSessionRepository(db),
            new ActiveCharacterTracker(),
            mediator ?? Mock.Of<ISender>(),
            new ZoneEntityTracker(),
            Mock.Of<ITileMapRepository>(),
            Mock.Of<IEnemyRepository>(),
            options);

        var clients = new FakeHubCallerClients();
        var groups  = new FakeGroupManager();
        var ctx     = new FakeHubCallerContext(connId, MakeUser(accountId));

        hub.Clients = clients;
        hub.Groups  = groups;
        hub.Context = ctx;

        return (hub, clients, groups, ctx);
    }

    // ── SelectCharacter — region-map bootstrap ────────────────────────────────

    [Fact]
    public async Task SelectCharacter_Creates_PlayerSession_With_Null_ZoneId()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, _, _, _) = CreateHub(db, accountId, "conn-region");

        await hub.SelectCharacter(character.Id);

        var session = db.PlayerSessions.SingleOrDefault(s => s.ConnectionId == "conn-region");
        session.Should().NotBeNull("a PlayerSession should be created by SelectCharacter");
        session!.ZoneId.Should().BeNull("characters start on the region map, not inside a zone");
    }

    [Fact]
    public async Task SelectCharacter_Joins_Region_Group()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, _, groups, _) = CreateHub(db, accountId);

        await hub.SelectCharacter(character.Id);

        // Region group name is "region:{regionId}"
        groups.AddedGroups.Should().Contain(g => g.StartsWith("region:"),
            "SelectCharacter should add the connection to the character's region group");
    }

    [Fact]
    public async Task SelectCharacter_Sets_CurrentRegionId_In_Context_Items()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, _, _, ctx) = CreateHub(db, accountId);

        await hub.SelectCharacter(character.Id);

        ctx.Items.Should().ContainKey("CurrentRegionId",
            "SelectCharacter must set CurrentRegionId so subsequent hub methods can read it");
    }

    [Fact]
    public async Task SelectCharacter_CharacterSelected_Payload_Includes_RegionId()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.SelectCharacter(character.Id);

        var msg = clients.CallerProxy.SentMessages.SingleOrDefault(m => m.Method == "CharacterSelected");
        msg.Should().NotBeNull();
        var json = JsonSerializer.Serialize(msg.Args[0]);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("RegionId", out var regionProp).Should().BeTrue(
            "CharacterSelected payload must include RegionId for the client to load the correct region map");
        regionProp.GetString().Should().NotBeNullOrEmpty();
    }

    // ── GetRegionMap — guard when no character selected ────────────────────────

    [Fact]
    public async Task GetRegionMap_Sends_Error_When_SelectCharacter_Not_Called()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // No SelectCharacter → no CharacterId in Context.Items
        await hub.GetRegionMap();

        clients.CallerProxy.SentMessages.Should().ContainSingle(m => m.Method == "Error",
            "GetRegionMap must send an Error when no character has been selected");
    }

    // ── MoveOnRegion — guard when no character selected ────────────────────────

    [Fact]
    public async Task MoveOnRegion_Sends_Error_When_SelectCharacter_Not_Called()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.MoveOnRegion(new RealmUnbound.Server.Features.Zones.MoveOnRegionHubRequest(6, 5, "right"));

        clients.CallerProxy.SentMessages.Should().ContainSingle(m => m.Method == "Error",
            "MoveOnRegion must send an Error when no character has been selected");
    }

    // ── ExitZone — guard when no active zone session ───────────────────────────

    [Fact]
    public async Task ExitZone_Sends_Error_When_SelectCharacter_Not_Called()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.ExitZone();

        clients.CallerProxy.SentMessages.Should().ContainSingle(m => m.Method == "Error",
            "ExitZone must send an Error when no character has been selected");
    }
}
