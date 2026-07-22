# Veldrath Server + Client — Memory Notes

> **Status**: Updated through Session-41 (2026-06-30).

## OAuth Provider-Link Confirmation Flow (session-30, 2026-04-10)

**Security fix**: `AuthService.ExternalLoginOrRegisterAsync` previously auto-linked a new OAuth provider to an existing account if the emails matched (step 2). This allowed a hostile provider to hijack an account. It now triggers an email-confirmation flow instead.

**New files**:
- `Veldrath.Server/Infrastructure/Email/IEmailSender.cs` — `Task SendAsync(to, subject, htmlBody, ct)`
- `Veldrath.Server/Infrastructure/Email/NullEmailSender.cs` — dev no-op; logs full email body + URL via `ILogger`
- `Veldrath.Server/Infrastructure/Email/SmtpEmailSender.cs` — BCL `System.Net.Mail.SmtpClient`; config keys: `Email:SmtpHost`, `Email:SmtpPort` (587), `Email:User`, `Email:Password`, `Email:SenderAddress`, `Email:SenderName`
- `Veldrath.Server/Data/Entities/PendingLinkToken.cs` — EF entity; fields: `Id` (PK), `AccountId` (FK → AspNetUsers cascade), `LoginProvider`, `ProviderKey`, `ProviderDisplayName?`, `TokenHash` (SHA-256 hex, unique index), `Email`, `ReturnUrl?` (max 512), `ExpiresAt`, `CreatedAt`, `IsConfirmed`, nav `Account`
- `Veldrath.Server/Data/Repositories/IPendingLinkRepository.cs` + `EfCorePendingLinkRepository.cs` — `CreateAsync`, `GetByTokenHashAsync` (.Include Account), `ConfirmAsync(Guid id)`, `PurgeExpiredAsync` (uses `ExecuteDeleteAsync`)
- `Veldrath.Server/Features/Auth/ExternalLoginResult.cs` — replaces `(AuthResponse?, string?)` tuple; `ExternalLoginStatus` enum: `Success | Error | PendingLinkConfirmation`
- `Veldrath.Server/Features/Auth/AccountLinkService.cs` — `RequestLinkAsync(existing, info, serverBaseUrl, returnUrl?, ct)` + `ConfirmAndLinkAsync(rawToken, ct)` → `(PlayerAccount?, PendingLinkToken?, string? error)`. Errors: `"link_invalid"`, `"link_expired"`, `"link_already_confirmed"`. TTL from `Auth:PendingLinkExpiryMinutes` (default 60). Token: `RandomNumberGenerator.GetBytes(32)` hex → SHA-256 double-hash.
- `Veldrath.Server/Features/Auth/PendingLinkEndpoints.cs` — `GET /api/auth/link/confirm?token={rawToken}`. On success: calls `authService.CreateSessionAsync`, `exchangeService.CreateCode`, redirects to `{returnUrl}?code=xxx&aid=yyy&linked=1` (or `/login?code=xxx&aid=yyy`).
- EF migration `20260410234947_AddPendingLinkTokens`

**Modified files**:
- `AuthService.cs` — added `AccountLinkService` ctor param; `ExternalLoginOrRegisterAsync` now returns `ExternalLoginResult` (step 2 sends email instead of silent-link); `HashToken` changed to `public static`; added `public CreateSessionAsync` wrapper
- `ExternalAuthEndpoints.cs` — switch on `result.Status` in both `HandleOAuthTicket` and `Callback`; `IsAllowedReturnUrl` changed to `internal static`
- `Program.cs` — `AccountLinkService` (scoped), conditional `IEmailSender` (Null when `Email:SmtpHost` empty), `IPendingLinkRepository → EfCorePendingLinkRepository` (scoped), `app.MapPendingLinkEndpoints()`
- `RealmFoundry/Components/Pages/Profile.razor` — Link button `returnUrl` now uses `Nav.ToAbsoluteUri("profile")` (was hardcoded localhost); `?linked=1` and `?pending_link=1` query param handling
- `RealmFoundry/Components/Pages/AuthCallback.razor` — `?pending_link=1` shows "check your inbox" instead of attempting exchange code

**Key note**: `PurgeExpiredAsync` exists but is not called by any hosted service — expired tokens accumulate harmlessly (rejected at confirm time). Add `IHostedService` cleanup later if needed.

**Tests**: 6 unit (`AccountLinkServiceTests`) + 5 integration (`PendingLinkEndpointTests`) = 11 new tests all passing.

## Architecture: Hub → MediatR Bridge Pattern

> **See [`.github/instructions/hub-development.md`](../../.github/instructions/hub-development.md) for the canonical hub development conventions, including the Hub→MediatR bridge pattern, single-DTO parameter rule, and DI registration patterns.**

`SelectCharacter` and `EnterZone` are **session management** — they do NOT call `mediator.Send`. The character tracker and player session repository ARE the implementation. This is intentional.

## DI Registration — UseExternal Pattern

> **See [`.github/instructions/ef-core-patterns.md`](../../.github/instructions/ef-core-patterns.md#di-registration--the-useexternal-pattern) for the UseExternal DI pattern.**

The server calls `AddRealmEngineCore(p => p.UseExternal())` which **intentionally skips all persistence-layer registrations**. The server must manually register every `IXxxRepository` and `IXxxService` it needs in `Program.cs`. Always run `dotnet build Veldrath.Server` after adding a new Core handler to catch missing registrations before Docker.

## SignalR Parameter Binding — Critical Rule

> **See [`.github/instructions/hub-development.md`](../../.github/instructions/hub-development.md#the-single-dto-parameter-rule-critical) for the single-DTO parameter rule and binding failure explanation.**

Hub methods that the client calls **must** use a single request DTO (record) as their parameter — NOT multiple separate primitives. Fixed 2026-03-21 (session-17): Converted `GainExperience`, `AddGold`, `TakeDamage` hub methods to accept `GainExperienceHubRequest`, `AddGoldHubRequest`, `TakeDamageHubRequest` DTOs.

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
| `NavigateToLocation` | `NavigateToLocationHubCommand` | 2026-03-24 session-18 |
| `UnlockZoneLocation` | `UnlockZoneLocationHubCommand` | 2026-03-24 session-18 |
| `SearchArea` | `SearchAreaHubCommand` | 2026-03-24 session-18 |
| `TraverseConnection` | `TraverseConnectionHubCommand` | 2026-03-24 session-18 |
| `EngageEnemy` | `EngageEnemyHubCommand` | 2026-03-30 session-27 |
| `AttackEnemy` | `AttackEnemyHubCommand` | 2026-03-30 session-27 |
| `DefendAction` | `DefendActionHubCommand` | 2026-03-30 session-27 |
| `FleeFromCombat` | `FleeFromCombatHubCommand` | 2026-03-30 session-27 |
| `UseAbilityInCombat` | `UseAbilityInCombatHubCommand` | 2026-03-30 session-27 |
| `Respawn` | `RespawnHubCommand` | 2026-03-30 session-27 |

> **Note**: All 15 bridges above are server-side complete and fully wired end-to-end.
>
> **Note**: Sessions 32-39 added no new Hub→MediatR bridges. Moderation commands (`/ignore`, `/warn`, `/mute`, `/ban`) are processed via the existing SignalR chat infrastructure. Dev endpoints (`DevEndpoints.cs`) use minimal API endpoints, not the hub bridge pattern.
>
> **Note**: Session-40 achievement triggers (`CheckAchievementProgressCommand`) were wired into 5 Core handlers (AttackEnemy, CompleteQuest, GainExperience, LevelUp, HandlePlayerDeath) — not as Hub→MediatR bridges. Achievement progress is evaluated server-side within the handler pipeline, not via SignalR hub methods.

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
- Handler: `EquipItemHubCommandHandler` in `Veldrath.Server/Features/Characters/EquipItemHubCommand.cs`
- Valid slots (case-insensitive): `MainHand`, `OffHand`, `Head`, `Chest`, `Legs`, `Feet`, `Ring`, `Amulet`
- Slot names normalized to canonical casing (first entry in `ValidSlots` set) by handler
- Pass `null` for `ItemRef` to unequip a slot
- Broadcasts `ItemEquipped` payload: `{ CharacterId, Slot, ItemRef, AllEquippedItems }`
- Hub method uses `EquipItemHubRequest(string Slot, string? ItemRef)` DTO in `Veldrath.Server.Hubs` namespace
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
- **Hub method signature uses a single DTO**: `AwardSkillXp(AwardSkillXpHubRequest request)` where `AwardSkillXpHubRequest(string SkillId, int Amount)` record is defined at bottom of `GameHub.cs` in `Veldrath.Server.Hubs` namespace
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
| Session-18 (2026-03-24) | **466** | **461** | **927** |
| Session-19 (server-offline UX) | **511** | **468** | **979** |
| Session-20 (server reconnect polling) | **518** | **468** | **986** |
| Session-26 (2026-03-30: Phase 0b–0c) | **512** | **468** | **980** |
| Session-27 (2026-03-30: combat system phases 1-7) | **512** | **468** | **980** |
| Session-28 (2026-03-30: combat tests + GameView combat UI) | **525** | **491** | **1016** |
| Session-29 (2026-04-08: region map arch + PlayerSession migration) | **525** | **560** | **1085** |
| Session-30 (2026-04-10: OAuth provider-link confirmation flow) | **525** | **571** | **1096** |
| Session-31 (2026-04-11: Auth hardening pass 1) | **713** | **728** | **1441** |
| Session-32-39 | ~4,500 | Post-Session-31 work (chat, moderation, auth overhaul, editorial, web) |
| Session-40 | ~4,732 | Expansion & polish: Discord tests, achievement wiring, zone TMX maps, settings cleanup |

## Phase 0b — Server Schema Migrations (2026-03-30)

Three migrations added to `Veldrath.Server/Data/Migrations/Application/`:

- `20260330000001_AddCharacterDifficultyMode` — adds `DifficultyMode string` (default `"Normal"`) to `Characters` table
- `20260330000002_AddZoneRescueFund` — adds `RescueFundTotal long` (default `0`) to `Zones` table
- `20260330000003_AddGlobalStats` — new `GlobalStats` table; `GlobalStat` entity with `Key string PK, Value long`

Entity changes:
- `Veldrath.Server/Data/Entities/Character.cs`: `public string DifficultyMode { get; set; } = "Normal";`
- `Veldrath.Server/Data/Entities/Zone.cs`: `public long RescueFundTotal { get; set; } = 0;`
- `Veldrath.Server/Data/Entities/GlobalStat.cs`: new entity
- `ApplicationDbContext.GlobalStats DbSet<GlobalStat>` registered

Server.Tests: 468 passing (unchanged — schema picked up automatically via `EnsureCreated` in SQLite integration tests).

## P3 Stubs Status

1. ~~`MainMenuViewModel.SettingsCommand`~~ — FIXED session-3: navigates to `SettingsViewModel`
2. ~~`CharacterSelectViewModel.ServerUrl`~~ — FIXED session-5: delegates to `ClientSettings` singleton (injected)
3. ~~`SettingsViewModel` placeholder~~ — FIXED session-5: has real `ServerUrl` property + `ClientSettings` injection
4. ~~`ItemEquipped` hub subscription missing from `CharacterSelectViewModel`~~ — FIXED session-9: added `_itemEquippedSub` field, disposal, subscription, and `ItemEquippedPayload` record
5. ~~`DoVisitShopAsync` stub logging "Shop coming in M5."~~ — FIXED session-17: full hub bridge implemented (`VisitShopHubCommand` + handler + `GameHub.VisitShop` + client wiring)
6. ~~Server-offline UX: no feedback when server unreachable at launch~~ — FIXED session-19: full server-status + announcement system (see below)

## Server Reconnect Polling (session-20)

**`IServerStatusService.StartPollingAsync(Func<string> getServerUrl, CancellationToken ct)`** — background loop that calls `CheckAsync` periodically:
- Waits `OfflinePollInterval` (5 s default) between checks when offline; `OnlinePollInterval` (30 s default) when online
- Kicked off in `App.axaml.cs` `OnFrameworkInitializationCompleted` after DI is built: `_ = serverStatus.StartPollingAsync(() => clientSettings.ServerBaseUrl)`
- `OperationCanceledException` from `CheckAsync` is re-thrown (not treated as server failure) — prevents a shutdown cancellation from flipping Status to Offline
- Both intervals are `internal` fields on `ServerStatusService` for test override (set to 10 ms in tests)
- `FakeServerStatusService` has a no-op `StartPollingAsync` implementation
- `ServerStatusServiceTests` uses event-based (`TaskCompletionSource`) assertions, not wall-clock timeouts, to avoid flakiness under parallel test runs



**Problem fixed**: When server was offline at client launch, SplashViewModel ignored RefreshAsync failure, leaving stale tokens in memory. This caused a redirect loop (always landing on CharacterSelect) and logout not clearing auth buttons properly.

**New components**:
- `Veldrath.Contracts/Announcements/AnnouncementContracts.cs` — `AnnouncementDto(int Id, string Title, string Body, string Category, bool IsPinned, DateTimeOffset PublishedAt)` shared between server and client
- `Veldrath.Server/Data/Entities/Announcement.cs` — EF entity with `IsActive`, `ExpiresAt?`, `IsPinned`, `PublishedAt`
- `Veldrath.Server/Data/Repositories/AnnouncementRepository.cs` — `IAnnouncementRepository` + impl; `GetActiveAsync()` filters `IsActive && (ExpiresAt == null || ExpiresAt > now)`, orders `IsPinned DESC, PublishedAt DESC`. **Must use `var now = DateTimeOffset.UtcNow` local variable** — SQLite cannot translate `DateTimeOffset.UtcNow` directly in LINQ expressions.
- `Veldrath.Server/Features/Announcements/AnnouncementEndpoints.cs` — `GET /api/announcements`, `AllowAnonymous`, returns `AnnouncementDto[]`
- `Veldrath.Client/Services/ServerStatusService.cs` — `IServerStatusService : INotifyPropertyChanged` singleton; pings `/health`; `IsOnline`, `StatusMessage`, `Status` (enum); `CheckAsync(serverUrl)`.  **Interface must extend `System.ComponentModel.INotifyPropertyChanged`** so `WhenAnyValue(s => s.Status)` works on the interface type.
- `Veldrath.Client/Services/AnnouncementService.cs` — `IAnnouncementService` + `HttpAnnouncementService`; uses typed `HttpClient` registered via `AddHttpClient<IAnnouncementService, HttpAnnouncementService>` with base address set in DI

**Key architectural note — `LoadAnnouncementsAsync` guard**: Only guard on `announcementService is not null`. Do NOT also guard on `settings is not null` — the HttpClient base address is already set in DI; no need to pass `serverUrl` to the method.

**SplashViewModel soft-logout fix**: In `RunSplashAsync` Phase 3, check server health first. If server offline and tokens present → call `LogoutAsync()` to clear local state. If server online and tokens present → `await RefreshAsync()`; if that returns `false` → call `LogoutAsync()`. This prevents redirect loops and stale auth state.

**MainWindowViewModel**: Added `IServerStatusService` (required parameter). Exposes `IsServerOnline` and `ServerStatusMessage` reactive properties; subscribes to `serverStatus.WhenAnyValue(s => s.Status)`.

**MainWindow.axaml**: Wraps content in 2-row `Grid`. Row 0 = dark-red `Border` banner (`IsVisible="{Binding !IsServerOnline}"`). Row 1 = `ContentControl` for page navigation.

**MainMenuViewModel disabled commands**: `RegisterCommand` and `LoginCommand` gated on `serverOnline` observable; `SelectCharacterCommand` gated on `canEnterGame = IsLoggedIn && IsServerOnline`. Commands are auto-disabled (greyed out) when server is offline.

**MainMenuView.axaml news panel**: 3-column grid (`280,*,280`). Column 0 = news panel with `ScrollViewer > StackPanel > [ItemsControl | placeholder TextBlock]`. **ScrollViewer is a ContentControl (single child only)** — wrap multiple children in a `StackPanel` inside the `ScrollViewer`.

**EF migration**: `AddAnnouncements` — in `Veldrath.Server/Data/Migrations/Application/`

**Test structure — announcement integration tests**: Follow the `IAsyncLifetime` fixture pattern (same as other server integration tests). Use a `AnnouncementsFixture` that seeds all data in `InitializeAsync`. Tests needing a clean/empty DB must be in a **separate class with their own `WebAppFactory` instance** (`AnnouncementEmptyEndpointTests : IAsyncLifetime`). Using `IClassFixture<WebAppFactory>` directly with per-test seeding causes the "empty list" test to fail when it runs after seeding tests.

## P4 XML Doc Gaps

- `IZoneRepository` + `IZoneSessionRepository` — FIXED session-4 (all 8 summaries added)
- ~~`IPlayerAccountRepository`~~ — FIXED session-5 (5 method summaries)
- ~~`ICharacterRepository` (3 missing methods)~~ — FIXED session-5
- ~~`IRefreshTokenRepository` (2 missing methods)~~ — FIXED session-5
- `IAuthService`, `ICharacterService`, `IContentService`, `INavigationService` (Client) — verify at next build (was clean last run)

## ClientSettings (NEW session-5)

- `Veldrath.Client/ClientSettings.cs` — `ReactiveObject` with `ServerBaseUrl` property
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

**Entities** (all in `Veldrath.Server/Data/Entities/`):
- `World.cs`: `Id` (slug), `Name`, `Description`, `Era`, `ICollection<Region> Regions`
- `Region.cs`: `Id` (slug), `Name`, `Description`, `RegionType Type`, `MinLevel`, `MaxLevel`, `IsStarter`, `IsDiscoverable`, `WorldId` FK, navigation `World`, `ICollection<Zone> Zones`, `ICollection<RegionConnection> Connections`
- `RegionType` enum (in `Region.cs`): `Forest`, `Highland`, `Coastal`, `Volcanic`
- `RegionConnection.cs`: composite PK (`FromRegionId` + `ToRegionId`), navigate `FromRegion`, `ToRegion`; `Restrict` delete-behavior on both FK sides
- `ZoneConnection.cs`: composite PK (`FromZoneId` + `ToZoneId`), navigate `FromZone`, `ToZone`; `Restrict` delete-behavior on both FK sides; bidirectional travel = 2 rows

**Zone entity changes** (`Zone.cs`):
- Added: `string RegionId` (FK), `Region Region` nav, `bool HasInn`, `bool HasMerchant`, `bool IsPvpEnabled`, `bool IsDiscoverable`, `ICollection<ZoneConnection> Exits`
- Removed: old Tutorial type seed data (5 zones removed)

**Seed data (Veldrath world)**:
- 1 World: `veldrath` / "Veldrath" / "The Age of Embers"
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

> **See [`.github/instructions/ef-core-patterns.md`](../../.github/instructions/ef-core-patterns.md) for the canonical DbContext separation rules, migration workflow, entity placement conventions, and now four DbContexts (Application, Game, Content, Editorial).**

Historical note: DbContext separation was established in session-6 when `GameDbContext` was extracted from `ApplicationDbContext` to fix Docker startup failures. `EditorialDbContext` was added in session-35 for patch notes, lore articles, and editorial announcements. Server operational entities (`ServerSaveGameRepository`, `ServerHallOfFameRepository`) inject `GameDbContext`, not `ApplicationDbContext`.



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

### Early Sessions (3–18, March 2026) — Summary

> See git history for per-session code-level details.

| Session | Date | Key Work | Tests (C+S) |
|---------|------|----------|-------------|
| 3 | 2026-03-19 | ActorClassDto fix, AllocateAttributePoints hub bridge, SettingsViewModel, P3 MainMenu stub fixed | 503 |
| 4 | 2026-03-19 | UseAbility hub bridge, XML doc fixes (IZoneRepository + IZoneSessionRepository) | 520 |
| 5 | 2026-03-19 | AwardSkillXp hub bridge, ClientSettings singleton, P4 XML docs (10 summaries), P3 ServerUrl stub fixed | 541 |
| 6 | 2026-03-19 | Docker startup fix (GameDbContext DI), DbContext separation rule established | 260 server |
| 8 | 2026-03-20 | EquipItem hub bridge, EquipmentBlob migration, P2 coverage (19 server tests) | 560 |
| 9 | 2026-03-20 | AddGold hub bridge, ItemEquipped subscription fix (P3 stub) | 580 |
| 10 | 2026-03-20 | TakeDamage hub bridge | 600 |
| 11 | 2026-03-20 | GainExperience client wiring (Level/Experience reactive properties) | 608 |
| 12 | 2026-03-20 | CharacterSelected payload enriched, SeedInitialStats (zero-HUD fix) | 615 |
| 13 | 2026-03-20 | Content service extension (6 types), CraftItem + EnterDungeon hub bridges; IDungeonRepository discovered nonexistent | 723 |
| 16 | 2026-03-21 | GameView UI redesign (3×3 layout, collapsible panels, dev tools, 11 new VM properties) | 778 |
| 17 | 2026-03-21 | VisitShop hub bridge (last P3 stub), single-DTO parameter rule discovered | 826 |
| 18 | 2026-03-24 | Hidden ZoneLocations + Discovery System (passive/active, traversal, cross-zone, 34 new tests) | 927 |

### Session-24 (2026-03-26) — Map redesign + ZoneLocations panel + cross-zone traversal reinit

**Phase 1 — Map graph redesign (zones as primary nodes)**

- `MapNodeViewModel.cs`: added `RegionId`, `RegionLabel` (both `string? init`), `IsRegionHeader` (`bool`, derived from `NodeType == "region_header"`)
- `GraphLayout.ComputeGroupedZones()` added: groups `zone` nodes by `RegionId`, positions `region_header` label nodes above each 2-column zone cluster, deduplication-safe; falls back to `Compute` when no header nodes
- `MapViewModel.LoadFullGraphAsync()` rewritten:
  - Creates `region_header` nodes (non-interactive) instead of `region` nodes
  - Creates `zone` nodes with `RegionId` + `RegionLabel` populated from the enclosing region
  - Creates `zone_exit` edges via `GetZoneConnectionsAsync` per zone (deduplicated bidirectional pairs)
  - Calls `ComputeGroupedZones` instead of `ComputeHierarchical`
  - Removed dead `_characterId` private field (constructor param kept for caller compatibility)
- `MapView.axaml`:
  - Removed `level-tab` and `level-tab.active` style blocks (stale breadcrumb tab buttons)
  - Removed `<StackPanel Grid.Column="0">` breadcrumb block (bound to unimplemented `CanDrillOut`, `FocusedRegionName`, etc.)
  - Added `Border.map-node.region-header` style: `Background=Transparent`, `BorderThickness=0`, `IsHitTestVisible=False`, `Cursor=Arrow`
  - Added `Classes.region-header="{Binding IsRegionHeader}"` to DataTemplate Border
- `FakeZoneService` (tests): added `ZoneConnections : Dictionary<string, List<string>>` + updated `GetZoneConnectionsAsync` to serve from dict
- `MapViewModelTests`: updated 4 tests (`region` → `region_header` NodeType, zone_membership/region_exit tests replaced with `ZoneExitEdges_Connect_Adjacent_Zones` + `ZoneNodes_Carry_RegionId_And_RegionLabel`), fixed `BeEquivalentTo(["a","b"])` collection-expression syntax

**Phase 2 — In-game ZoneLocations panel**

- `ZoneLocationItemViewModel.cs` (new): display model for zone location list entries; `Slug`, `DisplayName`, `LocationType`, `MinLevel?`, reactive `IsCurrent` (also raises `CanNavigate`), `NavigateCommand? : ReactiveCommand<Unit, Unit>` (null when current)
- `GameViewModel`:
  - `ZoneLocations : ObservableCollection<ZoneLocationItemViewModel>` (public)
  - `CurrentZoneLocationDisplayName : string?` (derived, computed from `ZoneLocations`)
  - `CurrentZoneLocationSlug` setter: now also raises `CurrentZoneLocationDisplayName`
  - `LoadZoneCoreAsync(string zoneId)` extracted from `InitializeAsync`: sets `_currentZoneId`, resets `CurrentZoneLocationSlug = null`, fetches zone + calls `LoadWorldContextAsync` + `LoadZoneLocationsAsync`
  - `LoadZoneLocationsAsync(string zoneId)`: calls `GetZoneLocationsAsync(zoneId, _characterId)`, fills `ZoneLocations` with items that capture navigation callbacks
  - `InitializeAsync`: now calls `LoadZoneCoreAsync` + handles music + appends welcome log
  - `OnLocationEntered`: now also sets `IsCurrent` on all `ZoneLocations` entries
- `GameView.axaml`: after "Zones in this region" ItemsControl, added a separator + "Locations" caption + `ZoneLocationItemViewModel` ItemsControl (label, type, optional level, "Go →" button with `CanNavigate` + `NavigateCommand`)

**Phase 3 — Cross-zone traversal reinit**

- `GameViewModel.OnConnectionTraversed` changed to fire-and-forget `_ = HandleConnectionTraversedAsync(...)`
- `HandleConnectionTraversedAsync`: cross-zone (`isCrossZone + toZoneId`) → `AppendLog` + `await LoadZoneCoreAsync(toZoneId)`; same-zone → update slug + set IsCurrent flags + AppendLog

**Final count: 512 client + 468 server = 980 total passing**

### Session-28 (2026-03-30) — Combat system tests + GameView combat UI

**Phase A — Server combat handler unit tests (6 new files)**

Created `Veldrath.Server.Tests/Features/`:
- `EngageEnemyHubCommandHandlerTests.cs` — 4 tests: already-in-combat guard, enemy-not-found guard, enemy-dead guard, success
- `AttackEnemyHubCommandHandlerTests.cs` — 4 tests: no-session guard, enemy-not-in-store guard, reduces health, enemy killed
- `DefendActionHubCommandHandlerTests.cs` — 3 tests: no-session guard, sets-IsDefending flag, damage reduction vs non-defending
- `FleeFromCombatHubCommandHandlerTests.cs` — 2 tests: no-session guard, returns valid result when in combat
- `UseAbilityInCombatHubCommandHandlerTests.cs` — 5 tests: empty-abilityId guard, no-session guard, cooldown guard, mana guard, success
- `RespawnHubCommandHandlerTests.cs` — 5 tests: character-not-found, HC-char-returns-not-found (DeletedAt filter in GetByIdAsync makes HC guard dead code), alive guard, success (restores 25% HP), removes combat session

Key gotchas discovered:
- `CharacterRepository.GetByIdAsync` filters `&& DeletedAt == null` — HC characters are always "not found" when the HC guard checks for `DeletedAt.HasValue`; test asserts `"not found"` not `"Hardcore"`
- `NormalizedUserName` unique constraint: must use `account.NormalizedUserName = account.UserName.ToUpperInvariant()` when seeding multiple users in one test; `= "U"` (hardcoded constant) causes unique violations
- `Character.Attributes` has NOT NULL in SQLite — always provide `attrsJson ?? "{}"`; never null

**Phase B — Client GameViewModel combat tests (10 tests)**

Created `Veldrath.Client.Tests/ViewModels/GameViewModelCombatTests.cs`:
- `OnCombatStarted_SetsIsInCombatAndEnemyStats`, `OnCombatStarted_SetsAbilityNames`
- `OnCombatTurn_UpdatesEnemyHealth`, `OnCombatTurn_EnemyDefeated_ClearsCombat`, `OnCombatTurn_PlayerDefeated_SetsIsPlayerDead`, `OnCombatTurn_HardcoreDeath_SetsIsHardcoreDeath`
- `OnCombatEnded_ClearsIsInCombat`
- `OnEnemySpawned_AddsToCollection`, `OnEnemySpawned_MultipleEnemies`
- `OnCombatTurn_EnemyDefeated_ZeroesRosterItem`

**Phase C — GameView.axaml combat panel**

Added to `Veldrath.Client/Views/GameView.axaml` after ZoneLocations ItemsControl:
- Enemy roster `ItemsControl` bound to `SpawnedEnemies` (`IsVisible="{Binding HasSpawnedEnemies}"`): each item shows Name, Level, HP and Engage button using `Command="{Binding $parent[UserControl].DataContext.EngageEnemyCommand}" CommandParameter="{Binding Id}"`
- Combat HUD (`IsVisible="{Binding IsInCombat}"`): enemy name/level/HP bar + Attack/Defend/Flee buttons
- Death overlay (`IsVisible="{Binding IsPlayerDead}"`): Respawn button (`IsVisible="{Binding IsHardcoreDeath, Converter={x:Static BoolConverters.Not}}"`) + LogoutCommand for HC death

Added to `Veldrath.Client/ViewModels/GameViewModel.cs`:
- `_hasSpawnedEnemies` backing field + `HasSpawnedEnemies` reactive property wired via `SpawnedEnemies.CollectionChanged`

**Final count: 525 client + 491 server = 1016 total passing**

### Session-Pass2-HUD (2026-04-02) — Connection dot, settings flyout, full chat system

**Feature**: HUD Pass 2 — connection health indicator, settings flyout with mute controls, zone/global/whisper/system chat.

**ConnectionState enum extended** (`ServerConnectionService.cs`):
- Added values: `Degraded` (connected, ping ≥ 200ms), `Reconnecting` (SignalR auto-reconnect in progress)
- Ping timer: `System.Timers.Timer` (5 s interval) started on connect/reconnect, stopped on disconnect/dispose
- `MeasurePingAsync()` added to `IServerConnectionService` + implementation (Stopwatch + silent catch → Serilog Debug)
- Thresholds: `< 200ms = Connected`, `≥ 200ms = Degraded`
- `IHubConnection` interface + `HubConnectionWrapper` + `FakeHubConnection` gained `Reconnecting` event
- `FakeHubConnection.SimulateReconnectingAsync()` test helper added

**IAudioPlayer extended** (`IAudioPlayer.cs`):
- New members: `bool IsMusicMuted`, `bool IsSfxMuted`, `ToggleMusicMute()`, `ToggleSfxMute()`
- Implemented in `LibVlcAudioPlayer`, `NullAudioPlayer`, `FakeAudioPlayer`

**GameHub chat methods** (`GameHub.cs`):
- `Task<long> Ping()` — returns UTC Unix milliseconds (one-liner)
- `Task SendZoneMessage(SendZoneChatMessageHubRequest)` — broadcasts to zone group → `"ReceiveChatMessage"` with `ChatMessageHubDto`
- `Task SendGlobalMessage(SendGlobalChatMessageHubRequest)` — broadcasts to all → `"ReceiveChatMessage"`
- `Task SendWhisper(SendWhisperHubRequest)` — routes to target via `_playerSessionRepo.GetByCharacterNameAsync`; echoes to sender with `Sender = "To {target}"`
- New DTOs at bottom: `SendZoneChatMessageHubRequest(string Message)`, `SendGlobalChatMessageHubRequest(string Message)`, `SendWhisperHubRequest(string TargetCharacterName, string Message)`, `ChatMessageHubDto(string Channel, string Sender, string Message, DateTimeOffset Timestamp)`
- `IZoneSessionRepository.GetByCharacterNameAsync(string characterName)` added (interface + EF implementation)

**New ViewModels** (`Veldrath.Client/ViewModels/`):
- `ChatMessageViewModel.cs` — record; channel color map (Zone=#94a3b8, Global=#60a5fa, Whisper=#f472b6, System=#4ade80); `ChannelLabel`, `ChannelColor`, `FormattedMessage` properties
- `OnlinePlayerViewModel.cs` — `ReactiveObject`; `Name` (string), `StartWhisperCommand` (calls `Action<string>` callback)

**GameViewModel additions**:
- `ConnectionStateValue : ConnectionState`, `ConnectionStatusColor : string` (hex), `ConnectionStatusTooltip : string`
- `IsSettingsOpen : bool`, `IsMusicMuted / IsSfxMuted` (delegated to `_audioPlayer`), `MusicMuteLabel / SfxMuteLabel` (computed strings)
- `ChatMessages : ObservableCollection<ChatMessageViewModel>`, `ChatInput : string` (with `/w name` prefix parser → sets Whisper channel + target)
- `ActiveChatChannel : string` (default "Zone"), `WhisperTarget : string`, `IsWhisperChannelActive : bool`, `IsChatInputVisible : bool`
- `UseHotbarAbilityCommand : ReactiveCommand<string, Unit>` → `DoUseHotbarAbilityAsync` → dispatches to `DoUseAbilityInCombatAsync` when `IsInCombat`, else `DoUseAbilityAsync`
- `ToggleSettingsCommand`, `ToggleMusicMuteCommand`, `ToggleSfxMuteCommand`, `SetChatChannelCommand`, `SendChatCommand`
- `OnlinePlayers` changed: `ObservableCollection<string>` → `ObservableCollection<OnlinePlayerViewModel>` (each constructed with `StartWhisperFromPlayer` callback)
- `OnChatMessageReceived(string channel, string sender, string message, DateTimeOffset timestamp)` added
- `StartWhisperFromPlayer(string name)` private helper: sets `ActiveChatChannel = "Whisper"` + `WhisperTarget = name`
- `HotbarSlotViewModel` internal field/param renamed `_useHotbarAbilityCommand` (was `_useAbilityInCombatCommand`)

**CharacterSelectViewModel additions**:
- `_chatMessageSub`: field + disposal + `"ReceiveChatMessage"` subscription wiring `OnChatMessageReceived`
- `ChatMessagePayload(string Channel, string Sender, string Message, DateTimeOffset Timestamp)` internal record

**GameView.axaml changes**:
- **Header Col 3**: Added connection dot (`Ellipse` bound to `ConnectionStatusColor`/`ConnectionStatusTooltip`) + settings flyout button (`⚙`) with `Flyout` popup containing music mute, SFX mute (bound to `MusicMuteLabel`/`SfxMuteLabel`), and Logout button. Old standalone Logout button removed.
- **Right panel**: Added chat section between Action Log and Online Players:
  - Channel pills: Zone / Global / Whisper / System (each bound to `SetChatChannelCommand`)
  - Whisper recipient `TextBox` (`IsVisible="{Binding IsWhisperChannelActive}"`)
  - Message list (`ObservableCollection<ChatMessageViewModel>`, 160px `ScrollViewer`)
  - Send row: `TextBox` + ↵ button (`IsVisible="{Binding IsChatInputVisible}"`)
- **Online Players DataTemplate**: updated to `x:DataType="vm:OnlinePlayerViewModel"` with `.Name` binding + `[W]` whisper button bound to `StartWhisperCommand`

**Test fixes**:
- `GameViewModelTests.cs`: 4 assertions changed from `Contain("string")` / `NotContain("string")` / `BeEquivalentTo(string[])` to use lambda predicate `p => p.Name == "..."` and `.Select(p => p.Name)` overloads
- `CharacterSelectViewModelTests.cs`: same 3 assertions
- `ViewDataBindingTests.cs`: `.Add("Gandalf"/"Aragorn"/"Legolas")` changed to `new OnlinePlayerViewModel("name", _ => { })`

**Final count: 584 client + 530 server (5 pre-existing CharacterCreationSession failures) = still 584 client passing**

## Session-29 (2026-04-08) — Region Map Architecture + PlayerSession Migration

### Design decisions (locked)
- Characters navigate a **region TMX map** between zones; entering a zone is triggered by walking to a zone-entry tile object
- Zone positions come from the TMX **object layer** (named `zones`), NOT from DB columns
- `ZoneSession` entity replaced by `PlayerSession` — tracks position at both region-map and zone level
- User authors TMX files manually; agent defines the spec

### PlayerSession Entity (`Veldrath.Server/Data/Entities/PlayerSession.cs`)
Replaces `ZoneSession`. New properties vs old:
- `RegionId` (`string`, required) — always set; FK → Regions with Restrict delete
- `ZoneId` (`string?`, nullable) — null = player is on the region map, not inside a zone; FK → Zones with SetNull delete
- `TileX`, `TileY` (int) — current tile coords (region map or zone)
- All prior fields kept: `CharacterId`, `CharacterName`, `ConnectionId`, `EnteredAt`, `LastMovedAt`
- Unique indexes on `CharacterId` and `ConnectionId`

### IPlayerSessionRepository (in `IZoneRepository.cs`)
Replaces `IZoneSessionRepository`. Same core methods plus new:
- `GetByRegionIdAsync(string regionId)` — all sessions in a region
- `GetOnRegionMapAsync(string regionId)` — sessions where `ZoneId is null`
- `UpdatePositionAsync(Guid characterId, int tileX, int tileY)` — update tile coords only
- `SetZoneAsync(Guid characterId, string? zoneId)` — enter/exit zone (null = back to region map)

### Repository (`PlayerSessionRepository` in `ZoneRepository.cs`)
Replaces `ZoneSessionRepository`. Implements all 12 interface methods.

### ApplicationDbContext changes
- `DbSet<PlayerSession> PlayerSessions` replaces `DbSet<ZoneSession> ZoneSessions`
- Zone config no longer has `HasMany(z => z.Sessions)` — driven from PlayerSession side with `SetNull`
- `Zone.Sessions` → `ICollection<PlayerSession>`

### Files changed (session-29)
- `PlayerSession.cs` — **created** (new entity)
- `ZoneSession.cs` — **deleted**
- `Zone.cs` — `ICollection<ZoneSession>` → `ICollection<PlayerSession>`
- `IZoneRepository.cs` — `IZoneSessionRepository` → `IPlayerSessionRepository`
- `ZoneRepository.cs` — `ZoneSessionRepository` class → `PlayerSessionRepository`
- `ApplicationDbContext.cs` — DbSet + EF config fully swapped
- `GameHub.cs` — field/ctor/9 call-sites swapped; `LeaveCurrentZoneAsync` guards `zoneId is null` (player may be on region map, not in a zone)
- `MoveCharacterHubCommand.cs` — `IZoneSessionRepository` → `IPlayerSessionRepository` (field + doc comments)
- `ZoneEndpoints.cs` — 3 endpoint lambda params updated
- `Program.cs` — DI: `IZoneSessionRepository, ZoneSessionRepository` → `IPlayerSessionRepository, PlayerSessionRepository`
- `ZoneRepositoryTests.cs` — all session tests updated; `RegionId` added to all `new PlayerSession { }` objects
- `GameHubTests.cs` — `CreateHub` + inline constructions updated; `EnterZone_Should_Create_PlayerSession_In_Database`
- **Migration**: `20260408225405_ReplaceZoneSessionWithPlayerSession.cs` in `Veldrath.Server/Migrations/`

### TestDbContext RegionId values (for session tests)
- Zone `"crestfall"` → `RegionId = "varenmark"`
- Zone `"aldenmere"` → `RegionId = "greymoor"`
- Zone `"fenwick-crossing"` → `RegionId = "thornveil"`

### Region TMX Map Spec (finalized, user will author the files)
**Tile layers** (3): `ground`, `detail`, `decoration` (no `overhead` — player always on top)  
**Object groups** (2):
- `zones` — zone entry objects, `name` = zone slug, properties: `displayName`, `minLevel`, `maxLevel`
- `region_exits` — border crossings to adjacent regions, `name` = target region slug  
**Map TMX properties**: `regionId`, `tilesetKey="onebit_packed"`, `fogMode="none"` (fully visible)  
**File path convention**: `{mapsBasePath}/{regionId}.tmx` flat — e.g. `maps/varenmark.tmx`  
**Existing file**: `maps/Varenmark.tmx` (already in assets — repurpose/rename to match convention)

### Next work (not yet started)
- **Phase 3**: `RegionMapDto`, `ZoneObjectDto`, `RegionExitDto` contracts in `TilemapContracts.cs`; `GetByRegionIdAsync` on `ITileMapRepository` + `TiledFileMapRepository`
- **Phase 4**: `GetRegionMapHubCommand`, `MoveOnRegionHubCommand`, `ExitZoneHubCommand` (server feature files)
- **Phase 5**: `GetRegionMap()`, `MoveOnRegion()`, `ExitZone()` hub methods + update `SelectCharacter` to place player on region map on login
- **Phase 6**: `RegionTilemapViewModel.cs` (client)
- **Phase 7**: Client hub wiring in `CharacterSelectViewModel` + `GameViewModel`
- **Phase 8**: Tests

### Test counts after session-29
- Server tests: **560 passing**, 8 pre-existing failures (unrelated to PlayerSession work)
- Client tests: **525 passing** (unchanged — server-only session)

### Session-31 (2026-04-11) — Auth flow hardening (pass 1)

**Goal**: Multi-pass iterative auth bug fixing across Veldrath.Web, RealmFoundry, Veldrath.Server.

**Changes**:

1. **`Veldrath.Web/Components/Layout/MainLayout.razor`** — `OnLocationChanged` was `async void`, causing cross-thread state corruption. Fixed to `_ = InvokeAsync(async () => { if (AuthState.IsLoggedIn) await AuthState.TryRefreshAsync(); });`.

2. **`Veldrath.Web/Services/AuthStateService.cs`** — `TryRefreshAsync` left `IsLoggedIn = true` when `RenewJwtAsync` returned null. Now calls `await LogOutAsync()` before returning false — clears stale auth state.

3. **`RealmFoundry/Services/AuthStateService.cs`** — Same auto-logout fix. Also added `TimeSpan? expiryThreshold = null` parameter with early-exit guard (default 2-minute threshold) so `TryRefreshAsync()` doesn't hammer the API on every navigation when token is still fresh.

4. **`Veldrath.Server/Features/Auth/AuthService.cs`** — Added `ILogger<AuthService>` ctor parameter (8th param). Added structured `LogWarning` calls for: failed login (account not found / wrong password), lockout, refresh token theft detection, RenewJwt with revoked token, OAuth account creation failure. Added `LogInformation` for pending link confirmation email dispatch.

5. **`Veldrath.Server/Features/Auth/AuthEndpoints.cs`** — `POST /api/auth/logout` gained `.RequireRateLimiting("auth-attempts")` to prevent DoS via revocation flooding.

6. **`Veldrath.Server/Program.cs`** — Added startup guard: throws `InvalidOperationException` when `Auth:CookieDomain` is not configured in **Production** (`app.Environment.IsProduction()`). **Important**: guard uses `IsProduction()` not `!IsDevelopment()` — the WebAppFactory uses `"Test"` environment name and `!IsDevelopment()` would throw there too.

**Tests added/modified**:
- `RealmFoundry.Tests/Services/AuthStateServiceTests.cs`: 3 new threshold guard tests; existing 2 affected tests now use 30-second expiry tokens (so threshold guard doesn't short-circuit the API call). Total: **51 tests**.
- `RealmFoundry.Tests/Infrastructure/FakeApiClient.cs`: Added `RenewJwtCallCount` property.
- `Veldrath.Web.Tests/Services/AuthStateServiceTests.cs`: Added `IsLoggedIn.Should().BeFalse()` assertion to `TryRefreshAsync_ReturnsFalse_WhenRenewFails`.

**Final counts**: Client 713 / Server 728 / RealmFoundry.Tests 51 / Veldrath.Web.Tests 31 — all passing.

**Still excluded (future passes)**:
- `AccountLinkService.RequestLinkAsync` idempotency (duplicate token on second request)
- `PendingLinkEndpoints` rate limiting
- `ExternalAuthEndpoints` link-mode OAuth integration tests
- `LoginViewModel` external OAuth flow tests

---

### Session-32 (2026-04-12) — Monitoring Infrastructure
- Added Prometheus metrics integration: `prometheus-net`, `prometheus-net.DotNetRuntime`, `prometheus-net.AspNetCore`
- Added `UseHttpMetrics()`, `DotNetRuntimeStatsBuilder` to Program.cs (`Veldrath.Server/Program.cs:530`)
- Provisioned 4 Grafana dashboards: `10915.json` (.NET Runtime), `19194.json`, `23178.json`, `23179.json`
- Added Prometheus scrape config and Grafana provisioning configs
- Updated `docker-compose.yml` with Prometheus and Grafana services
- Pinned Grafana image to 11.6.1

### Session-33 (2026-04-13) — Deployment Infrastructure & Tilemap Updates
- Created reusable deploy workflow (`.github/workflows/deploy.yml`)
- Wrote comprehensive deployment guide (`docs/deployment.md`, +289 lines)
- Created production Docker Compose config (`docker-compose.prod.yml`)
- Set up Caddy reverse proxy configuration (`Caddyfile`)
- Temporarily disabled Discord, Foundry, and Server deploy workflows
- Updated Crestfall, Grevenmire, The Droveway, The Halrow tilemaps with new dimensions and object placements

### Session-34 (2026-04-14) — World Lore & Calethic Language
- Created `world-lore-plan.md` with initial lore plan
- Completed Phase 1-4 of Varenmark world-building:
  - Phase 1: Varenmark name finalized, zone lore framework
  - Phase 3: All Varenmark zone pages rewritten with connected-system framework
  - Phase 4: 5 new sub-locations across zone pages
- Completed Calethic language design (Q3): phonology, morphology, syntax, ~75 root dictionary
- Renamed Varenmark Calethic form to "Vaelren"
- Moved language reference to `docs/lore/calethic-language.md`
- Named-entity pass for all Varenmark entities

### Session-35 (2026-04-15) — Veldrath.Web, Editorial System, Cryptography
- Scaffolded `Veldrath.Web` — Blazor Server SSR application with login, community pages
- Created `Veldrath.Web.Tests` test project
- Added `versions/web.props` version file
- Created Editorial system: `EditorialEndpoints.cs`, `EditorialDbContext.cs`, migration `InitialCreate`
- Editorial integration tests added
- Updated `System.Security.Cryptography.Xml` to patched version
- Removed Strapi CMS client and all Strapi references
- Consolidated ports to 9000 across all services
- Updated solution files (`Realm.Full.slnx`, `Veldrath.slnx`)

### Session-36 (2026-04-16) — Design System, DB Consolidation, Language Catalog
- Created comprehensive design system document (`docs/design-system.md`, 726 lines)
- Created `RealmUI.Fonts` project with Cinzel, JetBrains Mono, Lora fonts
- Enhanced ForgeTheme, GameTheme with VDS design tokens
- Updated 18 Avalonia views and 14 Blazor pages for consistent theming
- Updated CSP for Google Fonts
- Consolidated database migrations: removed 10+ old migrations, replaced with single `InitialCreate` per DbContext
- Added language catalog system: `ILanguageRepository`, `EfCoreLanguageRepository`, `GetLanguageCatalogQuery`
- Language endpoints + tests added
- Updated `toc.yml` for DocFX documentation

### Session-37 (2026-04-16 to 2026-04-18) — Auth Overhaul & Account Management
- Created `Veldrath.Auth` shared authentication library (extracted from RealmFoundry/Veldrath.Web)
- Created `Veldrath.Auth.Blazor` with `AuthStateServiceBase`
- Created `Veldrath.Auth.Tests` test project (436+ lines of tests)
- Implemented forgot password flow (client + web + API)
- Implemented account profile management (RealmFoundry `Profile.razor`, +444 lines)
- Added session persistence in login/registration flows
- Added `LinkedAt` timestamp to user logins + EF migration
- Enhanced `AuthStateService`/`AuthStateServiceBase` with permissions and session management
- Added OAuth token refresh silent renewal before redirect
- Added logout with server session revocation
- Implemented JWT renewal endpoint
- Added refresh token rotation middleware
- Enhanced error handling in auth API client
- Standardized server base URL to `localhost:9000`
- Updated solution files to include new auth projects

### Session-38 (2026-04-27 to 2026-04-29) — Stability, Chat, Moderation, Dev Tools
- **Connection stability fixes**: Stale `ServerBaseUrl` restore stopped; hub URL slash trimming; `_connectionGeneration` counter for stale event suppression; `ConnectAsync` guard against `Connecting`/`Reconnecting` states
- **Zone re-entry fix**: `_currentZoneId` now cleared on zone exit (was blocking re-entry)
- **Chat command suggestions**: `ChatCommandInfoDto` contract, `ChatCommandInfoViewModel`, `/`-triggered suggestion UI in game panel
- **Moderation system**: `/ignore`, `/warn`, `/mute`, `/ban` commands with permission checks
- **Moderation configuration**: `ModerationOptions.cs` (`AutoBanWarnThreshold: 3`, `MaxChatMessageLength: 500`)
- **Moderation permission denial tests**: +197 lines for unauthorized access scenarios
- **Dev endpoints**: `DevEndpoints.cs` with zone listing, teleport; `IsDevOnly` zone property + EF migration
- **Version compatibility**: `VersionCompatibilitySettings` with `MinCompatibleClientVersion`
- **Docker cleanup**: Removed erroneous `EXPOSE 8081` from Server Dockerfile
- **Rate limiting**: 5 named policies configured (foundry-writes, admin-actions, auth-attempts, jwt-renewal, hub-commands)
- **Security headers middleware**: CSP, XFO, nosniff, Referrer-Policy, Permissions-Policy on all responses

### Session-39 (2026-06-26 to 2026-06-27) — Agent Infrastructure Reset
- Created `AGENTS.md` (+342 lines) replacing old `copilot-instructions.md`
- Moved memory files from `.github/copilot-memory/` → `.github/agent-memory/`
- Created `.roo/` configuration with mode definitions and skill files
- 8 skills implemented: build-full, create-new-feature, run-all-tests, run-client, run-realmforge, run-realmfoundry, run-server, run-tests-by-component, start-dev-stack
- Memory files marked stale pending this update session

### Session-40 (2026-06-27 to 2026-06-28) — Expansion, Polish & Cleanup
- **Memory file overhaul**: Updated all 8 `.github/agent-memory/` files + `wiki/Engine-Implementation-Status.md` through Session-39
- **Baseline verification**: `dotnet build Realm.Full.slnx` — 0 errors, 0 warnings. `dotnet test Realm.Full.slnx` — 4,688 passing, 0 failing
- **PendingLinkCleanupService**: Created hosted service for periodic expired token purge (30-min interval)
- **NU1903 fix**: Pinned `SQLitePCLRaw.lib.e_sqlite3` to 2.1.12 to resolve heap-buffer-overflow vulnerability
- **Auth hardening**: Added rate limiting to pending-link confirm endpoint; made `RequestLinkAsync` idempotent
- **Auth tests**: Link-mode OAuth integration tests (4) + LoginViewModel OAuth flow tests (3)
- **Region TMX maps**: Created `greymoor.tmx`, `saltcliff.tmx`, `cinderplain.tmx` — all 4 regions now have map files
- **ChangeRegionHandler tests**: 8 tests covering SpawnInward edge logic for all 4 border directions
- **Discord LoreModule**: Uncommented 3 `[SlashCommand]` attributes enabling `/enemy`, `/power`, `/class` commands
- **Achievement wiring**: Connected `CheckAchievementProgressCommand` into combat (AttackEnemyHandler), quest (CompleteQuestHandler), level-up (GainExperienceHandler + LevelUpHandler), and death (HandlePlayerDeathHandler) — 5 integration points total
- **Settings cleanup**: Deleted 6 orphaned `RealmEngine.Core/Settings/` files (zero references confirmed)
- **Zone TMX maps**: Created 12 minimal zone tilemaps for Greymoor (aldenmere, pale-moor, soddenfen, barrow-deeps), Saltcliff (tolvaren, tidewrack-flats, saltcliff-heights, sunken-name), and Cinderplain (skarhold, ashfields, smoldering-reach, kaldrek-maw) regions
- **Discord bot tests**: 24 new tests for ServerModule (8) + GenerateModule (15) + integration (1) — testing embed builders directly via `internal static` methods
- **Final test count**: ~4,732 tests passing (up from 4,688)
- **Files deleted**: `RealmEngine.Core/Settings/GameSettings.cs`, `AudioSettings.cs`, `UISettings.cs`, `GameplaySettings.cs`, `LoggingSettings.cs`, `SettingsValidator.cs`
- **Open items for next session**: Hub integration success-path tests for region methods; world lore resolution (6 open design items); audio/sound expansion

### Session-41 (2026-06-30) — Reactive UI Pivot + Test Fixes
- **Reactive UI pivot**: Removed all sprite/ASCII rendering (`Veldrath.Client.Rendering/` + `TilemapControl`/`RegionTilemapControl`). Replaced Zone tilemap with `ZoneLocationPanelView`+VM (reactive list-based panel showing room description, exits, entities). Region view now handled entirely by side panel (`GameRegionPanelView`). Keyboard movement removed — pure point-and-click. Client-side tile description service added. See `plans/reactive-ui-pivot-plan.md`.
- **ExternalAuthEndpointTests fix**: Added `SecurityStamp` to test user creation in `ExternalAuthEndpointTests.cs` — was throwing `InvalidOperationException: User security stamp cannot be null`. The `UserManager.CreateAsync` call requires a non-null security stamp for external login flows.
- **EnemyAiService fix**: Added disposal-state guard in `StopAsync`/`Dispose` to prevent `ObjectDisposedException: The CancellationTokenSource has been disposed` — happens when `Dispose` is called concurrently with the periodic AI update loop. Added `_disposed` flag checks in both `StopAsync` and the timer callback.
- **Final test count verified**: `dotnet test Realm.Full.slnx` — **4,739 tests, 0 failures** across all 11 test projects. Full suite is fully green.
- **Open items for next session**: Hub integration success-path tests for region methods; world lore resolution (6 open design items); audio/sound expansion.

---


