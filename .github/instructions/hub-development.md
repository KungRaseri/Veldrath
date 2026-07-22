# Hub Development (SignalR)

Scope: [`Veldrath.Server`](../../Veldrath.Server/) — SignalR hub development. Also relevant to [`Veldrath.GameClient.Core`](../../Veldrath.GameClient.Core/) (the client-side hub connection service) and [`Veldrath.Contracts`](../../Veldrath.Contracts/) (shared DTOs).

---

## The Hub→MediatR Bridge Pattern

This is THE architectural rule for all hub methods. SignalR hubs never contain game logic — they are thin adapters that delegate to MediatR:

```
SignalR Hub → MediatR.Send(command) → Handler → Response → Hub returns result to client
```

### What This Means in Practice

- **Hub methods NEVER call Core handlers directly.** They always go through `IMediator`/`ISender`.
- **Hub methods NEVER contain game logic.** No damage calculations, no gold arithmetic, no XP formulas.
- **Hub methods ALWAYS delegate to MediatR.** The hub validates SignalR-specific concerns (connection state, authorization), then dispatches.
- **The hub is a THIN adapter.** Its responsibilities are:
  1. Validate the connection has a selected character (`TryGetCharacterId`)
  2. Validate the connection is in the correct context (zone, region)
  3. Call `_mediator.Send(command)`
  4. Map the result to a client broadcast or caller response
  5. Catch exceptions and send `"Error"` to the caller

### Example

```csharp
/// <summary>
/// Requests a one-tile movement for the caller's active character.
/// </summary>
/// <param name="request">Target tile coordinates and facing direction.</param>
public async Task MoveCharacter(MoveCharacterHubRequest request)
{
    // 1. Validate connection state
    if (!TryGetCharacterId(out var characterId))
    {
        await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before MoveCharacter");
        return;
    }

    var zoneId = Context.Items.TryGetValue("CurrentZoneId", out var z) && z is string s ? s : string.Empty;
    if (string.IsNullOrEmpty(zoneId))
    {
        await Clients.Caller.SendAsync("Error", "EnterZone must be called before MoveCharacter");
        return;
    }

    try
    {
        // 2. Dispatch to MediatR
        var result = await _mediator.Send(new MoveCharacterHubCommand(
            characterId, request.ToX, request.ToY, request.Direction, zoneId));

        // 3. Handle result
        if (!result.Success)
        {
            await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Move rejected");
            return;
        }

        // 4. Broadcast to appropriate group
        var payload = new CharacterMovedPayload(characterId, result.TileX, result.TileY, result.Direction);
        if (TryGetCurrentZoneGroup(out var zoneGroup))
            await Clients.Group(zoneGroup).SendAsync("CharacterMoved", payload);

        // 5. Trigger side-effects
        if (result.ZoneEntryTriggered is not null)
            await Clients.Caller.SendAsync("ZoneEntryTriggered", result.ZoneEntryTriggered);
        else if (result.TileExitTriggered is not null)
            await Clients.Caller.SendAsync("TileExitTriggered", result.TileExitTriggered);
    }
    catch (Exception ex)
    {
        // 6. Catch and convert to error message
        _logger.LogError(ex, "Error in MoveCharacter for character {CharacterId}", characterId);
        await Clients.Caller.SendAsync("Error", "Failed to process movement");
    }
}
```

---

## The Single-DTO Parameter Rule (CRITICAL)

**SignalR hub methods MUST accept a single request DTO parameter.** Multiple primitive parameters cause SILENT binding failures at runtime — no exception, no error log, the method simply never gets called.

### Why This Happens

When a client sends `HubConnection.InvokeAsync("DoThing", new { Amount = 100, Source = "combat" })`, SignalR deserializes the JSON object. If the hub method signature is `DoThing(int amount, string source)`, SignalR tries to deserialize the entire JSON object `{ Amount: 100, Source: "combat" }` as the first `int` parameter — which fails silently.

### Wrong ❌

```csharp
// SIGNALR WILL SILENTLY FAIL TO BIND THESE
public async Task GainExperience(int amount, string? source = null) { }
public async Task AddGold(int amount, string? source = null) { }
public async Task TakeDamage(int damageAmount, string? source = null) { }
```

### Right ✅

```csharp
// ALWAYS use a single DTO record parameter
public async Task GainExperience(GainExperienceHubRequest request) { }
public async Task AddGold(AddGoldHubRequest request) { }
public async Task TakeDamage(TakeDamageHubRequest request) { }
```

### DTO Location

Hub request DTOs can live in one of two places:

1. **Bottom of [`GameHub.cs`](../../Veldrath.Server/Hubs/GameHub.cs)** — For DTOs that are strictly internal to the hub method signature (not shared with client code):
```csharp
// At bottom of GameHub.cs
/// <summary>Request payload for <see cref="GameHub.GainExperience"/>.</summary>
/// <param name="Amount">Positive number of experience points to award.</param>
/// <param name="Source">Optional label for the XP source (e.g. <c>"Combat"</c>, <c>"Quest"</c>).</param>
public record GainExperienceHubRequest(int Amount, string? Source = null);
```

2. **[`Veldrath.Contracts/`](../../Veldrath.Contracts/)** — For DTOs shared between server and client (connection, chat, tilemap contracts):
```csharp
// In Veldrath.Contracts/Connection/ConnectionContracts.cs
/// <summary>Request payload for sending a chat message.</summary>
/// <param name="Message">Raw message text. Prefixing with <c>/</c> triggers command parsing.</param>
public record ChatMessageHubRequest(string Message);
```

### Zero-Parameter Methods

Methods that genuinely need no parameters can omit the DTO:

```csharp
// No parameters needed — valid
public async Task LeaveZone() { }

// Client calls:
// await connection.InvokeAsync("LeaveZone");
```

---

## Hub Structure

### Where Hubs Live

Hubs live in [`Veldrath.Server/Hubs/`](../../Veldrath.Server/Hubs/). Currently there is one primary hub:

| Hub | File | Purpose |
|---|---|---|
| [`GameHub`](../../Veldrath.Server/Hubs/GameHub.cs) | `GameHub.cs` | All gameplay operations: character selection, movement, combat, chat, inventory, shops, quests, zones |

If new domains grow large enough to warrant separation, create additional hubs (e.g., `ChatHub`, `CombatHub`) following the same pattern.

### Hub Class Structure

```csharp
[Authorize]
public class GameHub : Hub
{
    // --- Injected Dependencies ---
    private readonly ILogger<GameHub> _logger;
    private readonly ISender _mediator;       // Always inject ISender/IMediator
    private readonly ICharacterRepository _characterRepo;
    // ... other repositories and services

    // --- Static State (rare — only for cross-connection tracking) ---
    private static readonly ConcurrentDictionary<Guid, string?> _afkCharacters = new();

    // --- Constructor ---
    public GameHub(/* dependencies */) { }

    // --- Connection Lifecycle ---
    public override async Task OnConnectedAsync() { }
    public override async Task OnDisconnectedAsync(Exception? exception) { }

    // --- Session Management ---
    public async Task SelectCharacter(Guid characterId) { }
    public async Task EnterZone(string zoneId) { }
    public async Task LeaveZone() { }

    // --- Gameplay Operations ---
    public async Task MoveCharacter(MoveCharacterHubRequest request) { }
    public async Task GainExperience(GainExperienceHubRequest request) { }
    // ... etc.

    // --- Private Helpers ---
    private bool TryGetCharacterId(out Guid characterId) { }
    private static string ZoneGroup(string zoneId) => $"zone-{zoneId}";
}
```

### Method Naming

- Hub methods use **PascalCase**: `SelectCharacter`, `EnterZone`, `GainExperience`.
- Use `[HubMethodName("camelCaseName")]` only if you need the client to call a different name than the C# method. The SignalR client automatically converts PascalCase to camelCase by default in the .NET client, but explicit naming is clearer.

---

## Broadcast Conventions

### Client Proxies

| Proxy | Use Case |
|---|---|
| `Clients.Caller` | Send result/error back to the calling client only |
| `Clients.Group(groupName)` | Broadcast to all connections in a group (zone, region, party) |
| `Clients.OthersInGroup(groupName)` | Broadcast to group members EXCEPT the caller |
| `Clients.All` | Global broadcast (use sparingly — announcements, server shutdown) |

### Group Management

```csharp
// Join a group
await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

// Leave a group
await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
```

### Group Naming Conventions

```csharp
// Zone groups — all characters inside a zone
private static string ZoneGroup(string zoneId) => $"zone-{zoneId}";

// Region groups — all characters on a region map
private static string RegionGroup(string regionId) => $"region-{regionId}";

// Account groups — all connections for the same account
private static string AccountGroup(Guid accountId) => $"account-{accountId}";
```

### Broadcast Payload Pattern

```csharp
// Send structured data, not raw strings
var payload = new CharacterMovedPayload(characterId, result.TileX, result.TileY, result.Direction);
await Clients.Group(ZoneGroup(zoneId)).SendAsync("CharacterMoved", payload);

// Error messages are the exception — simple strings
await Clients.Caller.SendAsync("Error", "Insufficient gold");
```

---

## Error Handling in Hubs

### The Golden Rule: Return Errors, Don't Throw

Never let exceptions propagate to SignalR. Catch everything and convert to structured error messages sent to the caller:

```csharp
try
{
    var result = await _mediator.Send(command);

    if (!result.Success)
    {
        await Clients.Caller.SendAsync("Error", result.ErrorMessage ?? "Operation failed");
        return;
    }

    // Success path...
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in {MethodName} for character {CharacterId}", methodName, characterId);
    await Clients.Caller.SendAsync("Error", "An unexpected error occurred");
}
```

### Result Object Pattern

Hub command handlers return result DTOs with a `Success` bool and `ErrorMessage` string:

```csharp
/// <summary>Result returned by <see cref="AddGoldHubCommandHandler"/>.</summary>
public record AddGoldHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the character's gold total after the operation.</summary>
    public int NewGoldTotal { get; init; }
}
```

The hub checks `result.Success` and sends `"Error"` to the caller on failure.

---

## Authorization

### Hub-Level Authorization

```csharp
[Authorize]  // Requires authenticated user (valid JWT)
public class GameHub : Hub
```

### Connection Guard Pattern

Every gameplay hub method must verify the connection has an active character:

```csharp
if (!TryGetCharacterId(out var characterId))
{
    await Clients.Caller.SendAsync("Error", "SelectCharacter must be called before ...");
    return;
}
```

### Character Ownership

When operating on a character, verify the authenticated user owns it. This is handled in `SelectCharacter`:

```csharp
if (character.AccountId != accountId)
{
    await Clients.Caller.SendAsync("Error", "Character does not belong to this account");
    return;
}
```

Subsequent hub methods trust the `TryGetCharacterId` guard — it validates via `IActiveCharacterTracker` which only tracks characters that passed the ownership check in `SelectCharacter`.

### Context-Based State

The hub stores per-connection state in `Context.Items`:

| Key | Type | Set By |
|---|---|---|
| `AccountId` | `Guid` | `OnConnectedAsync` |
| `CharacterId` | `Guid` | `SelectCharacter` |
| `CharacterName` | `string` | `SelectCharacter` |
| `CurrentRegionId` | `string` | `SelectCharacter`, `ChangeRegion` |
| `CurrentZoneId` | `string` | `EnterZone`, `ExitZone` |
| `DifficultyMode` | `string` | `SelectCharacter` |

---

## DI in Hubs

### What to Inject

| Dependency | Always? | Notes |
|---|---|---|
| `ISender` / `IMediator` | **Always** | The MediatR dispatcher — every gameplay method needs this |
| `ILogger<THub>` | **Always** | For logging errors and connection events |
| Repositories (`ICharacterRepository`, `IZoneRepository`, etc.) | **As needed** | Only for session management lookups (`SelectCharacter`, `EnterZone`). Gameplay methods dispatch to MediatR instead of querying repos directly |
| `UserManager<PlayerAccount>` | **As needed** | For auth/admin operations (ban checks, role lookups) |
| `IOptions<T>` | **As needed** | For configuration access (version settings, moderation options) |
| `ApplicationDbContext` | **Rarely** | Only for operations that don't fit the MediatR pattern (e.g., `OnConnectedAsync` ban checks). Prefer MediatR |

### How to Inject

Constructor injection only — **never** use `[FromServices]` on hub method parameters:

```csharp
// RIGHT: Constructor injection
public GameHub(
    ILogger<GameHub> logger,
    ISender mediator,
    ICharacterRepository characterRepo)
{
    _logger = logger;
    _mediator = mediator;
    _characterRepo = characterRepo;
}

// WRONG: [FromServices] on method parameters
public async Task DoThing([FromServices] ILogger<GameHub> logger, DoThingRequest request) { }
```

---

## Client-Side Correspondence

For every hub method, there must be corresponding client-side support in [`Veldrath.GameClient.Core/`](../../Veldrath.GameClient.Core/).

### Hub Connection Service

[`IGameHubConnectionService`](../../Veldrath.GameClient.Core/Abstractions/IGameHubConnectionService.cs) manages the SignalR connection lifecycle:

```csharp
// In Veldrath.GameClient.Core/Services/GameHubConnectionService.cs
public sealed class GameHubConnectionService : IGameHubConnectionService, IAsyncDisposable
{
    private HubConnection? _connection;

    public async Task ConnectAsync(string serverUrl, string accessToken, CancellationToken ct = default) { }

    // Client calls hub methods via:
    // await _connection.InvokeAsync<T>("MethodName", request);
    // await _connection.InvokeAsync("MethodName");  // zero-arg methods

    // Client receives broadcasts via:
    // _connection.On<T>("EventName", handler);
}
```

### Payload Types

Payloads for both directions (client→server and server→client) live in [`Veldrath.GameClient.Core/Payloads/`](../../Veldrath.GameClient.Core/Payloads/) organized by domain:

| File | Domain |
|---|---|
| [`ChatPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/ChatPayloads.cs) | Chat messages, commands |
| [`CombatPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/CombatPayloads.cs) | Combat events, damage, death |
| [`DungeonPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/DungeonPayloads.cs) | Dungeon enter/exit |
| [`EconomyPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/EconomyPayloads.cs) | Gold, currency |
| [`EntityPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/EntityPayloads.cs) | Entity spawn/despawn/move |
| [`InventoryPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/InventoryPayloads.cs) | Inventory updates |
| [`ProgressionPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/ProgressionPayloads.cs) | XP, level ups |
| [`QuestPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/QuestPayloads.cs) | Quest progress |
| [`ShopPayloads.cs`](../../Veldrath.GameClient.Core/Payloads/ShopPayloads.cs) | Shop interactions |
| [`ZonePayloads.cs`](../../Veldrath.GameClient.Core/Payloads/ZonePayloads.cs) | Zone enter/exit, movement |

### Calling Hub Methods from Client

```csharp
// Single-DTO parameter
await _connection.InvokeAsync<GainExperienceHubResult>("GainExperience",
    new GainExperienceHubRequest(100, "Combat"));

// Zero-arg method
await _connection.InvokeAsync("LeaveZone");

// Fire-and-forget (no return value expected)
await _connection.SendAsync("SendChatMessage", new ChatMessageHubRequest("Hello!"));
```

### Receiving Broadcasts from Server

```csharp
_connection.On<CharacterMovedPayload>("CharacterMoved", payload =>
{
    // Update local entity position
    _entityTracker.MoveEntity(payload.CharacterId, payload.TileX, payload.TileY);
});

_connection.On<string>("Error", message =>
{
    // Display error to user
    _logger.LogWarning("Server error: {Message}", message);
});
```

---

## Testing

### Hub Test Location

Hub tests live in [`Veldrath.Server.Tests/Features/`](../../Veldrath.Server.Tests/Features/):
- [`GameHubTests.cs`](../../Veldrath.Server.Tests/Features/GameHubTests.cs) — Main hub integration tests
- [`GameHubChatCommandTests.cs`](../../Veldrath.Server.Tests/Features/GameHubChatCommandTests.cs) — Chat-specific
- [`GameHubRegionTests.cs`](../../Veldrath.Server.Tests/Features/GameHubRegionTests.cs) — Region movement
- Handler-specific tests: [`AttackEnemyHubCommandHandlerTests.cs`](../../Veldrath.Server.Tests/Features/AttackEnemyHubCommandHandlerTests.cs), etc.

### Test Infrastructure

The test project uses **Fake** implementations of SignalR primitives (defined at the top of [`GameHubTests.cs`](../../Veldrath.Server.Tests/Features/GameHubTests.cs)):

```csharp
// Fake SignalR client proxy — captures sent messages for assertion
public class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> SentMessages { get; } = [];

    public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
    {
        SentMessages.Add((method, args));
        return Task.CompletedTask;
    }
}

// Fake IHubCallerClients — provides FakeClientProxy instances for Caller, Group, OthersInGroup
public class FakeHubCallerClients : IHubCallerClients
{
    public FakeClientProxy CallerProxy     { get; } = new();
    public FakeClientProxy GroupProxy      { get; } = new();
    public FakeClientProxy OtherGroupProxy { get; } = new();

    public IClientProxy Caller                          => CallerProxy;
    public IClientProxy Group(string groupName)         => GroupProxy;
    public IClientProxy OthersInGroup(string groupName) => OtherGroupProxy;
    // ... other proxies return a no-op
}

// Fake IGroupManager — captures group join/leave operations
public class FakeGroupManager : IGroupManager
{
    public List<string> AddedGroups   { get; } = [];
    public List<string> RemovedGroups { get; } = [];
    // ...
}
```

### What to Test

| Test This | Don't Test This |
|---|---|
| ✅ Hub method calls `mediator.Send` with the correct command | ❌ The actual game logic inside the handler (tested in Core) |
| ✅ Hub returns the correct response shape for error/success cases | ❌ SignalR transport/infrastructure |
| ✅ Broadcasts are sent to the correct groups | ❌ That MediatR works (it's framework code) |
| ✅ Connection guards reject unauthenticated/uncharactered calls | ❌ JWT validation (framework code) |
| ✅ Character ownership is enforced | ❌ Database queries (test repos directly) |

### Test Pattern

```csharp
[Fact]
public async Task GainExperience_WithValidCharacter_CallsMediatorAndBroadcasts()
{
    // Arrange
    var mediator = new Mock<ISender>();
    mediator.Setup(m => m.Send(It.IsAny<GainExperienceHubCommand>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new GainExperienceHubResult { Success = true, NewTotalXp = 500 });

    var hub = CreateHub(mediator: mediator.Object);
    SetupAuthenticatedCaller(hub, characterId: testCharacterId);
    SetupZoneGroup(hub, "zone-darkwood");

    // Act
    await hub.GainExperience(new GainExperienceHubRequest(100, "Combat"));

    // Assert
    mediator.Verify(m => m.Send(
        It.Is<GainExperienceHubCommand>(c => c.CharacterId == testCharacterId && c.Amount == 100),
        It.IsAny<CancellationToken>()), Times.Once);

    Assert.Contains(hub.Clients.GroupProxy.SentMessages,
        m => m.Method == "ExperienceGained");
}

[Fact]
public async Task GainExperience_WithoutSelectedCharacter_SendsError()
{
    // Arrange
    var hub = CreateHub();
    // Do NOT call SetupAuthenticatedCaller — no character selected

    // Act
    await hub.GainExperience(new GainExperienceHubRequest(100, "Combat"));

    // Assert
    Assert.Contains(hub.Clients.CallerProxy.SentMessages,
        m => m.Method == "Error" && m.Args[0]?.ToString()!.Contains("SelectCharacter"));
}
```

---

## Anti-Patterns

These are things you must **never** do in hub code:

| ❌ Anti-Pattern | ✅ Correct Approach |
|---|---|
| Hub method with multiple parameters: `DoThing(int a, string b)` | Single DTO: `DoThing(DoThingRequest request)` |
| Hub calling Core handler directly (bypassing MediatR) | Always go through `_mediator.Send(command)` |
| Game logic in hub methods (XP formulas, damage calculations) | Hub dispatches to MediatR; handler contains logic |
| Throwing exceptions instead of returning error results | Catch, log, send `"Error"` to caller |
| `[FromServices]` on hub method parameters | Constructor injection only |
| Direct `ApplicationDbContext` access in gameplay hub methods | Go through MediatR → handler → repository |
| Hard-coding connection strings or configuration values | Use `IOptions<T>` |
| Sending raw strings as broadcast data | Use typed payload records |
| Hub method that does both session management AND gameplay | Session management is `SelectCharacter`/`EnterZone`; gameplay is separate MediatR-backed methods |

---

## See Also

- [`Veldrath.Server/Hubs/GameHub.cs`](../../Veldrath.Server/Hubs/GameHub.cs) — The full hub implementation (~3,300 lines, 30+ MediatR-backed methods)
- [`Veldrath.Server/Features/`](../../Veldrath.Server/Features/) — Hub command handlers organized by domain (Characters, Zones, Shop, Combat, etc.)
- [`Veldrath.Contracts/`](../../Veldrath.Contracts/) — Shared request/response DTOs
- [`Veldrath.GameClient.Core/Services/GameHubConnectionService.cs`](../../Veldrath.GameClient.Core/Services/GameHubConnectionService.cs) — Client-side hub connection management
- [`Veldrath.Server.Tests/Features/GameHubTests.cs`](../../Veldrath.Server.Tests/Features/GameHubTests.cs) — Hub test patterns and Fake infrastructure
- [`.github/agent-memory/unbound-memory.md`](../agent-memory/unbound-memory.md) — Hub→MediatR bridge history, SignalR parameter binding fix log
- [`AGENTS.md`](../../AGENTS.md) — Overall architecture and Hub→MediatR pattern overview
