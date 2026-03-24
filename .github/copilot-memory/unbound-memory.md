# RealmUnbound Server + Client — Memory Notes

## Architecture: Hub → MediatR Bridge Pattern

`SelectCharacter` and `EnterZone` are **session management** — they do NOT call `mediator.Send`. The character tracker and zone session repository ARE the implementation. This is intentional.

All gameplay operations follow this pattern:
1. Create `Features/{Feature}/{Name}HubCommand.cs` — command record + result record + handler class
2. Handler reads/writes the server `Character.Attributes` JSON blob (NOT the Core domain `Character`)
3. Add hub method in `GameHub.cs`: `TryGetCharacterId` guard → `mediator.Send` → zone-group broadcast or caller fallback
4. Add `ReactiveCommand` + callback on `GameViewModel`, wire hub subscription in `CharacterSelectViewModel`

**CRITICAL**: `RealmUnbound.Server.Data.Entities.Character` ≠ `RealmEngine.Shared.Models.Character`. Hub handlers work ONLY with the server EF entity and its JSON blob — they never call Core handlers directly.

## DI Registration — UseExternal Pattern

The server calls `AddRealmEngineCore(p => p.UseExternal())` which **intentionally skips all persistence-layer registrations**. The server must manually register every `IXxxRepository` and `IXxxService` it needs in `Program.cs`.

- If a new Core handler injects an interface not already in Program.cs, the server will crash at startup with `Unable to resolve service for type 'IFoo'`.
- Pattern: `builder.Services.AddScoped<IFoo, EfCoreFoo>();` grouped with the existing content repo block (~line 200 in Program.cs).
- Fixed 2026-03-19 session-5: `IArmorRepository → EfCoreArmorRepository`, `IEquipmentSetRepository → EfCoreEquipmentSetRepository`, `INamePatternRepository → EfCoreNamePatternRepository` also missing. Added `using RealmEngine.Core.Abstractions` and `using RealmEngine.Core.Repositories` to Program.cs for these.
- Always run `dotnet build RealmUnbound.Server` after adding a new Core handler to catch missing registrations before Docker.

## SignalR Parameter Binding — Critical Rule

Hub methods that the client calls **must** use a single request DTO (record) as their parameter — NOT multiple separate primitives. Multiple-primitive signatures (e.g. `GainExperience(int amount, string? source)`) cause SignalR binding failures when the client sends an anonymous object via `InvokeAsync(..., new { Amount, Source })` because SignalR tries to deserialize the JSON object as the first `int` parameter.

Fixed 2026-03-21 (session-17): Converted `GainExperience`, `AddGold`, `TakeDamage` hub methods to accept `GainExperienceHubRequest`, `AddGoldHubRequest`, `TakeDamageHubRequest` DTOs.

**Pattern for all hub methods**: Use a DTO record at the protocol boundary, just like `AwardSkillXp(AwardSkillXpHubRequest)` and `EquipItem(EquipItemHubRequest)`.

**`IServerConnectionService` zero-arg overload**: Use `SendCommandAsync(string method)` (no command object) for hub methods that take no parameters, e.g. `LeaveZone`. The underlying `IHubConnection.InvokeAsync<TResult>(string methodName)` zero-arg overload is also available.

## Wired Hub→MediatR Bridges

| Hub Method | HubCommand | Session |
|---|---|---|
| `GainExperience` | `GainExperienceHubCommand` | 2026-03-16 |
| `RestAtLocation` | `RestAtLocationHubCommand` | 2026-03-19 session-2 |
| `AllocateAttributePoints` | `AllocateAttributePointsHubCommand` | 2026-03-19 session-3 |
| `UseAbility` | `UseAbilityHubCommand` | 2026-03-19 session-4 |
| `AwardSkillXp` | `AwardSkillXpHubCommand` | 2026-03-19 session-5 |
| `EquipItem` | `EquipItemHubCommand` | 2026-03-20 session-8 |
| `AddGold` | `AddGoldHubCommand` | 2026-03-20 session-9 |
| `TakeDamage` | `TakeDamageHubCommand` | 2026-03-20 session-10 |
| `CraftItem` | `CraftItemHubCommand` | 2026-03-20 session-13 |
| `EnterDungeon` | `EnterDungeonHubCommand` | 2026-03-20 session-13 |
| `VisitShop` | `VisitShopHubCommand` | 2026-03-21 session-17 |
| `NavigateToLocation` | `NavigateToLocationHubCommand` | 2026-03-24 session-current |

> **Note**: All 12 bridges above are server-side complete and fully wired end-to-end.

## Character Attributes JSON Blob Schema

- `UnspentAttributePoints`: int — spent via `AllocateAttributePoints`
- `Strength`, `Dexterity`, etc.: int — core stats
- `Gold`: int — currency; deducted by `RestAtLocation` (default cost: 10); modified directly by `AddGold`
- `CurrentHealth`, `MaxHealth`: int — restored to max by `RestAtLocation`
- `CurrentMana`, `MaxMana`: int — restored to max by `RestAtLocation`
- Defaults when absent: `MaxHealth = Level * 10`, `MaxMana = Level * 5`
- Handler key constants are `internal const string KeyXxx` on the handler class
- **DO NOT store string-valued data in Attributes blob** — it's deserialized as `Dictionary<string, int>`; mixing types causes `JsonException` and data loss

## Equipment Blob Schema (session-8)

- `Character.EquipmentBlob` separate `text` column — `Dictionary<string, string>` JSON (slot → item-ref slug)
- Added via migration `AddEquipmentBlob` in `Migrations/Application/`
- Handler: `EquipItemHubCommandHandler` in `RealmUnbound.Server/Features/Characters/EquipItemHubCommand.cs`
- Valid slots (case-insensitive): `MainHand`, `OffHand`, `Head`, `Chest`, `Legs`, `Feet`, `Ring`, `Amulet`
- Slot names normalized to canonical casing (first entry in `ValidSlots` set) by handler
- Pass `null` for `ItemRef` to unequip a slot
- Broadcasts `ItemEquipped` payload: `{ CharacterId, Slot, ItemRef, AllEquippedItems }`
- Hub method uses `EquipItemHubRequest(string Slot, string? ItemRef)` DTO in `RealmUnbound.Server.Hubs` namespace
- Client sends: `SendCommandAsync<object>("EquipItem", new { Slot = slot, ItemRef = itemRef })`
- Client callback: `GameViewModel.OnItemEquipped(string slot, string? itemRef)` → AppendLog

## AddGold Handler Details (session-9)

- Blob key: `Gold` (same key used by `RestAtLocationHubCommandHandler.KeyGold`)
- `Amount` can be positive (add) or negative (spend); zero is rejected
- Over-spend guard: if `-Amount > currentGold`, fails with "Not enough gold" message
- Broadcasts `GoldChanged` payload: `{ CharacterId, GoldAdded, NewGoldTotal, Source }`
- `GoldChangedPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnGoldChanged(int goldAdded, int newGoldTotal)` → updates `Gold` property + AppendLog
- Hub sends: `SendCommandAsync<object>("AddGold", new { Amount, Source })`

## UseAbility Handler Details (session-4)

- `DefaultManaCost = 10` deducted from `CurrentMana` per use
- Healing detected via `AbilityId.Contains("heal", OrdinalIgnoreCase)`
- Healing restores `Min(HealingAmount=25, maxHealth - currentHealth)` HP
- Broadcasts `AbilityUsed` payload: `{ CharacterId, AbilityId, ManaCost, RemainingMana, HealthRestored }`
- `AbilityUsedPayload` internal record lives in `CharacterSelectViewModel.cs`

## AwardSkillXp Handler Details (session-5)

- Blob keys: `Skill_{SkillId}_XP` (int) and `Skill_{SkillId}_Rank` (int)
- `XpPerRank = 100` internal const on `AwardSkillXpHubCommandHandler`
- `newRank = newXp / 100`; `rankedUp = newRank > previousRank`
- Broadcasts `SkillXpGained` payload: `{ CharacterId, SkillId, TotalXp, CurrentRank, RankedUp }`
- `SkillXpGainedPayload` internal record lives in `CharacterSelectViewModel.cs`
- **Hub method signature uses a single DTO**: `AwardSkillXp(AwardSkillXpHubRequest request)` where `AwardSkillXpHubRequest(string SkillId, int Amount)` record is defined at bottom of `GameHub.cs` in `RealmUnbound.Server.Hubs` namespace
- Client sends: `SendCommandAsync<object>("AwardSkillXp", new { SkillId = skillId, Amount = amount })`

## Test Counts

| Session | Client Tests | Server Tests | Total |
|---|---|---|---|
| Session-2 (2026-03-19) | 203 | 217 | 420 |
| Session-3 (2026-03-19) | 268 | 235 | 503 |
| Session-4 (2026-03-19) | 274 | 246 | 520 |
| Session-5 (2026-03-19) | **281** | **260** | **541** |
| Session-8 (2026-03-20) | **281** | **279** | **560** |
| Session-9 (2026-03-20) | **288** | **292** | **580** |
| Session-10 (2026-03-20) | **296** | **304** | **600** |
| Session-11 (2026-03-20) | **304** | **304** | **608** |
| Session-12 (2026-03-20) | **308** | **307** | **615** |
| Session-13 (2026-03-20) | **340** | **383** | **723** |
| Session-14 (2026-03-21) | **340** | **383** | **723** |
| Session-15 (2026-03-21) | **340** | **416** | **756** |
| Session-16 (2026-03-21) | **362** | **416** | **778** |
| Session-17 (2026-03-21) | **401** | **425** | **826** |

## P3 Stubs Status

1. ~~`MainMenuViewModel.SettingsCommand`~~ — FIXED session-3: navigates to `SettingsViewModel`
2. ~~`CharacterSelectViewModel.ServerUrl`~~ — FIXED session-5: delegates to `ClientSettings` singleton (injected)
3. ~~`SettingsViewModel` placeholder~~ — FIXED session-5: has real `ServerUrl` property + `ClientSettings` injection
4. ~~`ItemEquipped` hub subscription missing from `CharacterSelectViewModel`~~ — FIXED session-9: added `_itemEquippedSub` field, disposal, subscription, and `ItemEquippedPayload` record
5. ~~`DoVisitShopAsync` stub logging "Shop coming in M5."~~ — FIXED session-17: full hub bridge implemented (`VisitShopHubCommand` + handler + `GameHub.VisitShop` + client wiring)

## P4 XML Doc Gaps

- `IZoneRepository` + `IZoneSessionRepository` — FIXED session-4 (all 8 summaries added)
- ~~`IPlayerAccountRepository`~~ — FIXED session-5 (5 method summaries)
- ~~`ICharacterRepository` (3 missing methods)~~ — FIXED session-5
- ~~`IRefreshTokenRepository` (2 missing methods)~~ — FIXED session-5
- `IAuthService`, `ICharacterService`, `IContentService`, `INavigationService` (Client) — verify at next build (was clean last run)

## ClientSettings (NEW session-5)

- `RealmUnbound.Client/ClientSettings.cs` — `ReactiveObject` with `ServerBaseUrl` property
- Registered as `AddSingleton(new ClientSettings(serverBaseUrl.TrimEnd('/')))` in `App.axaml.cs`
- Config key: `ServerBaseUrl` in `appsettings.json` (falls back to `http://localhost:8080/`)
- Both `CharacterSelectViewModel` (7th ctor param) and `SettingsViewModel` (2nd ctor param) inject it
- Both ViewModels now use `services.AddTransient<T>()` — no manual factory needed

## TakeDamage Handler Details (session-10)

- Blob keys: `CurrentHealth` (int), `MaxHealth` (int)
- `DamageAmount` must be > 0; zero/negative rejected with "must be positive" message
- Health clamped to 0 (never negative): `newHealth = Math.Max(0, currentHealth - damageAmount)`
- `IsDead = newHealth == 0`
- Default `MaxHealth = Level * 10` when blob missing `MaxHealth` key
- Default `CurrentHealth = MaxHealth` when blob missing `CurrentHealth` key
- Broadcasts `DamageTaken` payload: `{ CharacterId, DamageAmount, CurrentHealth, MaxHealth, IsDead, Source }`
- `DamageTakenPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnDamageTaken(int damageAmount, int currentHealth, int maxHealth, bool isDead)` → updates `CurrentHealth` property + AppendLog
- Hub sends: `TakeDamageCommand.Execute((damageAmount, source))` via `ReactiveCommand<(int, string?), Unit>`

## GainExperience Client Wiring Details (session-11)

- `GameViewModel.Level` (int) and `Experience` (long) reactive properties added
- `GainExperienceCommand : ReactiveCommand<(int Amount, string? Source), Unit>` — sends `"GainExperience"` with `new { Amount, Source }`
- `OnExperienceGained(int newLevel, long newExperience, bool leveledUp, int? leveledUpTo)` — updates `Level`, `Experience`, `AppendLog` with level-up branch
- `ExperienceGainedPayload(Guid CharacterId, int NewLevel, long NewExperience, bool LeveledUp, int? LeveledUpTo, string? Source)` internal record in `CharacterSelectViewModel.cs`
- `_experienceGainedSub` field + disposal + subscription added to `CharacterSelectViewModel.DoSelectAsync` — 14th subscription
- Server broadcasts payload via `GainExperience` hub method; field names match result properties: `NewLevel`, `NewExperience`, `LeveledUp`, `LeveledUpTo`

## CharacterSelected Payload Details (session-12)

- `GameHub.SelectCharacter` now deserializes `Character.Attributes` blob before broadcasting
- Broadcasting `CharacterSelected` includes: `Id, Name, ClassName, Level, Experience, CurrentZoneId, CurrentHealth, MaxHealth, CurrentMana, MaxMana, Gold, UnspentAttributePoints, SelectedAt`
- Defaults when blob empty: `MaxHealth = Level * 10`, `MaxMana = Level * 5`, `CurrentHealth = MaxHealth`, `CurrentMana = MaxMana`, `Gold = 0`, `UnspentAttributePoints = 0`
- Client: `CharacterSelectedPayload(Guid Id, string Name, string ClassName, int Level, long Experience, string CurrentZoneId, int CurrentHealth, int MaxHealth, int CurrentMana, int MaxMana, int Gold, int UnspentAttributePoints, DateTimeOffset SelectedAt)` internal record in `CharacterSelectViewModel.cs`
- Client subscription: `_characterSelectedSub` — 15th subscription total (14th active; `_characterStatusSub` declared but reserved for future online-status tracking)
- All 8 seeded values go to `_gameVm.SeedInitialStats(...)` — eliminates zero-HUD on login

## CraftItem Handler Details (session-13)

- `DefaultCraftingCost = 50` — `internal const int` on `CraftItemHubCommandHandler`
- Blob key: `"Gold"` (same as `AddGold` / `RestAtLocation` handlers)
- Guard: if `currentGold < 50` → fails with `"Not enough gold to craft this item"`
- On success: `newGold = currentGold - 50`; saves character; returns `RecipeSlug` as `ItemCrafted`
- Broadcasts `ItemCrafted` payload: `{ CharacterId, RecipeSlug, GoldSpent, RemainingGold }`
- `ItemCraftedPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnItemCrafted(string recipeSlug, int goldSpent, int remainingGold)` → updates `Gold = remainingGold` + `AppendLog`
- **`DefaultCraftingCost` is `internal` — tests must hardcode `50` (not reference the constant directly)**

## VisitShop Handler Details (session-17)

- Uses `IZoneRepository.GetByIdAsync(zoneId)` — validates zone exists + `HasMerchant == true`
- ZoneId empty string → fails with "Zone ID cannot be empty"
- Zone not found → fails with `"Zone '{zoneId}' not found"`
- Zone `HasMerchant == false` → fails with `"Zone '{name}' has no merchant"`
- Returns `{ ZoneId, ZoneName }` on success
- Broadcasts `ShopVisited` payload: `{ CharacterId, ZoneId, ZoneName }` — **to Caller ONLY** (not zone group)
- `ShopVisitedPayload(Guid CharacterId, string ZoneId, string ZoneName)` internal record in `CharacterSelectViewModel.cs`
- 18th subscription: `_shopVisitedSub` field + disposal + `"ShopVisited"` subscription in `DoSelectAsync`
- Client callback: `GameViewModel.OnShopVisited(string zoneId, string zoneName)` → `AppendLog("Welcome to the shop at {zoneName}!")`
- Client sends: `SendCommandAsync<object>("VisitShop", new { ZoneId = _currentZoneId })`
- `VisitShopHubRequest(string ZoneId)` DTO record at bottom of `GameHub.cs`
- Zone with `HasMerchant = true` for handler tests: `"fenwick-crossing"` (from seed data)
- Zone with `HasMerchant = false` for handler tests: `"greenveil-paths"`

## EnterDungeon Handler Details (session-13)

- Uses `IZoneRepository.GetByIdAsync(dungeonSlug)` — `Zone.Id` IS the slug (e.g., `"dungeon-grotto"`)
- **`IDungeonRepository` does NOT exist** — earlier iteration prompt note was a fiction; use `IZoneRepository`
- `IZoneRepository` is already registered in `Program.cs` — no new DI registration needed
- Validates `zone.Type == ZoneType.Dungeon`; fails with `"'{name}' is not a dungeon"` for non-dungeon zones
- Fails with `"Dungeon '{slug}' not found"` for missing zone; fails on empty slug
- Returns `zone.Id` as `DungeonId` in result
- Broadcasts `DungeonEntered` payload: `{ CharacterId, DungeonId, DungeonSlug }`
- `DungeonEnteredPayload` internal record lives in `CharacterSelectViewModel.cs`
- Client callback: `GameViewModel.OnDungeonEntered(string dungeonId, string dungeonSlug)` → `AppendLog`
- Seeded dungeon available in `TestDbContextFactory` (SQLite + `EnsureCreated`): **REPLACED in session-14 — use `"verdant-barrow"` (Verdant Barrow, ZoneType.Dungeon)**

## World / Region / Zone Schema (session-14 — 2026-03-21)

Full World → Region → Zone hierarchy implemented.

**Entities** (all in `RealmUnbound.Server/Data/Entities/`):
- `World.cs`: `Id` (slug), `Name`, `Description`, `Era`, `ICollection<Region> Regions`
- `Region.cs`: `Id` (slug), `Name`, `Description`, `RegionType Type`, `MinLevel`, `MaxLevel`, `IsStarter`, `IsDiscoverable`, `WorldId` FK, navigation `World`, `ICollection<Zone> Zones`, `ICollection<RegionConnection> Connections`
- `RegionType` enum (in `Region.cs`): `Forest`, `Highland`, `Coastal`, `Volcanic`
- `RegionConnection.cs`: composite PK (`FromRegionId` + `ToRegionId`), navigate `FromRegion`, `ToRegion`; `Restrict` delete-behavior on both FK sides
- `ZoneConnection.cs`: composite PK (`FromZoneId` + `ToZoneId`), navigate `FromZone`, `ToZone`; `Restrict` delete-behavior on both FK sides; bidirectional travel = 2 rows

**Zone entity changes** (`Zone.cs`):
- Added: `string RegionId` (FK), `Region Region` nav, `bool HasInn`, `bool HasMerchant`, `bool IsPvpEnabled`, `bool IsDiscoverable`, `ICollection<ZoneConnection> Exits`
- Removed: old Tutorial type seed data (5 zones removed)

**Seed data (Draveth world)**:
- 1 World: `draveth` / "Draveth" / "The Age of Embers"
- 4 Regions: `thornveil` (Forest, starter), `greymoor` (Highland), `saltcliff` (Coastal), `cinderplain` (Volcanic)
- 6 RegionConnections: thornveil↔greymoor, greymoor↔saltcliff, greymoor↔cinderplain (bidirectional)
- 16 Zones: 4 per region (1 Town, 2 Wilderness, 1 Dungeon)
- **Starter zone**: `fenwick-crossing` (Fenwick's Crossing, Thornveil)
- **Dungeon IDs**: `verdant-barrow`, `barrow-deeps`, `sunken-name`, `kaldrek-maw`
- 30 ZoneConnections (bidirectional = 2 rows per pair; cross-region borders included)

**Character default zone**: `"fenwick-crossing"` (was `"starting-zone"`)

**Repositories**: `IRegionRepository` + `RegionRepository`, `IWorldRepository` + `WorldRepository` added; `IZoneRepository.GetByRegionIdAsync` added  
**DI**: `IRegionRepository` + `IWorldRepository` registered in `Program.cs`  
**Contracts**: `RegionDto`, `WorldDto` added to `ZoneContracts.cs`; `ZoneDto` extended with optional `RegionId?`, `HasInn`, `HasMerchant`  
**Endpoints**: `GET /api/regions`, `GET /api/regions/{id}`, `GET /api/regions/{id}/connections`, `GET /api/worlds`, `GET /api/worlds/{id}`, `GET /api/zones/by-region/{regionId}`  
**Migration**: `AddWorldRegionZoneConnections` in `Migrations/Application/`

**SQLite FK tests**: FK constraints ARE enforced in `TestDbContextFactory`. Tests that add custom zones without RegionId will fail. Use seeded zone IDs (`"fenwick-crossing"`, `"aldenmere"`, etc.) in ZoneSession tests instead of `MakeZone`.  
When deleting all zones in tests: delete `ZoneConnections` first (FK Restrict on Zone), then Zones.

## IContentService Extension (session-13)

- 6 new content types added: `Organization`, `WorldLocation`, `Dialogue`, `ActorInstance`, `MaterialProperty`, `TraitDefinition`
- 12 new methods on `IContentService` + `HttpContentService` (2 per type: list + single)
- Routes: `api/content/organizations`, `api/content/world-locations`, `api/content/dialogues`, `api/content/actor-instances`, `api/content/material-properties`, `api/content/traits`
- `TraitDefinition` uses `key` param (not `slug`) in route and DTO
- All 12 implementations are one-liner `GetListAsync<T>` or `GetSingleAsync<T>` delegates
- `FakeContentService` stubs all 12 with `Task.FromResult(new List<T>())` / `Task.FromResult<T?>(null)`
- 24 new tests in `HttpContentServiceTests.cs` (4 per type)

## DbContext Separation (established 2026-03-19 session-6)

Three DbContexts share the same Postgres database but own distinct table sets:

| Context | Namespace | Tables | Purpose |
|---|---|---|---|
| `ApplicationDbContext` | `RealmUnbound.Server.Data` | Identity (AspNet*), Characters, RefreshTokens, Zones, ZoneSessions, Foundry* | Server auth + operational |
| `GameDbContext` | `RealmEngine.Data.Persistence` | SaveGames, HallOfFameEntries, InventoryRecords | Game-state entities (portable across clients) |
| `ContentDbContext` | `RealmEngine.Data.Persistence` | Weapons, Armors, Skills, Spells, Abilities, Recipes, etc. | Read-only content catalog |

**Rule**: game entities (saves, inventory, hall of fame) always go in `GameDbContext`, NOT in `ApplicationDbContext`. Server auth + operational rows go in `ApplicationDbContext`.

`ServerSaveGameRepository` and `ServerHallOfFameRepository` inject `GameDbContext` (not `ApplicationDbContext`).

Migrations for each context:
- `ApplicationDbContext`: `RealmUnbound.Server/Migrations/` (original) + `Migrations/Application/` (newer)
- `GameDbContext`: `RealmEngine.Data/Migrations/GameDb/`
- `ContentDbContext`: `RealmEngine.Data/Migrations/`

All three are migrated at startup in `Program.cs` with shared `allKnown` set to avoid `RepairStaleMigrationsAsync` false-positives.

Test factories in `RealmUnbound.Server.Tests/Infrastructure/`:
- `TestDbContextFactory` → `ApplicationDbContext` (SQLite) — used by Zone, Auth, Character, GameHub tests
- `TestGameDbContextFactory` → `GameDbContext` (SQLite) — used by `ServerSaveGameRepositoryTests` + `ServerHallOfFameRepositoryTests`



### ActorClassDto (breaking change, session-3)
- `ActorClassDto` ctor gained `HitDie` (int), `PrimaryStat` (string), `RarityWeight` (int)
- All `new ActorClassDto(slug, name, typeKey)` calls must be updated to include all 6 params

### AvailableClasses Test
- `CharacterSelectViewModel.LoadAsync` fires-and-forgets on construction; completes synchronously if `FakeContentService` returns `Task.FromResult`
- Fallback test: use `new FakeContentService { Classes = [] }` + `await Task.Delay(50)` before asserting
- Catalog-populated test: pass non-empty classes + same delay

### FakeServerConnectionService
- Tracks sent commands in `SentCommands` list (added session-3)

## Session Log

### Session-3 (2026-03-19)
- Fixed `ActorClassDto` breaking change in `FakeServices.cs` and `CharacterSelectViewModelTests.cs`
- Fixed `AvailableClasses_Should_Contain_Expected_Classes` test (empty catalog + delay pattern)
- Fixed P3 stub: `MainMenuViewModel.SettingsCommand` navigates to `SettingsViewModel`
- Created `SettingsViewModel` with `BackCommand`
- Implemented `AllocateAttributePoints` hub bridge (command + handler + hub method)
- Added 9 server tests + client tests for `AllocateAttributePoints`

### Session-4 (2026-03-19)
- Implemented `UseAbility` hub bridge end-to-end
- Fixed `IZoneRepository` + `IZoneSessionRepository` XML docs (8 summaries)
- Added 10 server tests + 6 client tests = 16 new tests
- Total: 274 client + 246 server = 520 passing

### Session-16 (2026-03-21) — GameView UI redesign
- **GameView.axaml** fully rewritten: 3-row (Auto/\*/Auto) × 3-column (Auto/\*/220) layout
  - Row 0: topbar border — character name → zone name + zone-type badge (StringConverters.IsNotNullOrEmpty), status message, logout button
  - Row 1 col 0: collapsible left panel via DockPanel `LastChildFill="False"` — toggle button always visible (28px, docked Right), content Border (196px, IsVisible="{Binding IsLeftPanelOpen}") with HP/MP ProgressBars + action log
  - Row 1 col 1: center map placeholder
  - Row 1 col 2: right character panel (220px) — level, XP progress (ExperienceToNextLevel), gold, unspent points badge, online players, dev tools Expander
  - Row 2: context-sensitive action bar (HasInn → "Rest at Inn", HasMerchant → "Shop", always: Craft Item, Enter Dungeon, Use Ability)
- **GameViewModel.cs** extended with 11 new properties/commands:
  - `IsLeftPanelOpen` (bool, default true) + `LeftPanelToggleIcon` (◀/▶) + `ToggleLeftPanelCommand`
  - `HasInn`, `HasMerchant`, `ZoneType` — set from `ZoneDto` in `InitializeAsync`
  - `HasUnspentPoints` (computed, raised when `UnspentAttributePoints` changes)
  - `ExperienceToNextLevel` (computed `Math.Max(1L, Level * 500L)`, raised when `Level` changes)
  - `DevGainXpCommand` (gains 100 XP), `DevAddGoldCommand` (+50 gold), `DevTakeDamageCommand` (takes 10 damage)
- **22 new tests** added to `GameViewModelTests.cs` for all new properties/commands
- AXAML bug: old layout content was left below `</UserControl>` after rewrite → caused AVLN1001 "multiple root elements"; fixed by stripping duplicate content after first closing tag
- Final count: **362 client + 416 server = 778 total passing**

### Session-5 (2026-03-19)
- Fixed 3 latent DI gaps: `IArmorRepository`, `IEquipmentSetRepository`, `INamePatternRepository`; added 2 new using directives to Program.cs
- Fixed P4 XML docs: 10 missing summaries across `IPlayerAccountRepository`, `ICharacterRepository`, `IRefreshTokenRepository`
- Fixed P3 `ServerUrl` stub: created `ClientSettings` singleton, injected into `CharacterSelectViewModel` + `SettingsViewModel`; both ViewModels now use plain `AddTransient<T>()`
- Promoted `SettingsViewModel` from placeholder to real (has `ServerUrl` property)
- Implemented `AwardSkillXp` hub bridge: `AwardSkillXpHubCommand` + handler + `GameHub.AwardSkillXp(AwardSkillXpHubRequest)` + client `AwardSkillXpCommand` + `OnSkillXpGained`
- Added `SkillXpGained` subscription + `SkillXpGainedPayload` in `CharacterSelectViewModel`
- Added 14 server tests (3 GetActiveCharacters + 11 AwardSkillXp) + 4 GameViewModel tests + 3 SettingsViewModelTests
- Total: 281 client + 260 server = 541 passing

### Session-6 (2026-03-19) — Docker startup fix
- Root cause: `GameDbContext` was never registered in server DI; `EfCoreInventoryService` injected it → `AggregateException` at container build time
- Architectural fix: removed `SaveGames`/`HallOfFameEntries` from `ApplicationDbContext` (they were added incorrectly in session-5)
- Established DbContext separation rule (see section above)
- `ServerSaveGameRepository` + `ServerHallOfFameRepository` now inject `GameDbContext`
- Registered `GameDbContext` with Npgsql in `Program.cs`; threaded into migration startup block with `allKnown` union
- New `ApplicationDbContext` migration: `RemoveGameEntitiesFromApplicationDbContext` (drops SaveGames/HallOfFameEntries)
- `GameDbContext` already had Postgres migrations in `Migrations/GameDb/` (created earlier sessions)
- Created `TestGameDbContextFactory` (SQLite `GameDbContext`) for server tests; updated `ServerRepositoryTests.cs`
- 260 server tests passing; Docker server reaches Healthy status clean

### Session-8 (2026-03-20) — EquipItem bridge + P2 coverage
- Added `Character.EquipmentBlob` (`text`, default `{}`) to server EF entity
- Configured in `ApplicationDbContext.OnModelCreating` + migration `AddEquipmentBlob` in `Migrations/Application/`
- Implemented `EquipItemHubCommand` + `EquipItemHubResult` + `EquipItemHubCommandHandler` (8 valid slots, case-insensitive)
- Added `GameHub.EquipItem(EquipItemHubRequest)` + `EquipItemHubRequest` record at bottom of `GameHub.cs`
- Added client `GameViewModel.EquipItemCommand` (ReactiveCommand<(string, string?), Unit>) + `DoEquipItemAsync` + `OnItemEquipped`
- Added 19 server tests: 2 SelectCharacter P2, 1 OnDisconnectedAsync P2, 5 catch-block (mediator throws), 6 EquipItem hub method, 5 EquipItemHubCommandHandler handler
- KEY GOTCHA: `OnDisconnectedAsync` reads `AccountId` from `Context.Items` (set by `OnConnectedAsync`); tests that call `OnDisconnectedAsync` directly must pre-seed `ctx.Items["AccountId"] = accountId`
- Also: `CreateHub` factory now accepts optional `IActiveCharacterTracker? tracker` param for P2 tests requiring pre-seeded trackers
- Final: 281 client + 279 server = 560 total passing

### Session-9 (2026-03-20) — AddGold bridge + DamageTaken prep
- Implemented `AddGold` hub bridge: `AddGoldHubCommand` + handler + `GameHub.AddGold` + client `AddGoldCommand` + `OnGoldChanged`
- Added `GoldChanged` subscription + `GoldChangedPayload` in `CharacterSelectViewModel`
- Fixed P3 stub: `ItemEquipped` hub subscription missing from `CharacterSelectViewModel` — added `_itemEquippedSub` + `ItemEquippedPayload`
- Added 8 server tests + 8 client tests
- Total: 288 client + 292 server = 580 total passing

### Session-10 (2026-03-20) — TakeDamage bridge
- Implemented `TakeDamage` hub bridge: `TakeDamageHubCommand` + handler + `GameHub.TakeDamage` + client `TakeDamageCommand` + `OnDamageTaken`
- Added `DamageTaken` subscription + `DamageTakenPayload` in `CharacterSelectViewModel`
- Added `AvailableClasses_Should_Fall_Back_To_Builtins_When_Catalog_Empty` test (NotBeEmpty guard)
- Added 8 server tests + 8 client tests
- Total: 296 client + 304 server = 600 total passing

### Session-11 (2026-03-20) — GainExperience client wiring
- Added `GameViewModel.Level` (int) + `Experience` (long) reactive properties
- Implemented `GainExperienceCommand : ReactiveCommand<(int Amount, string? Source), Unit>`
- Implemented `OnExperienceGained(int newLevel, long newExperience, bool leveledUp, int? leveledUpTo)` — updates `Level`, `Experience`, `AppendLog`
- Added `ExperienceGainedPayload` record + `_experienceGainedSub` field/disposal/subscription in `CharacterSelectViewModel`
- 8 new tests (4 server + 4 client)
- Total: 304 client + 304 server = 608 total passing

### Session-12 (2026-03-20) — CharacterSelected payload + SeedInitialStats
- Extended `GameHub.SelectCharacter`: deserializes `Attributes` blob, includes `Experience`, `CurrentHealth`, `MaxHealth`, `CurrentMana`, `MaxMana`, `Gold`, `UnspentAttributePoints` in `CharacterSelected` payload
- Added `GameViewModel.SeedInitialStats(int level, long experience, int currentHealth, int maxHealth, int currentMana, int maxMana, int gold, int unspentAttributePoints)` — eliminates all-zero HUD on login
- Added `CharacterSelectedPayload` internal record in `CharacterSelectViewModel.cs`
- Added `_characterSelectedSub` field + disposal + `"CharacterSelected"` subscription in `DoSelectAsync` — 15th subscription total (14th active; `_characterStatusSub` declared but reserved)
- Added 7 new tests: 3 server (CharacterSelected payload content including blob defaults) + 2 GameViewModel (SeedInitialStats) + 2 CharacterSelectViewModel (CharacterSelected subscription)
- Total: 308 client + 307 server = 615 total passing
- NOTE: actual server count at end of session was already 362 (includes +55 from subsequent Content service tests added during this session but logged as session-13)

### Session-13 (2026-03-20) — Content service extension + CraftItem + EnterDungeon bridges
- **Goal 1**: Extended `IContentService` / `HttpContentService` with 12 new methods (2 per type) for 6 new content types: `Organization`, `WorldLocation`, `Dialogue`, `ActorInstance`, `MaterialProperty`, `TraitDefinition`
- **Goal 2**: Added 24 new tests to `HttpContentServiceTests.cs` (4 per type); client 308→332
- **Goal 3**: Implemented `CraftItem` hub bridge end-to-end: `CraftItemHubCommand.cs` (handler deducts 50 gold), `GameHub.CraftItem`, client `CraftItemCommand` + `OnItemCrafted`, `ItemCraftedPayload` + `_itemCraftedSub` in `CharacterSelectViewModel`
- **Goal 4**: Implemented `EnterDungeon` hub bridge end-to-end: `EnterDungeonHubCommand.cs` (uses `IZoneRepository`, validates `ZoneType.Dungeon`), `GameHub.EnterDungeon`, client `EnterDungeonCommand` + `OnDungeonEntered`, `DungeonEnteredPayload` + `_dungeonEnteredSub` in `CharacterSelectViewModel`
- Key discovery: `IDungeonRepository` doesn't exist — `EnterDungeon` uses `IZoneRepository.GetByIdAsync(slug)` + `ZoneType.Dungeon` check
- Tests: 21 server tests for Goals 3+4 added to `GameHubTests.cs`; 5 client tests in `GameViewModelTests.cs`; 3 client tests in `CharacterSelectViewModelTests.cs`
- Final count: **340 client + 383 server = 723 total passing**

### Session-17 (2026-03-21) — VisitShop hub bridge
- **Gap analysis**: found 1 remaining P3 stub — `DoVisitShopAsync` logged "Shop coming in M5." with no hub call; no server counterpart existed
- **P4 false positive**: subagent reported missing CraftItem/EnterDungeon handler tests — already existed (5 CraftItem + 4 EnterDungeon handler tests in `GameHubTests.cs`)
- Created `RealmUnbound.Server/Features/Characters/VisitShopHubCommand.cs` — validates zone + `HasMerchant == true`; broadcasts `ShopVisited` to Caller only
- Added `GameHub.VisitShop(VisitShopHubRequest)` + `VisitShopHubRequest(string ZoneId)` DTO to `GameHub.cs`
- Replaced `DoVisitShopAsync` stub; added `GameViewModel.OnShopVisited`; added 18th subscription `_shopVisitedSub` + `ShopVisitedPayload` in `CharacterSelectViewModel`
- Added 9 server tests (5 hub dispatch + 4 handler) + 2 GameViewModel tests + 1 CharacterSelectViewModel test
- Key fix: subscription test used non-existent `MakeConnectedVmAsync` helper + `conn.Subscriptions` property — rewritten to follow `DungeonEntered` event-fire pattern; assertion changed to `Contain(msg => msg.Contains("Welcome to the shop at Fenwick Crossing"))` since zone name appears in multiple log entries
- Final count: **401 client + 425 server = 826 total passing**

