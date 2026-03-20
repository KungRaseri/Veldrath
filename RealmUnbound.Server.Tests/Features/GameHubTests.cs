using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Hubs;
using RealmUnbound.Server.Services;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

// ── Fake SignalR client proxy ─────────────────────────────────────────────────

public class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> SentMessages { get; } = [];

    public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
    {
        SentMessages.Add((method, args));
        return Task.CompletedTask;
    }
}

// ── Fake IHubCallerClients ────────────────────────────────────────────────────

public class FakeHubCallerClients : IHubCallerClients
{
    private readonly FakeClientProxy _noop = new();

    public FakeClientProxy CallerProxy       { get; } = new();
    public FakeClientProxy GroupProxy        { get; } = new();
    public FakeClientProxy OtherGroupProxy   { get; } = new();

    public IClientProxy Caller                                     => CallerProxy;
    public IClientProxy Others                                     => _noop;
    public IClientProxy All                                        => _noop;
    public IClientProxy OthersInGroup(string groupName)            => OtherGroupProxy;
    public IClientProxy Group(string groupName)                    => GroupProxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excluded)  => _noop;
    public IClientProxy Client(string connectionId)                => _noop;
    public IClientProxy Clients(IReadOnlyList<string> ids)         => _noop;
    public IClientProxy GroupExcept(string g, IReadOnlyList<string> e) => _noop;
    public IClientProxy Groups(IEnumerable<string> groups)         => _noop;
    public IClientProxy Groups(IReadOnlyList<string> groups)       => _noop;
    public IClientProxy User(string userId)                        => _noop;
    public IClientProxy Users(IEnumerable<string> userIds)         => _noop;
    public IClientProxy Users(IReadOnlyList<string> userIds)       => _noop;
}

// ── Fake IGroupManager ────────────────────────────────────────────────────────

public class FakeGroupManager : IGroupManager
{
    public List<string> AddedGroups    { get; } = [];
    public List<string> RemovedGroups  { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        AddedGroups.Add(groupName);
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        RemovedGroups.Add(groupName);
        return Task.CompletedTask;
    }
}

// ── Fake HubCallerContext ─────────────────────────────────────────────────────

public sealed class FakeHubCallerContext : HubCallerContext
{
    private readonly Dictionary<object, object?> _items = new();

    public override string ConnectionId       { get; }
    public override string? UserIdentifier    => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public override ClaimsPrincipal? User     { get; }
    public override IDictionary<object, object?> Items => _items;
    public override IFeatureCollection Features        { get; } = new FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public FakeHubCallerContext(string connectionId = "test-conn", ClaimsPrincipal? user = null)
    {
        ConnectionId = connectionId;
        User = user;
    }

    public override void Abort() { }
}

// ── GameHubTests ──────────────────────────────────────────────────────────────

public class GameHubTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal MakeUser(Guid accountId, string username = "TestUser") =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,        accountId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
        }));

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
            ClassName = "@classes/warriors:fighter",
            SlotIndex = 1,
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private (GameHub Hub, FakeHubCallerClients Clients, FakeGroupManager Groups, FakeHubCallerContext Ctx)
        CreateHub(ApplicationDbContext db, Guid accountId, string connId = "conn-1", ISender? mediator = null)
    {
        var hub     = new GameHub(NullLogger<GameHub>.Instance,
                                  new CharacterRepository(db),
                                  new ZoneRepository(db),
                                  new ZoneSessionRepository(db),
                                  new ActiveCharacterTracker(),
                                  mediator ?? Mock.Of<ISender>());
        var clients = new FakeHubCallerClients();
        var groups  = new FakeGroupManager();
        var ctx     = new FakeHubCallerContext(connId, MakeUser(accountId));

        hub.Clients = clients;
        hub.Groups  = groups;
        hub.Context = ctx;

        return (hub, clients, groups, ctx);
    }

    // ── OnConnectedAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task OnConnectedAsync_Should_Send_Connected_To_Caller()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.OnConnectedAsync();

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Connected");
    }

    // ── OnDisconnectedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_Should_Complete_When_No_Session_Exists()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, _, _, _) = CreateHub(db, accountId);

        var act = () => hub.OnDisconnectedAsync(null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnDisconnectedAsync_Should_Clean_Up_Zone_Session()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        // Add a zone session for this connection
        db.ZoneSessions.Add(new ZoneSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "conn-disc",
            ZoneId        = "starting-zone",
        });
        await db.SaveChangesAsync();

        var (hub, _, groups, _) = CreateHub(db, accountId, "conn-disc");

        await hub.OnDisconnectedAsync(null);

        db.ZoneSessions.Should().BeEmpty();
        groups.RemovedGroups.Should().Contain("zone:starting-zone");
    }

    // ── SelectCharacter ───────────────────────────────────────────────────────

    [Fact]
    public async Task SelectCharacter_Should_Send_Error_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.SelectCharacter(Guid.NewGuid()); // unknown ID

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task SelectCharacter_Should_Send_Error_When_Character_Belongs_To_Different_Account()
    {
        await using var db = _factory.CreateContext();
        var ownerAccountId = await SeedAccountAsync(db);
        var otherAccountId = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, ownerAccountId);

        // Log in as a *different* account
        var (hub, clients, _, _) = CreateHub(db, otherAccountId);

        await hub.SelectCharacter(character.Id);

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task SelectCharacter_Should_Store_CharacterId_In_Context_Items()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, _, _, ctx) = CreateHub(db, accountId);

        await hub.SelectCharacter(character.Id);

        ctx.Items.Should().ContainKey("CharacterId");
        ctx.Items["CharacterId"].Should().Be(character.Id);
    }

    [Fact]
    public async Task SelectCharacter_Should_Send_CharacterSelected_On_Success()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.SelectCharacter(character.Id);

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "CharacterSelected");
    }

    // ── EnterZone ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnterZone_Should_Send_Error_When_SelectCharacter_Not_Called()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // Skip SelectCharacter — no CharacterId in Items
        await hub.EnterZone("starting-zone");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task EnterZone_Should_Send_Error_When_Zone_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, ctx) = CreateHub(db, accountId);

        // Manually populate Context.Items as if SelectCharacter ran
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("nonexistent-zone");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task EnterZone_Should_Send_ZoneEntered_On_Success()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, ctx) = CreateHub(db, accountId);

        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("starting-zone"); // seeded zone

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ZoneEntered");
    }

    [Fact]
    public async Task EnterZone_Should_Add_Connection_To_Zone_Group()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, _, groups, ctx) = CreateHub(db, accountId);

        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("starting-zone");

        groups.AddedGroups.Should().Contain("zone:starting-zone");
    }

    [Fact]
    public async Task EnterZone_Should_Create_ZoneSession_In_Database()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, _, _, ctx) = CreateHub(db, accountId, "conn-enter");

        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("starting-zone");

        db.ZoneSessions.Should().ContainSingle(s => s.ConnectionId == "conn-enter");
    }

    // ── LeaveZone ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveZone_Should_Send_ZoneLeft_To_Caller()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.LeaveZone();

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ZoneLeft");
    }

    [Fact]
    public async Task LeaveZone_Should_Remove_Session_From_Database()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        db.ZoneSessions.Add(new ZoneSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "conn-leave",
            ZoneId        = "starting-zone",
        });
        await db.SaveChangesAsync();

        var (hub, _, groups, _) = CreateHub(db, accountId, "conn-leave");

        await hub.LeaveZone();

        db.ZoneSessions.Should().BeEmpty();
        groups.RemovedGroups.Should().Contain("zone:starting-zone");
    }

    // ── EnterZone with stale session handling ─────────────────────────────────

    [Fact]
    public async Task EnterZone_Should_Remove_Stale_Session_For_Character()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);

        // Simulate a stale session from a previous connection
        db.ZoneSessions.Add(new ZoneSession
        {
            CharacterId   = character.Id,
            CharacterName = character.Name,
            ConnectionId  = "stale-conn",
            ZoneId        = "town-millhaven",
        });
        await db.SaveChangesAsync();

        var (hub, _, _, ctx) = CreateHub(db, accountId, "new-conn");
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("starting-zone");

        // Old stale session should be gone; new one created
        db.ZoneSessions.Should().ContainSingle(s => s.ConnectionId == "new-conn");
    }

    // ── TryGetCharacterName false branch ─────────────────────────────────────

    [Fact]
    public async Task EnterZone_Should_Send_Error_When_CharacterName_Missing_From_Context()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var character = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, ctx) = CreateHub(db, accountId);

        // Set CharacterId but NOT CharacterName → TryGetCharacterName returns false
        ctx.Items["CharacterId"] = character.Id;
        // CharacterName intentionally omitted

        await hub.EnterZone("starting-zone");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // ── GainExperience ────────────────────────────────────────────────────────

    [Fact]
    public async Task GainExperience_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // Skip SelectCharacter — no CharacterId in Context.Items
        await hub.GainExperience(100);

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task GainExperience_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult
            {
                Success       = true,
                NewLevel      = 1,
                NewExperience = 100,
                LeveledUp     = false,
            });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.GainExperience(100, "Quest");

        mediatorMock.Verify(
            m => m.Send(It.Is<RealmUnbound.Server.Features.LevelUp.GainExperienceHubCommand>(
                c => c.CharacterId == character.Id && c.Amount == 100 && c.Source == "Quest"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GainExperience_Should_Broadcast_ExperienceGained_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult
            {
                Success       = true,
                NewLevel      = 2,
                NewExperience = 50,
                LeveledUp     = true,
                LeveledUpTo   = 2,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.GainExperience(150);

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ExperienceGained");
    }

    [Fact]
    public async Task GainExperience_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult
            {
                Success      = false,
                ErrorMessage = "Character not found",
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.GainExperience(50);

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task GainExperience_Should_Send_ExperienceGained_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult { Success = true, NewLevel = 1 });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId is intentionally NOT set

        await hub.GainExperience(50);

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ExperienceGained");
    }

    // ── AllocateAttributePoints ───────────────────────────────────────────────

    private static async Task<Character> SeedCharacterWithAttributePointsAsync(
        ApplicationDbContext db, Guid accountId, int unspent, Dictionary<string, int>? extra = null)
    {
        var attrs = new Dictionary<string, int>(extra ?? []) { ["UnspentAttributePoints"] = unspent };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"AttrChar_{Guid.NewGuid():N}",
            ClassName  = "@classes/warriors:fighter",
            SlotIndex  = 1,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task AllocateAttributePoints_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // Skip SelectCharacter — no CharacterId in Context.Items
        await hub.AllocateAttributePoints(new Dictionary<string, int> { ["Strength"] = 1 });

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AllocateAttributePoints_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(db, accountId, unspent: 5);

        var allocations = new Dictionary<string, int> { ["Strength"] = 2, ["Dexterity"] = 1 };

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult
            {
                Success         = true,
                PointsSpent     = 3,
                RemainingPoints = 2,
                NewAttributes   = new Dictionary<string, int> { ["Strength"] = 12, ["Dexterity"] = 8 },
            });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AllocateAttributePoints(allocations);

        mediatorMock.Verify(
            m => m.Send(
                It.Is<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommand>(
                    c => c.CharacterId == character.Id && c.Allocations["Strength"] == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AllocateAttributePoints_Should_Broadcast_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(db, accountId, unspent: 3);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult
            {
                Success = true, PointsSpent = 1, RemainingPoints = 2, NewAttributes = [],
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.AllocateAttributePoints(new Dictionary<string, int> { ["Intelligence"] = 1 });

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "AttributePointsAllocated");
    }

    [Fact]
    public async Task AllocateAttributePoints_Should_Send_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(db, accountId, unspent: 3);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult
            {
                Success = true, PointsSpent = 1, RemainingPoints = 2, NewAttributes = [],
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally not set

        await hub.AllocateAttributePoints(new Dictionary<string, int> { ["Wisdom"] = 1 });

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "AttributePointsAllocated");
    }

    [Fact]
    public async Task AllocateAttributePoints_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(db, accountId, unspent: 0);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult
            {
                Success      = false,
                ErrorMessage = "Not enough unspent attribute points. Have 0, need 1.",
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AllocateAttributePoints(new Dictionary<string, int> { ["Strength"] = 1 });

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AllocateAttributePoints_Handler_Should_Apply_Increases_And_Persist()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(
            db, accountId, unspent: 5,
            extra: new Dictionary<string, int> { ["Strength"] = 10, ["Dexterity"] = 8 });

        var handler = new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommand
        {
            CharacterId = character.Id,
            Allocations = new Dictionary<string, int> { ["Strength"] = 2, ["Dexterity"] = 1 },
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PointsSpent.Should().Be(3);
        result.RemainingPoints.Should().Be(2);
        result.NewAttributes["Strength"].Should().Be(12);
        result.NewAttributes["Dexterity"].Should().Be(9);
        result.NewAttributes["UnspentAttributePoints"].Should().Be(2);
    }

    [Fact]
    public async Task AllocateAttributePoints_Handler_Should_Fail_When_Insufficient_Points()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(db, accountId, unspent: 1);

        var handler = new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommand
        {
            CharacterId = character.Id,
            Allocations = new Dictionary<string, int> { ["Strength"] = 3 }, // 3 > 1 available
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not enough unspent attribute points");
    }

    [Fact]
    public async Task AllocateAttributePoints_Handler_Should_Fail_When_Empty_Allocations()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithAttributePointsAsync(db, accountId, unspent: 5);

        var handler = new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommand
        {
            CharacterId = character.Id,
            Allocations = [],
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No attribute allocations provided");
    }

    [Fact]
    public async Task AllocateAttributePoints_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubCommand
        {
            CharacterId = Guid.NewGuid(),
            Allocations = new Dictionary<string, int> { ["Strength"] = 1 },
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // ── RestAtLocation ────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterWithGoldAsync(
        ApplicationDbContext db, Guid accountId, int gold, int level = 1)
    {
        var attrs = new Dictionary<string, int>
        {
            ["Gold"]          = gold,
            ["MaxHealth"]     = level * 10,
            ["CurrentHealth"] = 0,   // depleted
            ["MaxMana"]       = level * 5,
            ["CurrentMana"]   = 0,   // depleted
        };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"RestChar_{Guid.NewGuid():N}",
            ClassName  = "@classes/warriors:fighter",
            SlotIndex  = 1,
            Level      = level,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task RestAtLocation_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.RestAtLocation("inn-millhaven");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task RestAtLocation_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 50);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.RestAtLocationHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.RestAtLocationHubResult
            {
                Success       = true,
                CurrentHealth = 10,
                MaxHealth     = 10,
                CurrentMana   = 5,
                MaxMana       = 5,
                GoldRemaining = 40,
            });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.RestAtLocation("inn-millhaven", costInGold: 10);

        mediatorMock.Verify(
            m => m.Send(
                It.Is<RealmUnbound.Server.Features.Characters.RestAtLocationHubCommand>(
                    c => c.CharacterId == character.Id && c.LocationId == "inn-millhaven" && c.CostInGold == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RestAtLocation_Should_Broadcast_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 50);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.RestAtLocationHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.RestAtLocationHubResult
            {
                Success = true, CurrentHealth = 10, MaxHealth = 10,
                CurrentMana = 5, MaxMana = 5, GoldRemaining = 40,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.RestAtLocation("inn-millhaven");

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "CharacterRested");
    }

    [Fact]
    public async Task RestAtLocation_Should_Send_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 50);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.RestAtLocationHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.RestAtLocationHubResult
            {
                Success = true, CurrentHealth = 10, MaxHealth = 10,
                CurrentMana = 5, MaxMana = 5, GoldRemaining = 40,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally not set

        await hub.RestAtLocation("inn-millhaven");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "CharacterRested");
    }

    [Fact]
    public async Task RestAtLocation_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 5);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(
                It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.RestAtLocationHubResult>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.RestAtLocationHubResult
            {
                Success      = false,
                ErrorMessage = "Not enough gold to rest. Have 5, need 10.",
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.RestAtLocation("inn-millhaven");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task RestAtLocation_Handler_Should_Restore_Health_And_Mana_And_Deduct_Gold()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 50, level: 3);

        var handler = new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommand
        {
            CharacterId = character.Id,
            LocationId  = "inn-millhaven",
            CostInGold  = 10,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().Be(30); // level 3 * 10
        result.MaxHealth.Should().Be(30);
        result.CurrentMana.Should().Be(15);  // level 3 * 5
        result.MaxMana.Should().Be(15);
        result.GoldRemaining.Should().Be(40); // 50 - 10
    }

    [Fact]
    public async Task RestAtLocation_Handler_Should_Fail_When_Not_Enough_Gold()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 5);

        var handler = new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommand
        {
            CharacterId = character.Id,
            LocationId  = "inn-millhaven",
            CostInGold  = 10,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not enough gold");
    }

    [Fact]
    public async Task RestAtLocation_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();
        await SeedAccountAsync(db);

        var handler = new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommand
        {
            CharacterId = Guid.NewGuid(),
            LocationId  = "inn-millhaven",
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task RestAtLocation_Handler_Should_Default_Pools_From_Level_When_Not_In_Blob()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);

        // Character has only Gold in attrs — no MaxHealth/MaxMana
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"NoPool_{Guid.NewGuid():N}",
            ClassName  = "@classes/warriors:fighter",
            SlotIndex  = 1,
            Level      = 4,
            Attributes = JsonSerializer.Serialize(new Dictionary<string, int> { ["Gold"] = 100 }),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();

        var handler = new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.RestAtLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.RestAtLocationHubCommand
        {
            CharacterId = c.Id,
            LocationId  = "inn-millhaven",
            CostInGold  = 0,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MaxHealth.Should().Be(40); // level 4 * 10
        result.MaxMana.Should().Be(20);   // level 4 * 5
    }

    // ── UseAbility ────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterWithManaAsync(
        ApplicationDbContext db, Guid accountId, int currentMana, int maxMana, Dictionary<string, int>? extra = null)
    {
        var attrs = new Dictionary<string, int>(extra ?? [])
        {
            ["CurrentMana"] = currentMana,
            ["MaxMana"]     = maxMana,
        };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"AblChar_{Guid.NewGuid():N}",
            ClassName  = "@classes/warriors:fighter",
            SlotIndex  = 1,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    [Fact]
    public async Task UseAbility_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // Skip SelectCharacter — no CharacterId in Context.Items
        await hub.UseAbility("fireball");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task UseAbility_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.UseAbilityHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.UseAbilityHubResult
            {
                Success       = true,
                AbilityId     = "fireball",
                ManaCost      = 10,
                RemainingMana = 40,
            });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.UseAbility("fireball");

        mediatorMock.Verify(
            m => m.Send(It.Is<RealmUnbound.Server.Features.Characters.UseAbilityHubCommand>(
                c => c.CharacterId == character.Id && c.AbilityId == "fireball"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UseAbility_Should_Broadcast_AbilityUsed_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.UseAbilityHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.UseAbilityHubResult
            {
                Success = true, AbilityId = "fireball", ManaCost = 10, RemainingMana = 40,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.UseAbility("fireball");

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "AbilityUsed");
    }

    [Fact]
    public async Task UseAbility_Should_Send_AbilityUsed_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.UseAbilityHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.UseAbilityHubResult
            {
                Success = true, AbilityId = "fireball", ManaCost = 10, RemainingMana = 40,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally NOT set

        await hub.UseAbility("fireball");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "AbilityUsed");
    }

    [Fact]
    public async Task UseAbility_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.UseAbilityHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealmUnbound.Server.Features.Characters.UseAbilityHubResult
            {
                Success      = false,
                ErrorMessage = "Not enough mana.",
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.UseAbility("fireball");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task UseAbility_Handler_Should_Deduct_Mana_And_Persist()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithManaAsync(db, accountId, currentMana: 50, maxMana: 50);

        var handler = new RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.UseAbilityHubCommand
        {
            CharacterId = character.Id,
            AbilityId   = "fireball",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ManaCost.Should().Be(10);
        result.RemainingMana.Should().Be(40);
        result.HealthRestored.Should().Be(0);
    }

    [Fact]
    public async Task UseAbility_Handler_Should_Restore_Health_For_Healing_Ability()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithManaAsync(db, accountId,
            currentMana: 50, maxMana: 50,
            extra: new Dictionary<string, int> { ["CurrentHealth"] = 60, ["MaxHealth"] = 100 });

        var handler = new RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.UseAbilityHubCommand
        {
            CharacterId = character.Id,
            AbilityId   = "minor_heal",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(25); // HealingAmount = 25; room to heal = 40
        result.RemainingMana.Should().Be(40);
    }

    [Fact]
    public async Task UseAbility_Handler_Should_Fail_When_Not_Enough_Mana()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithManaAsync(db, accountId, currentMana: 5, maxMana: 50);

        var handler = new RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.UseAbilityHubCommand
        {
            CharacterId = character.Id,
            AbilityId   = "fireball",
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mana");
    }

    [Fact]
    public async Task UseAbility_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db    = _factory.CreateContext();
        var accountId         = await SeedAccountAsync(db);
        var missingCharId     = Guid.NewGuid();

        var handler = new RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.UseAbilityHubCommand
        {
            CharacterId = missingCharId,
            AbilityId   = "fireball",
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task UseAbility_Handler_Should_Fail_When_AbilityId_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithManaAsync(db, accountId, currentMana: 50, maxMana: 50);

        var handler = new RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.UseAbilityHubCommand
        {
            CharacterId = character.Id,
            AbilityId   = "   ",
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task UseAbility_Handler_Should_Default_Mana_From_Level_When_Not_In_Blob()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        // Character has NO mana in attrs blob — should default from level
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"NoMana_{Guid.NewGuid():N}",
            ClassName  = "@classes/warriors:fighter",
            SlotIndex  = 1,
            Level      = 4, // MaxMana default = 4 * 5 = 20
            Attributes = "{}",
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();

        var handler = new RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<RealmUnbound.Server.Features.Characters.UseAbilityHubCommandHandler>.Instance);

        var result = await handler.Handle(new RealmUnbound.Server.Features.Characters.UseAbilityHubCommand
        {
            CharacterId = c.Id,
            AbilityId   = "fireball",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemainingMana.Should().Be(10); // 20 - 10 (DefaultManaCost)
    }
}

