using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Characters;
using RealmUnbound.Server.Features.Zones;
using RealmUnbound.Server.Hubs;
using RealmUnbound.Server.Services;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

// Fake SignalR client proxy
public class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> SentMessages { get; } = [];

    public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
    {
        SentMessages.Add((method, args));
        return Task.CompletedTask;
    }
}

// Fake IHubCallerClients
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

// Fake IGroupManager
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

// Fake HubCallerContext
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

// GameHubTests
public class GameHubTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    // Helpers
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
            ClassName = "Warrior",
            SlotIndex = 1,
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    private (GameHub Hub, FakeHubCallerClients Clients, FakeGroupManager Groups, FakeHubCallerContext Ctx)
        CreateHub(ApplicationDbContext db, Guid accountId, string connId = "conn-1", ISender? mediator = null, IActiveCharacterTracker? tracker = null)
    {
        var hub     = new GameHub(NullLogger<GameHub>.Instance,
                                  new CharacterRepository(db),
                                  new ZoneRepository(db),
                                  new ZoneSessionRepository(db),
                                  tracker ?? new ActiveCharacterTracker(),
                                  mediator ?? Mock.Of<ISender>());
        var clients = new FakeHubCallerClients();
        var groups  = new FakeGroupManager();
        var ctx     = new FakeHubCallerContext(connId, MakeUser(accountId));

        hub.Clients = clients;
        hub.Groups  = groups;
        hub.Context = ctx;

        return (hub, clients, groups, ctx);
    }

    // OnConnectedAsync
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

    // OnDisconnectedAsync
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
            ZoneId        = "fenwick-crossing",
        });
        await db.SaveChangesAsync();

        var (hub, _, groups, _) = CreateHub(db, accountId, "conn-disc");

        await hub.OnDisconnectedAsync(null);

        db.ZoneSessions.Should().BeEmpty();
        groups.RemovedGroups.Should().Contain("zone:fenwick-crossing");
    }

    // SelectCharacter
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

    [Fact]
    public async Task SelectCharacter_Should_Include_Level_And_Experience_In_CharacterSelected_Payload()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character = new Character
        {
            AccountId  = accountId,
            Name       = $"Char_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Level      = 4,
            Experience = 75L,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        var (hub, clients, _, _) = CreateHub(db, accountId);
        await hub.SelectCharacter(character.Id);

        var msg  = clients.CallerProxy.SentMessages.Single(m => m.Method == "CharacterSelected");
        var json = JsonSerializer.Serialize(msg.Args[0]);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("Level").GetInt32().Should().Be(4);
        doc.RootElement.GetProperty("Experience").GetInt64().Should().Be(75L);
        doc.RootElement.GetProperty("Name").GetString().Should().Be(character.Name);
    }

    [Fact]
    public async Task SelectCharacter_Should_Include_Blob_Stats_In_CharacterSelected_Payload()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var attrs = new Dictionary<string, int>
        {
            ["CurrentHealth"] = 65,
            ["MaxHealth"]     = 100,
            ["Gold"]          = 300,
            ["CurrentMana"]   = 20,
            ["MaxMana"]       = 50,
        };
        var character = new Character
        {
            AccountId  = accountId,
            Name       = $"BlobChar_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        var (hub, clients, _, _) = CreateHub(db, accountId);
        await hub.SelectCharacter(character.Id);

        var msg  = clients.CallerProxy.SentMessages.Single(m => m.Method == "CharacterSelected");
        var json = JsonSerializer.Serialize(msg.Args[0]);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("CurrentHealth").GetInt32().Should().Be(65);
        doc.RootElement.GetProperty("MaxHealth").GetInt32().Should().Be(100);
        doc.RootElement.GetProperty("Gold").GetInt32().Should().Be(300);
        doc.RootElement.GetProperty("CurrentMana").GetInt32().Should().Be(20);
        doc.RootElement.GetProperty("MaxMana").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task SelectCharacter_Should_Default_Health_And_Mana_When_Blob_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character = new Character
        {
            AccountId  = accountId,
            Name       = $"EmptyAttr_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Level      = 3,
            // Attributes left as default "{}"
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        var (hub, clients, _, _) = CreateHub(db, accountId);
        await hub.SelectCharacter(character.Id);

        var msg  = clients.CallerProxy.SentMessages.Single(m => m.Method == "CharacterSelected");
        var json = JsonSerializer.Serialize(msg.Args[0]);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("MaxHealth").GetInt32().Should().Be(30);      // Level 3 * 10
        doc.RootElement.GetProperty("MaxMana").GetInt32().Should().Be(15);        // Level 3 * 5
        doc.RootElement.GetProperty("CurrentHealth").GetInt32().Should().Be(30);  // defaults to MaxHealth
    }

    // EnterZone
    [Fact]
    public async Task EnterZone_Should_Send_Error_When_SelectCharacter_Not_Called()
    {
        await using var db = _factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // Skip SelectCharacter — no CharacterId in Items
        await hub.EnterZone("fenwick-crossing");

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

        await hub.EnterZone("fenwick-crossing"); // seeded zone

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

        await hub.EnterZone("fenwick-crossing");

        groups.AddedGroups.Should().Contain("zone:fenwick-crossing");
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

        await hub.EnterZone("fenwick-crossing");

        db.ZoneSessions.Should().ContainSingle(s => s.ConnectionId == "conn-enter");
    }

    // LeaveZone
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
            ZoneId        = "fenwick-crossing",
        });
        await db.SaveChangesAsync();

        var (hub, _, groups, _) = CreateHub(db, accountId, "conn-leave");

        await hub.LeaveZone();

        db.ZoneSessions.Should().BeEmpty();
        groups.RemovedGroups.Should().Contain("zone:fenwick-crossing");
    }

    // EnterZone with stale session handling
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
            ZoneId        = "aldenmere",
        });
        await db.SaveChangesAsync();

        var (hub, _, _, ctx) = CreateHub(db, accountId, "new-conn");
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;

        await hub.EnterZone("fenwick-crossing");

        // Old stale session should be gone; new one created
        db.ZoneSessions.Should().ContainSingle(s => s.ConnectionId == "new-conn");
    }

    // TryGetCharacterName false branch
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

        await hub.EnterZone("fenwick-crossing");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // GainExperience
    [Fact]
    public async Task GainExperience_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db  = _factory.CreateContext();
        var accountId       = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        // Skip SelectCharacter — no CharacterId in Context.Items
        await hub.GainExperience(new GainExperienceHubRequest(100));

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

        await hub.GainExperience(new GainExperienceHubRequest(100, "Quest"));

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

        await hub.GainExperience(new GainExperienceHubRequest(150));

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

        await hub.GainExperience(new GainExperienceHubRequest(50));

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

        await hub.GainExperience(new GainExperienceHubRequest(50));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ExperienceGained");
    }

    // AllocateAttributePoints
    private static async Task<Character> SeedCharacterWithAttributePointsAsync(
        ApplicationDbContext db, Guid accountId, int unspent, Dictionary<string, int>? extra = null)
    {
        var attrs = new Dictionary<string, int>(extra ?? []) { ["UnspentAttributePoints"] = unspent };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"AttrChar_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
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

    // RestAtLocation
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
            ClassName  = "Warrior",
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

        await hub.RestAtLocation(new RestAtLocationHubRequest("inn-millhaven"));

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

        await hub.RestAtLocation(new RestAtLocationHubRequest("inn-millhaven", 10));

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

        await hub.RestAtLocation(new RestAtLocationHubRequest("inn-millhaven"));

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

        await hub.RestAtLocation(new RestAtLocationHubRequest("inn-millhaven"));

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

        await hub.RestAtLocation(new RestAtLocationHubRequest("inn-millhaven"));

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
            ClassName  = "Warrior",
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

    // UseAbility
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
            ClassName  = "Warrior",
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
            ClassName  = "Warrior",
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

    // GetActiveCharacters
    [Fact]
    public async Task GetActiveCharacters_Should_Return_Empty_When_No_Characters_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, _, _, _) = CreateHub(db, accountId);

        var result = await hub.GetActiveCharacters();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveCharacters_Should_Return_Id_After_SelectCharacter()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);
        var (hub, _, _, ctx) = CreateHub(db, accountId);

        ctx.Items["CharacterId"] = character.Id;
        // Simulate the tracker being updated by SelectCharacter by registering the character.
        var tracker = new ActiveCharacterTracker();
        tracker.TryClaim(character.Id, ctx.ConnectionId);

        var hub2 = new GameHub(NullLogger<GameHub>.Instance,
                               new CharacterRepository(db),
                               new ZoneRepository(db),
                               new ZoneSessionRepository(db),
                               tracker,
                               Mock.Of<ISender>());
        hub2.Clients = hub.Clients;
        hub2.Groups  = hub.Groups;
        hub2.Context = ctx;

        var result = await hub2.GetActiveCharacters();

        result.Should().Contain(character.Id);
    }

    [Fact]
    public async Task GetActiveCharacters_Should_Return_Multiple_Active_Ids()
    {
        await using var db = _factory.CreateContext();
        var accountId1     = await SeedAccountAsync(db);
        var accountId2     = await SeedAccountAsync(db);
        var charA          = await SeedCharacterAsync(db, accountId1);
        var charB          = await SeedCharacterAsync(db, accountId2);

        var tracker = new ActiveCharacterTracker();
        tracker.TryClaim(charA.Id, "conn-A");
        tracker.TryClaim(charB.Id, "conn-B");

        var ctx = new FakeHubCallerContext("conn-A", MakeUser(accountId1));
        var hub = new GameHub(NullLogger<GameHub>.Instance,
                              new CharacterRepository(db),
                              new ZoneRepository(db),
                              new ZoneSessionRepository(db),
                              tracker,
                              Mock.Of<ISender>());
        hub.Clients = new FakeHubCallerClients();
        hub.Groups  = new FakeGroupManager();
        hub.Context = ctx;

        var result = await hub.GetActiveCharacters();

        result.Should().Contain(charA.Id).And.Contain(charB.Id);
    }

    // AwardSkillXp
    [Fact]
    public async Task AwardSkillXp_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.AwardSkillXp(new AwardSkillXpHubRequest("swordsmanship", 50));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AwardSkillXp_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardSkillXpHubResult
            {
                Success     = true,
                SkillId     = "swordsmanship",
                TotalXp     = 50,
                CurrentRank = 0,
                RankedUp    = false,
            });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AwardSkillXp(new AwardSkillXpHubRequest("swordsmanship", 50));

        mediatorMock.Verify(
            m => m.Send(It.Is<AwardSkillXpHubCommand>(
                c => c.CharacterId == character.Id && c.SkillId == "swordsmanship" && c.Amount == 50),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AwardSkillXp_Should_Broadcast_SkillXpGained_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardSkillXpHubResult
            {
                Success = true, SkillId = "swordsmanship", TotalXp = 50, CurrentRank = 0, RankedUp = false,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.AwardSkillXp(new AwardSkillXpHubRequest("swordsmanship", 50));

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "SkillXpGained");
    }

    [Fact]
    public async Task AwardSkillXp_Should_Send_SkillXpGained_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardSkillXpHubResult
            {
                Success = true, SkillId = "swordsmanship", TotalXp = 50, CurrentRank = 0, RankedUp = false,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally NOT set

        await hub.AwardSkillXp(new AwardSkillXpHubRequest("swordsmanship", 50));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "SkillXpGained");
    }

    [Fact]
    public async Task AwardSkillXp_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwardSkillXpHubResult
            {
                Success      = false,
                ErrorMessage = "Amount must be positive.",
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AwardSkillXp(new AwardSkillXpHubRequest("swordsmanship", 0));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AwardSkillXp_Handler_Should_Accumulate_Xp_And_Persist()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new AwardSkillXpHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(new AwardSkillXpHubCommand
        {
            CharacterId = character.Id,
            SkillId     = "herbalism",
            Amount      = 40,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalXp.Should().Be(40);
        result.CurrentRank.Should().Be(0);
        result.RankedUp.Should().BeFalse();
    }

    [Fact]
    public async Task AwardSkillXp_Handler_Should_Trigger_RankUp_At_100_Xp()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new AwardSkillXpHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(new AwardSkillXpHubCommand
        {
            CharacterId = character.Id,
            SkillId     = "swordsmanship",
            Amount      = 100,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalXp.Should().Be(100);
        result.CurrentRank.Should().Be(1);
        result.RankedUp.Should().BeTrue();
    }

    [Fact]
    public async Task AwardSkillXp_Handler_Should_Not_RankUp_Below_Threshold()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new AwardSkillXpHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(new AwardSkillXpHubCommand
        {
            CharacterId = character.Id,
            SkillId     = "swordsmanship",
            Amount      = 99,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RankedUp.Should().BeFalse();
        result.CurrentRank.Should().Be(0);
    }

    [Fact]
    public async Task AwardSkillXp_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var missingCharId  = Guid.NewGuid();

        var handler = new AwardSkillXpHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(new AwardSkillXpHubCommand
        {
            CharacterId = missingCharId,
            SkillId     = "herbalism",
            Amount      = 10,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task AwardSkillXp_Handler_Should_Fail_When_SkillId_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new AwardSkillXpHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(new AwardSkillXpHubCommand
        {
            CharacterId = character.Id,
            SkillId     = "   ",
            Amount      = 10,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task AwardSkillXp_Handler_Should_Fail_When_Amount_Is_Zero()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new AwardSkillXpHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var result = await handler.Handle(new AwardSkillXpHubCommand
        {
            CharacterId = character.Id,
            SkillId     = "herbalism",
            Amount      = 0,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("positive");
    }

    // SelectCharacter — missing paths
    [Fact]
    public async Task SelectCharacter_Should_Send_CharacterAlreadyActive_When_Already_Claimed()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        // Seed tracker with a different connection already holding this character
        var tracker = new ActiveCharacterTracker();
        tracker.TryClaim(character.Id, "other-conn");

        var (hub, clients, _, ctx) = CreateHub(db, accountId, connId: "this-conn", tracker: tracker);

        await hub.SelectCharacter(character.Id);

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "CharacterAlreadyActive");
    }

    [Fact]
    public async Task SelectCharacter_Should_Broadcast_CharacterStatusChanged_Online_To_Account_Group()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.SelectCharacter(character.Id);

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "CharacterStatusChanged");
    }

    // OnDisconnectedAsync — missing paths
    [Fact]
    public async Task OnDisconnectedAsync_Should_Broadcast_CharacterStatusChanged_Offline_When_Character_Was_Active()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        // Pre-seed tracker so GetCharacterForConnection returns the character
        var tracker = new ActiveCharacterTracker();
        tracker.TryClaim(character.Id, "conn-active");

        var (hub, clients, _, ctx) = CreateHub(db, accountId, connId: "conn-active", tracker: tracker);
        // AccountId is normally written to Context.Items by OnConnectedAsync
        ctx.Items["AccountId"] = accountId;

        await hub.OnDisconnectedAsync(null);

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "CharacterStatusChanged");
    }

    // Catch-block tests (mediator throws)
    [Fact]
    public async Task GainExperience_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.LevelUp.GainExperienceHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.GainExperience(new GainExperienceHubRequest(100));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AllocateAttributePoints_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.AllocateAttributePointsHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AllocateAttributePoints(new Dictionary<string, int> { ["Strength"] = 1 });

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task RestAtLocation_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.RestAtLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.RestAtLocation(new RestAtLocationHubRequest("inn-millhaven"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task UseAbility_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.UseAbilityHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.UseAbility("fireball");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AwardSkillXp_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<RealmUnbound.Server.Features.Characters.AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AwardSkillXp(new AwardSkillXpHubRequest("swordsmanship", 10));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // EquipItem hub method
    [Fact]
    public async Task EquipItem_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.EquipItem(new EquipItemHubRequest("MainHand", "iron_sword"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task EquipItem_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EquipItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EquipItemHubResult
            {
                Success = true,
                Slot    = "MainHand",
                ItemRef = "iron_sword",
                AllEquippedItems = new Dictionary<string, string> { ["MainHand"] = "iron_sword" },
            });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EquipItem(new EquipItemHubRequest("MainHand", "iron_sword"));

        mediatorMock.Verify(
            m => m.Send(It.Is<EquipItemHubCommand>(
                c => c.CharacterId == character.Id && c.Slot == "MainHand" && c.ItemRef == "iron_sword"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EquipItem_Should_Broadcast_ItemEquipped_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EquipItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EquipItemHubResult { Success = true, Slot = "MainHand", ItemRef = "iron_sword", AllEquippedItems = [] });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.EquipItem(new EquipItemHubRequest("MainHand", "iron_sword"));

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ItemEquipped");
    }

    [Fact]
    public async Task EquipItem_Should_Send_ItemEquipped_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EquipItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EquipItemHubResult { Success = true, Slot = "Head", ItemRef = "leather_helm", AllEquippedItems = [] });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally not set

        await hub.EquipItem(new EquipItemHubRequest("Head", "leather_helm"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ItemEquipped");
    }

    [Fact]
    public async Task EquipItem_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EquipItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EquipItemHubResult { Success = false, ErrorMessage = "Invalid slot" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EquipItem(new EquipItemHubRequest("Torso", null));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task EquipItem_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EquipItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EquipItem(new EquipItemHubRequest("MainHand", "iron_sword"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // EquipItemHubCommandHandler
    [Fact]
    public async Task EquipItem_Handler_Should_Equip_Item_In_Named_Slot()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new EquipItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<EquipItemHubCommandHandler>.Instance);

        var result = await handler.Handle(new EquipItemHubCommand
        {
            CharacterId = character.Id,
            Slot        = "MainHand",
            ItemRef     = "iron_sword",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Slot.Should().Be("MainHand");
        result.ItemRef.Should().Be("iron_sword");
        result.AllEquippedItems.Should().ContainKey("MainHand")
              .WhoseValue.Should().Be("iron_sword");
    }

    [Fact]
    public async Task EquipItem_Handler_Should_Unequip_Item_When_ItemRef_Is_Null()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        // Seed character with a pre-equipped item
        var c = new Character
        {
            AccountId     = accountId,
            Name          = $"Eq_{Guid.NewGuid():N}",
            ClassName     = "Warrior",
            SlotIndex     = 1,
            EquipmentBlob = JsonSerializer.Serialize(new Dictionary<string, string> { ["MainHand"] = "iron_sword" }),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();

        var handler = new EquipItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<EquipItemHubCommandHandler>.Instance);

        var result = await handler.Handle(new EquipItemHubCommand
        {
            CharacterId = c.Id,
            Slot        = "MainHand",
            ItemRef     = null,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemRef.Should().BeNull();
        result.AllEquippedItems.Should().NotContainKey("MainHand");
    }

    [Fact]
    public async Task EquipItem_Handler_Should_Return_All_Equipped_Items_After_Equip()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        // Seed character with one item already equipped
        var c = new Character
        {
            AccountId     = accountId,
            Name          = $"Eq_{Guid.NewGuid():N}",
            ClassName     = "Warrior",
            SlotIndex     = 1,
            EquipmentBlob = JsonSerializer.Serialize(new Dictionary<string, string> { ["Head"] = "leather_helm" }),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();

        var handler = new EquipItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<EquipItemHubCommandHandler>.Instance);

        var result = await handler.Handle(new EquipItemHubCommand
        {
            CharacterId = c.Id,
            Slot        = "Chest",
            ItemRef     = "chain_shirt",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AllEquippedItems.Should().HaveCount(2);
        result.AllEquippedItems.Should().ContainKey("Head").WhoseValue.Should().Be("leather_helm");
        result.AllEquippedItems.Should().ContainKey("Chest").WhoseValue.Should().Be("chain_shirt");
    }

    [Fact]
    public async Task EquipItem_Handler_Should_Fail_When_Slot_Is_Invalid()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new EquipItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<EquipItemHubCommandHandler>.Instance);

        var result = await handler.Handle(new EquipItemHubCommand
        {
            CharacterId = character.Id,
            Slot        = "Torso",   // not a valid slot
            ItemRef     = "iron_sword",
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public async Task EquipItem_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db    = _factory.CreateContext();
        var accountId         = await SeedAccountAsync(db);
        var missingCharId     = Guid.NewGuid();

        var handler = new EquipItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<EquipItemHubCommandHandler>.Instance);

        var result = await handler.Handle(new EquipItemHubCommand
        {
            CharacterId = missingCharId,
            Slot        = "MainHand",
            ItemRef     = "iron_sword",
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // AddGold helpers
    private static async Task<Character> SeedCharacterWithGoldAsync(
        ApplicationDbContext db, Guid accountId, int gold)
    {
        var attrs = new Dictionary<string, int> { ["Gold"] = gold };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"GoldChar_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    // AddGold hub method
    [Fact]
    public async Task AddGold_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.AddGold(new AddGoldHubRequest(100));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AddGold_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AddGoldHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddGoldHubResult { Success = true, GoldAdded = 50, NewGoldTotal = 50 });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AddGold(new AddGoldHubRequest(50, "Quest"));

        mediatorMock.Verify(
            m => m.Send(It.Is<AddGoldHubCommand>(
                c => c.CharacterId == character.Id && c.Amount == 50 && c.Source == "Quest"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddGold_Should_Broadcast_GoldChanged_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AddGoldHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddGoldHubResult { Success = true, GoldAdded = 100, NewGoldTotal = 100 });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.AddGold(new AddGoldHubRequest(100));

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "GoldChanged");
    }

    [Fact]
    public async Task AddGold_Should_Send_GoldChanged_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AddGoldHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddGoldHubResult { Success = true, GoldAdded = 25, NewGoldTotal = 25 });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally NOT set

        await hub.AddGold(new AddGoldHubRequest(25));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "GoldChanged");
    }

    [Fact]
    public async Task AddGold_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AddGoldHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddGoldHubResult { Success = false, ErrorMessage = "Not enough gold" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AddGold(new AddGoldHubRequest(-999));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task AddGold_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<AddGoldHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.AddGold(new AddGoldHubRequest(50));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // AddGoldHubCommandHandler
    [Fact]
    public async Task AddGold_Handler_Should_Add_Gold_To_Character()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 100);

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = character.Id,
            Amount      = 50,
            Source      = "Loot",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.GoldAdded.Should().Be(50);
        result.NewGoldTotal.Should().Be(150);
    }

    [Fact]
    public async Task AddGold_Handler_Should_Spend_Gold_When_Amount_Is_Negative()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 100);

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = character.Id,
            Amount      = -30,
            Source      = "Purchase",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.GoldAdded.Should().Be(-30);
        result.NewGoldTotal.Should().Be(70);
    }

    [Fact]
    public async Task AddGold_Handler_Should_Fail_When_Amount_Is_Zero()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = character.Id,
            Amount      = 0,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zero");
    }

    [Fact]
    public async Task AddGold_Handler_Should_Fail_When_Not_Enough_Gold_To_Spend()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 20);

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = character.Id,
            Amount      = -50,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gold");
    }

    [Fact]
    public async Task AddGold_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();
        await SeedAccountAsync(db);

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = Guid.NewGuid(),
            Amount      = 100,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task AddGold_Handler_Should_Default_To_Zero_When_Blob_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId); // no gold blob

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        var result = await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = character.Id,
            Amount      = 75,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.NewGoldTotal.Should().Be(75);
    }

    [Fact]
    public async Task AddGold_Handler_Should_Persist_Updated_Gold_To_Database()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithGoldAsync(db, accountId, gold: 200);

        var handler = new AddGoldHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<AddGoldHubCommandHandler>.Instance);

        await handler.Handle(new AddGoldHubCommand
        {
            CharacterId = character.Id,
            Amount      = 100,
        }, CancellationToken.None);

        var updated = await db.Characters.FindAsync(character.Id);
        var attrs   = JsonSerializer.Deserialize<Dictionary<string, int>>(updated!.Attributes)!;
        attrs["Gold"].Should().Be(300);
    }

    // TakeDamage helpers
    private static async Task<Character> SeedCharacterWithHealthAsync(
        ApplicationDbContext db, Guid accountId, int currentHealth, int maxHealth)
    {
        var attrs = new Dictionary<string, int>
        {
            ["CurrentHealth"] = currentHealth,
            ["MaxHealth"]     = maxHealth,
        };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"DmgChar_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    // TakeDamage hub method
    [Fact]
    public async Task TakeDamage_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.TakeDamage(new TakeDamageHubRequest(25));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task TakeDamage_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<TakeDamageHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TakeDamageHubResult { Success = true, CurrentHealth = 75, MaxHealth = 100, IsDead = false });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.TakeDamage(new TakeDamageHubRequest(25, "Enemy"));

        mediatorMock.Verify(
            m => m.Send(It.Is<TakeDamageHubCommand>(
                c => c.CharacterId == character.Id && c.DamageAmount == 25 && c.Source == "Enemy"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TakeDamage_Should_Broadcast_DamageTaken_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<TakeDamageHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TakeDamageHubResult { Success = true, CurrentHealth = 75, MaxHealth = 100, IsDead = false });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.TakeDamage(new TakeDamageHubRequest(25));

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "DamageTaken");
    }

    [Fact]
    public async Task TakeDamage_Should_Send_DamageTaken_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<TakeDamageHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TakeDamageHubResult { Success = true, CurrentHealth = 50, MaxHealth = 100, IsDead = false });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;
        // CurrentZoneId intentionally NOT set

        await hub.TakeDamage(new TakeDamageHubRequest(50));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "DamageTaken");
    }

    [Fact]
    public async Task TakeDamage_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<TakeDamageHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TakeDamageHubResult { Success = false, ErrorMessage = "Damage must be positive" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.TakeDamage(new TakeDamageHubRequest(-5));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task TakeDamage_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<TakeDamageHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.TakeDamage(new TakeDamageHubRequest(25));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // TakeDamageHubCommandHandler
    [Fact]
    public async Task TakeDamage_Handler_Should_Reduce_CurrentHealth_By_DamageAmount()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithHealthAsync(db, accountId, currentHealth: 100, maxHealth: 100);

        var handler = new TakeDamageHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<TakeDamageHubCommandHandler>.Instance);

        var result = await handler.Handle(new TakeDamageHubCommand
        {
            CharacterId  = character.Id,
            DamageAmount = 30,
            Source       = "Goblin",
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().Be(70);
        result.MaxHealth.Should().Be(100);
        result.IsDead.Should().BeFalse();
    }

    [Fact]
    public async Task TakeDamage_Handler_Should_Clamp_Health_To_Zero_Not_Negative()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithHealthAsync(db, accountId, currentHealth: 10, maxHealth: 100);

        var handler = new TakeDamageHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<TakeDamageHubCommandHandler>.Instance);

        var result = await handler.Handle(new TakeDamageHubCommand
        {
            CharacterId  = character.Id,
            DamageAmount = 50,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().Be(0);
        result.IsDead.Should().BeTrue();
    }

    [Fact]
    public async Task TakeDamage_Handler_Should_Fail_When_DamageAmount_Is_Zero_Or_Negative()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new TakeDamageHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<TakeDamageHubCommandHandler>.Instance);

        var result = await handler.Handle(new TakeDamageHubCommand
        {
            CharacterId  = character.Id,
            DamageAmount = 0,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("positive");
    }

    [Fact]
    public async Task TakeDamage_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();
        await SeedAccountAsync(db);

        var handler = new TakeDamageHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<TakeDamageHubCommandHandler>.Instance);

        var result = await handler.Handle(new TakeDamageHubCommand
        {
            CharacterId  = Guid.NewGuid(),
            DamageAmount = 25,
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task TakeDamage_Handler_Should_Default_To_Level_Based_Max_When_Blob_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        // Character at level 1 → MaxHealth defaults to 10; no blob stored
        var character      = await SeedCharacterAsync(db, accountId);

        var handler = new TakeDamageHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<TakeDamageHubCommandHandler>.Instance);

        var result = await handler.Handle(new TakeDamageHubCommand
        {
            CharacterId  = character.Id,
            DamageAmount = 4,
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.MaxHealth.Should().Be(10);           // level 1 * 10
        result.CurrentHealth.Should().Be(6);        // 10 - 4
    }

    [Fact]
    public async Task TakeDamage_Handler_Should_Persist_Updated_Health_To_Database()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithHealthAsync(db, accountId, currentHealth: 80, maxHealth: 100);

        var handler = new TakeDamageHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<TakeDamageHubCommandHandler>.Instance);

        await handler.Handle(new TakeDamageHubCommand
        {
            CharacterId  = character.Id,
            DamageAmount = 20,
        }, CancellationToken.None);

        var updated = await db.Characters.FindAsync(character.Id);
        var attrs   = JsonSerializer.Deserialize<Dictionary<string, int>>(updated!.Attributes)!;
        attrs["CurrentHealth"].Should().Be(60);
    }

    // CraftItem helpers
    private static async Task<Character> SeedCharacterWithCraftingGoldAsync(
        ApplicationDbContext db, Guid accountId, int gold)
    {
        var attrs = new Dictionary<string, int> { ["Gold"] = gold };
        var c = new Character
        {
            AccountId  = accountId,
            Name       = $"CraftChar_{Guid.NewGuid():N}",
            ClassName  = "Warrior",
            SlotIndex  = 1,
            Attributes = JsonSerializer.Serialize(attrs),
        };
        db.Characters.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    // CraftItem hub method
    [Fact]
    public async Task CraftItem_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.CraftItem("iron-sword");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task CraftItem_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<CraftItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CraftItemHubResult { Success = true, ItemCrafted = "iron-sword", GoldSpent = 50, RemainingGold = 50 });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.CraftItem("iron-sword");

        mediatorMock.Verify(
            m => m.Send(It.Is<CraftItemHubCommand>(
                c => c.CharacterId == character.Id && c.RecipeSlug == "iron-sword"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CraftItem_Should_Broadcast_ItemCrafted_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<CraftItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CraftItemHubResult { Success = true, ItemCrafted = "iron-sword", GoldSpent = 50, RemainingGold = 50 });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.CraftItem("iron-sword");

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ItemCrafted");
    }

    [Fact]
    public async Task CraftItem_Should_Send_ItemCrafted_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<CraftItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CraftItemHubResult { Success = true, ItemCrafted = "iron-sword", GoldSpent = 50, RemainingGold = 50 });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.CraftItem("iron-sword");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ItemCrafted");
    }

    [Fact]
    public async Task CraftItem_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<CraftItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CraftItemHubResult { Success = false, ErrorMessage = "Not enough gold to craft this item" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.CraftItem("iron-sword");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task CraftItem_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<CraftItemHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.CraftItem("iron-sword");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // CraftItemHubCommandHandler
    [Fact]
    public async Task CraftItem_Handler_Should_Deduct_Gold_And_Return_Crafted_Item()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithCraftingGoldAsync(db, accountId, gold: 200);

        var handler = new CraftItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, "iron-sword"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemCrafted.Should().Be("iron-sword");
        result.GoldSpent.Should().Be(50);
        result.RemainingGold.Should().Be(150);
    }

    [Fact]
    public async Task CraftItem_Handler_Should_Fail_When_Not_Enough_Gold()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithCraftingGoldAsync(db, accountId, gold: 10);

        var handler = new CraftItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, "iron-sword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("gold");
    }

    [Fact]
    public async Task CraftItem_Handler_Should_Fail_When_Recipe_Slug_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithCraftingGoldAsync(db, accountId, gold: 200);

        var handler = new CraftItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(character.Id, ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task CraftItem_Handler_Should_Fail_When_Character_Not_Found()
    {
        await using var db = _factory.CreateContext();

        var handler = new CraftItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<CraftItemHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new CraftItemHubCommand(Guid.NewGuid(), "iron-sword"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task CraftItem_Handler_Should_Persist_Updated_Gold_To_Database()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterWithCraftingGoldAsync(db, accountId, gold: 300);

        var handler = new CraftItemHubCommandHandler(
            new CharacterRepository(db),
            NullLogger<CraftItemHubCommandHandler>.Instance);

        await handler.Handle(
            new CraftItemHubCommand(character.Id, "magic-staff"),
            CancellationToken.None);

        var updated = await db.Characters.FindAsync(character.Id);
        var attrs   = JsonSerializer.Deserialize<Dictionary<string, int>>(updated!.Attributes)!;
        attrs["Gold"].Should().Be(250);
    }

    // EnterDungeon hub method
    [Fact]
    public async Task EnterDungeon_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.EnterDungeon("dungeon-grotto");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task EnterDungeon_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EnterDungeonHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnterDungeonHubResult { Success = true, DungeonId = "dungeon-grotto" });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EnterDungeon("dungeon-grotto");

        mediatorMock.Verify(
            m => m.Send(It.Is<EnterDungeonHubCommand>(
                c => c.CharacterId == character.Id && c.DungeonSlug == "dungeon-grotto"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnterDungeon_Should_Broadcast_DungeonEntered_To_Zone_Group_When_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EnterDungeonHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnterDungeonHubResult { Success = true, DungeonId = "dungeon-grotto" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "starting-zone";

        await hub.EnterDungeon("dungeon-grotto");

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "DungeonEntered");
    }

    [Fact]
    public async Task EnterDungeon_Should_Send_DungeonEntered_To_Caller_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EnterDungeonHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnterDungeonHubResult { Success = true, DungeonId = "dungeon-grotto" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EnterDungeon("dungeon-grotto");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "DungeonEntered");
    }

    [Fact]
    public async Task EnterDungeon_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EnterDungeonHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnterDungeonHubResult { Success = false, ErrorMessage = "Dungeon not found" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EnterDungeon("nonexistent-dungeon");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task EnterDungeon_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<EnterDungeonHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.EnterDungeon("dungeon-grotto");

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // EnterDungeonHubCommandHandler
    [Fact]
    public async Task EnterDungeon_Handler_Should_Return_DungeonId_For_Valid_Dungeon()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new EnterDungeonHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<EnterDungeonHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new EnterDungeonHubCommand(accountId, "verdant-barrow"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DungeonId.Should().Be("verdant-barrow");
    }

    [Fact]
    public async Task EnterDungeon_Handler_Should_Fail_When_Zone_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new EnterDungeonHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<EnterDungeonHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new EnterDungeonHubCommand(accountId, "nonexistent-dungeon"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task EnterDungeon_Handler_Should_Fail_When_Zone_Is_Not_A_Dungeon()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new EnterDungeonHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<EnterDungeonHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new EnterDungeonHubCommand(accountId, "aldenmere"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a dungeon");
    }

    [Fact]
    public async Task EnterDungeon_Handler_Should_Fail_When_Slug_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new EnterDungeonHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<EnterDungeonHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new EnterDungeonHubCommand(accountId, ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    // VisitShop hub method
    [Fact]
    public async Task VisitShop_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.VisitShop(new VisitShopHubRequest("fenwick-crossing"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task VisitShop_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<VisitShopHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitShopHubResult { Success = true, ZoneId = "fenwick-crossing", ZoneName = "Fenwick's Crossing" });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.VisitShop(new VisitShopHubRequest("fenwick-crossing"));

        mediatorMock.Verify(
            m => m.Send(It.Is<VisitShopHubCommand>(
                c => c.CharacterId == character.Id && c.ZoneId == "fenwick-crossing"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VisitShop_Should_Send_ShopVisited_To_Caller_On_Success()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<VisitShopHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitShopHubResult { Success = true, ZoneId = "fenwick-crossing", ZoneName = "Fenwick's Crossing" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.VisitShop(new VisitShopHubRequest("fenwick-crossing"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "ShopVisited");
    }

    [Fact]
    public async Task VisitShop_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<VisitShopHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisitShopHubResult { Success = false, ErrorMessage = "greenveil-paths has no merchant" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.VisitShop(new VisitShopHubRequest("greenveil-paths"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task VisitShop_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<VisitShopHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.VisitShop(new VisitShopHubRequest("fenwick-crossing"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // VisitShopHubCommandHandler
    [Fact]
    public async Task VisitShop_Handler_Should_Return_Zone_Info_For_Merchant_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new VisitShopHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<VisitShopHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new VisitShopHubCommand(accountId, "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ZoneId.Should().Be("fenwick-crossing");
        result.ZoneName.Should().Be("Fenwick's Crossing");
    }

    [Fact]
    public async Task VisitShop_Handler_Should_Fail_When_Zone_Not_Found()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new VisitShopHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<VisitShopHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new VisitShopHubCommand(accountId, "nonexistent-zone"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task VisitShop_Handler_Should_Fail_When_Zone_Has_No_Merchant()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new VisitShopHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<VisitShopHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new VisitShopHubCommand(accountId, "greenveil-paths"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no merchant");
    }

    [Fact]
    public async Task VisitShop_Handler_Should_Fail_When_Zone_Id_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);

        var handler = new VisitShopHubCommandHandler(
            new ZoneRepository(db),
            NullLogger<VisitShopHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new VisitShopHubCommand(accountId, ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    // NavigateToLocation hub method
    [Fact]
    public async Task NavigateToLocation_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("fenwick-market"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task NavigateToLocation_Should_Dispatch_Command_To_ISender()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<NavigateToLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NavigateToLocationHubResult { Success = true, LocationSlug = "fenwick-market", LocationDisplayName = "Fenwick Market", LocationType = "location" });

        var (hub, _, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]  = character.Id;
        ctx.Items["CurrentZoneId"] = "fenwick-crossing";

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("fenwick-market"));

        mediatorMock.Verify(
            m => m.Send(It.Is<NavigateToLocationHubCommand>(
                c => c.CharacterId == character.Id && c.LocationSlug == "fenwick-market"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NavigateToLocation_Should_Send_LocationEntered_On_Success()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<NavigateToLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NavigateToLocationHubResult { Success = true, LocationSlug = "fenwick-market", LocationDisplayName = "Fenwick Market", LocationType = "location" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]  = character.Id;
        ctx.Items["CurrentZoneId"] = "fenwick-crossing";

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("fenwick-market"));

        clients.GroupProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "LocationEntered");
    }

    [Fact]
    public async Task NavigateToLocation_Should_Send_Error_On_Handler_Failure()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<NavigateToLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NavigateToLocationHubResult { Success = false, ErrorMessage = "Location not available in zone" });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]  = character.Id;
        ctx.Items["CurrentZoneId"] = "fenwick-crossing";

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("nonexistent-slug"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task NavigateToLocation_Should_Send_Error_When_Mediator_Throws()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<NavigateToLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]  = character.Id;
        ctx.Items["CurrentZoneId"] = "fenwick-crossing";

        await hub.NavigateToLocation(new NavigateToLocationHubRequest("fenwick-market"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    // NavigateToLocationHubCommandHandler
    private static ContentDbContext CreateContentDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ZoneLocation MakeZoneLocation(string slug, string zoneId, string locationType = "location") =>
        new() { Slug = slug, ZoneId = zoneId, LocationType = locationType, IsActive = true, DisplayName = slug };

    [Fact]
    public async Task NavigateToLocation_Handler_Should_Persist_Location_And_Return_Info()
    {
        await using var contentDb  = CreateContentDbContext();
        await using var db         = _factory.CreateContext();
        var accountId              = await SeedAccountAsync(db);
        var character              = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocations.Add(MakeZoneLocation("fenwick-market", "fenwick-crossing"));
        await contentDb.SaveChangesAsync();

        var handler = new NavigateToLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<NavigateToLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new NavigateToLocationHubCommand(character.Id, "fenwick-market", "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be("fenwick-market");
        result.LocationType.Should().Be("location");

        var updated = await new CharacterRepository(db).GetByIdAsync(character.Id);
        updated!.CurrentZoneLocationSlug.Should().Be("fenwick-market");
    }

    [Fact]
    public async Task NavigateToLocation_Handler_Should_Fail_When_Location_Not_In_Zone()
    {
        await using var contentDb  = CreateContentDbContext();
        await using var db         = _factory.CreateContext();
        var accountId              = await SeedAccountAsync(db);
        var character              = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocations.Add(MakeZoneLocation("other-location", "different-zone"));
        await contentDb.SaveChangesAsync();

        var handler = new NavigateToLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<NavigateToLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new NavigateToLocationHubCommand(character.Id, "other-location", "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not available");
    }

    [Fact]
    public async Task NavigateToLocation_Handler_Should_Fail_When_Location_Slug_Is_Empty()
    {
        await using var contentDb  = CreateContentDbContext();
        await using var db         = _factory.CreateContext();
        var accountId              = await SeedAccountAsync(db);

        var handler = new NavigateToLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<NavigateToLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new NavigateToLocationHubCommand(accountId, "", "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task NavigateToLocation_Handler_Should_Fail_When_Zone_Id_Is_Empty()
    {
        await using var contentDb  = CreateContentDbContext();
        await using var db         = _factory.CreateContext();
        var accountId              = await SeedAccountAsync(db);

        var handler = new NavigateToLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<NavigateToLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new NavigateToLocationHubCommand(accountId, "fenwick-market", ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task NavigateToLocation_Handler_Should_PersistPassiveDiscovery_WhenLevelMeetsThreshold()
    {
        await using var contentDb  = CreateContentDbContext();
        await using var db         = _factory.CreateContext();
        var accountId              = await SeedAccountAsync(db);
        var character              = new Character
        {
            AccountId = accountId,
            Name      = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level     = 10,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        // A visible location to navigate to, plus a hidden passive-check location
        contentDb.ZoneLocations.AddRange(
            MakeZoneLocation("fenwick-market", "fenwick-crossing"),
            new RealmEngine.Data.Entities.ZoneLocation
            {
                Slug         = "hidden-grotto",
                ZoneId       = "fenwick-crossing",
                LocationType = "dungeon",
                IsActive     = true,
                DisplayName  = "Hidden Grotto",
                Traits       = new RealmEngine.Data.Entities.ZoneLocationTraits
                {
                    IsHidden          = true,
                    UnlockType        = "skill_check_passive",
                    DiscoverThreshold = 5,  // level 10 >= 5 → should trigger
                },
            });
        await contentDb.SaveChangesAsync();

        var handler = new NavigateToLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<NavigateToLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new NavigateToLocationHubCommand(character.Id, "fenwick-market", "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PassiveDiscoveries.Should().ContainSingle(l => l.Slug == "hidden-grotto");
        db.CharacterUnlockedLocations.Should().ContainSingle(u =>
            u.CharacterId == character.Id && u.LocationSlug == "hidden-grotto");
    }

    // UnlockZoneLocation hub method
    [Fact]
    public async Task UnlockZoneLocation_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("secret-cave", "quest"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task UnlockZoneLocation_Should_Dispatch_Command_And_Broadcast_ZoneLocationUnlocked()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<UnlockZoneLocationHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnlockZoneLocationHubResult
            {
                Success             = true,
                LocationSlug        = "secret-cave",
                LocationDisplayName = "Secret Cave",
                LocationType        = "dungeon",
                WasAlreadyUnlocked  = false,
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"] = character.Id;

        await hub.UnlockZoneLocation(new UnlockZoneLocationHubRequest("secret-cave", "quest"));

        mediatorMock.Verify(
            m => m.Send(It.Is<UnlockZoneLocationHubCommand>(
                c => c.LocationSlug == "secret-cave" && c.UnlockSource == "quest"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        clients.CallerProxy.SentMessages
            .Should().Contain(m => m.Method == "ZoneLocationUnlocked");
    }

    // SearchArea hub method
    [Fact]
    public async Task SearchArea_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.SearchArea();

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task SearchArea_Should_Send_Error_When_Not_In_Zone()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);
        var (hub, clients, _, ctx) = CreateHub(db, accountId);
        ctx.Items["CharacterId"] = character.Id;
        // No "CurrentZoneId" in context items

        await hub.SearchArea();

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task SearchArea_Should_Dispatch_Command_And_Broadcast_AreaSearched()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<SearchAreaHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchAreaHubResult { Success = true, RollValue = 7, AnyFound = false, Discovered = [] });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CurrentZoneId"] = "fenwick-crossing";

        await hub.SearchArea();

        mediatorMock.Verify(
            m => m.Send(It.Is<SearchAreaHubCommand>(
                c => c.CharacterId == character.Id && c.ZoneId == "fenwick-crossing"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        clients.CallerProxy.SentMessages
            .Should().Contain(m => m.Method == "AreaSearched");
    }

    // TraverseConnection hub method
    [Fact]
    public async Task TraverseConnection_Should_Send_Error_When_No_Character_Selected()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var (hub, clients, _, _) = CreateHub(db, accountId);

        await hub.TraverseConnection(new TraverseConnectionHubRequest("fenwick-market", "path"));

        clients.CallerProxy.SentMessages
            .Should().ContainSingle(m => m.Method == "Error");
    }

    [Fact]
    public async Task TraverseConnection_Should_Dispatch_Command_And_Broadcast_ConnectionTraversed()
    {
        await using var db = _factory.CreateContext();
        var accountId      = await SeedAccountAsync(db);
        var character      = await SeedCharacterAsync(db, accountId);

        var mediatorMock = new Mock<ISender>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IRequest<TraverseConnectionHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TraverseConnectionHubResult
            {
                Success        = true,
                ToLocationSlug = "fenwick-inn",
                ToZoneId       = null,
                IsCrossZone    = false,
                ConnectionType = "path",
            });

        var (hub, clients, _, ctx) = CreateHub(db, accountId, mediator: mediatorMock.Object);
        ctx.Items["CharacterId"]   = character.Id;
        ctx.Items["CharacterName"] = character.Name;
        ctx.Items["CurrentZoneId"] = "fenwick-crossing";

        await hub.TraverseConnection(new TraverseConnectionHubRequest("fenwick-market", "path"));

        mediatorMock.Verify(
            m => m.Send(It.Is<TraverseConnectionHubCommand>(
                c => c.CharacterId == character.Id && c.FromLocationSlug == "fenwick-market"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        clients.CallerProxy.SentMessages
            .Should().Contain(m => m.Method == "ConnectionTraversed");
    }

    // UnlockZoneLocationHubCommandHandler
    [Fact]
    public async Task UnlockZoneLocation_Handler_Should_Unlock_And_Return_Info()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocations.Add(new RealmEngine.Data.Entities.ZoneLocation
        {
            Slug         = "secret-cave",
            ZoneId       = "fenwick-crossing",
            LocationType = "dungeon",
            IsActive     = true,
            DisplayName  = "Secret Cave",
            Traits       = new RealmEngine.Data.Entities.ZoneLocationTraits { IsHidden = true },
        });
        await contentDb.SaveChangesAsync();

        var handler = new UnlockZoneLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<UnlockZoneLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UnlockZoneLocationHubCommand(character.Id, "secret-cave", "quest"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be("secret-cave");
        result.WasAlreadyUnlocked.Should().BeFalse();
        db.CharacterUnlockedLocations.Should().ContainSingle(u =>
            u.CharacterId == character.Id && u.LocationSlug == "secret-cave");
    }

    [Fact]
    public async Task UnlockZoneLocation_Handler_Should_Return_WasAlreadyUnlocked_When_Duplicate()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocations.Add(new RealmEngine.Data.Entities.ZoneLocation
        {
            Slug         = "secret-cave",
            ZoneId       = "fenwick-crossing",
            LocationType = "dungeon",
            IsActive     = true,
            DisplayName  = "Secret Cave",
            Traits       = new RealmEngine.Data.Entities.ZoneLocationTraits { IsHidden = true },
        });
        await contentDb.SaveChangesAsync();

        var unlockedRepo = new CharacterUnlockedLocationRepository(db);
        await unlockedRepo.AddUnlockAsync(character.Id, "secret-cave", "quest");

        var handler = new UnlockZoneLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            unlockedRepo,
            NullLogger<UnlockZoneLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UnlockZoneLocationHubCommand(character.Id, "secret-cave", "quest"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.WasAlreadyUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task UnlockZoneLocation_Handler_Should_Fail_For_NonHiddenLocation()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocations.Add(MakeZoneLocation("visible-market", "fenwick-crossing"));
        await contentDb.SaveChangesAsync();

        var handler = new UnlockZoneLocationHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<UnlockZoneLocationHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new UnlockZoneLocationHubCommand(character.Id, "visible-market", "quest"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a hidden location");
    }

    // TraverseConnectionHubCommandHandler
    [Fact]
    public async Task TraverseConnection_Handler_Should_Traverse_SameZone_Connection()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocationConnections.Add(new RealmEngine.Data.Entities.ZoneLocationConnection
        {
            FromLocationSlug = "fenwick-market",
            ToLocationSlug   = "fenwick-inn",
            ConnectionType   = "path",
            IsTraversable    = true,
        });
        await contentDb.SaveChangesAsync();

        var handler = new TraverseConnectionHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            NullLogger<TraverseConnectionHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new TraverseConnectionHubCommand(character.Id, "fenwick-market", "path"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ToLocationSlug.Should().Be("fenwick-inn");
        result.IsCrossZone.Should().BeFalse();
        var updated = await new CharacterRepository(db).GetByIdAsync(character.Id);
        updated!.CurrentZoneLocationSlug.Should().Be("fenwick-inn");
    }

    [Fact]
    public async Task TraverseConnection_Handler_Should_Fail_When_Connection_Is_Blocked()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = await SeedCharacterAsync(db, accountId);

        contentDb.ZoneLocationConnections.Add(new RealmEngine.Data.Entities.ZoneLocationConnection
        {
            FromLocationSlug = "fenwick-market",
            ToLocationSlug   = "locked-gate",
            ConnectionType   = "gate",
            IsTraversable    = false,
        });
        await contentDb.SaveChangesAsync();

        var handler = new TraverseConnectionHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            NullLogger<TraverseConnectionHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new TraverseConnectionHubCommand(character.Id, "fenwick-market", "gate"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("blocked");
    }

    [Fact]
    public async Task TraverseConnection_Handler_Should_Fail_When_No_Connection_Exists()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = await SeedCharacterAsync(db, accountId);

        var handler = new TraverseConnectionHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            NullLogger<TraverseConnectionHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new TraverseConnectionHubCommand(character.Id, "nonexistent-spot", "path"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No connection");
    }

    // SearchAreaHubCommandHandler
    [Fact]
    public async Task SearchArea_Handler_Should_Return_Error_When_Character_Not_Found()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();

        var handler = new SearchAreaHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<SearchAreaHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new SearchAreaHubCommand(Guid.NewGuid(), "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Character not found");
    }

    [Fact]
    public async Task SearchArea_Handler_Should_Unlock_Active_Locations_When_Roll_Meets_Threshold()
    {
        await using var contentDb = CreateContentDbContext();
        await using var db        = _factory.CreateContext();
        var accountId             = await SeedAccountAsync(db);
        var character             = new Character
        {
            AccountId = accountId,
            Name      = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level     = 10,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();

        contentDb.ZoneLocations.Add(new RealmEngine.Data.Entities.ZoneLocation
        {
            Slug         = "hidden-altar",
            ZoneId       = "fenwick-crossing",
            LocationType = "location",
            IsActive     = true,
            DisplayName  = "Hidden Altar",
            Traits       = new RealmEngine.Data.Entities.ZoneLocationTraits
            {
                IsHidden          = true,
                UnlockType        = "skill_check_active",
                DiscoverThreshold = 1,  // level 10 + even worst roll (-5) = 5 >= 1 → always found
            },
        });
        await contentDb.SaveChangesAsync();

        var handler = new SearchAreaHubCommandHandler(
            new EfCoreZoneLocationRepository(contentDb, NullLogger<EfCoreZoneLocationRepository>.Instance),
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            NullLogger<SearchAreaHubCommandHandler>.Instance);

        var result = await handler.Handle(
            new SearchAreaHubCommand(character.Id, "fenwick-crossing"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AnyFound.Should().BeTrue();
        result.Discovered.Should().ContainSingle(d => d.Slug == "hidden-altar");
        db.CharacterUnlockedLocations.Should().ContainSingle(u =>
            u.CharacterId == character.Id && u.LocationSlug == "hidden-altar");
    }
}

