using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Hubs;
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
        CreateHub(ApplicationDbContext db, Guid accountId, string connId = "conn-1")
    {
        var hub     = new GameHub(NullLogger<GameHub>.Instance,
                                  new CharacterRepository(db),
                                  new ZoneRepository(db),
                                  new ZoneSessionRepository(db));
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
}
