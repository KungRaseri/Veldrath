using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Hubs;
using Veldrath.Server.Services;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Recording proxy for a single addressed connection (returned by <c>IHubClients.Client</c>).</summary>
public sealed class FakeSingleClientProxy : FakeClientProxy, ISingleClientProxy
{
    /// <inheritdoc />
    public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((method, args));
        return Task.FromResult(default(T)!);
    }
}

/// <summary>
/// Record-everything <see cref="IHubCallerClients"/> for hub tests. Unlike
/// <see cref="FakeHubCallerClients"/> it exposes an individual proxy per
/// broadcast target (<see cref="All"/>, <see cref="Client"/>, group, others-in-group)
/// so a test can assert exactly which audience received which message.
/// </summary>
public sealed class RecordingHubClients : IHubCallerClients
{
    private readonly Dictionary<string, FakeClientProxy> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeClientProxy> _othersInGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeSingleClientProxy> _connections = new(StringComparer.Ordinal);
    private readonly FakeClientProxy _except = new();
    private readonly FakeClientProxy _users = new();
    private readonly FakeClientProxy _user = new();
    private readonly FakeClientProxy _groupsMany = new();

    /// <summary>Messages sent to the calling connection.</summary>
    public FakeClientProxy Caller { get; } = new();

    /// <summary>Messages sent to every other connection ("all except caller").</summary>
    public FakeClientProxy Others { get; } = new();

    /// <summary>Messages broadcast to the entire server.</summary>
    public FakeClientProxy All { get; } = new();

    /// <summary>Returns the proxy that received messages sent to the named SignalR group.</summary>
    /// <param name="groupName">Group name.</param>
    /// <returns>The recording proxy for that group (created on demand).</returns>
    public FakeClientProxy GroupNamed(string groupName) => Proxy(_groups, groupName);

    /// <summary>Returns the proxy that received messages sent to everyone-but-caller in a group.</summary>
    /// <param name="groupName">Group name.</param>
    /// <returns>The recording proxy for that group (created on demand).</returns>
    public FakeClientProxy OthersInGroupNamed(string groupName) => Proxy(_othersInGroups, groupName);

    /// <summary>Returns the proxy that received messages addressed to a single connection id.</summary>
    /// <param name="connectionId">Connection id.</param>
    /// <returns>The recording proxy for that connection (created on demand).</returns>
    public FakeSingleClientProxy Connection(string connectionId) =>
        ProxySingle(_connections, connectionId);

    IClientProxy IHubCallerClients<IClientProxy>.Caller => Caller;

    IClientProxy IHubCallerClients<IClientProxy>.Others => Others;

    IClientProxy IHubClients<IClientProxy>.All => All;

    IClientProxy IHubCallerClients<IClientProxy>.OthersInGroup(string groupName) => OthersInGroupNamed(groupName);

    IClientProxy IHubClients<IClientProxy>.Group(string groupName) => GroupNamed(groupName);

    IClientProxy IHubClients<IClientProxy>.AllExcept(IReadOnlyList<string> excludedConnectionIds) => _except;

    IClientProxy IHubClients<IClientProxy>.Client(string connectionId) => Connection(connectionId);

    IClientProxy IHubClients<IClientProxy>.Clients(IReadOnlyList<string> connectionIds) => _groupsMany;

    IClientProxy IHubClients<IClientProxy>.GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _except;

    IClientProxy IHubClients<IClientProxy>.Groups(IReadOnlyList<string> groupNames) => _groupsMany;

    IClientProxy IHubClients<IClientProxy>.User(string userId) => _user;

    IClientProxy IHubClients<IClientProxy>.Users(IReadOnlyList<string> userIds) => _users;

    /// <summary>Every message sent to any audience, in send order per audience.</summary>
    /// <returns>All recorded (method, args) pairs across every proxy.</returns>
    public IEnumerable<(string Method, object?[] Args)> EveryMessage =>
        new[] { Caller, Others, All }
            .SelectMany(proxy => proxy.SentMessages)
            .Concat(_groups.Values.SelectMany(proxy => proxy.SentMessages))
            .Concat(_othersInGroups.Values.SelectMany(proxy => proxy.SentMessages))
            .Concat(_connections.Values.SelectMany(proxy => proxy.SentMessages));

    private static FakeClientProxy Proxy(Dictionary<string, FakeClientProxy> map, string key)
    {
        if (!map.TryGetValue(key, out var proxy))
        {
            proxy = new FakeClientProxy();
            map[key] = proxy;
        }
        return proxy;
    }

    private static FakeSingleClientProxy ProxySingle(Dictionary<string, FakeSingleClientProxy> map, string key)
    {
        if (!map.TryGetValue(key, out var proxy))
        {
            proxy = new FakeSingleClientProxy();
            map[key] = proxy;
        }
        return proxy;
    }
}

/// <summary>
/// Builds a <see cref="GameHub"/> wired to fakes, seeding and mediator stubs for behaviour tests.
/// </summary>
internal static class HubHarness
{
    /// <summary>Creates a claims principal carrying the JWT subject and optional permission claims.</summary>
    /// <param name="accountId">Account id placed in the <c>sub</c> claim.</param>
    /// <param name="permissions">Values placed in <c>permission</c> claims.</param>
    /// <returns>The principal used by <see cref="FakeHubCallerContext"/>.</returns>
    public static ClaimsPrincipal User(Guid accountId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, "TestUser"),
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    /// <summary>Creates a user-manager mock that resolves to <paramref name="account"/> and always saves successfully.</summary>
    /// <param name="account">Account returned by <c>FindByIdAsync</c>/<c>FindByNameAsync</c>, or <see langword="null"/>.</param>
    /// <returns>The configured mock.</returns>
    public static Mock<UserManager<PlayerAccount>> NewUserManager(PlayerAccount? account = null)
    {
        var manager = new Mock<UserManager<PlayerAccount>>(
            Mock.Of<IUserStore<PlayerAccount>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(account);
        manager.Setup(m => m.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(account);
        manager.Setup(m => m.UpdateAsync(It.IsAny<PlayerAccount>())).ReturnsAsync(IdentityResult.Success);
        return manager;
    }

    /// <summary>Seeds a <see cref="PlayerAccount"/> with a unique user name.</summary>
    /// <param name="db">Context to write to.</param>
    /// <returns>The created account.</returns>
    public static async Task<PlayerAccount> SeedAccountAsync(ApplicationDbContext db)
    {
        var name = $"User_{Guid.NewGuid():N}";
        var account = new PlayerAccount { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    /// <summary>Seeds a character owned by <paramref name="accountId"/>.</summary>
    /// <param name="db">Context to write to.</param>
    /// <param name="accountId">Owning account.</param>
    /// <param name="level">Character level.</param>
    /// <param name="attributes">Attributes blob; defaults to an empty object.</param>
    /// <returns>The created character.</returns>
    public static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        Guid accountId,
        int level = 1,
        string attributes = "{}")
    {
        var character = new Character
        {
            AccountId = accountId,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Level = level,
            Attributes = attributes,
            CurrentZoneId = "crestfall",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    /// <summary>Seeds a live <see cref="PlayerSession"/> so hub lookups by connection/name/zone resolve.</summary>
    /// <param name="db">Context to write to.</param>
    /// <param name="character">Session owner.</param>
    /// <param name="connectionId">Connection id of the session.</param>
    /// <param name="zoneId">Zone the session sits in, or <see langword="null"/> for the region map.</param>
    /// <param name="regionId">Region the session sits in.</param>
    /// <returns>The created session.</returns>
    public static async Task<PlayerSession> SeedSessionAsync(
        ApplicationDbContext db,
        Character character,
        string connectionId,
        string? zoneId = null,
        string regionId = "varenmark")
    {
        var session = new PlayerSession
        {
            CharacterId = character.Id,
            CharacterName = character.Name,
            ConnectionId = connectionId,
            RegionId = regionId,
            ZoneId = zoneId,
        };
        db.PlayerSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    /// <summary>Configures <see cref="ISender.Send{TResponse}"/> to return <paramref name="response"/> for the matching request type.</summary>
    /// <typeparam name="TResponse">Result type of the request.</typeparam>
    /// <param name="sender">The mediator mock.</param>
    /// <param name="response">Result to return.</param>
    /// <returns>The same mock, for chaining.</returns>
    public static Mock<ISender> Returns<TResponse>(this Mock<ISender> sender, TResponse response)
        where TResponse : class
    {
        sender.Setup(s => s.Send(It.IsAny<IRequest<TResponse>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(response);
        return sender;
    }

    /// <summary>Configures <see cref="ISender.Send{TResponse}"/> to throw for the matching request type.</summary>
    /// <typeparam name="TResponse">Result type of the request.</typeparam>
    /// <param name="sender">The mediator mock.</param>
    /// <param name="exception">Exception to throw.</param>
    /// <returns>The same mock, for chaining.</returns>
    public static Mock<ISender> Throws<TResponse>(this Mock<ISender> sender, Exception exception)
        where TResponse : class
    {
        sender.Setup(s => s.Send(It.IsAny<IRequest<TResponse>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(exception);
        return sender;
    }

    /// <summary>Creates the hub plus its recording audience proxies and caller context.</summary>
    /// <param name="db">Context the hub reads and writes directly.</param>
    /// <param name="accountId">Account id of the calling user.</param>
    /// <param name="mediator">Mediator mock; a bare mock is created when omitted.</param>
    /// <param name="userManager">User-manager mock; an all-null mock is created when omitted.</param>
    /// <param name="mapRepo">Map-graph repository mock.</param>
    /// <param name="tracker">Active-character tracker.</param>
    /// <param name="sessions">Combat-session store.</param>
    /// <param name="enemies">Enemy-instance store.</param>
    /// <param name="afk">AFK tracker.</param>
    /// <param name="random">Random source.</param>
    /// <param name="moderation">Moderation options.</param>
    /// <param name="connectionId">Caller connection id.</param>
    /// <param name="permissions">Permission claims granted to the caller.</param>
    /// <returns>The hub and the fakes around it.</returns>
    public static (
        GameHub Hub,
        RecordingHubClients Clients,
        FakeGroupManager Groups,
        FakeHubCallerContext Ctx,
        Mock<ISender> Mediator,
        AfkTracker Afk) Create(
        ApplicationDbContext db,
        Guid accountId,
        Mock<ISender>? mediator = null,
        Mock<UserManager<PlayerAccount>>? userManager = null,
        Mock<IMapGraphRepository>? mapRepo = null,
        IActiveCharacterTracker? tracker = null,
        FakeCombatSessionStore? sessions = null,
        FakeEnemyInstanceStore? enemies = null,
        AfkTracker? afk = null,
        IRandomSource? random = null,
        ModerationOptions? moderation = null,
        string connectionId = "conn-1",
        params string[] permissions)
    {
        var sender = mediator ?? new Mock<ISender>();
        var afkTracker = afk ?? new AfkTracker();

        var hub = new GameHub(
            NullLogger<GameHub>.Instance,
            new CharacterRepository(db),
            new ZoneRepository(db),
            new RegionRepository(db),
            new PlayerSessionRepository(db),
            tracker ?? new ActiveCharacterTracker(),
            sender.Object,
            (mapRepo ?? new Mock<IMapGraphRepository>()).Object,
            Options.Create(new VersionCompatibilitySettings()),
            (userManager ?? NewUserManager()).Object,
            Options.Create(moderation ?? new ModerationOptions()),
            db,
            afkTracker,
            sessions ?? new FakeCombatSessionStore(),
            enemies ?? new FakeEnemyInstanceStore(),
            new Veldrath.Server.Features.Zones.EfCoreZoneInstanceService(
                db,
                NullLogger<Veldrath.Server.Features.Zones.EfCoreZoneInstanceService>.Instance),
            random);

        var clients = new RecordingHubClients();
        var groups = new FakeGroupManager();
        var ctx = new FakeHubCallerContext(connectionId, User(accountId, permissions));

        hub.Clients = clients;
        hub.Groups = groups;
        hub.Context = ctx;

        return (hub, clients, groups, ctx, sender, afkTracker);
    }

    /// <summary>Puts the caller into a zone so zone-scoped hub methods do not short-circuit.</summary>
    /// <param name="ctx">Caller context.</param>
    /// <param name="characterId">Selected character id.</param>
    /// <param name="characterName">Selected character name.</param>
    /// <param name="zoneId">Current zone id.</param>
    /// <param name="zoneGroup">Current zone SignalR group name.</param>
    public static void EnterZoneState(
        this FakeHubCallerContext ctx,
        Guid characterId,
        string characterName = "Tester",
        string zoneId = "crestfall",
        string zoneGroup = "zone:crestfall")
    {
        ctx.Items["CharacterId"] = characterId;
        ctx.Items["CharacterName"] = characterName;
        ctx.Items["CurrentZoneId"] = zoneId;
        ctx.Items["CurrentZoneGroupName"] = zoneGroup;
    }
}
