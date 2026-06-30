# Web Game Client Architecture — Veldrath.Web Blazor Server

> **Date:** 2026-06-30  
> **Status:** Draft / For Review  
> **Scope:** Adding browser-based game client to [`Veldrath.Web`](Veldrath.Web/) (Blazor Server Interactive SSR)

---

## Table of Contents

1. [Overview & Design Principles](#1-overview--design-principles)
2. [Component Tree & Routing](#2-component-tree--routing)
3. [Service Layer](#3-service-layer)
4. [State Management](#4-state-management)
5. [SignalR Event Catalog](#5-signalr-event-catalog)
6. [Tilemap Rendering Approach](#6-tilemap-rendering-approach)
7. [Data Flow Diagrams](#7-data-flow-diagrams)
8. [Phase 1 (MVP) vs Phase 2 Scope](#8-phase-1-mvp-vs-phase-2-scope)
9. [Implementation Order Recommendations](#9-implementation-order-recommendations)
10. [Project Changes Summary](#10-project-changes-summary)
11. [Risks & Mitigations](#11-risks--mitigations)

---

## 1. Overview & Design Principles

### Architecture Concept

```
┌──────────────────────────────────────────────────┐
│                  Browser                          │
│   (SignalR WebSocket to Blazor Server circuit)    │
└───────────────┬──────────────────────┬───────────┘
                │                      │
          Blazor SSR            Interactive Circuit
                │                      │
         ┌──────▼──────────────────────▼───────────┐
         │          Veldrath.Web (ASP.NET Core)     │
         │                                         │
         │  ┌─────────────────────────────────┐    │
         │  │  GameHubConnectionService        │    │
         │  │  (HubConnection to GameHub)      │    │
         │  └──────────┬──────────────────────┘    │
         │             │ Server-to-Server SignalR   │
         │  ┌──────────▼──────────────────────┐    │
         │  │  GameStateService               │    │
         │  │  (Scoped, per-circuit state)     │    │
         │  └──────────┬──────────────────────┘    │
         │             │                            │
         │  ┌──────────▼──────────────────────┐    │
         │  │  Blazor Components              │    │
         │  │  (Game.razor, GameTilemap.razor) │    │
         │  └─────────────────────────────────┘    │
         └─────────────────────────────────────────┘
                          │
                          │ SignalR (/hubs/game)
                          │ JWT Bearer Auth
                          ▼
         ┌─────────────────────────────────────────┐
         │         Veldrath.Server                  │
         │         GameHub (3087 lines)              │
         │         /hubs/game                       │
         └─────────────────────────────────────────┘
```

### Design Principles

1. **No new project** — All game client code lives inside [`Veldrath.Web`](Veldrath.Web/). New files are created in existing directory structure.
2. **Server-to-Server SignalR** — The Blazor Server app uses `HubConnection` (from `Microsoft.AspNetCore.SignalR.Client`) to connect to Veldrath.Server's GameHub. This connection lives on the server, not in the browser. The browser talks to Blazor via the existing Blazor Server SignalR circuit.
3. **Scoped per-circuit** — Game services are registered as scoped services so each user's Blazor circuit has its own isolated game connection and state.
4. **Reuse existing auth** — [`AuthStateService`](Veldrath.Web/Services/AuthStateService.cs) already holds the JWT in circuit memory. The `GameHubConnectionService` uses this JWT to authenticate the SignalR connection.
5. **Progressive enhancement** — MVP gets the core game loop working (character select, zone view, movement, combat, chat). Phase 2 adds inventory, shops, map, and other features.

---

## 2. Component Tree & Routing

### Route Map

| Route | Component | Auth Required | Phase |
|-------|-----------|---------------|-------|
| `/Game/CharacterSelect` | [`CharacterSelect.razor`](Veldrath.Web/Components/Pages/Game/CharacterSelect.razor) | Yes | MVP |
| `/Game/CreateCharacter` | [`CreateCharacter.razor`](Veldrath.Web/Components/Pages/Game/CreateCharacter.razor) | Yes | MVP |
| `/Game/Play` | [`Game.razor`](Veldrath.Web/Components/Pages/Game/Game.razor) | Yes | MVP |
| `/Game/Inventory` | [`GameInventory.razor`](Veldrath.Web/Components/Pages/Game/GameInventory.razor) | Yes | P2 |
| `/Game/Map` | [`GameMap.razor`](Veldrath.Web/Components/Pages/Game/GameMap.razor) | Yes | P2 |

### Full Component Tree

```
Routes.razor
├── MainLayout.razor (existing)
│   ├── NavMenu.razor (modified: add "Play Game" link when authenticated)
│   └── @Body
│
├── Game/CharacterSelect.razor          [Route: /Game/CharacterSelect]
│   └── CharacterCard.razor (per character)
│
├── Game/CreateCharacter.razor          [Route: /Game/CreateCharacter]
│   └── CreationStep*.razor (step wizard components)
│
├── Game/Game.razor                     [Route: /Game/Play]
│   ├── Layout: CSS Grid (game-layout)
│   ├── GameHeader.razor               (top: HP/MP/XP bars, character name)
│   │   └── StatusBar.razor (reusable HP/MP/XP bar)
│   ├── GameSidebar.razor              (left: chat panel)
│   │   ├── GameChat.razor             (message history + input)
│   │   └── GameCombat.razor           (shown during combat: enemy HP, action buttons)
│   ├── GameZoneView.razor             (center: the main play area)
│   │   └── GameTilemap.razor          (CSS Grid tilemap)
│   ├── GameFooter.razor               (bottom: action bar)
│   │   └── ActionBar.razor            (attack, defend, flee, abilities)
│   └── GameOverlay.razor              (modals: shop, inventory, confirmations)
│       ├── GameShop.razor             (shown when visiting shop)
│       └── GameInventory.razor        (shown when opening inventory)
│
├── Game/GameInventory.razor            [Route: /Game/Inventory, Phase 2]
├── Game/GameMap.razor                  [Route: /Game/Map, Phase 2]
│
└── Shared/
    ├── StatusBar.razor                 (reusable HP/MP/XP bar)
    ├── ActionBar.razor                 (reusable action buttons)
    ├── GamePanel.razor                 (reusable modal/panel wrapper)
    └── Tile.razor                      (single tile rendering)
```

### Layout Strategy

The game screen uses a CSS Grid layout distinct from the main site layout:

```css
.game-layout {
    display: grid;
    grid-template-areas:
        "header  header  header"
        "sidebar center  center"
        "footer  footer  footer";
    grid-template-columns: 280px 1fr;
    grid-template-rows: auto 1fr auto;
    height: 100vh;
    gap: 4px;
    background: #1a1a2e;
    color: #e0e0e0;
}
```

The game layout bypasses the main site header/footer using a separate layout or by hiding those elements when on `/Game/Play`. Recommended approach: a `GameLayout.razor` that's applied automatically to all game routes.

#### GameLayout.razor

```razor
@inherits LayoutComponentBase
@inject GameStateService GameState

<div class="game-layout @(GameState.IsInGame ? "in-game" : "")">
    @Body
</div>

@code {
    protected override void OnInitialized()
    {
        // Prevent main site layout from wrapping game pages
    }
}
```

Route registration in [`Routes.razor`](Veldrath.Web/Components/Routes.razor) uses cascading layout values or separate layout definition. Since Blazor Server routes are resolved via `@page` directives, create a `_GameImports.razor` with the layout override:

```razor
@layout GameLayout
```

---

## 3. Service Layer

### 3.1 GameHubConnectionService

**File:** [`Veldrath.Web/Services/GameHubConnectionService.cs`](Veldrath.Web/Services/GameHubConnectionService.cs)

**Registration:** Scoped (one per Blazor circuit)

**Purpose:** Manages the `HubConnection` from Veldrath.Web to Veldrath.Server's GameHub (`/hubs/game`). Provides a clean API for sending hub commands and subscribing to broadcast events.

#### Interface

```csharp
public interface IGameHubConnectionService : IAsyncDisposable
{
    // Connection lifecycle
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    bool IsConnected { get; }

    // Send commands (hub method invocation)
    Task SendAsync(string method);
    Task<TResult> SendAsync<TResult>(string method);
    Task<TResult> SendAsync<TResult>(string method, object arg);

    // Subscribe to broadcast events
    IDisposable On<T>(string eventName, Action<T> handler);
    IDisposable On(string eventName, Action handler);

    // Connection state
    event Action<ConnectionState> StateChanged;
    ConnectionState State { get; }
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed
}
```

#### Implementation Details

- Builds the `HubConnection` using `new HubConnectionBuilder().WithUrl(...)`
- URL: `{ServerUrl}/hubs/game` (reads `Veldrath:ServerUrl` from config, same as [`VeldrathApiClient`](Veldrath.Web/Services/VeldrathApiClient.cs))
- Auth: passes JWT via `AccessTokenProvider` callback that reads from [`AuthStateService.AccessToken`](Veldrath.Web/Services/AuthStateService.cs)
- Automatic reconnect via `.WithAutomaticReconnect()` (retry policy: 0s, 2s, 10s, 30s then give up)
- On reconnect, re-sends `SelectCharacter` and `EnterZone` if state was active
- Tracks connection state (`Connected`, `Reconnecting`, `Disconnected`, `Failed`)
- Registers generic `Closed` handler that sets state to `Disconnected`
- Exposes `On<T>` and `On` for subscribing to broadcast events; returns `IDisposable` for unsubscription
- All hub event subscriptions are registered lazily (on first `ConnectAsync`) and stored in a `CompositeDisposable`

#### Sequence (Connection)

```
Game.razor.OnInitializedAsync
  → AuthState.TryRefreshAsync()         // ensure fresh JWT
  → GameHubConnectionService.ConnectAsync()
      → creates HubConnection
      → HubConnection.On<ServerInfoPayload>("ServerInfo", ...)
      → HubConnection.StartAsync()
      → GameStateService.OnConnected()
```

#### Connection String Construction

```csharp
// From appsettings.json
var serverUrl = configuration["Veldrath:ServerUrl"]; // e.g. "http://localhost:5000"
var hubUrl = $"{serverUrl.TrimEnd('/')}/hubs/game";
```

### 3.2 GameStateService

**File:** [`Veldrath.Web/Services/GameStateService.cs`](Veldrath.Web/Services/GameStateService.cs)

**Registration:** Scoped (one per Blazor circuit)

**Purpose:** Single source of truth for all game state on the server side. Components read state from this service and call its methods to trigger actions. Implements `INotifyPropertyChanged` for Blazor rendering.

#### State Properties

```csharp
public class GameStateService : INotifyPropertyChanged
{
    // ── Auth / Connection ──
    public bool IsConnected { get; private set; }
    public bool IsInGame { get; private set; } // has selected character + entered zone
    public GameHubConnectionService.ConnectionState HubState { get; private set; }

    // ── Character ──
    public CharacterState Character { get; private set; }
    public bool HasCharacter => Character is not null;

    // ── Zone ──
    public ZoneState CurrentZone { get; private set; }
    public TileMapDto TileMap { get; private set; }
    public IReadOnlyList<ZoneEntitiesSnapshotPayload> Entities { get; private set; }
    public bool IsInZone => CurrentZone is not null;

    // ── Combat ──
    public CombatState Combat { get; private set; }
    public bool IsInCombat => Combat is not null && Combat.IsActive;

    // ── Chat ──
    public ObservableCollection<ChatMessageDto> ChatMessages { get; }
    public string LastSystemMessage { get; private set; }

    // ── UI State ──
    public bool ShowInventory { get; set; }
    public bool ShowShop { get; set; }
    public ShopState CurrentShop { get; set; }
    public string NotificationMessage { get; set; }
}
```

#### Sub-State Records

```csharp
public record CharacterState(
    Guid Id,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    int Gold,
    int UnspentAttributePoints,
    int Strength,
    int Dexterity,
    int Constitution,
    int Intelligence,
    int Wisdom,
    int Charisma,
    IReadOnlyList<string> LearnedAbilities);

public record ZoneState(
    string Id,
    string Name,
    string Description,
    string ZoneType,
    IReadOnlyList<OccupantInfo> Occupants);

public record CombatState(
    bool IsActive,
    Guid EnemyId,
    string EnemyName,
    int EnemyLevel,
    int EnemyCurrentHealth,
    int EnemyMaxHealth,
    string[] EnemyAbilityNames,
    string LastActionResult); // "attack", "defend", "ability", "flee_failed"

public record ShopState(
    string ZoneId,
    string ZoneName,
    IReadOnlyList<ShopItemDto> Items);

public record ChatMessageDto(
    Guid CharacterId,
    string Channel,
    string Sender,
    string Message,
    DateTimeOffset Timestamp);

public record OccupantInfo(Guid CharacterId, string CharacterName, DateTimeOffset EnteredAt);
```

#### Key Methods

```csharp
// Lifecycle
public Task InitializeAsync();            // Called when game page loads
public Task ResetAsync();                 // Called on disconnect/logout

// Hub event callbacks (called by GameHubConnectionService)
public void ApplyServerInfo(ServerInfoPayload info);
public void ApplyCharacterSelected(dynamic payload);        // from CharacterSelected event
public void ApplyZoneEntered(dynamic payload);              // from ZoneEntered event
public void ApplyCharacterMoved(CharacterMovedPayload payload);
public void ApplyCombatStarted(dynamic payload);
public void ApplyCombatTurn(dynamic payload);
public void ApplyCombatEnded(dynamic payload);
public void ApplyZoneEntitiesSnapshot(ZoneEntitiesSnapshotPayload payload);
public void ApplyChatMessage(ChatMessageDto message);
public void ApplySystemMessage(string message);
public void ApplyError(string message);
public void ApplyGoldChanged(dynamic payload);
public void ApplyInventoryLoaded(dynamic payload);
// ... etc for each broadcast event
```

**Pattern:** Each `Apply*` method updates the relevant state property, fires `PropertyChanged`, and optionally calls `StateHasChanged` on a registered component callback.

### 3.3 Service Interactions

```
Component (Game.razor)
    │
    ├── calls GameStateService.SomeMethod()
    │       └── calls GameHubConnectionService.SendAsync("HubMethod", args)
    │               └── HubConnection.InvokeAsync("HubMethod", args)
    │
    └── subscribes to GameStateService.PropertyChanged
            └── calls InvokeAsync(StateHasChanged) on state change

GameHubConnectionService (receives broadcast event)
    └── calls GameStateService.ApplyXxx(payload)
            └── updates state, fires PropertyChanged
                    └── component re-renders
```

### 3.4 Existing Service Changes

#### AuthStateService (no changes needed)

The existing [`AuthStateService`](Veldrath.Web/Services/AuthStateService.cs) exposes `AccessToken` property. [`GameHubConnectionService`](Veldrath.Web/Services/GameHubConnectionService.cs) reads this via DI to set the `AccessTokenProvider` on the SignalR `HubConnection`.

#### VeldrathApiClient (expand)

Add game REST endpoints for character creation (if not using SignalR for that):

```csharp
// Add to VeldrathApiClient
public async Task<CharacterDto[]?> GetCharactersAsync(CancellationToken ct = default);
public async Task<CharacterDto?> CreateCharacterAsync(CreateCharacterRequest request, CancellationToken ct = default);
public async Task<CharacterPreviewDto?> GetCreationPreviewAsync(CancellationToken ct = default);
public async Task<bool> CheckNameAvailabilityAsync(string name, CancellationToken ct = default);
```

---

## 4. State Management

### Architecture

```
┌──────────────────────┐
│   GameHubConnection   │  (SignalR transport)
│   Service             │
└─────┬────────────────┘
      │ Raw hub events (deserialized JSON)
      ▼
┌──────────────────────┐
│   GameStateService    │  (single source of truth)
│   - Holds all state   │
│   - INotifyPropChanged│
│   - ApplyXxx methods  │
└─────┬────────────────┘
      │ PropertyChanged events
      ▼
┌──────────────────────┐
│   Blazor Components   │  (read state, call methods)
│   - Game.razor        │
│   - GameTilemap.razor │
│   - GameChat.razor    │
│   - GameCombat.razor  │
└──────────────────────┘
```

### State Flow for a Movement Action

```
1. User clicks tile on GameTilemap.razor
2. Component calls GameStateService.RequestMoveAsync(toX, toY, direction)
3. GameStateService validates (basic client-side check)
4. GameStateService calls GameHubConnectionService.SendAsync("MoveCharacter", request)
5. HubConnection sends "MoveCharacter" to GameHub
6. Server validates, persists, broadcasts "CharacterMoved" to zone group
7. GameHubConnectionService receives "CharacterMoved" event
8. GameHubConnectionService calls GameStateService.ApplyCharacterMoved(payload)
9. GameStateService updates position, fires PropertyChanged
10. Blazor component re-renders, updating tile CSS classes
```

### Component Subscription Pattern

Components subscribe to state changes in `OnInitialized`:

```csharp
protected override void OnInitialized()
{
    _state.PropertyChanged += async (sender, args) =>
    {
        await InvokeAsync(StateHasChanged);
    };
}
```

For performance-critical updates (like combat turns or movement), use targeted subscriptions or `ShouldRender` overrides to avoid full re-renders of the entire game screen.

### Performance Considerations

- **Tilemap rendering:** Only re-render tiles that changed (use `@key` on tile `<div>` elements)
- **Chat:** Virtualize message list (only render last ~100 messages)
- **Combat:** Dedicated component isolates re-renders to the combat panel only
- **State batching:** Group rapid state updates (e.g., multiple entity movements) into a single `StateHasChanged` call using `Timer` or `InvokeAsync` debouncing

---

## 5. SignalR Event Catalog

### 5.1 Hub Methods (Client → Server)

These are the methods [`GameHubConnectionService`](Veldrath.Web/Services/GameHubConnectionService.cs) calls via `SendAsync`.

| Method | Args | Returns | Phase | Notes |
|--------|------|---------|-------|-------|
| `SelectCharacter` | `Guid characterId` | — (broadcast `CharacterSelected`) | MVP | Step 2 after connect |
| `GetActiveCharacters` | — | `IEnumerable<Guid>` | MVP | Show active chars |
| `EnterZone` | `string zoneId` | — (broadcast `ZoneEntered`) | MVP | |
| `LeaveZone` | — | — | MVP | |
| `ExitZone` | — | — | MVP | Return to region map |
| `GetZoneTileMap` | — | — (broadcast `ZoneTileMap`) | MVP | |
| `GetRegionMap` | — | — (broadcast `RegionMapData`) | P2 | |
| `MoveCharacter` | `MoveCharacterHubRequest` | — (broadcast `CharacterMoved`) | MVP | |
| `MoveOnRegion` | `MoveOnRegionHubRequest` | — (broadcast `RegionPlayerMoved`) | P2 | |
| `ChangeRegion` | `ChangeRegionHubRequest` | — (broadcast `RegionChanged`) | P2 | |
| `EngageEnemy` | `EngageEnemyHubRequest` | — (broadcast `CombatStarted`) | MVP | |
| `AttackEnemy` | — | — (broadcast `CombatTurn`) | MVP | |
| `DefendAction` | — | — (broadcast `CombatTurn`) | MVP | |
| `FleeFromCombat` | — | — (broadcast `CombatEnded/CombatTurn`) | MVP | |
| `UseAbilityInCombat` | `UseAbilityInCombatHubRequest` | — (broadcast `CombatTurn`) | P2 | |
| `UseAbility` | `string abilityId` | — (broadcast `AbilityUsed`) | P2 | Out-of-combat |
| `Respawn` | — | — (broadcast `CharacterRespawned`) | MVP | |
| `SendChatMessage` | `ChatMessageHubRequest` | — (broadcast `ReceiveChatMessage`) | MVP | |
| `GetChatCommands` | — | `List<ChatCommandInfoDto>` | P2 | |
| `GetInventory` | — | — (broadcast `InventoryLoaded`) | P2 | |
| `EquipItem` | `EquipItemHubRequest` | — (broadcast `ItemEquipped`) | P2 | |
| `VisitShop` | `VisitShopHubRequest` | — (broadcast `ShopVisited`) | P2 | |
| `GetShopCatalog` | — | — (broadcast `ShopCatalog`) | P2 | |
| `BuyItem` | `string itemRef` | — (broadcast `ItemPurchased`) | P2 | |
| `SellItem` | `string itemRef` | — (broadcast `ItemSold`) | P2 | |
| `DropItem` | `string itemRef` | — (broadcast `ItemDropped`) | P2 | |
| `CraftItem` | `string recipeSlug` | — (broadcast `ItemCrafted`) | P2 | |
| `GainExperience` | `GainExperienceHubRequest` | — (broadcast `ExperienceGained`) | P2 | |
| `AllocateAttributePoints` | `Dictionary<string,int>` | — (broadcast `AttributePointsAllocated`) | P2 | |
| `RestAtLocation` | `RestAtLocationHubRequest` | — (broadcast `CharacterRested`) | P2 | |
| `AwardSkillXp` | `AwardSkillXpHubRequest` | — (broadcast `SkillXpGained`) | P2 | |
| `AddGold` | `AddGoldHubRequest` | — (broadcast `GoldChanged`) | P2 | |
| `TakeDamage` | `TakeDamageHubRequest` | — (broadcast `DamageTaken`) | P2 | |
| `NavigateToLocation` | `NavigateToLocationHubRequest` | — (broadcast `LocationEntered`) | P2 | |
| `UnlockZoneLocation` | `UnlockZoneLocationHubRequest` | — (broadcast `ZoneLocationUnlocked`) | P2 | |
| `SearchArea` | — | — (broadcast `AreaSearched`) | P2 | |
| `GetQuestLog` | — | — (broadcast `QuestLogReceived`) | P2 | |
| `EnterDungeon` | `string dungeonSlug` | — (broadcast `DungeonEntered`) | P2 | |
| `Ping` | — | `long` (Unix ms) | MVP | For diagnostics |
| `SendZoneMessage` | `SendZoneChatMessageHubRequest` | — | P2 | |
| `SendGlobalMessage` | `SendGlobalChatMessageHubRequest` | — | P2 | |
| `SendWhisper` | `SendWhisperHubRequest` | — | P2 | |

### 5.2 Hub Broadcast Events (Server → Client)

These are the events [`GameHubConnectionService`](Veldrath.Web/Services/GameHubConnectionService.cs) subscribes to via `On<T>`.

| Event Name | Payload Type | Phase | GameStateService Handler | Description |
|------------|-------------|-------|-------------------------|-------------|
| `ServerInfo` | `ServerInfoPayload` | MVP | `ApplyServerInfo` | Sent on connect, version info |
| `CharacterSelected` | dynamic (anonymous) | MVP | `ApplyCharacterSelected` | Character data after select |
| `CharacterStatusChanged` | `{CharacterId, IsOnline}` | MVP | — | Broadcast to account group |
| `CharacterAlreadyActive` | `Guid` | MVP | — | Character already in use |
| `ZoneEntered` | dynamic | MVP | `ApplyZoneEntered` | Zone details after enter |
| `ZoneLeft` | none | MVP | `ApplyZoneLeft` | Confirmation of leaving zone |
| `ZoneExited` | `{RegionId, TileX, TileY}` | MVP | `ApplyZoneExited` | Returned to region map |
| `ZoneTileMap` | `TileMapDto` | MVP | `ApplyTileMap` | Tilemap for the zone |
| `ZoneEntitiesSnapshot` | `ZoneEntitiesSnapshotPayload` | MVP | `ApplyZoneEntitiesSnapshot` | Current entities in zone |
| `PlayerEntered` | `{CharacterId, CharacterName, ZoneId}` | MVP | `ApplyPlayerEntered` | Another player entered zone |
| `PlayerLeft` | `{CharacterId, CharacterName, ZoneId}` | MVP | `ApplyPlayerLeft` | Another player left zone |
| `CharacterMoved` | `CharacterMovedPayload` | MVP | `ApplyCharacterMoved` | Character moved one tile |
| `TileExitTriggered` | dynamic | MVP | — | Stepped on exit tile |
| `RegionMapData` | `RegionMapDto` | P2 | `ApplyRegionMapData` | Region map data |
| `RegionPlayerMoved` | `RegionPlayerMovedPayload` | P2 | `ApplyRegionPlayerMoved` | Moved on region map |
| `RegionChanged` | `{NewRegionId, TileX, TileY}` | P2 | `ApplyRegionChanged` | Changed to new region |
| `ZoneEntryTriggered` | dynamic | P2 | — | Stepped on zone entry |
| `RegionExitTriggered` | dynamic | P2 | — | Stepped on region exit |
| `CombatStarted` | dynamic | MVP | `ApplyCombatStarted` | Entered combat with enemy |
| `CombatTurn` | dynamic | MVP | `ApplyCombatTurn` | Turn result (attack/defend/flee) |
| `CombatEnded` | `{CharacterId, Reason}` | MVP | `ApplyCombatEnded` | Combat ended (fled/victory) |
| `EnemyEngaged` | `{CharacterId, EnemyId, EnemyName}` | P2 | — | Broadcast to zone group |
| `EnemyDefeated` | `{CharacterId}` | MVP | — | Enemy defeated notification |
| `ExperienceGained` | dynamic | P2 | `ApplyExperienceGained` | XP gained, possible level up |
| `AttributePointsAllocated` | dynamic | P2 | — | Attribute allocation result |
| `CharacterRested` | dynamic | P2 | — | Rest result |
| `AbilityUsed` | dynamic | P2 | — | Ability used (out of combat) |
| `SkillXpGained` | dynamic | P2 | — | Skill XP gained |
| `ItemEquipped` | dynamic | P2 | `ApplyItemEquipped` | Equipment changed |
| `GoldChanged` | dynamic | P2 | `ApplyGoldChanged` | Gold balance changed |
| `DamageTaken` | dynamic | P2 | — | Damage received |
| `ItemCrafted` | dynamic | P2 | — | Item crafted |
| `ReceiveChatMessage` | `ChatMessageHubDto` | MVP | `ApplyChatMessage` | Chat message |
| `SystemMessage` | `string` | MVP | `ApplySystemMessage` | System message (info/help) |
| `Error` | `string` | MVP | `ApplyError` | Error message |
| `CharacterRespawned` | dynamic | MVP | — | Character respawned after death |
| `InventoryLoaded` | dynamic | P2 | `ApplyInventoryLoaded` | Inventory data |
| `ItemPurchased` | dynamic | P2 | — | Item bought from shop |
| `ItemSold` | dynamic | P2 | — | Item sold to shop |
| `ItemDropped` | dynamic | P2 | — | Item dropped from inventory |
| `QuestLogReceived` | dynamic | P2 | — | Quest log data |
| `LocationEntered` | dynamic | P2 | — | Navigated to location |
| `ZoneLocationUnlocked` | dynamic | P2 | — | Hidden location unlocked |
| `AreaSearched` | dynamic | P2 | — | Area search result |
| `ShopVisited` | dynamic | P2 | `ApplyShopVisited` | Shop details |
| `ShopCatalog` | `{ZoneId, Items}` | P2 | `ApplyShopCatalog` | Shop item catalog |
| `DungeonEntered` | dynamic | P2 | — | Dungeon entry result |
| `OnAnnouncement` | `AnnouncementPayload` | P2 | — | Server announcement |
| `OnKicked` | `KickedPayload` | MVP | — | Force disconnect |
| `OnWhisper` | `WhisperPayload` | P2 | — | Private message |
| `OnEmote` | `EmotePayload` | P2 | — | Roleplay emote |
| `OnTeleported` | `TeleportedPayload` | P2 | — | Teleported by admin |
| `OnSummoned` | `SummonedPayload` | P2 | — | Summoned by admin |
| `OnWarned` | `WarnedPayload` | P2 | — | Warning issued |
| `OnMuted` | `MutedPayload` | P2 | — | Muted |
| `OnItemReceived` | `ItemReceivedPayload` | P2 | — | Item granted by admin |
| `CharacterIgnored` | `CharacterIgnoredPayload` | P2 | — | Ignore list toggle |
| `EnemyMoved` | `EnemyMovedPayload` | P2 | — | Enemy movement on tilemap |
| `FogRevealed` | `FogRevealedPayload` | P2 | — | Fog of war revealed |

### 5.3 Event Registration Pattern

```csharp
// In GameHubConnectionService.ConnectAsync()

_connection.On<ServerInfoPayload>("ServerInfo", payload =>
    _gameStateService.ApplyServerInfo(payload));

_connection.On<ZoneEntitiesSnapshotPayload>("ZoneEntitiesSnapshot", payload =>
    _gameStateService.ApplyZoneEntitiesSnapshot(payload));

_connection.On<CharacterMovedPayload>("CharacterMoved", payload =>
    _gameStateService.ApplyCharacterMoved(payload));

_connection.On<ChatMessageHubDto>("ReceiveChatMessage", dto =>
    _gameStateService.ApplyChatMessage(new ChatMessageDto(
        dto.CharacterId, dto.Channel, dto.Sender, dto.Message, dto.Timestamp)));

// Dynamic (anonymous) payloads use object or JsonElement:
_connection.On<JsonElement>("CombatTurn", json =>
    _gameStateService.ApplyCombatTurn(json));
```

---

## 6. Tilemap Rendering Approach

### MVP Strategy: CSS Grid

For the initial implementation, the tilemap is rendered as an HTML `<div>` grid. No JavaScript interop is needed for MVP.

```

```
```css
.tilemap-container {
    display: grid;
    grid-template-columns: repeat(var(--map-width), var(--tile-size));
    grid-template-rows: repeat(var(--map-height), var(--tile-size));
    gap: 0;
    position: relative;
    background: #111;
    user-select: none;
}

.tile {
    width: var(--tile-size, 40px);
    height: var(--tile-size, 40px);
    box-sizing: border-box;
    border: 1px solid rgba(255, 255, 255, 0.03);
    cursor: pointer;
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
}
```

### Tile CSS Classes

| Tile Type | CSS Class | Background |
|-----------|-----------|------------|
| 0 (walkable/grass) | `.tile-grass` | `#3a5a2a` |
| 1 (wall/stone) | `.tile-wall` | `#5a5a5a` |
| 2 (water) | `.tile-water` | `#2a4a6a` |
| 3 (door) | `.tile-door` | `#6a4a2a` |
| 4 (path) | `.tile-path` | `#8a7a5a` |
| 5 (dirt) | `.tile-dirt` | `#6a5a3a` |
| -1 (void/null) | `.tile-void` | `#000` |

### Entity Rendering

Entities (players, enemies, NPCs) are rendered as overlay `<div>` elements positioned absolutely on top of the tile grid:

```razor
@foreach (var entity in Entities)
{
    var x = entity.TileX * TileSize;
    var y = entity.TileY * TileSize;
    <div class="entity @GetEntityClass(entity)"
         style="position: absolute; left: @(x)px; top: @(y)px;
                width: @(TileSize)px; height: @(TileSize)px;">
        @GetEntityIcon(entity)
    </div>
}
```

- **Player:** Green circle with character initial or `⬟` character
- **Enemy:** Red diamond with `⬥` character
- **NPC:** Blue square with `⬛` character
- **Own character:** Yellow/gold border or pulsing glow

### Tilemap Component Structure

```razor
@* GameTilemap.razor *@
<div class="tilemap-container"
     style="--map-width: @Width; --map-height: @Height; --tile-size: @TileSize">
    
    @* Render tiles *@
    @for (var y = 0; y < Height; y++)
    {
        @for (var x = 0; x < Width; x++)
        {
            var tileIndex = y * Width + x;
            var tileType = GetTileType(tileIndex); // from layer data
            var isBlocked = CollisionMask[tileIndex];
            
            <div class="tile @GetTileClass(tileType) @(isBlocked ? "blocked" : "")"
                 @key="@($"tile_{x}_{y}")"
                 @onclick="() => OnTileClick(x, y)"
                 title="@($"({x}, {y})")">
                @* Show entity icon if present at this tile *@
                @if (EntityAt(x, y) is { } entity)
                {
                    <span class="entity-icon @GetEntityColor(entity)">
                        @GetEntityIcon(entity)
                    </span>
                }
            </div>
        }
    }
</div>
```

### Performance Considerations

- Use `@key` on each tile `<div>` for efficient DOM diffing
- Limit visible area to viewport + 1 tile buffer; only render visible tiles (virtual scrolling)
- Use CSS `will-change: transform` on entity overlays for smooth animations
- For large maps (>50x50), implement viewport culling — only render tiles within the visible area
- Phase 2: Consider HTML5 Canvas via JS interop for smoother rendering (60fps vs ~30fps for DOM grid)
- Entity movement should use CSS transitions (`transition: left 0.15s, top 0.15s`) for smooth animation

### Fog of War (Phase 2)

- `FogMask` array from `TileMapDto` indicates hidden tiles
- Hidden tiles rendered as black/dark with no content
- When `FogRevealed` event received, update `RevealedTiles` set and re-render
- Use CSS class `.tile-fog` with `background: #000` and `opacity: 0.8`

---

## 7. Data Flow Diagrams

### 7.1 Login → Game Flow

```
User logs in → AuthStateService stores JWT
User clicks "Play Game" → navigates to /Game/CharacterSelect
    ├── CharacterSelect calls Api.GetCharactersAsync()
    ├── Shows character list
    User clicks "Select" on a character
    └── Navigates to /Game/Play?characterId={id}
        ├── Game.razor OnInitializedAsync:
        │   ├── AuthState.TryRefreshAsync() (ensure fresh JWT)
        │   ├── GameHubConnectionService.ConnectAsync()
        │   │   ├── Create HubConnection to {ServerUrl}/hubs/game
        │   │   ├── Register all event handlers
        │   │   ├── HubConnection.StartAsync()
        │   │   └── Receive ServerInfo event
        │   ├── GameHubConnectionService.SendAsync("SelectCharacter", id)
        │   │   └── Server broadcasts CharacterSelected
        │   └── GameStateService.ApplyCharacterSelected(payload)
        │
        ├── On CharacterSelected received:
        │   ├── GameHubConnectionService.SendAsync("EnterZone", zoneId)
        │   │   └── Server joins zone group, broadcasts ZoneEntered
        │   ├── GameHubConnectionService.SendAsync("GetZoneTileMap")
        │   │   └── Server broadcasts ZoneTileMap
        │   └── Game.razor renders full game UI
        │
        └── Game state:
            ├── GameHeader: shows HP/MP/XP from CharacterState
            ├── GameTilemap: renders tiles + entities
            ├── GameChat: shows welcome message
            └── GameCombat: hidden (no combat yet)
```

### 7.2 Movement Flow

```
User clicks tile (x=5, y=8) on GameTilemap
    ├── GameTilemap.OnTileClick(5, 8)
    ├── Computes direction from current position
    ├── Calls GameStateService.RequestMoveAsync(5, 8, "S")
    │   ├── Basic validation (adjacent tile, not blocked)
    │   └── Calls GameHubConnectionService.SendAsync("MoveCharacter", request)
    │       └── HubConnection.InvokeAsync("MoveCharacter", {ToX:5, ToY:8, Direction:"S"})
    │
    ├── Server validates (1-tile step, collision, cooldown)
    ├── Server persists new position
    ├── Server broadcasts CharacterMoved to zone group
    │   └── payload: { CharacterId, TileX:5, TileY:8, Direction:"S" }
    │
    ├── GameHubConnectionService receives CharacterMoved
    │   └── GameStateService.ApplyCharacterMoved(payload)
    │       ├── Updates CharacterState position
    │       └── Fires PropertyChanged
    │
    └── GameTilemap re-renders:
        ├── Entity removed from old tile
        ├── Entity shown at new tile (5, 8)
        └── CSS transition animates the movement
```

### 7.3 Combat Flow

```
User clicks enemy entity on tilemap
    ├── GameTilemap.OnTileClick(enemyTile)
    ├── If entity at tile is an enemy → request EngageEnemy
    │   └── GameHubConnectionService.SendAsync("EngageEnemy", { LocationSlug, EnemyId })
    │
    ├── Server creates combat instance
    ├── Server broadcasts CombatStarted to caller
    │   └── payload: { EnemyId, EnemyName, EnemyLevel, EnemyCurrentHealth, EnemyMaxHealth, ... }
    │
    ├── GameStateService.ApplyCombatStarted(payload)
    │   ├── Sets IsInCombat = true
    │   └── Fire PropertyChanged
    │
    ├── GameUI updates:
    │   ├── GameTilemap disabled (can't move during combat)
    │   ├── GameCombat shown with enemy HP bar + action buttons
    │   └── ActionBar shows Attack / Defend / Flee / Abilities
    │
    ├── User clicks "Attack"
    │   ├── GameCombat calls GameHubConnectionService.SendAsync("AttackEnemy")
    │   │   └── HubConnection.InvokeAsync("AttackEnemy")
    │   ├── Server processes turn (player attack → enemy counter-attack)
    │   └── Server broadcasts CombatTurn
    │       └── payload: { Action:"attack", PlayerDamage, EnemyRemainingHealth, ... }
    │
    ├── GameStateService.ApplyCombatTurn(payload)
    │   ├── Updates player HP, enemy HP
    │   ├── Checks if enemy defeated or player defeated
    │   └── Fires PropertyChanged
    │
    ├── GameCombat re-renders:
    │   ├── Updates enemy HP bar
    │   ├── Shows damage numbers in combat log
    │   └── If enemy defeated: shows victory, hides combat UI
    │
    └── On enemy defeat:
        ├── Server broadcasts ExperienceGained (auto) + EnemyDefeated to zone
        └── GameStateService updates XP/level, clears combat state
```

### 7.4 Chat Flow

```
User types message in GameChat input and presses Enter
    ├── GameChat calls GameStateService.SendChatMessageAsync(text)
    │   └── Calls GameHubConnectionService.SendAsync("SendChatMessage", request)
    │       └── HubConnection.InvokeAsync("SendChatMessage", { Message: text })
    │
    ├── Server processes (slash commands parsed server-side)
    ├── Server broadcasts ReceiveChatMessage to zone group
    │   └── payload: ChatMessageHubDto
    │
    └── GameHubConnectionService receives ReceiveChatMessage
        └── GameStateService.ApplyChatMessage(dto)
            ├── Appends to ChatMessages collection
            └── Fires PropertyChanged
                └── GameChat re-renders (shows new message)
```

### 7.5 Disconnect / Reconnect Flow

```
Network blip or server restart
    ├── HubConnection enters Reconnecting state
    ├── GameStateService.HubState = Reconnecting
    ├── Game.razor shows reconnection banner
    │
    ├── SignalR automatic reconnect retries (0s, 2s, 10s, 30s)
    │
    ├── On successful reconnect:
    │   ├── Reconnected event fires
    │   ├── GameStateService.HubState = Connected
    │   ├── Re-send SelectCharacter with last character ID
    │   ├── Re-send EnterZone with last zone ID
    │   ├── Re-send GetZoneTileMap
    │   └── Game.razor hides reconnection banner, resumes normal rendering
    │
    └── On reconnection failure (after max retries):
        ├── Closed event fires
        ├── GameStateService.HubState = Disconnected
        ├── Show "Connection lost" message with "Reconnect" button
        └── User clicks Reconnect → repeat connect flow
```

---

## 8. Phase 1 (MVP) vs Phase 2 Scope

### Phase 1 — MVP (Core Game Loop)

| Feature | Components | Services | Priority |
|---------|-----------|----------|----------|
| Auth integration | Existing [`AuthStateService`](Veldrath.Web/Services/AuthStateService.cs) | No new code | P0 |
| SignalR connection | [`GameHubConnectionService`](Veldrath.Web/Services/GameHubConnectionService.cs) | Connect/Disconnect, auto-reconnect | P0 |
| Character select | [`CharacterSelect.razor`](Veldrath.Web/Components/Pages/Game/CharacterSelect.razor) | REST endpoint for character list | P0 |
| Create character (basic) | [`CreateCharacter.razor`](Veldrath.Web/Components/Pages/Game/CreateCharacter.razor) | REST endpoint for creation | P0 |
| Zone entry + tilemap | [`Game.razor`](Veldrath.Web/Components/Pages/Game/Game.razor), [`GameTilemap.razor`](Veldrath.Web/Components/Pages/Game/GameTilemap.razor) | Tile rendering, entity display | P0 |
| Movement (click-to-move) | [`GameTilemap.razor`](Veldrath.Web/Components/Pages/Game/GameTilemap.razor) | `MoveCharacter` command | P0 |
| Basic chat | [`GameChat.razor`](Veldrath.Web/Components/Pages/Game/GameChat.razor) | `SendChatMessage`, `ReceiveChatMessage` | P0 |
| Basic combat | [`GameCombat.razor`](Veldrath.Web/Components/Pages/Game/GameCombat.razor), [`GameHeader.razor`](Veldrath.Web/Components/Pages/Game/GameHeader.razor) | `EngageEnemy`, `AttackEnemy`, `DefendAction`, `FleeFromCombat`, `Respawn` | P0 |
| Status bars | [`StatusBar.razor`](Veldrath.Web/Components/Shared/StatusBar.razor) | HP/MP/XP display | P1 |
| Action bar | [`ActionBar.razor`](Veldrath.Web/Components/Shared/ActionBar.razor) | Basic combat actions | P1 |
| Zone entities snapshot | [`GameTilemap.razor`](Veldrath.Web/Components/Pages/Game/GameTilemap.razor) | Entity rendering on tiles | P1 |
| Player enter/leave notifications | [`GameChat.razor`](Veldrath.Web/Components/Pages/Game/GameChat.razor) | System messages | P1 |
| Disconnect handling | [`GameHubConnectionService`](Veldrath.Web/Services/GameHubConnectionService.cs) | Reconnect UI | P1 |
| Game layout | [`GameLayout.razor`](Veldrath.Web/Components/Layout/GameLayout.razor) | CSS Grid layout | P0 |
| Nav menu link | [`NavMenu.razor`](Veldrath.Web/Components/Layout/NavMenu.razor) | "Play Game" link | P0 |

### Phase 2 — Full Feature Set

| Feature | Components | Dependencies |
|---------|-----------|-------------|
| Region map | [`GameMap.razor`](Veldrath.Web/Components/Pages/Game/GameMap.razor) | `GetRegionMap`, `MoveOnRegion`, `ChangeRegion` |
| Inventory | [`GameInventory.razor`](Veldrath.Web/Components/Pages/Game/GameInventory.razor) | `GetInventory`, `EquipItem`, `DropItem` |
| Shops | [`GameShop.razor`](Veldrath.Web/Components/Pages/Game/GameShop.razor) | `VisitShop`, `GetShopCatalog`, `BuyItem`, `SellItem` |
| Abilities (combat) | [`GameCombat.razor`](Veldrath.Web/Components/Pages/Game/GameCombat.razor) | `UseAbilityInCombat` |
| Abilities (out-of-combat) | [`ActionBar.razor`](Veldrath.Web/Components/Shared/ActionBar.razor) | `UseAbility` |
| XP/Gold display | [`GameHeader.razor`](Veldrath.Web/Components/Pages/Game/GameHeader.razor) | `ExperienceGained`, `GoldChanged` |
| Level-up UI | [`GameOverlay.razor`](Veldrath.Web/Components/Pages/Game/GameOverlay.razor) | Attribute point allocation |
| Quest log | Panel/modal | `GetQuestLog`, quest events |
| Location navigation | Panel | `NavigateToLocation`, `LocationEntered` |
| Zone location discovery | System messages | `ZoneLocationUnlocked`, `SearchArea`, `AreaSearched` |
| Slash commands | [`GameChat.razor`](Veldrath.Web/Components/Pages/Game/GameChat.razor) | `/who`, `/emote`, `/roll`, `/afk`, `/whisper` |
| Dungeon entry | Panel | `EnterDungeon` |
| Crafting | Panel | `CraftItem` |
| Region map movement | [`GameMap.razor`](Veldrath.Web/Components/Pages/Game/GameMap.razor) | `MoveOnRegion`, `RegionPlayerMoved` |
| Region transitions | [`GameMap.razor`](Veldrath.Web/Components/Pages/Game/GameMap.razor) | `ChangeRegion`, `RegionChanged` |
| Fog of war | [`GameTilemap.razor`](Veldrath.Web/Components/Pages/Game/GameTilemap.razor) | `FogMask`, `FogRevealed` |
| Rest at inn | Panel | `RestAtLocation` |
| Sound effects (JS interop) | AudioService | Phase 2 stretch |
| HTML5 Canvas rendering | CanvasTilemap | Phase 2 stretch |

---

## 9. Implementation Order Recommendations

### Sprint 1: Foundation

1. **Add project references + packages** to [`Veldrath.Web.csproj`](Veldrath.Web/Veldrath.Web.csproj):
   - `Microsoft.AspNetCore.SignalR.Client` package
   - `RealmEngine.Core` project reference (for shared types)
   - `RealmEngine.Shared` project reference

2. **Create [`GameHubConnectionService`](Veldrath.Web/Services/GameHubConnectionService.cs)**
   - `ConnectAsync`, `DisconnectAsync`, `SendAsync`, `On<T>`
   - JWT auth via [`AuthStateService`](Veldrath.Web/Services/AuthStateService.cs)
   - Automatic reconnection
   - Connection state tracking

3. **Create [`GameStateService`](Veldrath.Web/Services/GameStateService.cs)**
   - Character state, zone state, combat state, chat log
   - `INotifyPropertyChanged` implementation
   - All `ApplyXxx` methods (stub out unimplemented ones)

4. **Register services in [`Program.cs`](Veldrath.Web/Program.cs)**
   - `builder.Services.AddScoped<GameHubConnectionService>()`
   - `builder.Services.AddScoped<GameStateService>()`
   - Configure SignalR client (JSON protocol)

5. **Create [`GameLayout.razor`](Veldrath.Web/Components/Layout/GameLayout.razor)**
   - CSS Grid layout for game screen
   - Override layout for game routes

6. **Update [`NavMenu.razor`](Veldrath.Web/Components/Layout/NavMenu.razor)**
   - Add "Play Game" link for authenticated users

### Sprint 2: Character Select + Game Hub

7. **Create [`CharacterSelect.razor`](Veldrath.Web/Components/Pages/Game/CharacterSelect.razor)**
   - Fetch characters via REST API (add endpoints to [`VeldrathApiClient`](Veldrath.Web/Services/VeldrathApiClient.cs))
   - Display character cards with name, class, level, last played
   - Select button navigates to `/Game/Play?characterId={id}`

8. **Create [`CreateCharacter.razor`](Veldrath.Web/Components/Pages/Game/CreateCharacter.razor)**
   - Step wizard: name → class → species → attributes → appearance
   - REST API calls for each step
   - Finalize and navigate to character select

9. **Create [`Game.razor`](Veldrath.Web/Components/Pages/Game/Game.razor)**
   - Main game screen component
   - Connect to hub on init, select character
   - Enter zone, load tilemap
   - Layout: header + sidebar + center + footer

10. **Create [`GameHeader.razor`](Veldrath.Web/Components/Pages/Game/GameHeader.razor) + [`StatusBar.razor`](Veldrath.Web/Components/Shared/StatusBar.razor)**
    - Character name, level
    - HP bar, MP bar, XP bar
    - Gold display

### Sprint 3: Tilemap + Movement

11. **Create [`GameTilemap.razor`](Veldrath.Web/Components/Pages/Game/GameTilemap.razor)**
    - CSS Grid rendering
    - Tile type → CSS class mapping
    - Click-to-move handler
    - Entity overlay rendering (players, enemies, NPCs)

12. **Implement movement flow end-to-end**
    - Wire up `MoveCharacter` command
    - Handle `CharacterMoved` event
    - Animate entity position changes with CSS transitions
    - Handle `PlayerEntered`/`PlayerLeft` for other players

### Sprint 4: Chat + Combat

13. **Create [`GameChat.razor`](Veldrath.Web/Components/Pages/Game/GameChat.razor)**
    - Message history display (virtualized)
    - Text input with send button
    - System message display
    - Basic slash command support (`/help`, `/who`)

14. **Create [`GameCombat.razor`](Veldrath.Web/Components/Pages/Game/GameCombat.razor) + [`ActionBar.razor`](Veldrath.Web/Components/Shared/ActionBar.razor)**
    - Engage enemy (click enemy on tilemap)
    - Attack/Defend/Flee buttons
    - Combat turn display (damage numbers)
    - Enemy HP bar
    - Victory/defeat handling
    - Respawn flow

### Sprint 5: Polish + Phase 2 Prep

15. **Disconnect/reconnect handling**
    - Reconnection UI banner
    - Automatic state restoration on reconnect
    - Connection lost → offer manual reconnect

16. **Error handling + rate limiting**
    - Display error messages from server
    - Rate limit awareness (120 commands/min)

17. **NPC and enemy movement on tilemap**
    - Handle `EnemyMoved` events
    - Update entity positions

18. **Phase 2 features** (in recommended order):
    - Inventory system
    - Shop system
    - Region map
    - Abilities
    - Quests
    - Location navigation
    - Fog of war
    - Dungeons
    - Crafting

---

## 10. Project Changes Summary

### 10.1 Files to Create

```
Veldrath.Web/
├── Components/
│   ├── Layout/
│   │   └── GameLayout.razor              # Game screen layout (no main site chrome)
│   ├── Pages/
│   │   └── Game/
│   │       ├── CharacterSelect.razor      # /Game/CharacterSelect
│   │       ├── CreateCharacter.razor      # /Game/CreateCharacter
│   │       ├── Game.razor                 # /Game/Play (main game hub)
│   │       ├── GameHeader.razor           # Status bars, character info
│   │       ├── GameChat.razor             # Chat panel
│   │       ├── GameCombat.razor           # Combat UI
│   │       ├── GameTilemap.razor          # CSS Grid tilemap
│   │       ├── GameSidebar.razor          # Left sidebar container
│   │       ├── GameFooter.razor           # Bottom action bar container
│   │       ├── GameOverlay.razor          # Modal overlay container
│   │       ├── GameInventory.razor         # Inventory panel (P2)
│   │       ├── GameShop.razor             # Shop panel (P2)
│   │       ├── GameMap.razor              # Region map (P2)
│   │       └── GameZoneView.razor         # Zone view container (P2)
│   └── Shared/
│       ├── StatusBar.razor               # Reusable HP/MP/XP bar
│       ├── ActionBar.razor               # Reusable action button set
│       ├── GamePanel.razor               # Reusable panel/modal wrapper
│       └── Tile.razor                    # Single tile rendering component
├── Services/
│   ├── GameHubConnectionService.cs       # SignalR hub connection manager
│   └── GameStateService.cs               # Game state singleton per circuit
└── wwwroot/
    └── css/
        └── game.css                      # Game-specific styles
```

### 10.2 Files to Modify

| File | Change |
|------|--------|
| [`Veldrath.Web/Veldrath.Web.csproj`](Veldrath.Web/Veldrath.Web.csproj) | Add `Microsoft.AspNetCore.SignalR.Client` package, `RealmEngine.Core` + `RealmEngine.Shared` project references |
| [`Veldrath.Web/Program.cs`](Veldrath.Web/Program.cs) | Register `GameHubConnectionService`, `GameStateService` as scoped; configure SignalR client |
| [`Veldrath.Web/Components/Routes.razor`](Veldrath.Web/Components/Routes.razor) | No changes needed (auto-discovery of `@page` directives) |
| [`Veldrath.Web/Components/Layout/NavMenu.razor`](Veldrath.Web/Components/Layout/NavMenu.razor) | Add "Play Game" link for auth users: `<a href="/game/characterselect">Play</a>` |
| [`Veldrath.Web/Services/VeldrathApiClient.cs`](Veldrath.Web/Services/VeldrathApiClient.cs) | Add character creation REST endpoints |
| [`Veldrath.Web/appsettings.json`](Veldrath.Web/appsettings.json) | Ensure `Veldrath:ServerUrl` is configured (should already exist) |
| [`Veldrath.Web/wwwroot/app.css`](Veldrath.Web/wwwroot/app.css) | Add game-specific CSS classes or create separate `game.css` |

### 10.3 Updated `.csproj` Dependencies

```xml
<ItemGroup>
  <!-- Existing -->
  <PackageReference Include="Markdig" />
  <PackageReference Include="Serilog.AspNetCore" />
  
  <!-- NEW: SignalR Client -->
  <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
</ItemGroup>

<ItemGroup>
  <!-- Existing -->
  <ProjectReference Include="..\Veldrath.Auth\Veldrath.Auth.csproj" />
  <ProjectReference Include="..\Veldrath.Auth.Blazor\Veldrath.Auth.Blazor.csproj" />
  <ProjectReference Include="..\Veldrath.Contracts\Veldrath.Contracts.csproj" />
  
  <!-- NEW: Engine references for shared game types -->
  <ProjectReference Include="..\RealmEngine.Core\RealmEngine.Core.csproj" />
  <ProjectReference Include="..\RealmEngine.Shared\RealmEngine.Shared.csproj" />
</ItemGroup>
```

### 10.4 Updated `Program.cs` Service Registration

```csharp
// After existing AddScoped<AuthStateService> line:

builder.Services.AddScoped<GameHubConnectionService>();
builder.Services.AddScoped<GameStateService>();

// SignalR client configuration (if needed for non-negotiated connections)
builder.Services.AddSignalRClient(options =>
{
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ServerTimeout = TimeSpan.FromSeconds(30);
});
```

---

## 11. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Rate limiting (120 cmd/min)** | Player actions blocked if exceeded | Implement client-side rate limiter that tracks command count per minute and shows cooldown indicator; batch rapid movements (e.g., keyboard-based movement queues direction changes) |
| **SignalR WebSocket from server-to-server** | Double-hop latency (browser → Blazor Server → GameServer) | Keep Blazor circuit in same region/DC as GameServer; use persistent connections; monitor latency |
| **Scoped service lifetime across circuits** | Memory leak if circuits aren't properly disposed | Ensure `IAsyncDisposable` implementation on both services; Blazor Server circuits auto-dispose scoped services on disconnect |
| **Anonymous payload deserialization** | Breaking changes when server updates payload shape | Use `JsonElement` for dynamic payloads; add integration tests that validate payload shape against actual GameHub output |
| **Blazor Server reconnection** | Game state lost on circuit reconnect | Store minimal state in `ProtectedLocalStorage` or re-fetch from server; [`GameStateService`](Veldrath.Web/Services/GameStateService.cs) can re-sync on reconnect |
| **Tilemap rendering performance** | Large maps (100x100+) cause DOM thrashing | Implement viewport culling (only render visible tiles); use CSS Grid with `@key`; Phase 2 consider Canvas JS interop |
| **Multiple browser tabs** | Multiple circuits, multiple hub connections | Allow it (each tab = independent circuit + game session); `IActiveCharacterTracker` on server prevents double-claiming same character |
| **JWT expiry during gameplay** | SignalR connection drops when token expires | Proactive refresh via [`AuthStateService.TryRefreshAsync()`](Veldrath.Auth.Blazor/AuthStateServiceBase.cs) on a timer (every 5 minutes); SignalR auto-reconnect with fresh token |
| **Concurrent state mutations** | Race conditions if multiple hub events arrive simultaneously | All `ApplyXxx` methods run on the Blazor circuit's synchronization context; no concurrent execution concerns for scoped services |
| **CSP restrictions** | Inline styles/scripts blocked | Current CSP in [`Program.cs`](Veldrath.Web/Program.cs) allows `unsafe-inline`; use CSS classes instead of inline styles where possible |

---

## Appendix A: Type Reference

Key DTOs and shared types used across the game client:

| Type | Source | Purpose |
|------|--------|---------|
| `TileMapDto` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Full zone tilemap definition |
| `TileLayerDto` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Single render layer |
| `TileEntityDto` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Live entity on tile grid |
| `CharacterMovedPayload` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Movement broadcast payload |
| `ZoneEntitiesSnapshotPayload` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Entity snapshot on zone entry |
| `RegionMapDto` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Region overview map |
| `RegionPlayerMovedPayload` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Region movement payload |
| `ServerInfoPayload` | [`Veldrath.Contracts/Connection`](Veldrath.Contracts/Connection/ConnectionContracts.cs) | Server version info on connect |
| `ChatMessageHubDto` | [`Veldrath.Server/Hubs/GameHub.cs`](Veldrath.Server/Hubs/GameHub.cs) (file-scoped) | Chat message payload |
| `ChatMessageHubRequest` | [`Veldrath.Contracts/Connection`](Veldrath.Contracts/Connection/ConnectionContracts.cs) | Chat send request |
| `CharacterDto` | [`Veldrath.Contracts/Characters`](Veldrath.Contracts/Characters/CharacterContracts.cs) | Character summary for select screen |
| `CreateCharacterRequest` | [`Veldrath.Contracts/Characters`](Veldrath.Contracts/Characters/CharacterContracts.cs) | Character creation request |
| `CharacterPreviewDto` | [`Veldrath.Contracts/Characters`](Veldrath.Contracts/Characters/CharacterCreationContracts.cs) | Creation preview snapshot |
| `AnnouncementPayload` | [`Veldrath.Contracts/Connection`](Veldrath.Contracts/Connection/ConnectionContracts.cs) | Server announcement |
| `KickedPayload` | [`Veldrath.Contracts/Connection`](Veldrath.Contracts/Connection/ConnectionContracts.cs) | Force disconnect payload |
| `MoveCharacterHubRequest` | [`Veldrath.Server/Hubs/GameHub.cs`](Veldrath.Server/Hubs/GameHub.cs) (file-scoped) | Movement request |
| `EngageEnemyHubRequest` | [`Veldrath.Server/Hubs/GameHub.cs`](Veldrath.Server/Hubs/GameHub.cs) (file-scoped) | Combat engagement request |
| `ZoneObjectDto` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Zone entry point on region map |
| `ExitTileDto` | [`Veldrath.Contracts/Tilemap`](Veldrath.Contracts/Tilemap/TilemapContracts.cs) | Zone-to-zone exit tile |

> **Note:** Many hub request/response DTOs are file-scoped records defined at the bottom of [`GameHub.cs`](Veldrath.Server/Hubs/GameHub.cs) (lines 3011-3087). The web client's `GameHubConnectionService` references them via fully qualified names or by defining equivalent records locally.

---

## Appendix B: CSS Structure

```
wwwroot/
└── css/
    ├── app.css          (existing, site-wide styles)
    └── game.css         (new, game-specific styles)
        ├── game-layout  (grid layout for game screen)
        ├── tilemap      (tile grid, tile types, entities)
        ├── status-bar   (HP/MP/XP bars)
        ├── chat         (chat panel)
        ├── combat       (combat UI)
        ├── action-bar   (action buttons)
        ├── inventory    (item grid)
        ├── shop         (buy/sell panels)
        ├── map          (region map)
        └── overlays     (modal panels)
```

The CSS is referenced from `GameLayout.razor` via:

```razor
<link href="/css/game.css" rel="stylesheet" />
```

---

## Appendix C: Animation Strategy (MVP)

Entity movement animations use CSS transitions for MVP:

```css
.entity {
    transition: left 0.15s ease-out, top 0.15s ease-out;
}

.tile-highlight {
    background: rgba(255, 255, 100, 0.3) !important;
    transition: background 0.1s;
}

.combat-hit {
    animation: hit-flash 0.3s;
}

@keyframes hit-flash {
    0%   { filter: brightness(2); }
    100% { filter: brightness(1); }
}
```

---

*End of architecture document.*
