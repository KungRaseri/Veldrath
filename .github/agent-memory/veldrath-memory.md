# Veldrath Server + Web Client — Memory Notes

> **Status**: Updated through Session-68 (2026-10-25). The Avalonia desktop client is gone —
> Veldrath is web-first, and the SvelteKit app in `web/` is the only client. Entries below that
> mention `Veldrath.Client`, `GameClient.Core`, `Razor`/`MudBlazor`, or `/api/foundry-bridge/*` are
> historical session logs, not current guidance.

---

## CRITICAL — CSS/Styling Rule

**Before writing ANY CSS**, read [`docs/design-system.md`](../../docs/design-system.md) and load the
`web-ui` skill. The web app is SvelteKit + Tailwind + Skeleton v5 and must use the Veldrath design
tokens in `web/client/src/routes/theme.css` and `brand.css` — no raw hex values, no ad-hoc component
stylesheets. The live token definitions are `theme.css` `:root` plus the `[data-theme='veldrath']`
Skeleton ramp block; the design document explains them and no longer carries any desktop-toolkit
material (Session-102 deleted it). `web/client/src/lib/parity/design-tokens.test.ts` fails when the
CSS, [`docs/design/tokens.json`](../../docs/design/tokens.json) and the document disagree — add a
token in all three places.

**Applies to**: [`web/client`](../../web/client) — the only client and the only UI surface (the
Avalonia `RealmForge` editor was deleted in Milestone B, so the rules that referred to it no longer
apply anywhere).

---

## Web gates in a worktree (standing recipe, 2026-11-20)

Cards run in `<repo>/.worktrees/<task-id>`, and a fresh worktree has **no** `web/node_modules`. Three
facts make the web gates runnable there; each one has cost a session to rediscover.

- **Symlink the main checkout's installs instead of installing.** `npx vitest` in a bare worktree
  either fails (`vitest: not found`) or npx installs a stray partial tree:

  ```bash
  ln -s <repo>/web/node_modules        <worktree>/web/node_modules
  ln -s <repo>/web/client/node_modules <worktree>/web/client/node_modules
  ```

  Both links show up as **untracked** in `git status` (`.gitignore`'s `node_modules/` matches real
  directories, not symlinks), so never `git add -A` on a web card.

- **Invoke vitest through the workspace script from `web/`** — that is what puts the cwd at
  `web/client`, matching CI:

  ```bash
  cd web && npm run test:unit --workspace client -- --run [path under client/]
  ```

- **Every parity suite resolves through `repoRoot()` — including `field-kinds.test.ts`** (fixed
  2026-11-20, card `t_497bf232`). That file used to read
  `Veldrath.Contracts/Content/ContentSchema.cs` through an inline `resolve(process.cwd(), '..', '..')`;
  from `web/`, `../..` left the repo (`<repo>/.worktrees/Veldrath.Contracts/…`) and its **2** tests
  died with `ENOENT`. It now calls `readRepoFile(...)`
  ([`web/client/src/lib/testing/source.ts`](../../web/client/src/lib/testing/source.ts)), which walks
  up until it finds `Veldrath.Server` + `web/client` and tolerates either cwd. A suite that still
  calls `process.cwd()` is now itself a test failure — see the next bullet.

- **A moved repo file is caught by `source-paths.test.ts`, not by an ENOENT.** The parity suites read
  C#/TS sources off disk, so a path in a suite is a dependency on the repository layout; when it moves
  the suite throws at module scope and vitest reports `Tests  no tests` — the guard has vanished, not
  failed. `web/client/src/lib/parity/source-paths.test.ts` (added 2026-11-20, card `t_497bf232`;
  extended by `t_fa258afd`) scans its siblings **and the suites named in `EXTERNAL_SUITES`** for
  `readRepoFile(…)` / `walkFiles(…)` / `join(repoRoot(), '…')` and asserts every path exists,
  reporting **all** missing ones in one assertion as `file "X" — readRepoFile at <suite>:<line>`.
  The reader itself is `web/client/src/lib/testing/source-paths.ts`: it resolves a quoted literal, a
  template literal with no `${…}`, or a `const|let|var` holding one **whatever the casing**, and
  accepts a `walkFiles('…')` entry (`for (const file of …)`, `.map((file) => …)`) because the
  directory literal is what gets checked. Any other argument is unresolved — a failure — so a suite
  cannot hide a path in an expression the reader does not follow. It also reads the polarity of
  `join(repoRoot())`: `expect(existsSync(…)).toBe(false)` is checked as *must stay gone*
  (`content-schema.test.ts` uses that to keep the deleted duplicate type-key map from coming back).
  `src/lib/testing/source-paths.test.ts` pins those rules on fixtures, so a future edit that
  re-breaks one fails a test instead of leaving the guard quietly green.
  From #147 until `t_fa258afd`, `development` was red on exactly that: the guard read
  content-schema's `.toBe(false)` as a missing path. The fix is the polarity, never deleting the
  assertion, and never allow-listing the path.

- **A second source reader lives outside the parity folder.** `web/client/src/lib/game/payload-contract.test.ts`
  parses `Veldrath.Server/Hubs/GameHub.cs` against `game/types.ts`; it now reads through
  `readRepoFile()` and is listed in `EXTERNAL_SUITES` with its reason, so a hub move fails the guard
  naming that suite. Adding another such suite means adding an entry with a reason — not widening a
  directory glob, which would drag in suites that never read repo source.

- **Export the `ci-web.yml` env** ([`.github/workflows/ci-web.yml`](../../.github/workflows/ci-web.yml))
  or SvelteKit aborts with `env_invalid`: at minimum `PUBLIC_API_URL` and `PUBLIC_HUB_URL`; `ORIGIN`,
  `BETTER_AUTH_SECRET`, `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `DATABASE_URL` complete the set.

Under that invocation the parity guard and its reader are **green — `src/lib/parity` +
`src/lib/testing` = 10 files / 56 tests** (9 files / 45 tests before card `t_fa258afd`; the 11 new
ones pin the reader's rules on fixtures). A missing repo file now fails an *assertion* in
`source-paths.test.ts` naming the path and the suite, so read that message before suspecting a parity
mismatch. Measured at `d0050f96`; the whole client unit suite was 106 files / 1556 tests there.

**Reproduce a bug claim against current `origin/development` first.** A worktree branch can lag it by
several merges: the `check-markdown-links.mjs` failure carded as t_3407c30b passed at the stale base
(61 files checked) and only reproduced after rebasing, because the file move that broke the link had
not landed yet. `git merge --ff-only origin/development` before concluding a defect is fixed or absent.

---

## OAuth Provider-Link Confirmation Flow (session-30, 2026-04-10) — DELETED in W12

> ⚠️ **This whole flow is gone (2026-10-25).** `PendingLinkToken`, `PendingLinkEndpoints`,
> `AccountLinkService`, `AuthService`, `ExternalAuthEndpoints`, and the `auth-attempts` rate-limit
> policy were deleted with the Identity auth surface; the `PendingLinkTokens` table was dropped by
> the `RemoveDesktopAuthTables` migration. Better Auth owns OAuth and account linking. Kept only as
> history for "why the server stopped auto-linking".

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
| `GetNpcDialogue` | `GetNpcDialogueHubCommand` | 2026-11-20 (dialogue depth) |
| `ChooseNpcOption` | `ChooseNpcOptionHubCommand` | 2026-11-20 (dialogue depth) |

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

> **Current (2026-11-04, after `Veldrath.Assets` was deleted in Session-97):** **5 .NET test
> projects, 4,011 tests** — `RealmEngine.Core` 2,040, `RealmEngine.Shared` 795, `RealmEngine.Data`
> 301, `Veldrath.Server` 846, `Veldrath.Discord` 29, which is exactly the 4,011 that
> `dotnet test Realm.Full.slnx --no-build` reported passing. There is no client or tooling test
> project any more — the web app is covered by Vitest + Playwright from `web/`. The table below is the
> historical per-session log; its "Client Tests" column refers to the deleted Avalonia client.

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

## World / Region / Zone Schema (session-14 — 2026-03-21; seed facts re-derived 2026-11-20)

Full World → Region → Zone hierarchy implemented.

**Entities** (all in `Veldrath.Server/Data/Entities/`):
- `World.cs`: `Id` (slug), `Name`, `Description`, `Era`, `ICollection<Region> Regions`
- `Region.cs`: `Id` (slug), `Name`, `Description`, `RegionType Type`, `MinLevel`, `MaxLevel`, `IsStarter`, `IsDiscoverable`, `WorldId` FK, navigation `World`, `ICollection<Zone> Zones`, `ICollection<RegionConnection> Connections`
- `RegionType` enum (in `Region.cs`): `Forest`, `Highland`, `Coastal`, `Volcanic`, `Countryside`
- `RegionConnection.cs`: composite PK (`FromRegionId` + `ToRegionId`), navigate `FromRegion`, `ToRegion`; `Restrict` delete-behavior on both FK sides
- **`ZoneConnection` and `Zone.Exits` do not exist.** Travel is `RegionConnection` (region ↔ region), the **region** map graph, and the **zone** map graph — each of the 19 zone `Map`s carries an `EntryPoint` node and one `Location` node per location (`RefId` = slug). Only **10 of the 19** also carry a `RegionExit` node; the other **nine** (aldenmere, ashfields, pale-moor, saltcliff-heights, skarhold, smoldering-reach, soddenfen, tidewrack-flats, tolvaren) have no exit node at all and are left through `LeaveZone` → `LeaveCurrentZoneAsync` (`GameHub.cs:915`, `:3178`), which ends the session's hold on the zone without moving the character to a node. Four Varenmark maps (crestfall, ashlen-wood, grevenmire, the-droveway) carry **9 `MapNodeType.ZoneExit` nodes** that jump straight to another zone's map — the only zone→zone adjacency authored at zone level. `Walk`/`ExitToRegion`/`ExitToZone` edges are executed by `TravelToNodeHubCommand` (any node with a `TargetMapId` is a transition, `:280-288`); `EnterZoneLocationHubCommand` resolves a slug to its `Location` node and delegates to it. A `ZoneLocationConnection` table exists but is seeded for Crestfall only (11 rows, four slugs) and read by no handler or repository — do not cite it as the in-zone mover.

**Zone entity** (`Zone.cs`): `Id` (slug), `Name`, `Description`, `ZoneType Type`, `MinLevel`, `MaxPlayers`, `IsStarter`, `HasInn`, `HasMerchant`, `IsPvpEnabled`, `IsDiscoverable`, `IsDevOnly`, `RescueFundTotal`, `RegionId` FK + `Region` nav, `ICollection<PlayerSession> Sessions`

**Seed data (Veldrath world)**, re-derived from `ApplicationDataSeeder.cs` (text lives in `WorldCanonText.cs`, which the seeder and `WorldCanonSweep` both read):
- 1 World: `veldrath` / "Veldrath" / era "The Ashen Age (the Age of Reclaiming)"
- 4 Regions: `varenmark` (Countryside, 0–6, starter), `greymoor` (Highland, 5–14), `saltcliff` (Coastal, 10–20), `cinderplain` (Volcanic, 18–30)
- 6 RegionConnections: varenmark↔greymoor, greymoor↔saltcliff, greymoor↔cinderplain (bidirectional)
- 19 Zones, of which `playground` is `IsDevOnly = true` and never returned by the public APIs (`ZoneRepository` filters on it)
- 5 `ZoneType.Dungeon` zones: `the-halrow`, `drowning-pits`, `barrow-deeps`, `sunken-name`, `kaldrek-maw` (display name "The Maw" — slugs are opaque keys and do not carry canon)
- **Starter zone**: `crestfall` (Crestfall, Varenmark)
- **Canon sweep**: on the Postgres startup path `WorldCanonSweep` rewrites a seeded value only when it still carries a retired token or *exactly* equals an entry in `WorldCanonText.KnownStaleValues` (that set holds the pre-rewrite `World.Description`, which names nothing retired). A future reword with no retired token must have its old string recorded there; until then the sweep reports the field in `DriftedFields` and leaves it alone. Drift is reported for `Worlds`/`Regions`/`Zones` only — `Maps`/`MapNodes`/`ZoneLocations` are also written by the Foundry editor, so divergence there is authored content.

**Character default zone**: `Character.StartingZoneId = "crestfall"` (`Character.cs:55`); first location is `crestfall-square` (`CharacterCreationSessionEndpoints.cs:37`)

**Repositories**: `IRegionRepository` + `RegionRepository`, `IWorldRepository` + `WorldRepository` added; `IZoneRepository.GetByRegionIdAsync` added  
**DI**: `IRegionRepository` + `IWorldRepository` registered in `Program.cs`  
**Contracts**: `RegionDto`, `WorldDto` added to `ZoneContracts.cs`; `ZoneDto` extended with optional `RegionId?`, `HasInn`, `HasMerchant`  
**Endpoints**: `GET /api/regions`, `GET /api/regions/{id}`, `GET /api/regions/{id}/connections`, `GET /api/worlds`, `GET /api/worlds/{id}`, `GET /api/zones/by-region/{regionId}`  
**Migration**: `AddWorldRegionZoneConnections` in `Migrations/Application/`

**SQLite FK tests**: FK constraints ARE enforced in `TestDbContextFactory`. Tests that add custom zones without a RegionId will fail. Use seeded zone IDs (`"crestfall"`, `"aldenmere"`, etc.) in ZoneSession tests instead of `MakeZone`.

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
- **Reactive UI pivot**: Removed all sprite/ASCII rendering (`Veldrath.Client.Rendering/` + `TilemapControl`/`RegionTilemapControl`). Replaced Zone tilemap with `ZoneLocationPanelView`+VM (reactive list-based panel showing room description, exits, entities). Region view now handled entirely by side panel (`GameRegionPanelView`). Keyboard movement removed — pure point-and-click. Client-side tile description service added. See `docs/client-ui-architecture.md`.
- **ExternalAuthEndpointTests fix**: Added `SecurityStamp` to test user creation in `ExternalAuthEndpointTests.cs` — was throwing `InvalidOperationException: User security stamp cannot be null`. The `UserManager.CreateAsync` call requires a non-null security stamp for external login flows.
- **EnemyAiService fix**: Added disposal-state guard in `StopAsync`/`Dispose` to prevent `ObjectDisposedException: The CancellationTokenSource has been disposed` — happens when `Dispose` is called concurrently with the periodic AI update loop. Added `_disposed` flag checks in both `StopAsync` and the timer callback.
- **Final test count verified**: `dotnet test Realm.Full.slnx` — **4,739 tests, 0 failures** across all 11 test projects. Full suite is fully green.
- **Open items for next session**: Hub integration success-path tests for region methods; world lore resolution (6 open design items); audio/sound expansion.

### Session-42 (2026-09-11) — NuGet Fallback Folder / Linux Docker `dotnet watch` Fix
- **Symptom**: `dotnet watch` build of `Veldrath.Server` inside the Linux dev container failed with
  `error MSB4018: NuGet.Packaging.Core.PackagingException: Unable to find fallback package folder 'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'`
  raised by `ResolvePackageAssets` at `Microsoft.PackageDependencyResolution.targets(266,5)` (`/usr/share/dotnet/sdk/10.0.401`).
- **Root cause**: The repo had **no** `NuGet.config`. On Windows dev machines, the machine-wide
  `C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config` injects the VS offline
  fallback folder. NuGet records inherited fallback folders into the generated restore output
  (`obj/project.assets.json` → `fallbackFolders`; `obj/<project>.csproj.nuget.g.props` → `NuGetPackageFolders`;
  `obj/<project>.csproj.nuget.dgspec.json`). `docker-compose.dev.yml` **bind-mounts the host project dirs**
  (lines 78–91) — and bind mounts bypass `.dockerignore` (which *does* already exclude `**/obj/` / `**/bin/`) —
  so the Linux container consumed the Windows-generated `obj/` and tried to resolve the non-existent Windows folder.
- **Fix**: Added repo-root [`NuGet.config`](../../NuGet.config) containing `<fallbackPackageFolders><clear /></fallbackPackageFolders>`.
  This stops the Windows-only VS fallback folder from being inherited/recorded on any OS, so the `obj/`
  artifacts that leak into the container are portable. **Do not remove the `<clear />`.**
- **Verified**: `dotnet restore Veldrath.Server/Veldrath.Server.csproj` regenerates `obj/*.nuget.g.props` and
  `obj/project.assets.json` with no `Microsoft Visual Studio\Shared\NuGetPackages` entry (dgspec `configFilePaths`
  now lists `G:\code\Veldrath\NuGet.Config` first); `dotnet build Veldrath.Server.slnx` succeeds with 0 errors.
- **Convention**: Any future repo NuGet configuration must not reintroduce a machine-specific `fallbackPackageFolders`
  entry.
- **Superseded (Session-43)**: The follow-on assumption that host `obj/` merely needs to be *portable* is no longer
  required — `docker-compose.dev.yml` now gives each container its own `obj/`/`bin/` via anonymous volumes (see Session-43).

---

### Session-43 (2026-09-11) — Dev-container `obj/`/`bin/` isolation (fixes NETSDK1064 + dotnet-watch watcher crash)

**Symptom (after the Session-42 NuGet.config fix)**: `dotnet watch` inside the Linux dev container failed with
`error NETSDK1064: Package <name>, version <ver> was not found. It might have been deleted since NuGet restore...`
for `Veldrath.Server`, and earlier crashed with
`System.ArgumentException: An item with the same key has already been added. Key: /src/Veldrath.Server/obj/Veldrath.Server.csproj.nuget.g.targets`
from `Microsoft.DotNet.Watch.PollingDirectoryWatcher`.

**Root cause**: `docker-compose.dev.yml` bind-mounts each host project directory into the container at the same path
(e.g. `./Veldrath.Server:/src/Veldrath.Server`). Bind mounts bypass `.dockerignore`, so the Linux container inherited
the **Windows host's** `obj/` restore output:
- `obj/<project>.csproj.nuget.g.props` → `<NuGetPackageFolders>C:\Users\<user>\.nuget\packages\</NuGetPackageFolders>`
  and `<SourceRoot Include="C:\Users\<user>\.nuget\packages\" />`
- `obj/project.assets.json` → `"packagesPath": "C:\\Users\\<user>\\.nuget\\packages\\"`

Because the mounted `project.assets.json` looked "up to date", the container's restore was a no-op and MSBuild resolved
packages from the non-existent Windows path → `NETSDK1064`. The same host-rewritten `obj/` tree also churned under the
container's polling watcher, causing the duplicate-key `ArgumentException`.

**Fix** (`docker-compose.dev.yml`): every bind-mounted project directory also gets **anonymous volumes** over its `obj/`
and `bin/`, so the container restores into its own Linux build output:
```yaml
- ./Veldrath.Server:/src/Veldrath.Server
- /src/Veldrath.Server/obj
- /src/Veldrath.Server/bin
```
Applied to all mounted projects: `server` (Veldrath.Server, Veldrath.Contracts, Veldrath.Assets, RealmEngine.Core/.Data/.Shared),
`discord` (Veldrath.Discord + Contracts + RealmEngine.*), `foundry` (RealmFoundry, Contracts, Veldrath.Auth, Veldrath.Auth.Blazor),
`web` (Veldrath.Web, Contracts, Auth, Auth.Blazor, RealmEngine.Core/.Data/.Shared). Each .NET service also sets
`NUGET_PACKAGES=/root/.nuget/packages` explicitly, backed by the existing `nuget_cache` named volume.

**Verified** (Docker Desktop 29.4.3, Linux containers): `docker compose -f docker-compose.dev.yml down` then
`up -d --build` → `server` and `discord` log `Build succeeded.` / `Application started.` with **no** `MSB4018`,
**no** `NETSDK1064`, and **no** `PollingDirectoryWatcher` `ArgumentException`. Inside the container,
`/src/Veldrath.Server/obj/Veldrath.Server.csproj.nuget.g.props` now records `/root/.nuget/packages` (Linux), confirming
the host Windows metadata no longer leaks in. Server healthcheck reaches `healthy`.

**Convention**: Do NOT remove the per-project `obj`/`bin` anonymous volumes. Any newly bind-mounted project in
`docker-compose.dev.yml` MUST also get `- /src/<Project>/obj` and `- /src/<Project>/bin` entries.

**Separate pre-existing issue discovered (NOT fixed here — out of scope)**: `web` and `foundry` cannot build because their
compose mounts are incomplete. `Veldrath.Web.csproj` references `Veldrath.Server.csproj` (line 25) but the `web` service
does not mount `./Veldrath.Server` (and `Veldrath.Web/Dockerfile.dev` never `COPY`s its csproj). `RealmFoundry.csproj`
→ `Veldrath.GameClient.Components` → `Veldrath.GameClient.Core` → `RealmEngine.Core`, but the `foundry` service mounts
none of those. Result: `dotnet watch ❌ The project file could not be loaded. Could not find a part of the path
'/src/Veldrath.Server/Veldrath.Server.csproj'` (web) and `'/src/RealmEngine.Core/RealmEngine.Core.csproj'` (foundry).
These services previously never started because they use `depends_on: server (healthy)`, and the server never became
healthy. Fixing them requires adding the missing source bind mounts + Dockerfile `COPY` lines. **Fixed by Session-44** —
the mounts and Dockerfile COPY lines are now complete.

---

### Session-44 (2026-10-21) — Linux workspace health audit

**Environment**: workspace is now at `/media/kungraseri/Projects/code/Veldrath` on Linux (earlier sessions used
`G:\code\Veldrath` on Windows). Verified: .NET SDK **10.0.402**, runtimes **10.0.13**, Docker **29.9.1**,
Compose **5.5.1**, PowerShell **7.6.7**. `dotnet-ef` is not installed (optional; migrations run at startup).

**Build/test baseline**:
- Purged 29 stale Windows `obj/` + `bin/` trees (they contained `C:\` NuGet paths) before the first Linux restore.
  The `<clear />` in `NuGet.config` remains essential.
- `dotnet build Realm.Full.slnx`: **29 projects, 0 warnings, 0 errors**.
- `dotnet test Realm.Full.slnx`: **4,848 tests across 13 test projects, 0 failures** (baseline was 4,739/11 at
  Session-41; `Veldrath.GameClient.Core.Tests` and `Veldrath.GameClient.Components.Tests` are now included).
- `dotnet list Realm.Full.slnx package --vulnerable --include-transitive`: **0 vulnerable packages**.

**Security/package bump (commit 99ab5c76)**: new advisories against `System.Security.Cryptography.Pkcs`/`Xml`
10.0.10 and `Microsoft.AspNetCore.SignalR.Common` 10.0.9. Pinned `System.Security.Cryptography.ProtectedData`/`Xml`,
all `Microsoft.Extensions.*`, and `Microsoft.AspNetCore.SignalR.Client` to **10.0.13** (matches the installed
runtime). Future package bumps require removing/recreating the `docker-compose.dev.yml` `nuget_cache` volume —
a stale cache caused `NETSDK1064` (MudBlazor missing) inside containers.

**Test-drift fixes (commit 99ab5c76)**:
- `GameCombat.razor` passed `Color="#e53935"` to `StatusBar`, which declares `BarColor` (`MudBlazor.Color`);
  fixed to `BarColor="@Color.Error"`.
- Added stable CSS hooks (`status-bar-label`, `action-btn-*`, `game-tile`, `tile-*`, `tile-indicator-player`,
  `game-tilemap-viewport`, `game-tilemap-empty`, `tile-status-bar`) so RCL tests target the current MudBlazor markup.
- bUnit RCL tests call `AddMudServices()`, set `JSInterop.Mode = JSRuntimeMode.Loose`, and register
  `NoopKeyInterceptorService`/`NoopResizeObserverFactory` (`Veldrath.GameClient.Components.Tests/Infrastructure/MudBlazorTestDoubles.cs`).
  MudBlazor 9.7 services are async-dispose-only while bUnit disposes synchronously.
- `GameLayoutRenderTests` updated to the current `MudLayout` structure (the old `in-game` class no longer exists).
- `RealmFoundry.Tests` excludes the intentionally public `RefreshToken` (used by `AuthDelegatingHandler`).
- `FakeCharacterCreationService` supplies an `EquipmentTypeCatalogDto`; the two `AvailableArmor/WeaponTypes`
  client tests await initialization.
- `ApplyStatusEffectHandler` now skips immunity/resistance rolls for beneficial effects (`Buff`, `HealOverTime`).
  Wisdom-derived resistance (1% per 10 Wisdom) previously gave every buff a ~1% chance to fail, which made
  `Should_Refresh_Duration_When_Enabled` intermittently fail (verified 5 consecutive clean runs after the fix).

**DataProtection native-run fix (commit e0fe2529)**: `Veldrath.Server`, `Veldrath.Web`, and `RealmFoundry` hardcoded
`/root/.aspnet/DataProtection-Keys`, breaking auth/register on native non-root Linux (500 in
`SendEmailConfirmationAsync`) and antiforgery. They now use the framework default
(`$HOME/.aspnet/DataProtection-Keys`, still `/root/...` in containers, preserving compose volumes) with an optional
`DataProtection:KeysPath` override.

**Docker dev fixes (uncommitted at audit end)**: `Veldrath.Web`, `RealmFoundry`, and `Veldrath.Discord`
`Dockerfile.dev` entrypoints now pass `--no-launch-profile`. Without it `dotnet watch` loaded `launchSettings.json`
and bound to `:5000`/HTTPS instead of `ASPNETCORE_URLS`, so the Foundry (:8081) and Web (:8082) containers were
unreachable. Server already had the flag.

**Runtime smoke results (native)**:
- Server `:9000`: `/health` 200; migrations + seed against Postgres; `/api/content/browse?type=item` 200
  (23 items, 4 archetypes in DB); `/api/announcements` 200; `POST /api/auth/register` 200 with JWT;
  authenticated `POST /hubs/game/negotiate` 200 (WebSockets).
- `Veldrath.Web` `:8082`: `/` and `/login` 200; SSR calls server editorial announcements 200.
- `Veldrath.Client` on X11: launches, holds two established connections to the server, server logs `/health` +
  `/api/announcements` 200 from the client.
- `RealmFoundry` `:8081`: 200 with correct title. `RealmForge` launches (content load blocked only by the port
  conflict below).

**Docker dev stack**: verified `postgres`, `seq`, `server`, `foundry`, `web` — server healthy on 9000, Foundry 200 on
8081, Web 200 on 8080, `dotnet watch` hot reload enabled. Services were started sequentially after clearing
`nuget_cache`.

**Host-specific conflict (not a repo defect)**: host port **5433** is occupied by an unrelated
`kungharness-postgres-1` container. Compose/native defaults (5433) cannot reach Veldrath's DB while that project
runs. Smoke tests used a temporary Postgres on **5434** and a compose override
`postgres.ports: !override ["5434:5432"]`. `RealmForge` reads `ConnectionStrings:ContentDb` (5433) from persistent
settings; override it or change its settings when using 5434. The Discord bot was not started (live external
connection/token side effects).

**Git note**: an external auto-commit process committed the package/test fixes as `99ab5c76` and `e0fe2529` during
the session; the three `Dockerfile.dev` fixes remained uncommitted at audit end.

---

### Session-44 (2026-10-21) — Text dashboard map graph: Phases 0–2 complete

**Schema + persistence**
- `RealmEngine.Data/Entities/Content/MapGraph.cs`: `Map`, `MapNode`, `MapEdge` (string enums stored via
  `HasConversion<string>()`; unique `(MapType, OwnerId)` and `(MapId, FromNodeId, Direction)`).
- `ContentDbContext` DbSets + `ContentModelConfiguration`; entities only exist in `ContentDbContext`.
- Added `Character.CurrentMapId/CurrentNodeId` and `PlayerSession.CurrentMapId/CurrentNodeId`.
- Migrations: `20261021172256_AddMapGraph` (Data), `20261021172306_AddCharacterMapPosition` (Server).
- `IMapGraphRepository` / `EfCoreMapGraphRepository`; DI registration in `Veldrath.Server/Program.cs`.

**Importer + seeder**
- `RealmEngine.Data/Import/TmxToMapGraphImporter.cs` is pure (no DB/TMX repo dependency); input is
  `MapGraphImportCatalog` + in-memory `TiledMap` dictionaries.
- Region maps: TMX `zones`, `region_exits`, `spawns`, `paths`; zone maps: TMX `labels`, `exits`, `spawns`,
  `paths`, supplemented by `ZoneLocation` and `ZoneLocationConnection` rows.
- Generates deterministic edge GUIDs (SHA-256 of map/from/direction/to) and unique layout positions.
- `MapGraphImportReport` separates warnings (auto-connected, synthesized exits, direction conflicts) from
  errors (unknown refs, missing target maps/nodes).
- `RealmEngine.Data/Seeders/MapGraphSeeder.cs` is the generated snapshot: **23 maps / 130 nodes / 196 edges**.
  It does not read TMX. Wired into `DatabaseSeeder` after zone-location connections.
- Fresh Postgres verification: counts matched; second server start left counts unchanged (idempotent).
- `MapGraphSeederTests` assert exact counts, structural validity, and idempotency.

**Server navigation**
- `GetMapGraphHubCommand`: resolves current map (or explicit), filters hidden nodes/edges using
  `ICharacterUnlockedLocationRepository`, always includes the current node, returns map summaries.
- `TravelToNodeHubCommand`: validates current map/position, visibility, direct edge, and exit target;
  updates Character + PlayerSession via new `UpdateMapPositionAsync` methods; sets zone/region context;
  updates tile coords from node layout for zone maps; runs passive discovery and location roster spawn.
- `GameHub`: `GetMapGraph(string? mapId)` → `MapGraphReceived`; `TravelToNode(TravelToNodeHubRequest)` →
  `NodeTraveled`. `SelectCharacter` sets region entry; `EnterZone` sets zone entry.
  Old tile methods are deprecated in XML remarks only (keep until TMX removal).

**Test baselines**
- `RealmEngine.Data.Tests`: 265 passing (9 validator, 9 importer incl. real TMX asset, 3 seeder).
- `Veldrath.Server.Tests`: 806 passing (10 GetMapGraph, 10 TravelToNode).
- `Veldrath.Server.slnx`: 0 warnings / 0 errors.

**Next**: Phase 3 client core (`IGameStateService`/`GameStateService` map state, hub subscriptions),
then Phases 4–6 UI, then Phase 7 TMX removal.

---

### Session-45 (2026-10-21) — Text dashboard, cardinal map, and tile removal (Phases 3–7a)

**Commits**
- `9625d071` — text-first dashboard, cardinal map, and overlay conversion (phases 11–18).
- `f40f761a` — remove tile navigation from server, client core, and Blazor UI (phase 19a).

**Client core**
- `MapPayloads.cs`: `MapGraphReceivedPayload`, `NodeTraveledPayload`, `TravelSpawnedEnemy`.
- `IGameStateService`/`GameStateService`: `CurrentMapId`, `CurrentNodeId`, `CurrentMap`,
  `AvailableMaps`, `ApplyMapGraph`, `ApplyNodeTraveled`; `ZoneEnteredPayload` and
  `CharacterSelectedPayload` carry optional map/node ids.
- `MapHubCommands.cs`: `RequestMapGraphAsync`, `TravelToNodeAsync`.

**Blazor RCL**
- Text primitives: `TextPanel`, `StatRow`, `StatTable`, `EntityList`, `ItemTable`, `DetailPane`,
  `Models/TextUiModels.cs`; `game.css` now holds the full text UI and cardinal map CSS.
- Shell: responsive `game-shell` grid; text header/footer/sidebars; `Game.razor` renders
  `CardinalMap` + `MapDetailPanel` whenever `CurrentMap` is loaded.
- `Layout/CardinalGraphLayout.cs`: authored positions, cardinal BFS, spiral overlap avoidance.
  `CardinalMap.razor(.cs)` supports arrow-key node selection and Enter travel.
- Overlays: inventory/shop/journal/character sheet use tables + detail panes; shop uses
  `GetDisplayName()` (not `GetShortName()`, which is empty when `BaseName` is unset).
- `ActionBar` hotbar is now an abilities table with keybind text, status text, Material icons,
  and a Use button; no emoji.
- Component test suite: 80 passing (10 map state, 9 layout, 9 cardinal map, 11 text foundation,
  10 overlay, 10 hotbar, 8 combat, plus page tests).

**Server / tile removal (phase 19a)**
- Deleted hub commands and handlers: `MoveCharacter`, `GetZoneTileMap`, `GetRegionMap`,
  `MoveOnRegion`, `ExitZone`, `ChangeRegion`; removed their `GameHub` methods and request DTOs.
- Removed `ITileMapRepository` DI registration, `EnemyAiService`, tile enemy spawning, and
  `ZoneEntitiesSnapshot` broadcasts from `GameHub`.
- `SelectCharacter`/`EnterZone` now persist only map/node position; tile spawn/unstick logic gone.
- `IZoneEntityTracker`/`ZoneEntityTracker` are retained only because `InspectEntity` and
  `TalkToNpc` handlers still read their entity list (no producer remains — replace with a
  node-scoped NPC/enemy source in phase 7b).
- Removed `ZoneTileMap`, `PlayerTileX/Y`, `RegionState`, tile apply methods, and tile payload
  subscriptions from `GameClient.Core`; `OccupantInfo` and `EnemyInfo` no longer carry tiles.
- Deleted Blazor `GameTilemap`, `GameMap`, `Tile`, `TileInfoCard`, `TileStatusBar` and tests.

**Verification**
- `dotnet build Realm.Full.slnx` — 0 warnings / 0 errors.
- Full suite with coverage — **4,877 passing** across 13 projects (Server 759, Client 768,
  Core 1,955, Shared 825, Data 265, Components 80, and others).

**Still remaining (phase 7b)**
- Avalonia native tile stack: `TilemapViewModel`, `RegionTilemapViewModel`, `MapViewModel`,
  `MapRegionGroupViewModel`, `ZoneLocationPanelViewModel`, `TileDescriptionService`,
  `Views/Map/*`, `ZoneLocationPanelView`, related `GameViewModel` handlers/commands, tests.
- Delete `Veldrath.Contracts/Tilemap`, RealmEngine tile query/repo, `TiledMap` models,
  `TmxToMapGraphImporter` and fixtures, tilemap assets/manifests/TMX files.
- Drop `Character.TileX/TileY/TileZoneId`, `PlayerSession.TileX/TileY`, tile repository
  methods, and the compatibility tile write in `TravelToNodeHubCommand`.
- Mark older tilemap plans superseded; runtime smoke tests on web and desktop hosts.

---

### Session-46 (2026-10-22) — Phase 7b complete: TMX layer fully removed

**Deleted from the engine/contracts**
- `Veldrath.Contracts/Tilemap`, `GetTileMapQuery`, `ITileMapRepository`,
  `CompositeITileMapRepository`, `InMemoryTileMapRepository`, `TiledFileMapRepository`,
  `TiledMap` + `TiledMapGameExtensions`, `DungeonGenerator`, `TmxToMapGraphImporter`,
  and their test files.
- `Veldrath.Assets/Manifest/TilemapAssets.cs` and the whole
  `Veldrath.Assets/GameAssets/tilemaps` tree (22 TMX maps, TSX/PNG sheets, Tiled project).
- TMX generation/scan scripts (`generate-zone-tmx.ps1`, `_gen_varenmark_zones.py`,
  `generate-onebit-labeled.ps1`, `scan-tiles.ps1`).

**Avalonia client**
- Removed `TilemapViewModel`, `RegionTilemapViewModel`, `MapViewModel`,
  `MapRegionGroupViewModel`, `ZoneLocationPanelViewModel`, `TileDescriptionService`,
  `Views/Map/*`, `ZoneLocationPanelView`, and 5 tile/region test files.
- `GameViewModel` now exposes `CurrentMap`, `CurrentNodeId`, `CurrentMapName`,
  `MapPositionText`, `MapNodes`, `SelectedMapNode`, `RefreshMapCommand`,
  `TravelToSelectedNodeCommand`, `ApplyMapGraph`, `ApplyNodeTraveled`.
- `CharacterSelectViewModel` subscribes to `MapGraphReceived`/`NodeTraveled` and sends
  `GetMapGraph` after `EnterZone`; the Blazor `Game.razor` page is used through WebView2.
- Native fallback centre panel is a text ListBox of map nodes with a Travel button;
  footer “Mini-map” replaced with a Map refresh button.

**Server/schema**
- Dropped `Character.TileX/TileY/TileZoneId`, `PlayerSession.TileX/TileY`,
  `PlayerSession.LastMovedAt`, plus `UpdateTilePositionAsync`, `UpdatePositionAsync`,
  `UpdateLastMovedAtAsync`, `SetZoneAsync`, `SetRegionAsync`.
- Migration `20261022142119_RemoveTilePositionFields` (ApplicationDbContext).
- Removed tile writes from `NavigateToLocationHubCommand` and `TravelToNodeHubCommand`.
- Removed `TalkToNpc`/`InspectEntity` hub methods and handlers plus `ZoneEntityTracker`
  (their only producer was the removed tile spawner). Re-introduce them later with a
  node-scoped NPC/enemy source.
- `LastSessionDto` now carries `CurrentMapId`/`CurrentNodeId` instead of `TileX`/`TileY`.

**Verification**
- `Realm.Full.slnx`: 0 warnings / 0 errors.
- Full suite: **4,725 passing**, 0 failed (Server 759, Client 663, Core 1,944,
  Shared 803, Data 253, Components 80, others 213).
- Runtime: Postgres container healthy; server `/health` 200; database has
  23 maps / 130 nodes / 196 edges and **0** tile columns; web `/`, `/Game`,
  `/Game/Play`, `/health` all 200; Avalonia client ran 25 s without error.
- Grep check confirms no `Tilemap`, `TiledMap`, `.tmx`, `ZoneEntityTracker`,
  `TalkToNpc`, or `InspectEntity` references remain outside historical migrations/plans.

**Remaining**: Phase 9 optional RealmForge map graph editor and JSON import/export.

---


---

### Session-47 (2026-10-22) — Phase 25 complete: RealmForge map graph editor + JSON authoring

**Engine (RealmEngine.Data)**
- `Authoring/MapGraphJsonSerializer.cs`: `MapGraphFile(FormatVersion=1, ExportedAt, Maps)`;
  camelCase + indented + string enums + deterministic ordering; import rejects empty or
  malformed JSON, unsupported versions, and duplicate map ids (`InvalidDataException`).
- 9 tests in `RealmEngine.Data.Tests/Authoring/MapGraphJsonSerializerTests.cs`.

**RealmForge**
- `Services/MapGraphEditorService.cs`: `IServiceScopeFactory` + `ContentDbContext` with a
  nullable `GetService` so the tool runs without a configured database. Save performs
  explicit node/edge sync (`db.MapNodes.Remove` / `db.MapEdges.Remove` for stale rows),
  versions start at 1 and increment on update, and export/import support strings and files
  with an overwrite toggle.
- `ViewModels/MapGraphEditorViewModel.cs` + `MapNodeRowViewModel` + `MapEdgeRowViewModel`:
  map picker, metadata, node/edge editing, add/remove (removing a node removes its edges),
  validation list, save/delete, and import/export. `BuildGraph()` returns `MapGraph`.
- `Views/MapGraphEditorView.axaml(.cs)`: code-behind owns the Avalonia StorageProvider file
  pickers; the details pane edits the selected node or edge.
- `MainWindowViewModel.OpenMapGraphEditorCommand` + **Maps** toolbar button; the VM calls
  `LoadMapsAsync()` explicitly (no fire-and-forget async in its constructor).
- DI: `MapGraphEditorService` registered as a singleton in `App.axaml.cs`.

**Testing gotcha**
- Registering an EF Core InMemory database through
  `AddDbContext(o => o.UseInMemoryDatabase($"db-{Guid.NewGuid():N}"))` executes the lambda
  per scope, so every scope gets a different database. Capture the database name once
  outside the lambda. This caused `SaveAsync` followed by `LoadAsync` to return null.

**Verification**
- `Realm.Full.slnx`: 0 warnings / 0 errors.
- Full suite: **4,753 passing**, 0 failed across 13 projects (RealmForge 27, Data 262,
  Server 759, Client 663, Core 1,944, Shared 803, Components 80, others).
- RealmForge ran 20 s without crashing; the expected Postgres connection error is handled
  and the window stays up when the content database is offline.
- The text-dashboard/map-graph effort is complete; the plan was later folded into
  `docs/client-ui-architecture.md` and deleted.

---

### Session-48 (2026-10-22) — P1 documentation re-audit + P2 gap fixes

**P1 docs**
- Wiki zone/region pages now show `Map Graph` (`zone:<slug>` / `region:varenmark`) and entry-node ids instead of TMX file names and sizes.
- `wiki/Engine-Implementation-Status.md` refreshed for Session-47/48 (4,759 tests, unified map graph).
- `engine-codebase.md` open items corrected: `PurgeExpiredAsync` is scheduled, tile-era hub tests are obsolete, and web character creation is session-based.
- The game-client parity checklist was re-audited (later deleted with the Blazor RCL); the live hub contract is `Veldrath.GameClient.Core/Payloads` + `web/client/src/lib/game/types.ts`.
- The character-creation sync plan was marked superseded (later deleted); auth-plan leftovers corrected (idempotency and rate limiting already implemented).

**P2 code**
- `Game.razor`: subscriptions added for `EnemyEngaged`, `EnemySpawned`, `OnWhisper`, `OnEmote`, `OnAnnouncement`, `CharacterIgnored`, `OnWarned`, `OnMuted`, `OnTeleported`, `OnSummoned`, and `OnItemReceived`.
- `EntityPayloads.cs`: `EnemyEngagedPayload` and `EnemySpawnedPayload`; new `CharacterStatusChangedPayload`.
- `IGameApiClient.DeleteCharacterAsync` implemented in the web and embedded clients; `FakeGameApiClient` updated.
- `CharacterSelect`: inline delete confirmation, 10-second select timeout, and `CharacterStatusChanged` online/offline badge.
- `EmbeddedRoutes`: removed the redundant assembly reference and wrapped NotFound in `GameLayout`.
- Tests: Components project 86 passing (2 Game event tests, 2 CharacterSelect tests, plus GamePanel/ReconnectOverlay render tests).

**Verification**
- `Realm.Full.slnx`: 0 warnings / 0 errors; full suite **4,759 passing** across 13 projects.

**Still open (P2)**
- G106/G108/G109/G110 routes/layout; G117/G118 bridge interop; G119 `CreateCharacter` interaction tests; G125 `GameSettings` blocked by MudBlazor's internal async-dispose-only `PointerEventsNoneService`.

---

### Session-49 (2026-10-22) — OAuth "No authentication handler" fix

- **Root cause**: local `dotnet run` does not read `.env`. Compose maps `OAUTH_*` to `OAuth__*`, but a direct run had no OAuth configuration, so the Discord scheme was never registered while `/api/auth/external/discord` still challenged it → unhandled 500.
- `ExternalAuthEndpoints.Challenge` now checks `IAuthenticationSchemeProvider` and returns a 503 ProblemDetails ("OAuth provider not configured") instead of throwing. Unknown providers still return 400.
- `Program.cs` logs `OAuth providers configured: ...` at startup.
- New `Infrastructure/DotEnvConfiguration.cs` loads the repo-root `.env` in Development only, mapping `JWT_KEY`, `OAUTH_*`, `FOUNDRY_PUBLIC_URL`, `WEB_PUBLIC_URL`, `ALLOWED_RETURN_URL_HOSTS`, `EMAIL_*`, and `SEQ_SERVER_URL` to ASP.NET configuration keys. Existing environment variables are never overridden.
- OAuth lookups in `Program.cs` also fall back to the raw `OAUTH_*` environment names, so a shell-sourced `.env` works too.
- Verified: plain Development `dotnet run` on port 9001 registered Discord, Google, and Microsoft; `/health` returned 200 and `/api/auth/external/discord` returned a 302 to Discord.
- Tests: 2 new `ExternalAuthEndpointTests` (unconfigured → 503, unknown → 400). Full suite **4,761 passing**.
- Operational: restart the long-running server after editing `.env`; the dev client/server are launched via `dotnet run`.

---

### Session-50 (2026-10-22) — Character creation equipment dropdowns fixed

- **Root cause (Avalonia wizard)**: `AvailableArmorTypes` / `AvailableWeaponTypes` are computed from `_equipmentCatalog`, but assigning the catalog after the async session load never raised `PropertyChanged`. The ComboBoxes were bound while the catalog was null and never refreshed, so both lists stayed empty.
- `CreateCharacterViewModel` now assigns the catalog through `SetEquipmentCatalog(...)`, which raises change notifications for both lists.
- **Second bug**: step 5 derived API slugs from display names (`"Light Armor"` → `light-armor`). It now maps display names back to `EquipmentTypeOptionDto.Slug` through the catalog (`light`, `sword`, …).
- `FakeCharacterCreationService` records `LastEquipmentPreferences` and supports a `BeginSessionGate` for async-notification tests.
- Tests: 3 new `CreateCharacterViewModelTests` (catalog populated, change notifications raised, canonical slugs sent). All 44 CreateCharacter tests pass.
- Note: the web/RCL wizard already bound slugs directly and re-renders after load, so it was unaffected.

---

### Session-51 (2026-10-23) — First-login map, starting equipment, and map button fixed

**Map failed to load (GetMapGraph)**
- Root cause: `GameHub.GetMapGraph(string? mapId = null)` used an optional parameter. SignalR does not supply default values, so zero-argument invocations failed during binding ("Failed to invoke 'GetMapGraph' due to an error on the server") before the method body ran — no MediatR log entry appeared.
- Fix: the hub parameter is now required (`GetMapGraph(string? mapId)`); all callers send an explicit null or map id:
  - `MapHubCommands.RequestMapGraphAsync` always sends the argument.
  - Avalonia `GameViewModel` and `CharacterSelectViewModel` send `null` via `SendCommandAsync<object?>`.
- Server restart **and client rebuild** are required, since old clients still send zero arguments.

**Starting equipment not added**
- Server log showed `Loading equipment for class Warrior (Armor: , Weapons: )` followed by `Loaded 0 weapons and 0 armor items` and `No equipment found for class warrior`.
- Root cause: `EfCoreCharacterClassRepository.MapToModel` hardcoded `ArmorProficiency = []` and `WeaponProficiency = []`; `ActorClass` only stores playstyle traits.
- Fix: derive armor proficiencies (`light`, plus `medium`/`heavy` when `CanWearHeavy`, plus `shields` when `CanWearShield`) and weapon proficiencies (`simple`/`daggers` baseline, plus melee/ranged/spellcaster/stealth/dual-wield categories) from `ActorClassTraits`.
- `CreateCharacterHandler.SelectStartingEquipment` no longer returns early when no weapons match; it logs and still equips armor.

**Tests**
- New repository test: `GetById_Derives_Equipment_Proficiencies_From_Class_Traits`.
- Updated map-state test and RCL teleport test for the explicit null `GetMapGraph` argument.
- Full suite expected green; build clean.

---

### Session-52 (2026-10-23) — Delete confirmation, shields, and inventory/equipment sync

**Character deletion confirmation**
- Avalonia `CharacterSelectViewModel` now exposes `RequestDeleteCommand` / `CancelDeleteCommand`; `DeleteCommand` still performs the actual deletion.
- `CharacterEntryViewModel.IsPendingDelete` / `IsNotPendingDelete` drive the inline Delete → Confirm/Cancel buttons in `CharacterSelectView.axaml`. Only one entry can be pending at a time, and a failed delete clears the pending state.
- The RCL `CharacterSelect` already had an inline confirm flow.

**Shield granted when requested**
- `CreateCharacterHandler.SelectStartingEquipment` now filters shields out of the armor candidates (they previously could be equipped to Chest), detects shields via `TypeKey == "shield"` / `ItemType.Shield` / `ArmorType`, and synthesizes a `wooden-shield` fallback when the content catalog has no shield item.
- `EquipArmorToSlot` routes shields to `OffHand` and never to an armor slot.
- `FinalizeCreationSessionAsync` writes canonical slot names: `MainHand`, `OffHand`, `Head`, `Chest`, `Legs`, `Feet`, `Ring`, `Amulet` (Helmet→Head, Boots→Feet, Necklace→Amulet) so clients can display starter gear.

**Inventory duplication and equipment visibility**
- `GetInventoryHubCommand` now injects `IItemRepository`, filters equipped refs out of the bag list, and returns full `ItemDetails` plus a slot→`Item` `Equipped` map.
- `InventoryLoaded` payload shape is `{ CharacterId, Items, ItemDetails, Equipped, EquippedItems }`: `Equipped` is the slot→ref map (Avalonia) and `EquippedItems` is the slot→`Item` map (RCL). Keep those two names distinct — binding full item objects into the Avalonia string-ref map throws during SignalR deserialization and the inventory overlay never opens.
- `GetInventoryHubCommandResult` filtering means an equipped item is no longer listed in the bag; unequipping makes it reappear because the inventory blob still contains it.
- `GameHub.EquipItem` now sends a resolved `Equipped` map in `ItemEquipped` and follows it with a caller-only `InventoryLoaded` snapshot, removing the doubled-item view immediately after equipping.
- `CharacterSelected` now includes `Equipment` (slot→ref); Avalonia applies it in `SeedInitialStats` and the RCL seeds placeholder `GameState.EquippedItems`, so starter items appear equipped at login.
- `EfCoreItemRepository.MapToModel` now maps the model `ItemType` (weapon/shield/armor slot) from the persisted discriminator and category.
- Avalonia `InventoryItemViewModel.EquipSlot` routes Equip to the correct slot (shield→OffHand, helm→Head, etc.); the RCL `GuessSlot` now handles `ItemType.Shield` before the armor branch.

**Tests**
- New: 4 Avalonia delete-confirmation tests, 2 shield-granting tests, 2 server inventory-snapshot tests, 2 Avalonia inventory-slot tests, 1 EquipmentSelected-seeding test, 2 item-mapping tests.
- Full suite: **4,780 passing** across 13 projects; `Realm.Full.slnx` builds with 0 warnings / 0 errors.
- Runtime note: restart the server and rebuild the client; existing characters keep their existing blobs, so only newly created characters show the synthesized shield.

---

### Session-53 (2026-10-23) — Web-first pivot and RCL action correctness

**Direction**
- The browser game client (SvelteKit) is now the primary experience. Desktop/Avalonia follows decisions made in the web client. See `docs/client-ui-architecture.md`.

**Web action fixes**
- `LeftSidebar` now sends the server-bound request shapes: `VisitShop { ZoneId }`, `RestAtLocation { LocationId, CostInGold }`, `NavigateToLocation { LocationSlug }`.
- `ExitZone` (removed hub method) replaced with `LeaveZone` plus `RequestMapGraphAsync`; button relabelled "Leave Zone".
- Removed the dead `/Game/Map` link; the cardinals map is inline on `/Game/Play`.
- `GameFooter` no longer renders an unbound combat `ActionBar`; `GameCombat` owns Attack/Defend/Flee/Respawn bindings.
- `ZoneEntered` now carries `HasInn`/`HasMerchant`; `GameStateService.ApplyZoneEntered` stores `ZoneState` (including `Description`/`Type`) so the web sidebar/footer render shop and rest actions.
- `GameFooter.RestAsync` uses the same `RestAtLocation` payload.

**Tests**
- New `Veldrath.GameClient.Components.Tests/WebGameActionTests.cs` (6 tests) covering payloads, `LeaveZone` + map refresh, removed map route, and no dead footer combat buttons.
- `GameStateServiceTests` covers zone facilities from `ZoneEntered`.
- `Veldrath.Web.slnx` builds clean; Components tests 92 passing.

---

### Session-54 (2026-10-23) — Web inventory/shop transaction correctness (web-first P0-3)

**Shop**
- RCL `ShopOverlay` now sends `BuyItem`/`SellItem` with the bare item slug. It previously sent `{ ItemSlug, Quantity }`, which cannot bind to the server's `BuyItem(string itemRef)` / `SellItem(string itemRef)`.
- `Game.razor` now requests `GetShopCatalog` after `ShopVisited`; the server sends both `Items` (Avalonia `ShopItemDto`) and `Catalog` (RCL `ShopItemEntry`: slug/name/rarity/price).
- Removed `ShopOverlay`'s dead `ShopCatalogUpdated` subscription — `Game.razor` owns the `ShopCatalog` handler.

**Transactions**
- `ItemPurchased`/`ItemSold`/`ItemDropped` RCL payloads now match the server fields; handlers update gold/messages only.
- Server sends a background `InventoryLoaded` snapshot after buy/sell/drop (best-effort; wrapped in `TrySendInventorySnapshotAsync`).
- `InventoryLoaded` now carries `OpenOverlay`; `EquipItem`, buy, sell, and drop snapshots set it false so the current overlay does not switch. Explicit `GetInventory` still opens the inventory panel.
- `InventoryOverlay` equipment panel uses `GetDisplayName()` (was `GetShortName()`, which rendered blank for catalog items without name components).
- Removed the dead `Use` button (`UseItem` hub method does not exist).

**Tests**
- New `WebInventoryInteractionTests` (6 tests): shield→OffHand equip payload, drop slug, no bag duplication of equipped gear, no Use button, buy slug, sell slug.
- Components tests **98 passing**; Core tests 51 passing; targeted server shop/transaction tests 29 passing.

---

### Session-55 (2026-10-23) — Cardinal map layout refresh for the post-tile world

**Layout**
- `CardinalMap` is now a compact text graph rather than a tile-sized canvas: node cards are
  152×60 with 8px horizontal / 10px vertical gutters (cells 160×70), an 8px canvas padding,
  and a centred canvas inside a bounded (`max-height: 52vh`) bordered viewport.
- Connectors now run between facing card edges instead of card centres, and each carries a
  cardinal arrow (`↑`/`↓`/`→`/`←`) plus a tooltip with the edge label. Blocked and hidden
  connectors get distinct styles.
- Authored `LayoutX/LayoutY` values are density-gated: dense layouts keep their relative grid
  (compacted to ordered indices), while sparse TMX-era layouts (e.g. 8 nodes spread across a
  64×40 tile rectangle) are discarded in favour of a compact edge-driven layout seeded at the
  entry node. This removes the large empty gaps between nodes.
- Node metadata uses compact inline badges (`Entry`/`Exit`) instead of MudChips; the current node
  uses the player-indicator gold token and a `● You are here` label.
- Added a map legend toolbar (Current / Selected / Exit) and `MapSubtitle` counts.
- `Game.razor` keeps the shell at exactly three columns (left sidebar / center / right chat);
  `.map-layout` stacks the map above the detail panel inside the center column.
- Keyboard arrow navigation now prefers a traversable graph edge in the requested direction before
  falling back to spatial nearest-neighbour selection. Enter no longer travels to the current node.
- Removed the unused `--vds-tile-*` design tokens from the RCL token file.

**Orientation follow-up (Session-56)**
- `CardinalGraphLayout` now reserves computed nodes in the edge's cardinal half-plane, so a North
  edge cannot place its target to the west when the straight cell is occupied; it uses a nearby
  perpendicular offset instead.
- Dense authored grids are only used when their positions agree with the edge directions (80%
  threshold); otherwise the compact edge-driven layout is used. This keeps arrows and placement
  consistent.
- L-shaped routes hug the destination card and suppress the direction arrow (no single direction).
- Added a compass hint to the map toolbar: `N ↑ · E → · S ↓ · W ←`.
- Tests cover conflicting north placement, authored-layout rejection on contradiction, and
  arrow-free L connectors.

**Tests**
- `CardinalGraphLayoutTests` updated for compacted authored coordinates.
- `CardinalMapComponentTests` adds direction-label, compact badge, edge-first keyboard navigation,
  and no-travel-to-current-node coverage. Components project: **102 passing**.

## SvelteKit Migration — Phase 0 + .NET Auth Bridge (Session-57, 2026-10-23)

**Phase 0 (complete)**
- SvelteKit app lives in `web/` (`shared` + `client` workspaces). Colyseus, the TypeScript engine,
  and the Caddyfile are gone.
- Better Auth 1.7.1 owns the `auth` Postgres schema (`user`, `session`, `account`, `verification`,
  `__drizzle_migrations`). `pgSchema("auth")` keeps FKs out of `public`.
- SvelteKit mints the .NET JWT with `jose` (issuer `Veldrath.Server`, audience `Veldrath.Client`,
  HS256, 10-minute TTL). The `JWT_KEY` in `web/client/.env` must match the root `.env` value.
- Social providers configured: Discord, Google, Microsoft (Discord confirmed working).
- Docker: web entrypoint waits for Postgres, applies Drizzle migrations, then starts adapter-node.
  `config/postgres/init-auth-schema.sh` creates the `veldrath_auth` role/schema on first boot.
  Deployment is Coolify + Traefik; no Caddy.
- Gotcha: Better Auth scopes the session cookie to the login host. Use `localhost`, not `127.0.0.1`.

**.NET auth bridge (complete)**
- `JwtClaimsEnrichment.EnrichAsync` is wired to `JwtBearerEvents.OnTokenValidated`. It validates the
  `sub` GUID, provisions unknown accounts, and appends `ClaimTypes.Role` + `permission` claims.
- `AccountProvisioningService` creates a `PlayerAccount` with the token GUID as its primary key.
  Identity is configured with `RequireUniqueEmail`, so accounts without an email claim (or whose
  email is already owned) get a deterministic placeholder: `{accountId:N}@external.veldrath.local`.
  Access snapshots are cached for 60 seconds (`ClearCache()` exists for tests/admin changes).
- `RolePermissionResolver` is shared by `AuthService` token issuance and inbound enrichment so RBAC
  derivation cannot drift.
- `game-token.ts` now includes `email` and `email_verified`.
- Verified live: Better Auth user with `0` matching `AspNetUsers` rows → `/api/characters` `200` →
  row created with matching GUID/username/email → repeat request keeps one row →
  `/hubs/game/negotiate` `200`. `Veldrath.Server.Tests`: **771 passing** (8 new).

**Temporary Blazor Foundry bridge**
- `POST /api/internal/accounts/sync` (`InternalAccountBridgeEndpoints`) accepts a Better Auth
  account (GUID, username, email, optional plaintext password, optional provider logins) and
  writes the Identity password hash directly via `PasswordHasher.HashPassword` (bypasses the
  12-char policy on purpose). Guarded by `X-Internal-Bridge-Token` == `InternalBridge:Token`;
  returns `503` when unconfigured.
- `web/client/src/lib/server/foundry-bridge.ts` calls it server-side. Registration posts the
  plaintext password once to `/api/foundry-bridge/register`; `hooks.server.ts` does a once-per-user
  account/provider-login sync and retries on failure. `INTERNAL_BRIDGE_URL=http://server:8080`
  inside Docker.
- Provider IDs are normalized `discord|google|microsoft` → `Discord|Google|Microsoft` for
  `AspNetUserLogins`.
- Concurrency gotcha: registration and the hooks sync race to provision the same account.
  `CreateAccountAsync` catches `DbUpdateException` (duplicate `PK_AspNetUsers`), detaches the
  failed entity, and re-queries the winning row.
- Identity requires a unique email (`RequireUniqueEmail`), so accounts without one get
  `{accountId:N}@external.veldrath.local`.

**Existing-account migration**
- `web/scripts/migrate-identity-accounts.mjs` (`npm run db:migrate-identity --workspace client`)
  copies `AspNetUsers` → `auth.user` (same GUID), `PasswordHash` → `auth.account`
  (`provider_id='credential'`), and `AspNetUserLogins` → `auth.account`. Idempotent; `--dry-run`
  rolls back.
- Better Auth 1.7.1 matches credential accounts by `issuer` too. Insert
  `issuer='local:credential'` for password accounts and `issuer='local:oauth:{provider}'` for
  social logins, or sign-in reports `INVALID_EMAIL_OR_PASSWORD` even with a correct hash.
- Migration needs `MIGRATION_DATABASE_URL` with access to `public."AspNetUsers"`; the restricted
  `veldrath_auth` role cannot read public tables (and its `search_path=auth` resolves unqualified
  `"AspNetUsers"` to `auth."AspNetUsers"`).
- Identity PBKDF2 v3 hashes stay valid: `auth.ts` uses a custom `emailAndPassword.password.verify`
  that handles Better Auth scrypt (`salt:key`) and Identity v3
  (`0x01 | prf | iterations | saltLen | salt | subkey`). New password changes write scrypt.
- Verified live: migrated Identity-only account signed in through Better Auth with its legacy
  password, minted a game token, and `/api/characters` returned `200`.
- Client Vitest: **4 passing** (`auth-password.test.ts`). Server tests: **776 passing**.

**Phase 1 vertical slice (complete)**
- `/characters` is an SSR page: `game-api.ts` mints a token and calls `/api/characters` using
  `INTERNAL_BRIDGE_URL` (or `PUBLIC_API_URL` as fallback).
- `/play/[id]` uses `GameSession` (`$lib/game/session.svelte.ts`), a runes store that owns the
  SignalR connection. Hub methods: `SelectCharacter`, `EnterZone`, `GetMapGraph`, `TravelToNode`,
  `GetInventory`. Events: `CharacterSelected`, `ZoneEntered`, `MapGraphReceived`, `NodeTraveled`,
  `InventoryLoaded`, `SystemMessage`, `Error`, `OnKicked`.
- SelectCharacter always resets the character to the region-map entry. If `currentZoneId` is set,
  the client calls `EnterZone` first, then `GetMapGraph`; `EnterZone` moves the session to the zone
  map, so the subsequent graph request returns the zone map.
- Travel into a zone node returns `NodeTraveled.mapChanged=true` with `zoneId`; request the new
  graph with `mapId`.
- `/game` now redirects to `/characters`; Playwright covers unauthenticated guards.
- Live smoke (local DB, migrated legacy user + seeded character `Thornwarden`):
  CharacterSelected → ZoneEntered (Aldenmere, 1 occupant) → MapGraphReceived (4 nodes) →
  TravelToNode (The Grey Cup) → InventoryLoaded (overlay true), no hub errors.
- Local smoke scripts live in `scripts/dev/` (gitignored). The SignalR package must be loaded via
  `createRequire` from `web/node_modules/@microsoft/signalr/dist/cjs/index.js` because the ESM
  build uses extensionless imports Node cannot resolve.

**Phase 2 start**
- `CardinalGraphLayout` is ported to `web/client/src/lib/game/cardinal-layout.ts`; all 13 C#
  layout tests were mirrored in `cardinal-layout.test.ts` and pass unchanged. `CardinalMap.svelte`
  renders the computed grid (current/reachable/known, direction hints, entry/exit badges).
- Browser SignalR CORS gotcha: the negotiate preflight includes `x-signalr-user-agent`. The
  `AllowLocalClient` policy now allows it, and origins come from `Cors:AllowedOrigins` with
  fallback local dev origins + `Web:BaseUrl`. Regression tests live in `CorsTests`.
- Playwright UI smoke with a real session confirmed the play page renders the map, enters the
  zone, opens the inventory drawer, and produces no page errors.

**Phase 2 shell (complete)**
- Game UI components under `web/client/src/lib/game/`: `GameHeader`, `GameFooter`, `LeftSidebar`,
  `RightSidebar` (Zone/Global/Whisper chat), `CombatPanel`, `GameOverlays` (inventory/shop/quests/
  settings), `CardinalMap`.
- `GameSession` now tracks stats, occupants, location/enemies, chat buffer, shop catalog, quests,
  combat log, death, and overlays. Pure math lives in `reducers.ts` (`appendChat`,
  `describeCombatTurn`, stat reducers, `barPercent`).
- Hub methods wired: SelectCharacter, EnterZone, GetMapGraph, TravelToNode, GetInventory,
  EquipItem, GetQuestLog, SearchArea, RestAtLocation, LeaveZone, VisitShop, GetShopCatalog,
  BuyItem, SellItem, DropItem, EngageEnemy, AttackEnemy, DefendAction, FleeFromCombat,
  UseAbilityInCombat, Respawn, SendZoneMessage, SendGlobalMessage, SendWhisper.
- Live hub smoke: chat round-trip, inventory, quest log (empty), search, rest (CharacterRested),
  shop catalog (23 items), no errors. Seeded Aldenmere spawns no enemies, so combat is covered by
  reducer/component tests rather than a live fight.
- Browser smoke: map, inventory, quests, settings, chat send — zero page/console errors.
- Vitest now has 31 tests (layout 13, reducers 10, components 4, password 4).
  Playwright still covers unauthenticated guards; authenticated flows need the seeded local stack.

## SvelteKit Migration — Phase 3 Public Site + Account (Session-58, 2026-10-23)

**Public site (complete)**
- SSR routes: `/` (announcements + latest lore + patch notes + server status), `/lore`, `/lore/[slug]`,
  `/patch-notes`, `/patch-notes/[slug]`, `/community`, `/players/[id]`.
- `editorial-api.ts` server client calls `/api/editorial/*` and `/api/players/*` using
  `INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL`. The editorial endpoints return the .NET
  `PagedResult<T>` serialized as `{ items, totalCount, page, pageSize }` — use `totalCount`, not
  `total`. Announcements are paged too (not a bare array); the home loader slices
  `announcements.page.items`.
- Markdown: `marked` + `sanitize-html` in `$lib/server/markdown.ts`; anchors get
  `rel="noopener noreferrer"`. Detail pages use `{@html}` with a targeted
  `svelte/no-at-html-tags` disable because the server sanitizes. `<script>` payloads are stripped.
- Account provider query moved to `$lib/server/account.ts`; routes must not import
  `@veldrath/shared/db` directly (eslint `no-restricted-imports` DB boundary).
- Blazor public routes intentionally remain until Phase 6; SvelteKit is the canonical site.

**Live verification (local stack)**
- Seeded `public."LoreArticles"`, `"PatchNotes"`, `"EditorialAnnouncements"` with `Status=1`
  (Published). Home/lore/patch-notes/community render the seeded titles; detail pages render the
  sanitized markdown; `/players/<kungraseri guid>` renders 200 with username.
- Account flow with a fresh Better Auth user: sign-up 200 → `/account` shows username +
  `Email & password` provider chip → change password 200 → `/api/foundry-bridge/register`
  `{"synced":true}` → sign-out 200 → Better Auth old password 401 / new 200 → .NET
  `/api/auth/login` old 401 / new 200.
- Gotcha: Better Auth rejects cookie-authenticated POSTs without an `Origin` header (403 CSRF).
  Browsers always send it; curl smoke scripts must pass `-H "Origin: http://localhost:5173"`.
- `npm run format`, `typecheck`, `lint`, `build` all pass; Vitest 31; Playwright 10 (home,
  lore, patch notes, community, forgot password, account guard, game guards).
- Temporary test accounts `phase3-*`, `phase3b-*` and the editorial seed rows exist only in the
  local dev DB (not committed).

## SvelteKit Migration — Phase 4 Staff Portals (Session-59, 2026-10-23)

**Route groups (complete)**
- `(foundry)`: `/submissions` (filter/search/paging), `/submissions/[id]` (`id='new'` = create;
  vote and Curator review actions), `/notifications`, `/content`, `/content/[type]`,
  `/content/[type]/[slug]`, `/languages`, `/languages/new`.
- `(editorial)`: `/editorial` dashboard plus lore, patch-notes, and announcements lists and
  `[id]` editors (`id='new'` = create; save/publish/delete actions).
- `(admin)`: `/admin`, `/admin/users`, `/admin/users/[id]` (roles, permissions, kick/warn/mute/
  ban/unban), `/admin/roles`, `/admin/audit`, `/admin/sessions`, `/admin/reports`,
  `/admin/status`, `/admin/server` (broadcast).
- `(mod)`: `/mod`, `/mod/users`, `/mod/users/[id]` (kick/warn/mute/unmute), `/mod/reports`.
- Public `/players/[id]` already covered the old Foundry player profile (no report form existed).

**Server API layer**
- `game-request.ts` mints a game token and calls `INTERNAL_BRIDGE_URL ?? PUBLIC_API_URL` with
  Authorization, query building, 8s timeout, 204 handling, and `{error}` extraction.
- `foundry-api.ts`, `content-api.ts`, `admin-api.ts` and editorial admin functions in
  `editorial-api.ts` are typed wrappers. Response shapes differ: Foundry/Editorial use
  `items/totalCount`, admin uses `items/total`.
- `access.ts` loads `GET /api/account/profile` (returns roles + permissions), exposes
  `Roles`/`Permissions` constants, and special-cases the Admin role in `hasPermission`.
- Root `+layout.server.ts` returns `{ user, access, staff }`; route-group layouts use
  `await parent()` and enforce `admin.users`, `content.manage`, or `players.view`. `/login`
  redirect for anonymous visitors; 403 for authenticated users without the permission.

**Gotchas**
- Foundry review uses the legacy role policy `RequireRole("Curator")`, not
  `content.review`; the UI gate must check `roles.includes('Curator')`. Admins cannot review
  unless they also hold Curator.
- `FoundryContentType` was missing `Language`; it is now appended (enum value 7) with a
  FoundryEndpointTests regression test. The old Blazor language builder would have received
  "Unknown content type: language".
- SvelteKit form actions return JSON (`{type:'redirect'|'failure',status,data}`) for non-browser
  POSTs (curl), not HTTP 303; browsers get normal navigation. `%{redirect_url}` is empty in curl.
- Foundry write rate limit is 5/min per IP (`foundry-writes`); rapid create+vote+review can
  return 429 in smoke tests.
- RBAC snapshots are cached 60s by `AccountProvisioningService`; role changes in the DB take up
  to a minute to appear on new tokens.
- Test-account pattern used locally: Better Auth sign-up → `/api/foundry-bridge/register` →
  insert `AspNetUserRoles` rows (Admin, Curator) via psql → cookie session drives SSR pages.

**Verification (local stack)**
- Every gated page returned 200 with an Admin+Curator session; anonymous Playwright guards
  redirect to `/login` (5 new tests, 15 total).
- Editorial: create lore → publish → public `/lore/{slug}` 200 → delete → public 404.
- Foundry: create item submission → upvote → Curator approval → status Approved with review
  notes → notification appears → admin audit shows the review.
- Language builder submitted a `language` payload accepted by the server and rendered on the
  submission detail page. Admin broadcast action succeeded.
- Typecheck/lint/format clean, production build, 31 Vitest, 15 Playwright, 30 Foundry server
  tests.

## SvelteKit Migration — Phase 5 Desktop WebView (Session-60, 2026-10-23)

**Architecture (complete on build/test path)**
- `Veldrath.Client/HostedWeb/DesktopUiServer` replaces `HostedGameServer` (deleted). It:
  - binds Kestrel to `127.0.0.1:0` and serves the static Svelte build with
    `UseDefaultFiles`/`UseStaticFiles` + `MapFallbackToFile("index.html")`;
  - exposes `/health`;
  - proxies `/api/{**path}` and `/hubs/{**path}` (HTTP + WebSocket) to the remote server,
    injecting `Authorization: Bearer <TokenStore token>` server-side;
  - refreshes expiring tokens through `IAuthService.RefreshAsync` supplied by App DI.
- UI path resolution: `VELDRATH_DESKTOP_UI` → `SvelteApp` next to the executable →
  `web/client/build-desktop` during development. Throws a clear error when missing.
- `Veldrath.Client.csproj` copies `web/client/build-desktop/**` to output as `SvelteApp/`
  when it exists, and no longer references `Veldrath.GameClient.Components` or
  `Veldrath.Auth.Blazor`. `Microsoft.AspNetCore.App` FrameworkReference stays.
- `GameViewModel.WebViewUrl` is now `{BaseUrl}/desktop`; native Avalonia fallback unchanged.

**Svelte desktop build**
- `vite.config.ts` selects `@sveltejs/adapter-static@3` when mode is `desktop`
  (`vite build --mode desktop`, script `npm run build:desktop`), output
  `web/client/build-desktop`, fallback `index.html`. adapter-static v4 requires SvelteKit 3.
- No prerender: pure SPA fallback. Root layout uses optional chaining
  (`data?.user`, `data?.staff?.foundry`) because the fallback shell has no server load data.
  `$app/state` `page.url` hides the public header/footer on `/desktop`.
- `/desktop` (`ssr = false`) loads `/api/characters` through the same-origin proxy, then
  starts `GameSession` with `{ hubUrl: '/hubs/game' }`. `GameHubOptions.hubUrl` overrides the
  env URL and omits `accessTokenFactory` so the proxy supplies auth.
- `GameScreen.svelte` extracted from `/play/[id]` and shared by both routes.

**Gotchas**
- Do NOT run `vite build` while `vite dev` is running: both use `.svelte-kit/output`, which
  produced a `.fuse_hidden*` chunk, an hour-long stuck prerender, and
  `ENOENT ... .fuse_hidden` during the static adapter walk. Stop the dev server first.
- The static SPA fallback serves `/desktop` at any path; client-side routing handles it.
- WebView2 execution cannot be tested on Linux; `DesktopUiServer` integration tests cover
  static hosting, fallback routing, token-injecting proxy, 401, and shutdown on the build host.

**Verification**
- `npm run build:desktop` writes `build-desktop/index.html`.
- `Veldrath.Client.Tests`: **677 passing**, including 7 `DesktopUiServerIntegrationTests`.
- Chromium smoke of the static bundle with a mocked `/api/characters`: character list renders,
  public nav hidden, clicking a character renders the game shell; hub negotiation fails only
  because the smoke server intentionally has no `/hubs` proxy.
- Remaining release check: Windows WebView2 runtime smoke with a live desktop session.

## SvelteKit Migration — Phase 6 Decommission (Session-61, 2026-10-23)

**Deleted**
- Projects: `Veldrath.Web`, `Veldrath.Web.Tests`, `RealmFoundry`, `RealmFoundry.Tests`,
  `Veldrath.Auth.Blazor`, `Veldrath.GameClient.Components`,
  `Veldrath.GameClient.Components.Tests`.
- Solutions: `Veldrath.Web.slnx`, `RealmFoundry.slnx`.
- Version props: `versions/web.props`, `versions/foundry.props`.
- CI: `.github/workflows/ci-foundry.yml`.
- Docs/assets: `llms.txt`, `.github/instructions/blazor-component-development.md`,
  `.github/instructions/styling-and-css.md`, `.roo/skills/run-realmfoundry/`.
- Package versions: MudBlazor, bunit, Markdig, HtmlSanitizer, AngleSharp, AngleSharp.Css.
- `Veldrath.Auth.Tests/AuthStateServiceBaseTests.cs` and its `Veldrath.Auth.Blazor` reference.

**Kept deliberately**
- .NET auth endpoints (`/api/auth/*`) and the internal SvelteKit → Identity bridge
  (`InternalBridge:Token`, `/api/internal/accounts/sync`, SvelteKit
  `foundry-bridge.ts`): the desktop client's native fallback still signs in through
  ASP.NET Core Identity. The bridge is now documented as desktop compatibility, not
  a Foundry transition.
- `Foundry:BaseUrl` config is optional; `ExternalAuthEndpoints` falls back to `Web:BaseUrl`.

**Updated**
- `Realm.Full.slnx`, `Veldrath.slnx`, `Veldrath.Client.slnx`, `Veldrath.Libraries.slnx`,
  and `RealmForge.slnx` no longer list the deleted projects. `.NET` solution now has
  22 projects (13 source + 9 test).
- Compose: removed the `foundry` service, `foundry_dataprotection_keys` volumes, and
  `Foundry__BaseUrl` env from base/dev/prod/coolify files. Kept the `web` service.
- CI: `ci-web.yml` also runs `npm run build:desktop` and uploads both builds;
  `ci-client.yml` installs Node and builds the desktop bundle before the .NET build so
  `SvelteApp/` is included. `release.yml` and `deploy.yml` no longer reference Foundry,
  and the Caddy reload step was removed from deploy.
- VS Code: removed Veldrath.Web/RealmFoundry launch/tasks; added `web:dev`, `web:build`,
  `web:desktop`, `web:test`, a `Web (SvelteKit)` launch config, and an updated full-stack
  compound.
- `AGENTS.md`, `README.md`, build-strategy/testing/xml-doc/ef-core instructions,
  deployment docs, design-system banner, and test/run skills updated for the SvelteKit
  layout.

**Verification**
- `dotnet build Realm.Full.slnx`: 0 warnings, 0 errors.
- Full `dotnet test Realm.Full.slnx`: **4,617 passing** across 9 test projects.
  `ApplyStatusEffectTests.Should_Not_Apply_Same_Effect_Twice_Without_Stacking` failed once
  in the first post-deletion run and passed in isolation and in the final full rerun — a
  pre-existing flake, not related to Phase 6.
- SvelteKit: typecheck, lint, 31 Vitest, 15 Playwright, `npm run build`, and
  `npm run build:desktop` pass.
- Compose files remain valid; `.vscode` JSONC files validated.

## SvelteKit Account + Character Lifecycle Completion (Session-62, 2026-10-24)

**Transactional email**
- Better Auth now defines `emailAndPassword.sendResetPassword` and
  `emailVerification.sendVerificationEmail` / `sendOnSignUp` in
  `web/client/src/lib/server/auth.ts`. `requireEmailVerification` stays `false` so migrated
  accounts are not locked out before they confirm.
- `web/client/src/lib/server/email.ts` forwards messages to the .NET `IEmailSender` via
  `POST /api/internal/email/password-reset|verification`, guarded by
  `X-Internal-Bridge-Token`. When `INTERNAL_BRIDGE_URL`/`INTERNAL_BRIDGE_TOKEN` are absent
  (local web-only dev), the helper logs the action link instead of sending.
- Links are rebuilt to the SvelteKit pages (`/reset-password?token=...`,
  `/verify-email?token=...`); the Better Auth tokens are self-contained JWTs.
- The repository `.env` has a real `EMAIL_SMTP_HOST`, so run the server with
  `Email__SmtpHost=` to force `NullEmailSender` and capture links from the console/log.
- `changeEmail` is enabled (`user.changeEmail.enabled = true`); verified accounts receive a
  confirmation link at the new address and the change applies when it is clicked.

**Identity bridge**
- `/api/internal/accounts/sync` now updates username, email, `EmailConfirmed`, and
  `DisplayName` on existing Identity rows. Previously JIT provisioning only created rows, so
  profile/email changes never reached the .NET account. The request contract added optional
  `DisplayName`.
- `hooks.server.ts` re-syncs when the `username|email|emailVerified` snapshot changes
  instead of only once per process. The account page also calls
  `/api/foundry-bridge/account` immediately after profile edits, passing the new username and
  display name.
- Note: the 60-second `AccountProvisioningService` access-snapshot cache is independent of
  the bridge writes; the bridge calls `ClearCache()` after each sync.

**Character lifecycle**
- `/characters/new` is a seven-step wizard (name, class, species, background, point-buy
  attributes, equipment, difficulty/review) that calls authenticated SvelteKit routes under
  `/api/characters/creation/*`; those forward to the .NET creation-session API using
  `character-api.ts`. The game token never reaches the browser.
- Roster (`/characters`) shows slot usage from `GET /api/characters/slots`, a
  resume-last-session card (`/api/characters/last-session`), a delete form action
  (`?/delete`), and a proper empty state.
- `CharacterSlotsDto(Used, Max, Remaining)` and `GET /api/characters/slots` were added;
  production/development accounts currently have `MaxCharacterSlots = 5`.
- The engine `SetCreationNameHandler` now accepts letters **and spaces**
  (`Character name may only contain letters and spaces.`), matching the server endpoint and
  both UI clients. The previous letters-only rule made the wizard's name step fail for
  names like `Live Hero`.

**Live verification (2026-10-24)**
- Register → verification email → verify; request reset → reset link → set new password →
  sign in with username.
- Create a normal warrior and a hardcore mage (name with a space), preview attributes,
  finalize, enter the world over SignalR (WebSocket, zone, 8-node map, character header),
  edit display name/username and see Identity update, change email + verify + Identity sync,
  list/revoke sessions, delete characters, confirm empty roster and slot reset.
- Web checks: 56 Vitest, 22 Playwright, typecheck, lint, `npm run build`, `npm run build:desktop`.

## SvelteKit Theme + Asset Parity (Session-63, 2026-10-24)

**Theme**
- `web/client/src/routes/theme.css` now maps Skeleton v5 tokens to the VDS palette from
  `docs/design-system.md` and the deleted `VeldrathTheme.cs`: Crimson Seal primary, Gold
  secondary, Emberfall tertiary, warm charcoal surface scale, 4/6px radii.
- `web/client/src/routes/brand.css` adds self-hosted `@font-face` rules, VDS typography
  utilities, base page chrome, site header/footer styles, auth cards, game shell/panels, map
  connector lines, rarity colours, and OAuth button variants.
- `web/client/src/app.html` forces `data-mode="dark"`, adds the favicon/manifest set, preloads
  Cinzel and Lora, and loads Inter from Google Fonts with a system fallback.
- `src/layout.css` was previously unused; `routes/layout.css` now imports it, so rendered
  Markdown (lore/patch notes) finally gets themed headings, code/table/blockquote styles.

**Assets**
- Restored all `Veldrath.Web/wwwroot/images/**` files (logos, crests, herald emblems,
  favicons, `site.webmanifest`) into `web/client/static/images/**`; the manifest now points at
  `/images/favicon/...` and uses `#0C0D13`.
- Copied `RealmUI.Fonts/Assets/Fonts/*.ttf` (Cinzel, Cinzel Decorative, Lora, Lora Italic,
  JetBrains Mono) to `web/client/static/fonts/`.
- `web/client/src/lib/brand-icons.ts` vendors the Discord/Google/GitHub SVG paths. Do **not**
  import from `simple-icons` root in SvelteKit — it pulls the entire icon set through the SSR
  transform (thousands of extra modules).

**UI parity**
- New `src/lib/components/site/SiteHeader.svelte` and `SiteFooter.svelte` restore the brand
  logo, primary nav, staff links, account/Play actions, and the fat footer. Both are hidden on
  `/play/*` and `/desktop`, which use the full-screen game shell.
- Game components (`GameScreen`, `GameHeader`, `GameFooter`, `LeftSidebar`, `RightSidebar`,
  `CombatPanel`, `GameOverlays`, `CardinalMap`) now use Lucide icons, VDS panels, map
  connectors, rarity classes, and a compact 20px character heading.
- Auth pages use the crest, `auth-card`, icon inputs, and brand-coloured OAuth buttons; account,
  roster, wizard, lore, patch notes, and community pages use crests/section icons and Cinzel
  headings.

**Gotchas**
- Importing `Map` from `@lucide/svelte` shadows the global `Map` constructor — import it as
  `Map as MapIcon` (hit in `CardinalMap.svelte`).
- Skeleton's `.h1/.h2/.h3` utilities reset the family; unlayered heading rules at the end of
  `brand.css` restore Cinzel **and** `--vds-text` colour (otherwise linked headings inherit the
  seal-red link colour).
- Keep game-shell pages out of the public layout; otherwise the footer renders below the
  full-screen game and the public header consumes vertical space.

**Verification**
- Playwright visual smoke at 1600×1000: home, login, lore, roster, account, wizard, and live
  game. Computed body background `rgb(12,13,19)`, Cinzel headings, loaded logos/crests,
  8-node map with 11 connector lines, 10 header icons, 7 sidebar icons, 3 footer icons, and no
  site header/footer on `/play`.

## Repository Config Consolidation (Session-64, 2026-10-24)

**Removed nested config (single source of truth is now the repo root)**
- `web/.github/` (template `node.yml` + CI-CD-DASHBOARD.md) — superseded by `.github/workflows/ci-web.yml`.
- `web/.roo/skills/` — merged into root `.roo/skills/` as `web-development` and `web-ui`;
  catalog added at `.roo/skills/README.md`.
- `web/.gitignore` and `web/client/.gitignore` — patterns folded into the root `.gitignore`.
- `web/AGENTS.md` (stale Colyseus/template guidance) and `web/TEMPLATE-GUIDE.md` — content merged
  into the root `AGENTS.md` → **Web App (SvelteKit)** section.
- `web/scripts/ci-summary.mjs` — only used by the deleted template workflow.

**Workflow/config fixes**
- `.github/dependabot.yml` had an empty `package-ecosystem` (invalid); now configures nuget,
  npm (`/web`), github-actions, and docker with grouped weekly updates. *(File deleted — Forgejo
  runs no bot; the cadence now lives in [`docs/ci-forgejo.md`](../../docs/ci-forgejo.md).)*
- `.github/workflows/sonarcloud.yml` was missing a checkout and used the deprecated
  `sonarcloud-github-action`; now checks out with `fetch-depth: 0` and uses
  `SonarSource/sonarqube-scan-action@v5`.
- `ci-web.yml` gained a `deploy` job calling `deploy.yml` with `service: web` (disabled via
  `if: false`, matching server/discord until the VPS exists).
- `docs.yml` API landing table no longer claims RealmEngine.Data uses JSON/LiteDB.

**Gotchas**
- Deleting `web/client/.gitignore` broke `includeIgnoreFile` in `web/client/eslint.config.js`;
  that config now declares generated-output ignores (`.svelte-kit`, `build`, `build-desktop`,
  coverage, Playwright artifacts) explicitly. Keep this in sync if more outputs appear.
- All web scripts run from `web/` (workspaces root); `npm run test` there runs 56 Vitest tests and
  22 Playwright tests, and the Playwright webServer performs the production `vite build`.
- `.NET` solution `Realm.Full.slnx` has 22 projects: 12 source + 10 test projects.

### Dependency consolidation (Session-64)

Dependabot PRs #9–#15 were consolidated into `main` instead of merged individually:

- **Applied:** the `actions` group (checkout v7, setup-node v7, setup-dotnet v6, upload-artifact v7,
  download-artifact v8, codecov v7, docker buildx v4 / build-push v7, pages v5, SonarQube scan v8),
  the `web-minor-patch` group, `vitest`/`@vitest/coverage-v8` **5.0.5**, `vite` **7.3.7** (security
  patch), and `@microsoft/signalr` **10.0.11**.
- **Deferred:** `@sveltejs/adapter-static` 4.0.0 (and `adapter-node` 6) require `@sveltejs/kit` **3**;
  Vite 8 requires `@sveltejs/vite-plugin-svelte` **7**. Both are Kit-3 migration work — do not bump
  them independently, and do not merge the `npm_and_yarn` PR that pairs Vite 8 with plugin-svelte 6.
- `svelte/no-navigation-without-resolve` is disabled in `web/client/eslint.config.js`: the app is
  served from the domain root (`paths.base` unset), so plain hrefs are correct. Re-enable and adopt
  `resolve()` from `$app/paths` before introducing a base path.
- After the bumps: `npm audit` reports 8 low/moderate advisories (SvelteKit 2.x URL/cache advisories,
  `runed`/`svelte-toolbelt`, and `@tailwindcss/typography` → `postcss-selector-parser`); all clear
  with a SvelteKit 3 / Tailwind typography refresh.

### SvelteKit 3 + Vite 8 migration (Session-64)

The web app now runs SvelteKit **3.1.0**, Vite **8.3.5**, `@sveltejs/vite-plugin-svelte` **7**,
`adapter-node` **6**, `adapter-static` **4**, and `#lib` subpath imports.

- `$lib` is removed in Kit 3. Imports now use `#lib/*`, backed by `web/client/package.json`
  `"imports": { "#lib/*": "./src/lib/*" }` plus `paths` in `web/client/tsconfig.json` for
  TypeScript (the package `imports` map alone is not picked up by TS here).
- `resolve()` is now typed against the generated `RouteId` union, which **includes route groups**
  (e.g. `/(mod)/mod`). Dynamic routes use `resolve('/play/[id]', { id })`.
- `$app/environment` is now `$app/env`; `Handle` moved to `@sveltejs/kit/hooks`.
- `web/client/tsconfig.json` extends **`$app/tsconfig`** (generated in
  `node_modules/$app/tsconfig.json`) with an explicit `include` (`src`, `tests`, config files) and
  the `#lib` paths mapping. Extending `./.svelte-kit/tsconfig.json` still works but Kit warns
  `tsconfig_extends_missing` on every dev start.
- Public env vars are declared in `web/client/src/env.ts` via `defineEnvVars` and imported by name
  from `$app/env/public` (`PUBLIC_API_URL`, `PUBLIC_HUB_URL`). `$env/dynamic/*` is legacy in Kit 3.
- **Public env vars are needed twice in containers:** inlined into the browser bundle at build time
  (`--build-arg`) *and* validated from `process.env` when `node build` starts. An image built with
  the build args but started without runtime env vars originally died with
  `SvelteKit error: env_invalid … PUBLIC_API_URL Value is missing`; fixed by re-declaring the ARGs in
  the runtime stage as `ENV` defaults in `web/client/Dockerfile` (build args alone now boot fine).
  Optional runtime overrides must match the build values or the browser bundle and SSR disagree.
- `+layout.server.ts` / `+page.server.ts` load functions now annotate `LayoutServerLoad` /
  `PageServerLoad` from `./$types`; raw `tsc` (not just svelte-check) needs those annotations.
- `$env/dynamic/private` is snapshotted by the Kit 3 + Vite 8 (Rolldown) build and gets
  constant-folded, so server secrets must come from `process.env`. `#lib/server/env.ts` reads
  `process.env` and loads `.env` (trimming quotes) for local dev/preview; all server modules import
  `env` from there instead of `$env/dynamic/private`.
- The root `hooks.server.ts` returns `resolve(event)` early while `building` so prerendering needs
  no auth secret.
- The `cookie` override is now `^1.1.1` (adapter-node 6 needs `parseCookie`); the `vite` override
  is `^8.3.5`.
- Web e2e (`vite preview`) relies on `#lib/server/env.ts` loading `.env`; do not parse `.env` in
  `playwright.config.ts` — quoted values like `ORIGIN="http://…"` break `new URL()`.
- Remaining blocked majors are ignored in `.github/dependabot.yml`: Avalonia 12,
  FluentAvaloniaUI 3, ReactiveUI 26 / Microsoft.Reactive.Testing 7 (RxVoid), MediatR 14
  (commercial licence), TypeScript 7 (native port; tooling not ready). *(File deleted — the holds
  now live in [`docs/ci-forgejo.md`](../../docs/ci-forgejo.md); MediatR's current hold is below 13.)*

### Diagnosing `401` from the game API (Session-65)

`/characters` (and every other authenticated page) mints a game JWT in
`#lib/server/game-token.ts` (`HS256`, `iss`, `aud`, `sub` = Better Auth user id, which is a GUID
because `generateId: () => crypto.randomUUID()`), and Veldrath.Server validates it against
`Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` before `JwtClaimsEnrichment` JIT-provisions the Identity
account. A `401` therefore means the token was rejected — never "no session" (that redirects to
`/login`).

- Verified signatures (local repro, server on `:9099`, dev key from `.env`):
  - correct key → `200` in ~220 ms, body `[]`, log `JIT provisioned game account <guid> as 'probe'`;
  - wrong key → `401` in ~1 ms with
    `WWW-Authenticate: Bearer error="invalid_token", error_description="The signature key was not found"`
    and server log `JWT authentication failed: IDX10517: Signature validation failed…`.
  A sub-2 ms `401` in production is a **key/issuer/audience mismatch**, not an account problem.
- Instrumentation added: the server logs `JWT configuration: issuer=… audience=… keyFingerprint=…`
  at startup (SHA-256 prefix) plus an `OnAuthenticationFailed` warning; the web logs
  `[game-token] issuer=… audience=… keyFingerprint=…` on the first mint and
  `[game-request] <METHOD> <path> responded 401: <reason>` (reason parsed from
  `WWW-Authenticate` by `describeAuthFailure`). Compare the two fingerprints first.
- In Development the server key comes from the repo `.env` via `DotEnvConfiguration`
  (`JWT_KEY` → `Jwt__Key`), which overrides `appsettings.Development.json`; the appsettings dev
  key is ignored whenever `.env` defines `JWT_KEY`.
- `fetchCharactersForUser` now delegates to `gameApiFetch` (shared token + diagnostics) instead of
  its own fetch loop.
- `[access] could not load the account profile. An unexpected error occurred.` is the .NET 500
  ProblemDetails title (from `UseExceptionHandler` in `Program.cs`): the JWT validated, but the
  endpoint — or provisioning inside `OnTokenValidated` — threw. Root cause found: a fresh login makes
  SvelteKit fire four concurrent SSR calls (profile, characters, slots, last-session) with the same
  brand-new subject; each raced to insert the same Identity primary key and the losers rethrew
  `DbUpdateException` → 500. `AccountProvisioningService` now serializes provisioning per account
  (`ProvisionLocks` semaphore, cache re-check inside the lock) and polls up to ~200 ms for the
  winning row; `JwtClaimsEnrichment` catches provisioning exceptions, logs them, and rejects the
  token instead of surfacing a 500.
- SvelteKit gained `handleError` in `hooks.server.ts` (Kit 3 signature: `{ kind, error, event }`,
  returns void — do not destructure `status`/`message`) so server exceptions print a stack trace in
  the container log instead of vanishing into an "Internal Error" page.
- Coolify internal hostname is `veldrath-server` (set as the resource's Container Name), so
  `INTERNAL_BRIDGE_URL` must be `http://veldrath-server:8080` — `http://server:8080` is the
  docker-compose service name and does not resolve inside Coolify. Without it the account bridge is
  skipped (`[foundry-bridge] skipped`) and Identity rows only appear via JIT provisioning.
- **SignalR `Failed to complete negotiation … NetworkError`** is a browser-side CORS/transport
  problem, not a hub bug. The policy `AllowLocalClient` allows `Web__BaseUrl` (trimmed) plus
  localhost unless `Cors__AllowedOrigins` is set (indexed `__0` or one comma-separated value).
  `Program.cs` now logs `CORS allowed origins: …` at startup and **throws in Production** when the
  list contains only localhost — a silent misconfiguration otherwise. The other cause is an image
  built with the default `PUBLIC_HUB_URL=http://localhost:9000/hubs/game`, which an `https://` page
  blocks; `createGameHub` throws a descriptive error for that case.

**Security note:** a GitHub PAT was briefly committed inside `.roo/mcp.json` (`6648b6c5`) and the
file was deleted in `85bb8492`. Never commit MCP tokens — pass them via environment variables, and
treat any token that landed in git history as compromised (rotate it).

### Environment variable audit (Session-66)

Authoritative inventory — anything not listed is not read by code.

- **Veldrath.Server** (`Configuration[...]`): `ConnectionStrings:DefaultConnection`,
  `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` / `Jwt:AccessTokenExpiryMinutes` /
  `Jwt:RefreshTokenExpiryDays`, `OAuth:{Discord,Google,Microsoft}:ClientId/ClientSecret`,
  `Email:{SmtpHost,SmtpPort,EnableTls,User,Password,SenderAddress,SenderName}`,
  `Web:BaseUrl`, `Foundry:BaseUrl`, `AllowedReturnUrlHosts`, `Auth:CookieDomain`,
  `Auth:PendingLinkExpiryMinutes`, `Seq:ServerUrl`, `DataProtection:KeysPath`,
  `Cors:AllowedOrigins`, plus the `Moderation` / `VersionCompatibility` /
  `ActiveCharacterTracker` / `CharacterCreation` sections. In Development
  `DotEnvConfiguration` maps `.env` names onto these (`JWT_KEY` → `Jwt__Key`, etc.).
- **SvelteKit web**: `DATABASE_URL` (read by `web/migrate.mjs` and by the
  Drizzle/Better Auth client), `ORIGIN`, `BETTER_AUTH_URL`,
  `BETTER_AUTH_SECRET`, `JWT_KEY` / `JWT_ISSUER` / `JWT_AUDIENCE`,
  `PUBLIC_API_URL` / `PUBLIC_HUB_URL` (inlined at build + validated at runtime),
  `INTERNAL_BRIDGE_URL` (falls back to `PUBLIC_API_URL`), `INTERNAL_BRIDGE_TOKEN`,
  `{DISCORD,GOOGLE,MICROSOFT}_CLIENT_ID` / `_SECRET`, `MICROSOFT_TENANT_ID`,
  and adapter-node's `PORT` / `HOST` / `PROTOCOL_HEADER` / `HOST_HEADER` /
  `ADDRESS_HEADER` / `XFF_DEPTH`.
- **Removed as dead (Session-66):** `Database__Provider` / `Database:Provider` (never
  read — every DbContext is `UseNpgsql`), `CERT_PASSWORD` (no consumer anywhere),
  `FOUNDRY_PUBLIC_SERVER_URL` (the `.env` alias is `FOUNDRY_PUBLIC_URL`), stale
  `Web__BaseUrl` defaults of `:8080` / `:8082` in the compose files (web is `:8081`),
  and the `http://localhost:8080` default CORS origin in `Program.cs` (now `:8081`).
- **Must be identical on server + web:** `JWT_KEY` ↔ `Jwt__Key`,
  `JWT_ISSUER`/`JWT_AUDIENCE` ↔ `Jwt__Issuer`/`Jwt__Audience`,
  `INTERNAL_BRIDGE_TOKEN` ↔ `InternalBridge__Token`, and the OAuth client
  id/secret pairs. Web-only: `MICROSOFT_TENANT_ID`, `BETTER_AUTH_SECRET`,
  `INTERNAL_BRIDGE_URL`, proxy headers. Server-only: `Auth__CookieDomain`,
  `Email__*`, `Seq__ServerUrl`, `Web__BaseUrl`, `AllowedReturnUrlHosts`.
- `docs/coolify-deployment.md` §0 carries the same matrix for humans.

### Session-67 (2026-10-24) — Payload contract guard, bundle smoke, decommission tail

- **The hub wire is the contract, not the payload records.** `GameHub.cs` sends anonymous objects;
  `Veldrath.GameClient.Core/Payloads/*.cs` and `web/client/src/lib/game/types.ts` are two
  hand-maintained mirrors of them. `web/client/src/lib/game/payload-contract.test.ts` now fails when a
  mirrored payload's property sets diverge. Check GameHub first when it fires.
- **Wire shapes worth remembering** (they are *not* the old record shapes):
  `LocationEntered` → `{CharacterId, LocationSlug, LocationDisplayName, TypeKey, SpawnedEnemies[]}`;
  `CharacterRested` → `{CharacterId, LocationId, CurrentHealth, MaxHealth, CurrentMana, MaxMana, GoldRemaining}`;
  `AbilityUsed` → `{CharacterId, AbilityId, ManaCost, RemainingMana, HealthRestored}`;
  `AreaSearched` → `{CharacterId, RollValue, AnyFound, Discovered[]}`;
  `ShopCatalog` → `{ZoneId, Items, Catalog}` (Avalonia reads `Items`, web reads `Catalog`).
- **Desktop bundle gate**: `Veldrath.Client.Tests/HostedWeb/DesktopBundleSmokeTests.cs`
  (`[Trait("Category","Bundle")]`) serves the real `web/client/build-desktop` through
  `DesktopUiServer`. It no-ops locally without the bundle; `ci-client.yml` builds it first.
- **Server startup split**: `Infrastructure/CorsSetup.cs` and `Infrastructure/AuthSetup.cs`
  (`AddVeldrathIdentityAndAuth`) hold the CORS and Identity/JWT/OAuth registration; `Program.cs` is now
  ~520 lines. The rate limiter and health checks are still inline.
- **Docs gate**: `node scripts/check-markdown-links.mjs` (also run by `repo-hygiene.yml`) validates
  relative links, understands `path:line`, and skips `web/llms/` + the `wiki/` submodule. It found 18
  dead links after the plan prune — run it before touching docs.
- Full verification after this session: `dotnet build Realm.Full.slnx` + `dotnet test Realm.Full.slnx`
  = 4,630 tests, 0 failures; web `typecheck`/`lint`/`test` (22 passed, 1 skipped) all green.

### Session-68 (2026-10-25) — Desktop client deleted, Veldrath is web-first only

- **There is no .NET client any more.** `Veldrath.Client`, `Veldrath.Client.Tests` (681 tests),
  `Veldrath.GameClient.Core` + tests, and `Veldrath.Auth` + tests are gone, along with
  `Veldrath.Client.slnx`, `Veldrath.slnx`, `versions/client.props`, `ci-client.yml`, and the `client`
  Codecov flag. Do not look for `GameViewModel`, `DesktopUiServer`, `GameWebView`, or the `/desktop`
  route: the SvelteKit app in `web/` is the only client.
- **Remaining .NET solutions:** `RealmEngine.slnx` (engine), `Veldrath.Server.slnx`,
  `Veldrath.Discord.slnx`, `Veldrath.Libraries.slnx` (assets/contracts/fonts/engine),
  `RealmForge.slnx`, `Realm.Full.slnx`. 7 test projects, 3,868 tests.
- **Better Auth only, but the server still owns email.** The account half of the bridge was deleted
  (`InternalAccountBridgeEndpoints`, `foundry-bridge.ts`, `/api/foundry-bridge/*`, the sync calls in
  `hooks.server.ts`, `/register`, `/account`). The **email half stays**: Better Auth relays
  verification/reset messages to `InternalEmailEndpoints` via
  [`email.ts`](../../web/client/src/lib/server/email.ts) so there is one SMTP configuration and one
  set of templates, and `INTERNAL_BRIDGE_URL`/`INTERNAL_BRIDGE_TOKEN` remain in use. Do not move mail
  into the web app without a reason — that was tried and reverted.
- **Payload guard repointed at the wire.** `payload-contract.test.ts` now parses `GameHub.cs`
  directly (unioning multi-variant events like `CombatTurn`) instead of the deleted C# mirrors, and it
  caught two real mismatches during the deletion: `InventoryLoaded` and `ShopCatalog` were still
  sending Avalonia-only `Items`/`Equipped` projections, which are now removed.
- **JWT audience is still the string `Veldrath.Client`** in `appsettings*.json`, `docker-compose*.yml`,
  and `ci-web.yml`. Rename only as a coordinated change to both Coolify resources (a mismatch is an
  opaque `401`).
- Web build is `adapter-node` only now; `build:desktop`, `adapter-static`, and `GameHubOptions` were
  removed with the shell.

### Session-69 (2026-10-25) — Identity auth surface removed; RBAC stays on the server

- **The `/api/auth/*` group is gone.** `AuthEndpoints`, `AuthService`, `AuthDtos`,
  `AuthExchangeCodeService`, `ExternalAuthEndpoints`, `AccountLinkService`, `PendingLinkEndpoints`,
  `PasswordResetEndpoints`, `EmailConfirmationEndpoints`, `SessionEndpoints`,
  `PendingLinkCleanupService`, the `PendingLinkToken`/`RefreshToken` entities, and their repositories
  were deleted. `GET /api/account/profile` is the only account route left — `/api/internal/email/*`
  is the token-guarded email relay Better Auth calls.
- **Identity stays, as the server-owned RBAC store.** `PlayerAccount : IdentityUser<Guid>`,
  `AspNetUserRoles`, and `RolePermissionResolver` are still the source of roles/permissions;
  `JwtClaimsEnrichment` provisions the row just in time from Better Auth claims. `AuthSetup` now
  registers Identity + JWT + authorization only — `AddOAuthProviders` is deleted, so the server
  registers **no** social providers and reads no `OAuth:*` config.
- **Migration `20261025031857_RemoveDesktopAuthTables`** drops `PendingLinkTokens` and `RefreshTokens`.
  Also removed: the `auth-attempts`/`jwt-renewal` rate-limit policies, the `Auth:CookieDomain`
  production guard, and the `OAUTH_*` / `FOUNDRY_PUBLIC_URL` / `ALLOWED_RETURN_URL_HOSTS` `.env`
  aliases. `Jwt:RefreshTokenExpiryDays` went with the entity.
- **Tests authenticate without the deleted endpoints.** `WebAppFactory.CreateAccountTokenAsync(
  username, params string[] roles)` creates the Identity row, ensures the roles exist and are
  assigned, and mints a Better-Auth-shaped HMAC JWT (`Veldrath.Test` issuer/audience) so the
  integration suite still exercises JIT provisioning + RBAC. Eight test files were rewired; the
  endpoint tests for the deleted routes were deleted. Server suite: **675 tests green**.
- **Wire-contract reminder:** the web only ever sees `GET /api/account/profile` of this area, and it
  reads `{ accountId, username, displayName, email, roles, permissions }`.

### Session-70 (2026-10-25) — HUD rework: character sheet, world column, dialog overlays

- **Layout is now sheet / world / social.** Top bar = identity + zone breadcrumb + connection dot +
  `Characters`. Left = the full character sheet (HP/MP, gold/XP, 8-slot equipment rack, bag list,
  menu). Center = map → node detail → `Local` (enemies + Search/Rest/Merchant/Leave zone); combat
  replaces that block. Right = chat + "Also here". `GameFooter.svelte` was deleted — it duplicated the
  header (connection, map, occupants).
- **Drawers became centered dialogs.** Quest log / Shop / Settings are Bits UI `Dialog`s styled by
  `.game-dialog` + `.game-dialog-backdrop` in `brand.css`; `GameOverlays.svelte` owns all three. There
  is no inventory overlay any more — the bag lives in the sheet, and `GameSession.showInventory` was
  removed (`InventoryLoaded.openOverlay` is still sent but ignored).
- **Bag actions are per row**: Equip (only when `slot` is present), Unequip, Sell (merchant only) and
  Discard, which is a two-step inline confirm → `DropItem` (permanently removes one unit; no ground
  drop exists yet).
- **`ItemRarity`/`ItemType` serialised as numbers.** The old bag read `rarity: 2`, so `rarityClass()`
  matched nothing and every item rendered muted, and `itemSubtitle()` printed `Rare · 2`. Fixed by
  sending the flat `InventoryItemView` projection with string `Rarity`/`Type` instead of the raw
  `Item` graph (which also dropped the socket/enchantment payload).
- **Equip needs no slot from the client.** `EquipItemHubRequest(string? Slot, string? ItemRef)`; the
  server derives the slot from the item type when `Slot` is omitted.
- **Tests**: `components.test.ts` rewritten around `GameHeader` + `LeftSidebar` (vitals, rack, bag
  badges/actions, menu) and the two dialogs; `types.test.ts` covers `rarityClass` / `itemSubtitle` /
  `isEquippable` / `EQUIPMENT_SLOTS`. Web: 69 unit tests + 22 e2e. Server: 677 tests.
- **Known gameplay gaps surfaced by this pass** (next candidates): no `UseItem` for consumables, no
  real ground drop, two-handed weapons do not clear `OffHand`, `Shoulders`/`Bracers`/`Gloves`/`Belt`
  have no slots, unspent attribute points can be shown but not allocated from the sheet.

### Session-71 (2026-10-25) — Gameplay gaps closed: items, ground drops, quests, attributes, abilities

- **The bag loads on entry.** `selectCharacter` now requests `GetInventory` after `CharacterSelected`
  and stores `attributes` from that payload, so the sheet renders vitals, rack and bag without a
  refresh. `showInventory` is gone; the sheet is the only inventory surface.
- **`InventoryItemView` grew** to `Effect`/`EffectPower`, `IsTwoHanded`, `ArmorClass`/`ArmorType`/
  `WeaponType`, `DamageMin`/`DamageMax`, `RequirementsLevel`/`Requirements`, `Bonuses`. That feeds the
  item detail dialog (damage, armour, durability, effect, requirements, bonuses, value, stack).
- **`UseItem` command + hub** (`ItemUsed`): `heal`, `heal_overtime`, `restore`, `cure_poison` consume
  one unit, edit the Attributes blob, and the hub then refreshes the bag. Unknown effects fail loudly.
- **Two-handed weapons** own both hands: equipping one frees `OffHand` (`EquipItemHubResult.ClearedSlots`
  → `ItemEquipped.ClearedSlots`), and off-hand items are rejected while a two-handed main hand is worn.
- **Twelve equipment slots**: `Shoulders`, `Bracers`, `Gloves`, `Belt` joined the eight in
  `EquipmentSlots` + `ValidSlots` + `EQUIPMENT_SLOTS` (web order: MainHand, OffHand, Head, Shoulders,
  Chest, Bracers, Gloves, Belt, Legs, Feet, Ring, Amulet).
- **GroundItems table** (zone + location keyed, quantity, durability, dropped-by) with commands
  `GetGroundItems` / `DropItemToGround` / `PickUpGroundItem` and the `GroundItemsChanged` broadcast.
  Drop is *not* destroy: `DropItem` still permanently removes. The client refreshes the ground list on
  every `LocationEntered`.
- **Quests are real now**: `Character.QuestBlob` journal, `QuestJournal.Advance(kind, key)`, and
  `AcceptQuest` / `AbandonQuest` / `TurnInQuest`. Objective keys are `kill:<enemy>`, `collect:<item>`,
  `visit:<location>`; the hub advances them from `AttackEnemy`/`UseAbilityInCombat` (`EnemyName` was
  added to both results), `BuyItem`, `PickUpGroundItem` and `NavigateToLocation`. Turn-in grants gold/XP
  via `AddGoldHubCommand` / `GainExperienceHubCommand` so level-ups stay in one place. The old
  save-game-backed quest log is gone (server characters never had a save game).
- **`GameHub` payload gotcha for the contract test**: `payload-contract.test.ts` only reads
  `Name = value` properties and inline `new { ... }` payloads. Member shorthands (`result.Items`) and
  `new SomeRecord(...)` are invisible to it — write explicit `Name = ...` assignments or inline
  anonymous objects, otherwise the guard reports "not on the wire".
- **Attributes + abilities**: the sheet's attribute strip opens a dialog that accumulates points and
  submits capitalised keys to `AllocateAttributePoints`; `AttributePointsAllocated` updates the values.
  The abilities dialog lists `learnedAbilities` and casts them out of combat via `UseAbility`.
- **Search results** are kept in `lastSearch` and rendered in the Local panel; a discovery that matches
  a reachable node offers travel.
- Verification: 693 server tests, 79 web unit tests, 22 Playwright specs, dotnet format clean.

### Session-71b (2026-10-25) — Bag rows and the item tooltip

- **Bag rows are three lines now**: `.bag-row-head` (small muted type icon + the name in its rarity
  colour + stack count), `.bag-row-status` (equipped badge + rarity/type/slot/durability, upper-case
  micro type) and `.bag-row-actions` (icon-only buttons). The icon is deliberately secondary — do not
  put it back in front as the lead element.
- **`ItemTooltip.svelte`** is the ARPG-style hover card: fixed-position beside the hovered element
  (`getBoundingClientRect`), flips to the left column edge and clamps vertically, `role="tooltip"`,
  `pointer-events: none` so it never eats clicks. It renders the rarity-coloured name, type/slot,
  italic description, damage/armour/durability/effect/state rows, bonuses, a Requires block that turns
  `--vds-danger` when the character's level/attributes fall short, and a price/stack footer.
- Hover **and** keyboard focus open it (rows and equipment slots both wire
  `mouseenter`/`mouseleave`/`focusin`/`focusout`); clicking still opens the detail dialog, so the two
  surfaces stay separate (card = glance, dialog = act).

### Session-72 (2026-10-25) — Login resumes the last known node

- **Bug:** `SelectCharacter` stamped `CurrentNodeId = regionGraph?.EntryNodeId ?? character.CurrentNodeId`,
  so the arrival node always won over the persisted position. `EnterZone` had the same problem twice
  over: it set the session/payload node to `zoneGraph?.EntryNodeId` **and** persisted that value (with
  `locationSlug: null`), wiping the saved node + location on every login — because the client re-enters
  the current zone right after `CharacterSelected`.
- **Fix:** `GameHub.ResolveResumeNodeId(nodeIds, savedNodeId, fallbackNodeId)` — the saved node wins when
  it exists on the graph being sent (or when there is no graph to check against), else the entry node.
  `SelectCharacter` uses it for the session row and the `CharacterSelected` payload; `EnterZone` uses it
  only when the character's `CurrentZoneId` matches the zone being entered, and then persists the
  resumed node **and** `CurrentZoneLocationSlug` instead of the entry node/null.
- `GetMapGraphHubCommand` already prefers `character.CurrentNodeId` when `CurrentMapId` matches, so the
  map payload, `CharacterSelected` and `ZoneEntered` now all agree.
- Brand-new characters keep the entry node (creation writes `CurrentNodeId` as null → fallback), which
  is the seam a tutorial/onboarding flow will hook into: first login should override the resume with
  the tutorial start node.
- Tests: `SelectCharacter_Should_Resume_At_Last_Known_Node`,
  `EnterZone_Should_Keep_The_Saved_Node_When_Reentering_The_Same_Zone`,
  `EnterZone_Should_Use_The_Arrival_Node_For_A_New_Zone` (696 server tests green).

### Session-73 (2026-10-25) — Dead-service sweep, StatusEffectParser hardening, tracker emptied

**Tracker:** all five legacy epics (#3–#7) closed with rationale comments; the web-first rewrites
live in [`plans/github-issue-refresh.md`](../../plans/github-issue-refresh.md) until the next
milestone is chosen. No open issues. GitHub MCP has **no label endpoints** — labels must be created
in the UI.

**Deleted as dead code (three commits, `63688c19` / `f860bfa1` / `f6ef5deb`):**
- `RespawnService` — never injected. The live path is `GameHub.Respawn` → `RespawnHubCommandHandler`,
  which only restores a portion of HP + full mana. **There is no death cost today** (no XP/gold loss,
  no death sickness, no blessing pick). The deleted service had that logic, unwired.
- `CharacterInitializationService` — duplicated by the live
  `InitializeStartingAbilitiesCommand`/`Handler` (dispatched from `CreateCharacterHandler`).
- `EquipmentPowerService` (`RealmEngine.Shared/Services/EquipmentAbilityService.cs`) — the only writer
  of `Character.EquipmentGrantedAbilities`, and nothing called it. Property plus `HasAbility` /
  `GetAvailableAbilityIds` removed with it; all live ability checks read `LearnedAbilities` directly.
  Equipment granting powers is therefore an *unbuilt feature*, not a broken one.

**`StatusEffectParser` fixes:** a `statusDuration` override is now honoured only when it parses to a
positive number (0/-1 used to create an effect that expired on the turn it landed); `statModifiers`
is read from `JsonElement` objects (JSON-authored content always arrives that way) as well as both
dictionary shapes; custom stat names are lower-cased to match the `attack`/`defense` keys
`CombatService` looks up. `StatusEffectParserTests` went 70 → 75.

**Docs:** removed the `web:desktop` VS Code task and the `build:desktop`/`adapter-static` lines in the
`web-development` skill; `plans/README.md` now marks the decommission plan ✅ Complete (W1–W12).
`scripts/dev/` is fully untracked scratch — its `desktop-static-server.py` / `verify-desktop.mjs` are
not repo debt.

**Verified:** `dotnet build Realm.Full.slnx` clean, `dotnet test Realm.Full.slnx` = **3,841 passed /
0 failed** (Core 2021, Shared 793, Server 696, Data 265, Discord 29, Assets 10, Forge 27);
`dotnet format --verify-no-changes` clean; `scripts/check-markdown-links.mjs` clean over 39 files.

### Session-74 (2026-10-25) — D2: the live world moved into Postgres (#33)

**Deleted:** `ZoneLocationEnemyStore` and `CombatSessionStore` (both `static ConcurrentDictionary`
singletons). `grep -rn "static readonly ConcurrentDictionary" Veldrath.Server/Features/Characters/Combat`
now returns nothing.

**Added:**
- `IEnemyInstanceStore` / `ICombatSessionStore` in `Veldrath.Server/Features/Characters/Combat/`.
- `EfCoreEnemyInstanceStore`: `GetAliveOrSpawnAsync`, `TryGetAsync`, `SaveAsync`, `TryMarkDeadAsync`,
  `RemoveAsync`, plus a private `SettleLocationAsync` maintenance pass.
- `EfCoreCombatSessionStore`: `TryGetAsync`, `IsInCombatAsync`, `SetAsync`, `RemoveAsync`,
  `RemoveByEnemyIdAsync`.
- Both registered scoped in `Program.cs` next to the repositories.
- `ActorPoolResolver.ResolveArchetypeAsync(slug)` so a materialised enemy can rebuild its content template.
- `SpawnedEnemy.Id` became `init` (was get-only) so rows can be materialised with their real id.
- `CombatHelpers.RespawnDelay` (60 s) replaced the deleted `ScheduleRespawn` timer.

**Invariants that now live in the database:**
- **Death is one conditional UPDATE.** `SaveAsync` persists health + participants only; `TryMarkDeadAsync`
  runs `UPDATE … WHERE IsAlive` and returns true for exactly one caller. Only that caller distributes
  rewards, so the old in-memory `WasRewarded` guard is unnecessary (and would not have survived per-request
  materialisation).
- **Respawns are lazy.** `GetAliveOrSpawnAsync` first runs `SettleLocationAsync`: it kills zero-health rows
  still flagged alive (crash between save and death), then revives rows whose `RespawnAt` has passed,
  restoring full health and deleting their participants. D3 (#34) replaces this with a proactive sweeper
  plus the `EnemySpawned` broadcast.
- **`ExecuteUpdate`/`ExecuteDelete` bypass the change tracker.** The store detaches affected rows
  (`DetachTracked`) and reads with `AsNoTracking`. Missing this masked a death transition inside one scope
  and cost two failing tests — keep it in mind for D3/D4.
- **No cache.** Materialising from indexed lookups is fast enough; if a cache is ever added it goes inside
  the store (invalidate on write, check `Version` on read).

**Handler changes:** `Engage`, `Attack`, `DefendAction`, `FleeFromCombat`, `UseAbilityInCombat` and
`Respawn` now take the two stores; `Attack`/`UseAbility` lost their `IServiceScopeFactory`/`IHubContext`
dependencies with the timer. `TravelToNodeHubCommandHandler` and `NavigateToLocationHubCommandHandler`
call `GetAliveOrSpawnAsync`, which also makes first arrival the place a wiped world comes back.

**Tests:** new `EfCoreEnemyInstanceStoreTests` (6) and `EfCoreCombatSessionStoreTests` (4) against the
SQLite harness; the six combat handler test classes now use `FakeEnemyInstanceStore` /
`FakeCombatSessionStore` from `Veldrath.Server.Tests/Infrastructure/` instead of the statics.
Server suite 696 → **706**, full solution **3,851 passing**, format clean.

### Session-75 (2026-10-25) — D3: the respawn sweeper replaces the old timer (#34)

**Added:**
- `EnemyRespawnService : BackgroundService` (`Features/Characters/Combat/`): a `PeriodicTimer` loop that calls
  `IEnemyInstanceStore.SweepExpiredRespawnsAsync`, broadcasts `EnemySpawned` per revived enemy to its zone group,
  then calls `ICombatSessionStore.PruneExpiredAsync`. `SweepAsync` is public so a test (or an operator tool) can
  trigger one sweep; `ExecuteAsync` never dies from an exception (logged, retried next tick).
- `EnemyWorldOptions` (`Settings/`): section `Enemies`, `ValidateOnStart` via DataAnnotations —
  `RespawnDelaySeconds` (60), `SweepIntervalSeconds` (5), `RespawnBatchSize` (50), `SessionGraceSeconds` (60).
  A bad value fails the host instead of silently defaulting.
- `EnemyRespawnedEvent` record carries what the broadcast needs.
- Store methods: `IEnemyInstanceStore.SweepExpiredRespawnsAsync(batchSize, ct)` — on PostgreSQL a raw
  `SELECT … FOR UPDATE SKIP LOCKED` inside a transaction, portable LINQ elsewhere, then revive + clear participants;
  `ICombatSessionStore.PruneExpiredAsync(grace, ct)`.
- `Program.cs`: `AddOptions<EnemyWorldOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` +
  `AddHostedService<EnemyRespawnService>()`.

**Changed:** `TryMarkDeadAsync` lost its `TimeSpan` parameter — the store applies the configured delay — so
`CombatHelpers.RespawnDelay` is gone.

**Gotcha:** `AddOptions<T>().Bind()` registers `IOptions<T>`, **not** `T`. Injecting `EnemyWorldOptions` directly into
the hosted service failed DI for every integration test (288 failures) until it took `IOptions<EnemyWorldOptions>`.
The stores take `IOptions<>` for the same reason.

**Tests:** `EnemyRespawnServiceTests` — sweep revives a due enemy with full health and cleared participants, ignores
an enemy whose timer has not passed, `PruneExpiredAsync` deletes only sessions disconnected past the grace, the hosted
service broadcasts `EnemySpawned` exactly once and prunes, and the options reject zero for every field
(4 theory cases). Server suite 706 → **714**, full solution **3,859**, format clean.

### Session-76 (2026-10-25) — D4: reconnect grace, AFK service, claim refusal (#35)

**AFK moved out of the hub.** `GameHub._afkCharacters` (a static dictionary) is gone; `IAfkTracker` / `AfkTracker`
live in `Services/` and are registered as a singleton. Documented as *ephemeral by design* — cleared on any action,
on reconnect *and* on disconnect, never persisted. `GameHub` now takes `IAfkTracker`, `ICombatSessionStore` and
`IEnemyInstanceStore` in its constructor, so **every** `new GameHub(...)` in tests needs the three extras
(`GameHubTests.CreateHub`, two inline sites there, plus `GameHubChatCommandTests`, `GameHubModerationCommandTests`,
`GameHubRegionTests`).

**Combat reconnect grace.** `ICombatSessionStore` gained `MarkDisconnectedAsync` / `MarkConnectedAsync`.
`OnDisconnectedAsync` now distinguishes the two cases: a *clean* disconnect ends the fight (`RemoveAsync`), an
*unexpected* one suspends it (`MarkDisconnectedAsync` sets `DisconnectedAt`) and the D3 sweeper prunes it after
`Enemies:SessionGraceSeconds`. `SelectCharacter` clears the suspension and calls `ResumeCombatIfAnyAsync`, which
re-sends the **existing** `CombatStarted` payload for a live enemy (no new wire type, so `payload-contract` and the
client handler are untouched) or sends "Your fight ended while you were away." and drops a session whose enemy is
gone.

**Client fix (small but necessary):** `LocationEntered` in `session.svelte.ts` now filters the enemy the player is
already fighting out of `enemies`. Without it a resumed fight arrives before the client enters the zone, and the
subsequent roster refresh re-added the engaged enemy to the location list.

**Same tracker trap, second store.** `EfCoreCombatSessionStore` also uses `ExecuteUpdate`/`ExecuteDelete`, so it now
detaches affected rows after every mutation exactly like the enemy store — a suspended session looked unsuspended to
a later read in the same scope until that was added.

**Claim refusal.** The `PlayerSessions.CharacterId` unique index is translated into the existing
"character is active elsewhere" message instead of a 500. Known limitation, recorded in the plan: `SelectCharacter`
sweeps a previous row before inserting, so a *second live instance* claiming the same character would
delete-then-recreate it. Real fencing needs a lease or the SignalR backplane; not attempted here.

**Tests:** `CombatReconnectTests` (AFK set/get/clear, suspend→resume, `MarkConnected` false when nothing was
suspended, prune inside/outside the grace) and two hub tests (resume sends `CombatStarted`; a fight with no enemy
is closed with a system message). Server 714 → **721**, web **156**, full solution **3,866**, format clean.

### Session-77 (2026-10-25) — D5: content audit and failsafe-not-fallback (#36, closes #31)

**The rule, in code.** `ContentReferenceRules` holds three pure checks so they can be tested with plain data and
reused by Milestone B's authoring validators:
- `AuditSpawnPools` — a `ZoneLocation`'s `ActorPool[].ArchetypeSlug` must exist in `ActorArchetypes`.
- `AuditQuestObjectives` — `kill` → archetypes, `collect` → items, `reach`/`visit` → zone locations; other objective
  types carry no content reference and are skipped on purpose.
- `AuditMapEdges` — both ends of a `MapEdge` must be nodes of its own `Map`.
Each returns `ContentIssue(Rule, Message)` naming both sides; none can throw.

**Startup audit.** `ContentAuditService : BackgroundService` runs once (`Task.Yield` first so seeding can finish),
reads **`ContentDbContext` via `IDbContextFactory`** — note `ApplicationDbContext` does *not* map content entities
despite what `ef-core-patterns.md` says — and logs one summary plus one `Error` per finding. It swallows every
exception: a broken audit must never be a broken server. `RunAsync` is public for tests/tooling.

**Play-path failsafes.**
- `ActorPoolResolver` lost its `"common"` fallback: a null/empty pool spawns nothing (a safe town is authored
  content), and an unknown archetype logs `Error` naming the slug and spawns nothing for it. It now takes an optional
  logger (`NullLogger` default) so existing test constructions still work.
- `NavigateToLocationHubResult` and `TravelToNodeHubResult` gained `ContentWarnings`, populated when a location has a
  pool but produced no enemies (or when a map node's `RefId` points at a location that does not exist). `GameHub`
  forwards each one as `SystemMessage` after `LocationEntered` / `NodeTraveled`.
- `EnsureRosterAsync` now fetches the location up front instead of inside the spawn lambda, so a dangling `RefId` is
  reported rather than silently producing no roster.

**Tests:** `ContentReferenceAuditTests` (8: clean pools, dangling pool message, per-type quest resolution, dangling
quest target, ignored objective types, dangling edge ends, unknown map, all-empty content) and
`ActorPoolResolverTests` (4: no pool → empty with no repository calls, configured archetype spawns, unknown archetype
spawns nothing, `ResolveArchetypeAsync` returns null). Server 721 → **733**, full solution **3,878**, web 156,
format clean.

**Milestone D is complete:** #32–#36 closed, #31 (epic) closed. The world, its respawns, its fights and its
reconnect grace all live in Postgres; the only remaining process-local state is the claim tracker and AFK, both
documented as caches.

### Session-78 (2026-10-25) — Milestone D review: runtime smoke and two content findings

**Smoke test (the part tests cannot cover):** `dotnet run --project Veldrath.Server -- --urls http://localhost:9110`
reaches `Now listening` and `Application started` with both hosted services live and no exceptions. Note the dev
config binds `0.0.0.0:9000`, so the `--urls` argument is required when another server instance is already running.

**The audit immediately found real debt in the dev database** (exactly what D5 was for):
- `the-lost-shipment` objective `collect:lost-cargo` → no such item exists.
- `gather-mushrooms` objective `collect:glowcap-mushroom` → no such item exists (and no item slug matching
  `cargo`, `glowcap` or `mushroom` exists at all).

Both quests are un-completable today. This is content work — either author the items or retarget the objectives —
not code.

**Compounding bug the audit cannot see:** the *kill* path advances objectives with `result.EnemyName` (display name)
while objectives are authored as slugs (`kill:bandit-ruffian` in `the-lost-shipment`, and the audit therefore reports
it as valid). So a correctly authored `kill:<slug>` objective never progresses at runtime. Fixing that is Milestone A's
A5 item; worth doing before any content pass relies on kill objectives.

### Session-79 (2026-10-25) — C1: party schema and hub commands (#37)

**Schema (migration `AddPartyTables`, applied to dev):** `Parties` (`Id`, `LeaderCharacterId` → Characters cascade,
`CreatedAt`, `LootRule` default `contribution`), `PartyMembers` (PK `(PartyId, CharacterId)`, **UNIQUE `CharacterId`** —
"one party per character" is a constraint), `PartyInvites` (`PartyId`+`InvitedCharacterId` unique, `ExpiresAt` indexed,
invitee cascade, inviter restrict). Entities are `Veldrath.Server.Data.Entities.Party`, `PartyMemberEntity` (name avoids
clashing with the save-game `PartyMember`) and `PartyInvite`.

**Command layer** (`Features/Party/`):
- `PartyStore` (scoped): `GetPartyForCharacterAsync`, `GetPartyAsync`, `CreateAsync`, `RemoveMemberAsync`,
  `DeleteAsync`, `PromoteLeaderAsync`, `ToSnapshotAsync`. It clears the change tracker after every
  `ExecuteUpdate`/`ExecuteDelete` — the same discipline D2/D4 established.
- `PartySnapshot` / `PartyMemberView` are the *only* shapes clients see; vitals are read from the character attribute
  blob, and a malformed blob yields `0/0` rather than throwing.
- Commands: `CreatePartyHubCommand`, `LeavePartyHubCommand`, `KickPartyMemberHubCommand`, `GetPartyHubQuery`. The
  result carries `PartyId` so the hub can move a connection between SignalR groups even when the caller ends up solo.
- Leader leaving promotes the longest-standing member; the last member leaving deletes the party; kicking yourself is
  refused with "leave the party instead".

**Hub surface:** `CreateParty`, `LeaveParty`, `KickPartyMember(Guid)`, `GetParty`, group `party:{partyId}`,
events `PartyUpdated` (full snapshot, or `null` when solo) and `PartyDisbanded`. `PartyUpdated` is snapshot-first —
clients never fold deltas.

**Bug the tests caught:** the first `GetPartyForCharacterAsync` applied `.Include` after a `.Select` projection, which
EF rejects outright. The lookup now starts from `Parties` with `Members.Any(...)`. Worth remembering: `Include` must
come before any projection that changes the entity type.

**Tests:** `PartyCommandTests` (10) — create makes the creator sole leader with real vitals, a second create is
refused without touching the DB, last-member leave disbands, leader leave promotes the veteran, solo leave refused,
kick removes and re-snapshots, non-leader kick refused, self-kick refused, solo read is a successful empty answer, and
a party read from a *fresh context* (the restart case) still comes back.

Server suite 733 → **743**; full solution **3,888**; format clean. Next: C2 (invites + party panel).

### Session-80 (2026-10-25) — C2 server half: invite flow

**Commands** (`Features/Party/PartyInviteCommands.cs`): `InviteToPartyHubCommand(Inviter, TargetName)`,
`AcceptPartyInviteHubCommand(Character, Party)`, `DeclinePartyInviteHubCommand`. Refusals each name a reason —
unknown name, offline (checked through `IPlayerSessionRepository`), self-invite, already grouped, in combat (through
`ICombatSessionStore`), party full. `PartyOptions` (section `Party`, `ValidateOnStart`) supplies `MaxSize` (5) and
`InviteExpiryMinutes` (5).

**Store additions:** `GetMemberCountAsync`, `AddMemberAsync` (unique-index violation → "already in a party"),
`CreateOrRefreshInviteAsync`, `GetValidInviteAsync`, `GetPendingInvitesAsync`, `DeleteInviteAsync`,
`DeleteInvitesForCharacterAsync`, `PurgeExpiredInvitesAsync`. `GetParty` now purges expired invites and returns
`PartyCommandResult.PendingInvites`, so an invite sent to an offline character still appears at login.

**Hub:** `InviteToParty(string)`, `AcceptPartyInvite(Guid)`, `DeclinePartyInvite(Guid)`; accept joins
`party:{id}` and broadcasts `PartyUpdated` to the group. Invites are **not pushed as payloads** — the invitee gets
`PartyInvitesChanged` on their connection and re-reads through `GetParty`, so offline/reconnect follows one path.
`InviteToParty` returns `InvitedCharacterId` so the hub can find that connection.

**Two traps, both caught by tests:**
1. `DateTimeOffset.UtcNow` inside a LINQ predicate is **not translatable** once `DateTimeOffset` columns go through the
   SQLite string converter — capture `var now = DateTimeOffset.UtcNow;` first. This broke every `GetParty` call.
2. Seeding `PlayerSession` rows in tests to fake presence hit an SQLite FK failure; a
   `FakePlayerSessionRepository` (presence-only, everything else throws) is deterministic and faster. Prefer it for any
   future "is the character online" test.

Also: a local helper named `Options(...)` shadows `Microsoft.Extensions.Options.Options` — name test helpers
`PartyOptionsFor(...)` instead.

**Tests:** `PartyInviteTests` (11). Server 743 → **754**, full solution **3,899**, format clean. Client half of C2
(handlers + party panel + invite toast) still to do.

---

### Session-81 (2026-10-25) — C2 client half: party panel, invite toast, snapshot handlers

**Wire shape stays snapshot-first.** `PartyUpdated` carries a whole `PartySnapshot` (or `null` for solo);
`PartyDisbanded` sends `{ partyId }`; `PartyInvitesChanged` sends `{ partyId: string | null }` — a value means *that*
invite is gone, `null` means "an invite arrived, re-read". The hub never pushes invite data, so login, reconnect and a
live nudge all take the same `GetParty` path.

**Client (`web/client/src/lib/game/`):**
- `types.ts`: `PartyMemberView`, `PartySnapshot`, `PartyInviteView`, `PartyDisbandedPayload`,
  `PartyInvitesChangedPayload`.
- `session.svelte.ts`: `party`, `partyInvites`, `showParty` state; `isPartyLeader` getter; `requestParty`,
  `openParty`, `createParty`, `leaveParty`, `kickPartyMember`, `inviteToParty`, `acceptPartyInvite`,
  `declinePartyInvite`; handlers that reduce the snapshot and announce joins/leaves; `handleCharacterSelected` now
  calls `requestParty()` **before** `openInventory()`.
- `GameOverlays.svelte`: party dialog (member list with HP, leader chip, leader-only Remove, invite-by-name box,
  leave/disband) plus a fixed bottom-right invite toast with Accept/Decline.
- `LeftSidebar.svelte`: **Party** entry in the character menu, badged with the invite count or party size.

**Two gotchas worth remembering:**
1. `payload-contract.test.ts` only sees *anonymous* payloads. `SendAsync("PartyUpdated", result.Party)` and
   `SendAsync("PartyUpdated", null)` are skipped, so the snapshot needs no `<Event>Payload` interface — but
   `PartyDisbanded`/`PartyInvitesChanged` do. Add any new mirrored interface to the third test's `expected` list,
   otherwise the contract check silently stops covering it.
2. `handleCharacterSelected` grows a hub call whenever login needs more state. Every test that asserts an exact
   `hub.methods()`/`hub.invocations` list after `CharacterSelected` has to be updated (two in `session.test.ts`),
   and `components.test.ts`'s `fakeSession` needs a stub for each new session field or the components throw
   `Cannot read properties of undefined`.

**Verification:** `npm run typecheck` 0 errors, `npm run lint` clean, Vitest **175 passed** (session 90, components
21), Playwright 22 passed, `npm run build` clean. C3 (#41) is next.

---

### Session-82 (2026-10-25) — C3: shared combat participation and contribution rewards

**Participation.** `EngageEnemyHubCommandHandler` now asks `IPartyCombatQuery` (implemented by `PartyStore`,
registered scoped and aliased in `Program.cs`) who else is standing there, and adds them to
`SpawnedEnemy.Participants` before the single `SaveAsync`. Location comes from
`Characters.CurrentZoneId` + `Characters.CurrentZoneLocationSlug` — the columns travel and navigation already
maintain — inner-joined with `PlayerSessions` so only online members join. The allies get **no combat session**:
they are participants (share + quest progress) but only act if they engage themselves.

**Rewards.** New public `CombatRewards` (`Features/Characters/Combat/CombatRewards.cs`):
`SplitByWeight(total, weights)` uses largest-remainder rounding so the parts sum to the whole, and
`Split(xp, gold, contributions)` applies it to both pools. `CombatHelpers.DistributeRewardsAsync` now pays those
shares instead of `Math.Max(1, (int)(baseXp * share))`, which could over-pay a party. Zero-damage participants get
nothing; a fled member keeps their recorded damage (Flee no longer deletes `DamageContributions`).

**Kill objectives.** `AttackEnemyHubResult` carries `EnemyArchetypeSlug` and `ParticipantIds`; the hub's
`AdvanceKillObjectivesAsync` advances `kill:<archetypeSlug>` **and** `kill:<displayName>` for every participant,
pushing `QuestLogReceived` to each participant's own connection (via `IPlayerSessionRepository`) and writing the
journal silently when they are offline. Advancing both keys fixes the A5 slug/name mismatch reported by the D audit
without waiting for A5.

**Bug found while testing C3:** the kill branch parsed the attribute blob *before* `DistributeRewardsAsync` wrote the
gold, then saved that stale blob back — every kill silently dropped the gold reward (XP survived because
`Experience` was mutated on the same tracked entity). The handler now re-reads the character after distribution before
writing cooldowns. Regression test asserts `"Gold":5` lands in the stored blob.

**Tests:** `CombatRewardsTests` (6, pure split maths), 2 new `EngageEnemyHubCommandHandlerTests` (peers added, no
session forced), 2 new `AttackEnemyHubCommandHandlerTests` (solo unchanged + exact split + gold persisted), 1 new flee
test (keeps damage — loops until the 50/50 roll succeeds), 2 `PartyStore.GetOnlinePartyMembersAtLocationAsync` tests.
Server 754 → **767**, full solution **3,912**, format clean.

**Known gap:** no DB-backed test seeds a `PlayerSession` for a second member (needs World/Region FK chains), so the
"online member at the same location" path is only covered up to the join; the peer-add path is covered with
`FakePartyCombatQuery`. Also no hub-level test yet for per-participant `QuestLogReceived` fan-out.

---

### Session-83 (2026-10-25) — C4: party chat, and Milestone C closed

**Server.** `GameHub.SendPartyMessage(SendZoneChatMessageHubRequest)` reuses the zone-chat request record, reads the
party id through the existing `GetPartyHubQuery` (so the hub needed no new store dependency and **no ctor change** —
which is why no other hub test needed touching), and sends `ReceiveChatMessage` with `Channel = "Party"` to
`party:{partyId}` only. A character with no party gets `Error` "You are not in a party." instead of a message into
the void. Because it reuses the `ReceiveChatMessage` event, logging/moderation tooling sees party traffic like any
other channel.

**Client.** `ChatChannel` gained `'Party'`; `sendChat` routes it to `SendPartyMessage`; `RightSidebar` derives its
channel list from `session.party`, so the Party tab only exists while the character is grouped, and
`GameSession.endPartyChat()` drops every `Party` message and falls back to `Zone` when the party becomes null or
disbands. Party chat is **live-only** — the UI must never imply a backlog the server does not keep. The tab/history
half landed in the follow-up commit `95b3cd37` (Vitest 177).

**Tests:** 2 hub tests (solo refusal asserts the exact error and that no group send happened; in-party asserts a
single `ReceiveChatMessage` with the trimmed message and nothing sent to the caller) + 1 Vitest for the routing.
Server **769**, full solution **3,914**, web typecheck/lint clean, Vitest 176, Playwright 22 passed.

**Milestone C wrap-up.** All of #43/#40/#41/#42 are closed, so epic #37 is complete. Party state is in Postgres,
snapshots are the only wire shape, invites are re-read rather than pushed, combat is shared through the existing
enemy rows, and the reward split cannot lose a coin.

**Gap closure.** Both gaps named at wrap-up were then closed in the same session:
1. `PartyCommandTests.GetOnlinePartyMembersAtLocation_Returns_Only_Members_Online_At_That_Location` seeds a real
   World → Region → `PlayerSession` chain and proves the filter: three online members, only the one at the same zone
   **and** location slug comes back. Writing it exposed a test-harness trap — the `Character` instances returned by
   `SeedCharacterAsync` are detached by the time `PartyStore` has attached its own tracked copies, so `UpdateRange`
   throws *"another instance with the same key value for {'Id'} is already being tracked"* and in-memory mutations are
   silently never saved. Use `ExecuteUpdateAsync` to move the row, then read it back: the store reads with
   `AsNoTracking`, so the database is the only source of truth that matters.
2. `GameHubTests.AttackEnemy_Advances_The_Kill_Objective_For_Every_Participant_By_Slug_And_Name` mocks `ISender`, so it
   asserts the commands themselves — four `AdvanceQuestObjectivesHubCommand`s (slug and name for each participant).
   Note `FakeHubCallerClients.Client(id)` returns a **no-op** proxy, so outbound per-connection sends cannot be observed
   from `GameHubTests`; assert the command fan-out (or extend the fake) rather than reading `SentMessages`.

Server **771**, full solution **3,916**, format clean.

---

### Session-84 (2026-10-25) — Milestone A kickoff + A1: gold-first death cost

**Issues filed before any code** (the lesson from C): A1 #44, A2 #45, A3 #46, A4 #47, A5 #48, A6 #49, and
`plans/death-and-first-hour.md` carries those numbers in its tracker line.

**A1.** `DeathPenaltyOptions` (section `Death`, `ValidateOnStart`): `GoldLossPercent` 10, `GoldLossCap` 100,
`GoldFloor` 10, `XpLossPercent` 5. `DeathPenalties.Calculate(carriedGold, experience, options)` is pure and returns
`DeathPenalty(GoldLost, XpLost)`: gold when `gold >= GoldFloor`, experience only when the character is effectively
broke, nothing when it holds neither; the share rounds **up** (minimum 1) so a small balance still notices, and the gold
loss is capped. `HandleDeathIfNeededAsync` now returns `DeathOutcome(IsDead, IsHardcore, Penalty)`; hardcore still
soft-deletes with **no** penalty (there is nothing left to punish, and the client is told 0/0 rather than a lie).

**Wiring cost, stated honestly:** all four handlers that can kill a player (`AttackEnemy`, `Defend`, `Flee`,
`UseAbility`) take `IOptions<DeathPenaltyOptions>` and report `GoldLost`/`XpLost`; the hub adds both to every
`CombatTurn` variant. That meant touching four handler tests' `MakeHandler` helpers — the mechanical churn is the price
of not hiding options in a static.

**Client:** `CombatTurnPayload` gained `goldLost`/`xpLost` (the payload-contract test enforces the mirror), and
`GameSession.applyDeathPenalty` folds the loss into the stat block and logs "You lost N gold." / "… experience."

**Trap:** `Character.Experience` is a `long`, so the calculator takes `long` and clamps internally; an `int` parameter
fails to compile (CS1503) rather than truncating silently.

**Tests:** `DeathPenaltiesTests` (5 pure cases), two integration deaths through `AttackEnemy` (gold-first with a
stored-blob assertion, and broke → XP), one Vitest for the client mirror. Server 771 → **778**, full solution **3,923**,
Vitest **178**, typecheck/lint/format clean.

---

### Session-85 (2026-10-25) — A2: safe respawn location, death sickness, loud failsafes

**Safe respawn is a content trait.** `ZoneLocationTraits.SafeRespawn` (bool?) on the content zone location, surfaced
as `ZoneLocationEntry.IsSafeRespawn`. No migration: `Traits` is configured `.ToJson("Traits")`, so adding a property
changes the JSON payload, not the schema — `dotnet ef migrations add` produced an **empty** migration, which was then
removed (it had also rewritten the snapshot with tooling churn: ProductVersion 10.0.9 → 10.0.13 and
`ToTable("X")` → `ToTable("X", (string)null)`; revert that, it is noise).

**Respawn flow.** `RespawnHubCommandHandler` resolves the character's `CurrentZoneId`, asks
`IZoneLocationRepository.GetByZoneIdAsync`, prefers the flagged safe location, else falls back to the zone's first
location, else stays put — and every degradation logs `Error` and returns a `Warning` the hub forwards as a
`SystemMessage`. It then persists `CurrentZoneLocationSlug` via `UpdateCurrentZoneLocationAsync` (note the name: there
is no `UpdateCurrentLocationAsync` on the interface).

**Death sickness** is a timed damage penalty in the attribute blob, not a status effect: the server never hydrates or
ticks status effects (`CombatCharacterHydrator` sets `ActiveStatusEffects = []`), so a "DB-authored power" would have
been decorative. `Death:DeathSicknessMinutes` (5) and `Death:DeathSicknessDamagePenaltyPercent` (25) write
`DeathSicknessUntil` (unix **minutes**, so it survives 2038) and `DeathSicknessPenalty`;
`DeathPenalties.ApplySicknessToDamage` reduces `CalculatePlayerDamage` while the window is open and never below 1.
A disabled window clears stale keys.

**Wire + client.** `CharacterRespawned` now carries `CurrentHealth`, `CurrentMana`, `LocationSlug`,
`LocationDisplayName`, `SicknessExpiresAt` (`CharacterRespawnedPayload` in `types.ts`, enforced by the contract test);
`GameSession` moves `session.location`, refreshes ground items and says "respawned at X … weakened until Y".

**Traps:** a `Mock.Of<IZoneLocationRepository>()` returns **null** for `GetByZoneIdAsync`, which NREs in the handler —
give test doubles a `ReturnsAsync([])` default. And `svelte/prefer-svelte-reactivity` rejects `new Date(...)` inside a
`.ts` file: use `SvelteDate` for the message timestamp.

**Tests:** 5 new respawn tests (safe location, fallback to first, no locations, sickness applied, stale sickness
cleared) + 2 sickness maths tests + 1 Vitest. Server 778 → **785**, full solution **3,930**, Vitest **179**,
typecheck/lint/format clean.

---

### Session-86 (2026-10-25) — A3: tutorial handoff

**Where the tutorial lives.** The start location is content (`ZoneLocationTraits.IsTutorialStart`, surfaced as
`ZoneLocationEntry.IsTutorialStart`; another JSON-column property, so again **no migration**). The *quest* comes from
options (`Tutorial:TutorialQuestSlug`, default `first-steps`) because finding a quest by a content flag would need a
new `IQuestRepository` method plus every implementation and fake; a configured slug is equivalent and loud when the
content is missing.

**Two-state design, deliberately:**
- `TutorialStarted` in the attribute blob marks "already handed to the tutorial", so logging in again never re-places
  or re-accepts.
- Completion is **derived from the journal** (the tutorial quest entry reaching `Completed`) instead of being copied
  into `TutorialCompleted`. One source of truth, no drift — the plan's blob flag is intentionally not used.

**Flow.** `PrepareTutorialHubCommandHandler` (Features/Quest) runs from `SelectCharacter` **after** the map-position
block: skip if the journal entry is completed, skip if `TutorialStarted`, find the location flagged
`IsTutorialStart` (via `GetAllAsync`), accept the quest through the existing `AcceptQuestHubCommand` so the journal is
written once, then set `CurrentZoneId` + `CurrentZoneLocationSlug` + `TutorialStarted` in one save. Missing start
location or a failed accept → `Error` log + `Warning` the hub forwards as `SystemMessage`, and the character keeps the
normal starting point.

**Hub detail:** `SelectCharacter` treats a **null** handoff result as a loud failure (Error log) rather than an NRE —
which is also what keeps every existing hub test working, since they run with `Mock.Of<ISender>()`.

**Tests:** `PrepareTutorialHubCommandTests` (5: places + accepts, skips completed, skips started, no start location,
quest missing). Server 785 → **790**, full solution **3,935**, format clean.

**Trap:** `Characters.QuestBlob` is NOT NULL — test seeders must default it to `string.Empty`, and
`Character.CurrentZoneId` already defaults to `crestfall`, so "did not move" assertions must compare against the
seeded value, not against empty.

---

### Session-87 (2026-10-25) — A4: quest HUD tracker, completion diffing, turn-in toast (client only)

**No new wire event.** `GameSession` keeps the previous journal snapshot (`previousQuests`) and
`announceQuestProgress` diffs each `QuestLogReceived` / `QuestTurnedIn` against it, emitting one
`Objective complete: <key> (<title>).` system line per objective that flipped to complete. The **first** snapshot
produces nothing — there is nothing to compare against, and a reload must not spam the log. Repeating the same
snapshot announces nothing because the objective is already complete in the previous one.

**Tracker.** `trackedQuests` is a pure getter: active quests mapped to `{ slug, title, objective }` where `objective`
is the nearest incomplete `QuestObjective` (or null ⇒ "Ready to turn in"). New `TrackedQuest` type in `types.ts`, new
[`QuestTracker.svelte`](../../web/client/src/lib/game/QuestTracker.svelte) rendered from `LeftSidebar` above the character
menu, and it renders nothing when no quest is active.

**Turn-in toast.** `questTurnInToast` state + `dismissQuestToast()`, rendered in `GameOverlays` at the top right
(the party invite toast owns the bottom right). Deliberately **no auto-dismiss timer**: a testable state beats a
setTimeout, and the player dismisses it when they have read it.

**Tests:** 3 Vitest (diff announces once, tracker derivation, toast set/dismiss) + 3 component tests (tracker lists the
nearest objective, tracker renders nothing when empty, toast renders and dismisses). Vitest 179 → **185**, typecheck
and lint clean, `npm run build` clean.

---

### Session-88 (2026-10-25) — A5: objective kinds, kind constant, audit coverage

**`QuestJournal.Advance` was already prefix-generic** (`kind:` + exact key, case-insensitive), so adding a kind is
*documentation + one call site + audit coverage*, not a matcher change. That is now written down where it belongs:
`QuestObjectiveKinds` (kill, collect, visit, craft, reach_level, talk, deliver) with XML docs on every constant, and
`Advance`'s parameter doc points at it.

**Call sites added:**
- `craft:` — `CraftItem` advances with `CraftItemHubResult.ItemCrafted` after a successful craft.
- `reach_level:` — `GainExperience` advances **both** `NewLevel` and `LeveledUpTo` on a level-up, so content naming
  either value completes.
- `talk:` — new hub method `TalkToNpc(string npcSlug)` (validates the slug is non-empty, then advances
  `talk:<npcSlug>`). There was no dialogue interaction surface at all; this is the one call site, and the client will
  use it once NPC dialogue lands.
- **`deliver:` is reserved, loudly.** A hand-over needs inventory-removal semantics and a recipient model that do not
  exist, so instead of a fake call site the content audit now reports rule `unsupported-quest-kind` for any
  `deliver:` objective. Authored content can therefore never sit there silently never progressing.

**Audit widened** (`ContentReferenceRules.AuditQuestObjectives`): `craft:` validates against item slugs,
`reach_level:` must be a positive number, `deliver:` is reported as reserved. `talk:` is deliberately **not**
validated yet — there is no NPC catalog passed into the rules; the switch carries a comment saying so.

**Kill-key shim:** the dual slug+name advance already exists from C3; the name path now logs a `Debug` line naming the
legacy key so migration is visible without spamming Info.

**Tests:** new `QuestObjectiveKindTests` (8 theory rows across the new kinds + name-vs-slug mismatch, plus
completed-quest immunity) and `QuestObjectiveAuditTests` (craft known/unknown, reach_level number, deliver reserved).
Server 790 → **803**, full solution **3,948**, format clean.

---

### Session-89 (2026-10-25) — A6: hardcore memorial and leaderboard (Milestone A complete)

**No schema change** — `DeletedAt` + `DifficultyMode` already carried everything. `ICharacterRepository` gained
`GetMemorialAsync(accountId)` (soft-deleted **hardcore** for one account, newest death first, sorted **client-side**
because the SQLite provider cannot `ORDER BY` a `DateTimeOffset`) and `GetHardcoreLeaderboardAsync(skip, take)`
returning `(Items, Total)` so the caller can paginate without a second query (level desc, experience desc, name asc).

**Endpoints** (`CharacterEndpoints`): `GET /api/characters/memorial` and a separate authorized group
`GET /api/leaderboard/hardcore?page=&pageSize=` (page ≥ 1, pageSize clamped 1–100) which marks the caller's own rows
with `isOwn` and includes `diedAt` for the fallen.

**Web:** `fetchMemorial` / `fetchHardcoreLeaderboard` in `#lib/server/character-api.ts`; the roster page's loader now
fetches the memorial and renders a "Memorial" section (only when non-empty); new route
`routes/leaderboard/hardcore` with a paged board, `You` chips, a skull for the fallen and honest empty state.
Everything is fetched server-side through `gameApiFetch` — no client-side tokens.

**Acceptance that was already true:** `CharacterRepository.GetByIdAsync` filters `DeletedAt == null`, so a soft-deleted
hardcore character can be neither selected nor respawned; that is now **asserted** (`A_Soft_Deleted_Character_Can_No_Longer_Be_Loaded_For_Play`) instead of assumed. Respawn's hardcore refusal was already tested.

**Traps:** `Characters` has `UNIQUE (AccountId, SlotIndex)`, so multi-character seeders must allocate the next free slot
(`Count(existing) + 1`) rather than hardcoding `SlotIndex = 1`; and the SQLite `DateTimeOffset` ordering limitation
bites any new query that sorts on `DeletedAt`/`LastPlayedAt`.

**Tests:** `CharacterMemorialTests` (4: memorial filtering/order, board ordering incl. the fallen, pagination totals,
deleted characters not loadable). Server 803 → **807**, full solution **3,952**, Vitest 185, web typecheck/lint/build
and `dotnet format` clean.

**Milestone A is complete:** A1–A6 all closed; epic #38 can be closed.

### Session-90 (2026-10-25) — Milestone B scope shift: RealmForge removed, Foundry is the editor

**Decision (user):** "pull RealmForge out of the project entirely. Focus on Foundry (in svelte app)."
RealmForge was the last remaining Avalonia surface, so this also finishes the web-first pivot.

**Deleted:** `RealmForge/`, `RealmForge.Tests/` (27 tests), `RealmUI.Fonts/`, `RealmForge.slnx`,
`versions/tooling.props`, `.github/workflows/ci-tooling.yml`, `.roo/skills/run-realmforge/`,
`THIRD-PARTY-NOTICES.md` (its only content was the RealmForge game-icons attribution).

**Config cleaned:** `Realm.Full.slnx` (13 projects left) and **`Veldrath.Libraries.slnx`, which still
listed `RealmUI.Fonts/RealmUI.Fonts.csproj` and would no longer build**; `Directory.Packages.props`
(Avalonia + ReactiveUI ItemGroups); `Directory.Build.props`; `codecov.yml` (`forge` flag);
`coverage.runsettings`; `.dockerignore`; `.github/dependabot.yml` (Avalonia/ReactiveUI ignore rules);
`.github/workflows/release.yml` (`tooling/v*` trigger, tooling tag branch, `tooling)` release body);
`.vscode/tasks.json` + `launch.json` (6 dead tasks/configs); `scripts/generate-version.ps1`;
`scripts/README.md`.

**Docs updated:** `AGENTS.md`, `README.md`, `SECURITY.md`, `docs/design-system.md` (all Avalonia/AXAML
guidance is now explicitly historical, matching the earlier Blazor treatment), `docs/client-ui-architecture.md`,
`.github/instructions/{build-strategy,testing,xml-documentation,ef-core-patterns}.md`, `.roo/skills/*`,
`.roo/modes/*/instructions.md`, `web/client/README.md`, `web/client/vite.config.ts`, the Foundry
content page copy.

**Memory:** `forge-foundry-sync.md` → `foundry-content-schema.md` (the name referenced a deleted app).

**Milestone B re-scoped:** Foundry was read-only, so B2 is no longer "validate saves in a desktop app" —
it now owns the server write path: `GET /api/content/{type}/{slug}/validate`,
`POST /api/content/{type}`, `PUT /api/content/{type}/{slug}`, `DELETE /api/content/{type}/{slug}`,
RBAC-gated while reads stay anonymous, an `UpdatedAt` precondition for concurrency, and a
schema-driven edit form in Foundry. See `plans/content-pipeline.md` §2.2.

**Verification:** `dotnet build Realm.Full.slnx` + `Veldrath.Libraries.slnx` 0 warnings 0 errors,
`dotnet format --verify-no-changes` clean, **3,925 tests** across 6 projects all green
(Core 2,021 / Shared 793 / Data 265 / Server 807 / Discord 29 / Assets 10), web typecheck 0 errors,
lint clean (`prettier --check` needs the Foundry page reformatted after edits), Vitest 10 files,
Playwright 22 passed + 1 skipped, production build OK.

### Session-91 (2026-10-26) — B1: one content rule set, shared by audit / write path / CI

**Shipped:** `RealmEngine.Data/Validation/Content/` — a pure content-validation library. `ContentIssue`
(rule, message, severity, field, identifier) + `ContentValidationResult`, `ContentRules` (stable ids),
`ContentReferenceCatalog` (+ `LoadAsync(ContentDbContext)`, `ForDomain`), `IContentValidator` /
`ContentValidator<TEntity>`, `ContentShapeRules`, one validator per family (item/weapon/armor, ability/spell,
quest, recipe, loottable, zonelocation, archetype, instance, dialogue, plus `StructuralContentValidator` for
species/class/background/skill/material/materialproperty/enchantment/organization/language), and
`ContentValidatorRegistry.CreateDefault()`.

**Key decisions**
- `QuestObjectiveKinds` moved to `RealmEngine.Shared/Models` (gameplay *and* validation need it; the server
  copy in `CharacterQuestEntry.cs` is gone). `ContentReferenceRules` moved into the same library with its
  rule-id constants aliased to `ContentRules`, so the audit's existing logs stay comparable — the previous
  `ContentReferenceAuditTests`/`QuestObjectiveAuditTests` now prove "same rule, same identifier".
- The registry is keyed by the **`ContentSchemaRegistry`** keys, which is what B2's `/{type}` segment will
  resolve — including the easy-to-miss ones: `zonelocation` (not `worldlocation`) and `language`.
- `spawn_pool_empty`, `rarity_weight_out_of_range`, `craftingSkill` misses and "recipe consumes nothing" are
  **warnings**, not errors. Safe locations and free recipes are legitimate; blocking them would be a false block.

**Three real content bugs the validators found** (allow-listed by name in
`SeededContentValidationTests`, for B3 to fix):
1. `the-lost-shipment` objective `collect:lost-cargo` — item does not exist.
2. `gather-mushrooms` objective `collect:glowcap-mushroom` — item does not exist.
3. `arcane-focus` has **ManaCost = -5** (a negative cost would refund mana if it were ever cast).

**Trap found once:** recipe ingredients point at **materials** with `ItemDomain = "items/materials"`, not at
items. Resolving every ingredient against `Items` reports ten false "unknown item" errors — always resolve
through `ContentReferenceCatalog.ForDomain(ingredient.ItemDomain)`.

**Tests:** `ContentValidatorTests` (36, Data.Tests) covers every family with good/bad fixtures plus
registry behaviour; `ContentValidatorCoverageTests` (4, Server.Tests) asserts every schema key has a validator
and no validator exists for a key the schema cannot describe; `SeededContentValidationTests` (3, Server.Tests)
runs all validators over the real `DatabaseSeeder` output. Data 265 → **301**, Server 807 → **814**, full
solution 3,925 → **3,968**.

**Also fixed:** two latent `CS8602`/`CS8604` warnings in `RealmEngine.Core.Tests/Behaviors/PipelineBehaviorTests.cs`
(they only surfaced once the project recompiled) — Moq `It.Is` takes an expression tree, so the null-safe
`ToString()` comparison now lives in `LogMatchers.MessageContains`.

### Session-92 (2026-10-26) — B2: the Foundry content editor (write path + edit form)

**Server (committed first, `ca3997b7`):** `GET /{type}/{slug}/validate`, `POST /{type}`,
`PUT /{type}/{slug}`, `DELETE /{type}/{slug}` behind `Permissions.ManageContent`. `PUT` takes
`expectedUpdatedAt` (409 when stale), keeps `Id`, and stamps `UpdatedAt`/`Version` itself.
`DELETE` deactivates (`IsActive = false`) — never a hard delete, because live game rows reference
content by slug. `ContentEntityTypes` maps a type key to the CLR type + DbSet by reflection over the
context's typed sets.

**Web:** per-type `GET /api/content/{type}/schema` (public, from `ContentSchemaRegistry`) plus a
`POST /{type}/{slug}/validate` that validates an *unsaved payload*; the GET version only re-reports
what is stored, which is why the POST was needed for the Validate button to be honest.
`web/client/src/lib/foundry/content-editor.ts` holds the pure form logic (dot-path read/write, value
coercion, `issuesByField`, `buildPayload`, `initialValues`) and the edit page
`(foundry)/content/[type]/[slug]/edit` is entirely schema-driven.

**Traps**
- `$state(initialValues(data.payload, data.schema))` trips `svelte/state_referenced_locally`; the
  documented escape is `$state(untrack(() => …))`. Do that instead of disabling the warning.
- `bind:checked={values[name]}` on a `Record<string, string | boolean>` does not typecheck; use
  `checked={…} onchange={(e) => …}` (same for `select`/`textarea` via `toInputValue()`).
- `GET /api/content/{type}/schema` shadows a `/{type}/{slug}`-shaped literal: content whose slug is
  literally `schema` is unreachable through `/powers/schema`. Acceptable, but remember it.
- `EF.Functions.ILike` is Npgsql-only, so any test that exercises `?search=` against the SQLite test
  host fails at translation; assert on a plain browse page instead.

**Tests:** `ContentWriteEndpointTests` (14) + `ContentEditorEndpointTests` (4) on the server;
`content-editor.test.ts` (21 cases) in Vitest. Server 814 → **832**, full solution **3,986**;
web typecheck 0 errors/warnings, lint clean, Vitest 11 files, build OK.

### Session-93 (2026-10-26) - B3: content guard, and the three defects it made non-negotiable

**Content fixed (not tolerated):** `glowcap-mushroom` and `lost-cargo` added to `ItemsSeeder` as real quest
items; `the-lost-shipment` objective `interact:village-blacksmith` changed to `talk:` (the implemented kind
advanced by `TalkToNpc`); `arcane-focus` `ManaCost = -5` changed to `0` (a passive is never cast, so a negative
cost was only a landmine that would refund mana). `SeededContentValidationTests` lost its allow-list and now
demands **zero** errors over the real seeder.

**The hole this exposed:** the audit *skipped* objective kinds it did not recognise (`_ => null`), which is why
`interact:` had been invisible since A5. Unknown kinds are now `unsupported-quest-kind` errors, `talk:` is
explicitly recognised as implemented-but-unverifiable (no NPC catalog yet), and
`AuditQuestObjectives_Ignores_Objective_Types_Without_A_Content_Reference` became
`..._Reports_An_Unknown_Objective_Kind` - the old test was encoding the bug.

**CI:** `.github/workflows/content-guard.yml` builds `Veldrath.Server.Tests` and runs only
`SeededContentValidationTests`, path-filtered to seeders/entities/validators/content feature/this workflow.
Pinned SDK `10.0.402`, matching `ci-server.yml`.

**Counts:** Server 832 -> **834**, full solution **3,988**.

### Session-94 (2026-10-26) - Milestone B closed (B1-B5)

**Commits:** B1 `dc18be95`, B2 server `ca3997b7` + web `2828686a`, B3 `63f4efb8`, B4 `f57663e3`,
CI consolidation `98b53179`, B5 `45057457`. Epic #39 and #50-#54 all closed.

**What the milestone produced**
- `RealmEngine.Data/Validation/Content/` - one validator per content family, a type-key registry, and the
  reference rules, shared by the startup audit, the `/api/content` write path and the CI test.
- `Veldrath.Server/Features/Content/ContentWriteEndpoints.cs` - validate / POST / PUT / DELETE behind
  `Permissions.ManageContent`, soft delete only, `expectedUpdatedAt` for concurrency.
- `web/client/src/routes/(foundry)/content/[type]/[slug]/edit/` - schema-driven editor; its pure logic lives in
  `web/client/src/lib/foundry/content-editor.ts` with 21 Vitest cases.
- `GET /api/content/{type}/schema` and `POST /{type}/{slug}/validate` (dry run over an UNSAVED payload).
- Mailpit in `docker-compose.dev.yml`; `NullEmailSender` logs no bodies in any environment.

**Content defects found and fixed** (not allow-listed): two dangling quest objectives (`glowcap-mushroom`,
`lost-cargo` now real items), `arcane-focus` ManaCost -5 -> 0, and the story quest's invented `interact:`
objective kind -> `talk:`. Unknown objective kinds are now `unsupported-quest-kind` errors, and
`talk:` is explicitly accepted as implemented-but-unverifiable.

**CI is now seven workflows on purpose.** `format.yml`, `repo-hygiene.yml` and the `content-guard.yml` this
milestone briefly added are gone: formatting is a step in each .NET component job, the markdown link check is a
step in `docs.yml` (which now also runs on PRs, with deploy gated), and the content validation already runs in
`ci-server.yml` because it is part of that test project. Do not add a workflow for a check that a component
workflow already runs.

**Counts at close:** 3,999 .NET tests (Core 2,021 / Shared 793 / Data 301 / Server 845 / Discord 29 / Assets 10),
web Vitest 11 client files + 12 shared cases, typecheck/lint/build clean.

### Session-88 (2026-11-02) - Forgejo CI migration: mirrored actions, registry, releases

The repository now lives on Forgejo (`https://git.kungraseri.cloud/Veldrath/Veldrath`, instance
15.0.11). The first migration attempt left CI red on every push: 49 job tasks existed and every single
one was `failure` or `skipped`, each dying within a second of the push.

Root cause: the runner resolves actions from `DEFAULT_ACTIONS_URL` (default
`https://data.forgejo.org`), and that mirror does not host `actions/setup-dotnet`,
`actions/upload-pages-artifact`, `actions/deploy-pages`, `codecov/codecov-action`,
`marocchino/sticky-pull-request-comment`, `EnricoMi/publish-unit-test-result-action`,
`softprops/action-gh-release` or `SonarSource/sonarqube-scan-action`. An unresolvable action kills the
job before step 1, so there is no log worth reading. It is not a YAML problem.

Verified mirrored: `actions/checkout` (v7), `actions/cache`, `actions/setup-node` (v7.1.0),
`actions/upload-artifact` (v7.0.2), `actions/download-artifact` (v8.0.2), `docker/setup-buildx-action`,
`docker/login-action`, `docker/build-push-action`, `forgejo/upload-artifact`, `forgejo/download-artifact`.
Check any candidate with:
`curl -o /dev/null -w '%{http_code}' https://data.forgejo.org/api/v1/repos/<owner>/<action>`

Decisions (user-approved): Forgejo-native, no SaaS. Codecov dropped, SonarCloud dropped, docs published
as an artifact instead of Pages, images to the Forgejo registry.

The workflow set is now five: `ci-engine`, `ci-server`, `ci-discord`, `ci-web`, `docs`.
`release.yml`, `sonarcloud.yml` and `codecov.yml` are deleted. Releases are created inside each package
job, because fetching an artifact cross-run is a GitHub-API dependency.

New scripts (bash; the runner image is not guaranteed to ship PowerShell):
- `scripts/generate-version.sh` - port of `generate-version.ps1`, byte-identical output across all four
  `versions/*.props` files. Also fixes a latent bug: the .ps1 mis-reads `Directory.Build.props` because
  `VersionMajor`/`VersionMinor` carry `Condition` attributes, producing `VersionMajor .VersionMinor .<n>`.
- `scripts/lib/forgejo-api.sh` - shared curl/awk helpers.
- `scripts/forgejo-release.sh` - creates the release and uploads assets via the API, idempotently.
- `scripts/forgejo-pr-comment.sh` - sticky PR comment located by hidden marker (replaces marocchino).

Two bugs worth remembering, both found by testing against a local fake API:
- `set -u` plus a global set inside a function called as `X="$(fn)"`: the command substitution runs in a
  subshell, so the global never reaches the caller. Pass an output file path instead.
- JSON extraction with a greedy `sed 's/.*"id":...'` picks the LAST id. In a Gitea/Forgejo release object
  that is the nested author/repo id, not the release id. Use awk `index()` for first-match.

Registry: images are `git.kungraseri.cloud/veldrath/<image>` (owner lowercased). `docker-compose.coolify.yml`
now pulls from there, so Coolify needs registry credentials for that host. No buildx cache: `type=gha` is
GitHub-only and registry cache manifests are not reliably supported.

Docs hosting: Forgejo has no Pages server. `docs.yml` uploads `_site/` as the `docs-site` artifact.

Reference: `docs/ci-forgejo.md` (allow-list, runner setup, registry, releases, troubleshooting).
Open: the wiki push uses port 22222 (machine-local config, not in `.gitmodules`); `plans/github-issue-refresh.md`
still carries historical github.com issue URLs.

### Session-89 (2026-11-02) - Forgejo CI actually green: the four real blockers

Session-88 recorded the wrong root cause. Corrected after reading job logs, which ARE
reachable anonymously on this build:

  curl -s "https://git.kungraseri.cloud/Veldrath/Veldrath/actions/runs/<run_number>/jobs/<job_index>/attempt/1/logs"

The per-run jobs API 404s, which made it look like logs needed auth. They do not - only
the URL shape was wrong. Runner logs use emoji markers, not ##[error]: grep for
"star Run Main", "check Success - ", the cross mark, "gear [runner]" and "flag Job failed".

Blocker 1 - actions resolve from THIS INSTANCE's DEFAULT_ACTIONS_URL, which is
https://github.com, not data.forgejo.org. Proof in every log:
`git fetch 'https://github.com/actions/checkout' # ref=v7`. So `forgejo/upload-artifact`
cannot be cloned at all ("could not read Username for 'https://github.com'"). Use
`actions/upload-artifact`. The rule is: the name must exist at DEFAULT_ACTIONS_URL, and
the action must not call the GitHub API (codecov, gh-release, sticky-comment, check-runs).

Blocker 2 - artifact actions must be v3. v4+ embeds @actions/artifact v2 and fails at the
END of a green job with:
  ::error::@actions/artifact v2.0.0+, upload-artifact@v4+ and download-artifact@v4+ are not currently supported on GHES.

Blocker 3 - `$GITHUB_PATH` is not honoured by this runner. Installing the SDK and
exporting PATH inside the install step made the NEXT step fail with "dotnet: command not
found". Fixed by `scripts/ci-toolchain.sh`: install into /usr/local/share/dotnet, symlink
into /usr/local/bin (always on PATH), same for reportgenerator, and provision zip.

Blocker 4 - service containers are on the job network and are reached by SERVICE NAME.
`DATABASE_URL=...@127.0.0.1:5432` fails with a bare exit 1 after "applying migrations";
`...@postgres:5432` works. The mapped port is not published to the job's loopback.

Also: actions/setup-node with `cache: npm` spends minutes in the POST step tarring
node_modules; that is what makes web runs look stuck, not the build.

Outcome on run 55/56/59: Engine, Discord and SvelteKit Web are GREEN. Server and the
game-loop e2e job failed only on a transient DNS failure fetching an action
("Could not resolve host: github.com"), which is why setting
DEFAULT_ACTIONS_URL = https://data.forgejo.org is recommended in docs/ci-forgejo.md -
all six actions used here exist there with the exact tags.

### Session-90 (2026-11-02) - web parity matrix + coverage gate (PR 1 of the web pass)

**Goal.** The user asked for a full feature-parity and coverage pass on the SvelteKit app:
90% unit coverage ("well above 85"), plus parity validation. Crafting, NPC dialogue, dungeons,
the skills UI, and every engine-only system (harvesting, enchanting, salvaging, socketing,
upgrading, reputation, achievements listing, NG+) are **deferred** with their own plans.
Plan: `plans/web-feature-parity-and-coverage.md`.

**Baseline measured before anything changed** (`npm run coverage --workspace client`, 11 files /
204 tests): statements 24.28%, branches 23.35%, functions 28.10%, lines 27.12% (1262/4652).
`src/lib/foundry` 100%, `src/lib/game` 75.9%, `src/lib/server` 8.3%, every route 0%.
4652 executable lines is the whole denominator — there is no path to 90 that skips the ~55 page
components, because `account/+page.svelte` (290 lines) and `characters/new/+page.svelte`
(280 lines) are the two largest untested files.

**Gate.** `web/client/vite.config.ts` coverage thresholds: 90 global (lines/statements/functions/
branches) plus glob floors — 92 for `src/lib/server/**`, `src/lib/game/**`, `src/lib/foundry/**`;
90 for `src/routes/**/*.ts`; 85/85/80/85 for `src/**/*.svelte`. Reporters now include
`text` + `json-summary`. Verify glob thresholds work: Vitest 5 supports them and glob files still
count toward the global aggregate, so floors raise the bar without exempting anything.
`web/shared` stays out of coverage on purpose (declaration-only; documented in its vitest config).

**Parity suites** (`web/client/src/lib/parity/`, 27 tests): hub methods (52 vs 41 invokes),
hub events (55 vs 39 subscriptions), REST routes (141 vs 67 client paths), content vocabulary
(21 registry keys vs browse/detail/write switches), route breadth (47 pages + 6 handlers vs
Playwright specs). Each has an allow-list of `Record<name, reason>`; stale or shadowed entries
fail the test, which is what stops the lists rotting.

**Two real defects the matrix found, both fixed:**
1. `ContentSchemaRegistry` advertises `weapon` and `armor`, but `BrowseAsync` and
   `BrowseDetailAsync` had no switch arms for them — the Foundry browser 404'd on both catalogs.
   Fixed with `db.Items.Where(x => x.ItemType == "weapon" | "armor")` (NOTE: the discriminator is
   `Item.ItemType`, not `TypeKey`).
2. 16 hub broadcasts had no subscriber. Nine are now handled in `session.svelte.ts`
   (`OnWarned`, `OnMuted`, `OnEmote`, `OnAnnouncement`, `OnTeleported`, `OnSummoned`,
   `OnItemReceived`, `CharacterIgnored`, `ZoneLeft`); seven stay documented (three deferred
   features, plus `EnemyEngaged`, `CharacterStatusChanged`, `ZoneLocationUnlocked`, `OnWhisper`).

**Also fixed in the same PR:** chat never called `SendChatMessage`, so every slash command was
unreachable. `/…` now routes through `SendChatMessage` and `GetChatCommands` feeds a command list
in `RightSidebar`. Added a root `+error.svelte` (there was none, so 404s and thrown loads fell
through to SvelteKit's unstyled page).

**Recorded, not fixed:** `GET /api/announcements` has zero consumers (superseded by
`/api/editorial/announcements`) — removal candidate, and the route-parity rule names it.
`NavigateToLocation`/`UnlockZoneLocation` have no web caller by design: the map graph is the
navigation surface and unlocks happen server-side.

**Test harness** (`web/client/src/lib/testing/`, `#lib/testing/*`, excluded from coverage):
`makeServerEvent`/`fakeUser`/`fakeSession`, a recording `installFetch` + response factories,
`withEnv` (works because `#lib/server/env` exports `process.env` itself), and `walkFiles`/
`readRepoFile`/`stripComments`/`extractBracedBlock` for source-reading tests.

**Traps found:** `walkFiles` needs `Dirent.parentPath` (Node ≥20.12); scanning client paths must
stop only at quotes/backticks or `${encodeURIComponent(x)}` gets truncated; normalize `${x}`
**before** `{x}` or a stray `$` survives; `session.test.ts`'s `FakeHub.invoke` now returns
`results.get(method)` so `GetChatCommands` can be exercised; `connect()` records `GetChatCommands`,
so the connect-lifecycle assertion expects two invocations.

**Verification:** web typecheck 0/0, lint clean, **249 Vitest tests** (was 204), production build
OK, `dotnet build Veldrath.Server.slnx` 0 warnings / 0 errors, 129 content server tests green.
The coverage step is deliberately red until the coverage pass lands; total 27.12% is the number
PR 2 starts from.

**PR 2 - the whole `src/lib/server` layer is now covered (90 -> 94%).** 13 new test files, 390
Vitest tests total. `src/lib/server`: lines 8.27% -> **94.03%**, statements **94.26%**, functions
**95.90%**, branches **93.79%** — the 92 floor passes on every metric. Global: 27.12% -> **33.29%**
lines.

How the layer is tested, because the patterns repeat:
- `vi.hoisted` + `vi.mock('#lib/server/game-request')` turns `character-api`, `content-api`,
  `editorial-api`, `foundry-api`, `admin-api`, `game-api`, and `access` into path/verb/query
  assertions. One case per helper; the expectations are the contract with
  `Veldrath.Server/Features/**`.
- `game-request` is tested against a real `fetch` stub (`installFetch`) so the JSON parsing, 204,
  invalid-JSON, 400/409/422 bodies, 401 challenge logging, 5xx body logging, and transport failure
  paths all execute. `vi.mock('#lib/server/game-token')` is mandatory there; otherwise the real
  minting runs and `jose` throws "Key for HS256 must be ... Received an instance of Uint8Array"
  under jsdom (cross-realm `Uint8Array`).
- `// @vitest-environment node` is required for `game-token`, `email`, `env`, `db`, and `auth`
  for the same reason. `email.test.ts` also mocks `$app/env` with `dev: false` to cover the
  production branch that must not log reset links.
- `getAuth()` is coverable without a database: `postgres()` and `betterAuth()` are lazy, so a
  fake `DATABASE_URL` is enough to execute the whole config (including all three OAuth provider
  branches).
- `env.ts` is covered by writing a temp `.env`, `process.chdir`, `vi.resetModules()`, then a
  dynamic import. That pins the precedence rule: a process value is never overridden by the file.

Traps worth remembering: `gameApiFetch(identity, path)` records only two args, so a mock-call
assertion must pad to three or compare `slice(0, 2)`; `listLoreArticles` takes `category` first,
not `page`; `CreationEquipmentPreferences` is `preferredWeaponType` / `preferredArmorType` /
`includeShield`; `form.Field` values need `new File([...], 'x')` in `FormData` to reach the
non-string branches.

Next blocker for the global gate: `src/lib/game` sits at 76.3% because the shell components
(`GameScreen` 0%, `CardinalMap` 0%, `RightSidebar` 0%) have no render tests, then every route
file at 0% and the ~55 page components.

**PR 3 - the game shell is covered, `src/lib/game` 76.3% -> 92.16% lines.** One new suite
(`src/lib/game/shell.test.ts`, 20 tests) covers `GameScreen`, `CardinalMap`, and `RightSidebar`;
`src/lib/game` tests now number 210 and the whole web suite 410. Global lines 33.29% -> **38.75%**.

Two things that made it work:
- **Drive the real `GameSession`, not a fake.** The shell reads ~20 derived getters
  (`zoneLabel`, `canRest`, `canShop`, `adjacentNodeIds`, `messagesForChannel`), so a fake would be
  a parallel model. `new GameSession()` + direct state assignment reproduces the browser, and
  `vi.spyOn(session, 'travel')` still asserts the command. This also avoids extending the 170-line
  `fakeSession` in `components.test.ts`.
- **Mock `#lib/hub` in any component test that imports the session class.** `hub.ts` imports
  `$app/env/public`, whose client runtime does `const { env } = globalThis.__sveltekit_dev` and
  throws under Vitest. `vi.mock('#lib/hub', () => ({ createGameHub: () => null }))` is enough
  because the session only creates a hub inside `connect()`.

**Gate correction worth remembering:** the `src/lib/game/**` glob included the `.svelte` shell, so
its 92 *branch* floor was counting v8 branches from compiled templates (`{#if}` chains, snippets)
and was unreachable by design. The glob is now `src/lib/game/**/*.ts`; components are governed by
the `src/**/*.svelte` floor. Same rule as before: fix the glob, never lower the number.

Remaining: `GameOverlays` dialog branches, `hub.ts`/`auth-client`, every route file at 0%, and the
~55 page components.

**PR 4 - the route layer has started (public loaders + roster/play guards).** 25 tests in
`src/routes/public-loaders.test.ts` and `src/routes/game-loaders.test.ts`; web suite 435 tests.
`src/routes` 0% -> **3.33%** lines, global 38.75% -> **40.16%**.

Two harness additions the rest of the route tests need:
- **`makeServerEvent({ form })` builds a urlencoded body, not `FormData`.** Node's `Request` and
  jsdom's `FormData` are different realms, so the multipart body does not survive and
  `request.formData()` rejects with "Content-Type was not one of multipart/form-data". Tests that
  exercise a form action must use `form: { id }`.
- **`callLoad(load, event)`** is the one place the route-specific `ServerLoadEvent` cast lives.
  Every generated load expects a literal `route.id` and a required `params.id`, so a shared fake
  event never typechecks on its own. Do not add per-route aliases; call `callLoad`.

Two behaviours worth remembering: guards in non-async loads (`/game`) throw synchronously, so they
are asserted with `expect(fn).toThrow()` and not `.rejects`; async guards (`/play/[id]`,
`/characters`) reject, and a `fail(...)` result is a value, not a rejection.

Still zero coverage: the creation wizard (`/characters/new`), the `api/**/+server.ts` handlers,
auth/account actions, the four staff route groups, and the ~55 page components.

**PR 5 - browser libs and the auth route loads.** `src/lib` root 0% -> **66.66%** lines, global
40.16% -> **41.13%**, web suite **455 tests** in 40 files.

- `hub.test.ts` pins the two connection misconfigurations (missing `PUBLIC_HUB_URL`, `http://` hub
  on an `https://` page) plus a real `HubConnection` build. It mocks `$app/env/public` with a
  getter-backed object so each case can change the URL.
- `auth-client.test.ts` asserts `signIn.username` exists — the username plugin's own availability
  helper must NOT be read in a test: reading it fires a network request (the first attempt produced
  `ECONNREFUSED 127.0.0.1:3000` as an unhandled error).
- `brand-icons.test.ts` guards the vendored SVG paths against truncation.
- `auth-pages.test.ts` covers `/login` and `/register` (signed-in redirect, anonymous allowed),
  `/verify-email` (missing token short-circuits before Better Auth; verified; invalid logs and
  returns a status instead of throwing), and `/account` (provider/session mapping, all three OAuth
  providers linkable, and the degraded path where both lookups fail).
- `forgot-password` and `reset-password` have no `+page.server.ts`; they are client-side
  `authClient` calls, so they belong to the component tests.

Harness change: `session: null` now means "signed in with no session row" — previously `??` treated
null as absent and injected a synthetic session, which hid the account page's null-session path.

**PR 6 - the public pages render, and `src/routes` went from 3.33% to 63.33% lines.** One suite
(`src/routes/pages-public.test.ts`, 15 tests) renders home, lore index/detail, patch-notes index,
player profile, and the hardcore board. Global 41.13% -> **44.87%** lines (470 tests, 41 files).

- These pages take only `data` and import `resolve()` from `$app/paths`; both work under Vitest with
  no stubbing. That is why they came before the auth pages, which read `$app/state` and
  `$app/navigation`.
- Cast the fixture once at the render call (`props: { data: fixture as never }`). Each route has its
  own generated `PageData`; importing six of them into one test is noise, and the loaders already
  enforce the shapes.
- Cover the empty states deliberately — a page with no rows is where these ship broken, and each
  one has its own copy branch.
- `{@html}` content is asserted through `container.querySelector('.markdown h1')` rather than
  `getByText`, because the injected markup is the thing under test.

**PR 7 - the auth pages render, and the `$app/*` harness landed.** Global 44.87% -> **56.24%** lines
(2666/4740), 51.67% statements, 46.94% branches, 58.72% functions; web suite **532 tests in 43
files**. `.svelte` lines 37.31% -> **49.58%**.

The stubbing, which every page-level test now depends on:
- `web/client/vite.config.ts` has a `test.alias` (not a build alias) mapping `$app/state` ->
  `src/lib/testing/app-state.ts` and `$app/navigation` -> `src/lib/testing/app-navigation.ts`. Both
  live under `src/lib/testing/**`, which is excluded from coverage.
- `app-state.ts` exports a mutable `page` and `setPage(overrides)`. **Call `setPage` before render**:
  `$state(page.url.searchParams.get('token') ?? '')` runs when the instance is created, so a later
  call changes nothing.
- `app-navigation.ts` exports spies. Declare them as `vi.fn<(args) => result>(async () => {})` - the
  generic carries the caller-visible signature and the empty body avoids ESLint's `no-unused-vars`,
  which rejects `_`-prefixed parameters in this repo. `resetNavigation()` clears them all through a
  `{ mockClear(): void }[]` so the differing mock types do not fight.
- `$app/paths` needs no stub; `resolve()` works under Vitest as-is.

Suites: `src/routes/pages-auth.test.ts` (29 tests - login, register, verify-email, forgot-password,
reset-password) and `src/routes/pages-account.test.ts` (33 tests). `SocialLoginButtons` is covered
through the login page instead of in isolation.

Test-writing details that cost time:
- `getByText('Unknown IP')` fails when the string is an interpolation sharing a paragraph with other
  text nodes; use `/Unknown IP/`. testing-library's `getNodeText` only concatenates *direct* text
  children, which is also why the same call succeeds for a plain `<span>`.
- jsdom does not implement `window.confirm`, so the unlink prompt is `vi.spyOn(window, 'confirm')`.
- jsdom only allows same-document navigation, so the "follow the returned provider URL" case feeds a
  `#linked` fragment and asserts `window.location.hash`; the `href` assignment under test is real.
- `reset-password` schedules `goto` with `setTimeout(..., 1500)`; assert the message first, then
  `await vi.advanceTimersByTimeAsync(1500)` under `vi.useFakeTimers()` (restored in `afterEach`).
- The account page renders three forms in order (profile, email, password) and has no form ids, so
  `forms(container)[n]` is the selector; the password form's three inputs are ordered
  current/new/confirm.
- The login "username path" test must not assert `goto` was not called - it navigates to `/characters`
  on success. Assert `signInEmail` was not called instead.

**PR 8 - the `api/**` route handlers.** One suite (`src/routes/api-handlers.test.ts`, 25 tests) covers
`api/game-token`, `api/characters/creation`, `creation/[id]`, `creation/check-name`,
`creation/[id]/[step]`, and `auth/[...all]`. Global 56.24% -> **57.74%** lines; web suite **557 tests
in 44 files**; `src/routes/**/*.ts` 17.57% -> **29.69%** lines.

- Call the exported `GET`/`POST`/`PATCH`/`DELETE` directly with `makeServerEvent`. Only
  `#lib/server/character-api`, `#lib/server/game-token`, and `#lib/server/auth` are mocked;
  `gameResultResponse` and `requireUser` run for real, so the status mapping and the guard are
  covered by the same assertions.
- `requireUser` throws inside an `async` handler, so an anonymous case is
  `await expect(call(GET, event)).rejects.toThrow()` - never `expect(fn).toThrow()`, which would pass
  for the wrong reason or miss a rejection entirely.
- `makeServerEvent({ params, body, method: 'PATCH' })` already sets `content-type: application/json`
  for a body, so `request.json()` works; an array body is what exercises the `readJson` rejection
  branch (the route rejects non-objects).
- The `[step]` route is worth one case per arm: each choice step has its own field name and its own
  `400` message, and `attributes` filters non-integer values before calling the API, so a
  `{ strength: 2, luck: 'lots', fate: 1.5 }` body must arrive as `{ strength: 2 }`.

**PR 9 - the character creation wizard.** One suite (`src/routes/pages-creation.test.ts`, 21 tests)
drives `characters/new/+page.svelte` (638 lines, the largest component in the app). Global 57.74% ->
**63.18%** lines; web suite **578 tests in 45 files**; `.svelte` 49.58% -> **58.05%**.

- Stub `fetch` with `installFetch` and answer by `` `${method} ${pathname}` ``; make the handler
  **throw** on an unknown route so a page calling something unexpected fails loudly instead of
  returning a confusing parse error.
- The step header (`Step 1 of 7 · Name`) renders even while the session is still starting, so waiting
  for it proves nothing and the form is not there yet. Wait for the **Cancel** button, which only
  exists inside the started branch. This cost a full round of 16 false failures.
- There is a 350 ms debounce before the name availability check. Keep real timers and use
  `findByText` (1 s default) rather than fake timers: `waitFor`/`findBy` both need real timers, and
  the whole suite still runs in about a second.
- `getByText('9')` is ambiguous on the attributes step because the remaining-points counter also
  reads 9 after one increase; assert the allocation from the PATCH body instead.
- `aria-pressed` is an attribute, not a property: use `.getAttribute('aria-pressed')`, not
  `toHaveProperty`.
- `validateCharacterName` returns the full sentence ("Name may only contain letters and spaces."),
  so match the real string rather than a fragment.

**PR 10 - the staff group guards and the moderation pages.** One suite
(`src/routes/staff-loaders.test.ts`, 23 tests) covers the four group layouts (admin, editorial,
mod, foundry) and the four moderation route modules. Global 63.18% -> **64.49%** lines; web suite
**601 tests in 46 files**; `src/routes/**/*.ts` 29.69% -> **40.27%**.

- `error(...)` has an **empty** `message` - the text is in `body.message` - so `toThrow('...')` cannot
  assert a 403/404 message. A small `thrownMessage()` helper that reads `body.message` is needed, and
  `expect.requireAssertions` means the helper must throw if nothing was thrown at all.
- A layout that is **not `async`** (`(foundry)/+layout.server.ts`) throws synchronously, so it needs
  `expect(() => ...).toThrow()`; every other layout rejects. Same rule already known from `/game`.
- The layouts read `access` from `parent()`, so the fake event's `parent` is replaced per test:
  `event.parent = async () => ({ access })`. `requirePermission` and `hasPermission` run for real, so
  `roles: ['Admin']` works with an empty permission list while a plain account is refused.
- Declare the access fixtures with an explicit interface. `{ permissions: [] }` infers `never[]`, and
  spreading a `['content.manage']` variant into it fails typecheck.

**PR 11 - the admin and editorial page modules, and the `docs/coolify-site` merge.** One suite
(`src/routes/staff-admin-editorial.test.ts`, 31 tests) covers the eight admin page modules and the
seven editorial ones. Web suite **632 tests in 47 files**; typecheck 0/0, lint clean.

- The admin user detail page has ten form actions; they all follow the same shape (`stringField` ->
  API -> `fail(400, { error })` or `back()`), so a table of assertions per action is enough and the
  two validation guards (warn and ban require a reason) are the only branches worth their own cases.
- The role catalogue page is static data. Assert that the Admin row's permissions **equal**
  `allPermissions`: if someone adds a permission and forgets the Admin row, that is a real bug.
- Editorial `publish`/`delete` mocks must be seeded in `beforeEach` like the others. A missing mock
  returns `undefined`, and `result.error` then throws a `TypeError` that reads like an assertion
  failure.

**Merge fixes (five incoming commits, `docs/coolify-site`).** The merge was clean; the incoming work
had two defects that had to be repaired:

1. **A workflow step swallowed `retention-days`.** The new `Queue the docs deployment` step was
   inserted between `path:` and `retention-days: 30` of the `Upload the generated site` step. The
   artifact lost its retention, and `retention-days: 30` became a line of the deploy step's `run:`
   script. Fixed by moving it back under the upload step's `with:`; verify workflow edits by parsing
   the YAML (`python3 -c "import yaml; yaml.safe_load(...)"`) and printing each step's keys, not by
   eyeballing indentation.
2. **36 broken relative links in `plans/web-feature-parity-and-coverage.md`.** From `plans/` the
   correct prefix is a single `../`; the file had `../../web/...` (resolves above the repo root) and
   root-relative `web/...`. `node scripts/check-markdown-links.mjs` is a gate inside `docs.yml` and
   was failing on all of them. It now prints *Checked 43 Markdown files: no broken relative links.*
   Run that script after touching any Markdown that links into the tree.

**PR 12 - the Foundry page modules.** One suite (`src/routes/foundry-loaders.test.ts`, 30 tests)
covers the content browser, the editor (validate/save/deactivate), the language fork, the
notification inbox, and the submission queue. Global 64.49% -> **71.35%** lines; web suite **662
tests in 48 files**.

- Mock `#lib/server/access` with `importOriginal` and override only `loadAccountAccess`: the routes
  need the real `Permissions`, `hasPermission`, `requireUser`, and `toGameIdentity`, and the
  important assertion is that `canEdit` follows `content.manage`.
- The editor passes `detail.data.summary.updatedAt` as the fifth argument to `updateContent`. That is
  optimistic-concurrency plumbing; a test that only checks four arguments would not notice it going
  missing.
- Form field names and payload keys differ in the language fork: `initialClusters` ->
  `allowedInitialClusters`, `syllablePatterns` -> `allowedSyllablePatterns`. Assert the payload key.
- `listSubmissions` uses `totalCount` (Foundry API) while the admin API uses `total`. Both appear in
  this codebase; check which shape a loader reads before writing the fixture.
- `vi.mock` is hoisted above imports, so static imports of the route modules are enough - no
  top-level `await import(...)` dance needed.

`src/routes/**/*.ts` now clears its lines/functions/statements floor; only its **branch** coverage
is red at 76.33%. Still red: `src/**/*.svelte` 58.05% lines (staff pages, layout chrome, site
components) and `src/lib/game/**/*.ts` branches 82.74 (real `session.svelte.ts` guard branches, not a
glob problem; belongs with the `GameOverlays` work).

### Session-91 (2026-11-02) — No dependency bot: `.github/dependabot.yml` deleted

Forgejo runs no Dependabot service, so `.github/dependabot.yml` was inert; it is deleted (PR #9,
which moved its content into docs). The update cadence and every major-version hold now live in
[`docs/ci-forgejo.md`](../../docs/ci-forgejo.md), section "Dependency and action updates (there is
no bot)": a first-Monday-of-the-month grouped NuGet (`Directory.Packages.props`) + npm (`web/`)
review, MediatR kept **below 13**, the coupled SvelteKit / Vite / TypeScript majors moved as one
migration, and action tags never swept.

Earlier session entries that describe the file as configuring nuget / npm / github-actions / docker
are historical — do not go looking for `.github/dependabot.yml`, and do not re-create it. If a bot
is ever wanted here it is self-hosted Renovate with `renovate.json`, not a resurrected Dependabot
config.

### Session-92 (2026-11-03) — Merge gate: `development` and `main` are branch-protected

Both integration branches carry one protection rule, applied by
[`scripts/apply-branch-protection.sh`](../../scripts/apply-branch-protection.sh) (idempotent, and it
reads the rule back; `--check` is the drift test) and reasoned in
[`docs/ci-forgejo.md`](../../docs/ci-forgejo.md#branch-protection-what-can-block-a-merge) (PR #17):

- `enable_status_check: true` with `status_check_contexts: ["*"]`
- `enable_push: false`, no whitelist — a protected branch changes only through a pull request
- `required_approvals: 0`, `apply_to_admins: true`

**Why `["*"]` and not the five job names.** Every workflow filters on `on.pull_request.paths`, and
Forgejo 15 treats a required pattern that matches no status as `pending` — a merge blocker — while a
workflow excluded by `paths` creates no run and reports no status at all (`detectWorkflows`,
`services/actions/notifier_helper.go`; the upstream skipped-status fix, go-gitea/gitea#38237, is not
in 15.0.11). Naming the five contexts would deadlock every docs and `plans/` pull request: PR #14
(head `95ecfecc`, `plans/*.md` only) carries exactly one status, `Deploy Documentation / Build Docs
(pull_request)`. The single glob means "at least one status must exist on the head commit, and every
status that exists must be `success`". A detector job holding a second copy of the five `paths` lists
was rejected — a drifted copy either deadlocks or silently skips a component.

**A refused merge is now the normal failure mode, not a bug.** A pull request with a red check is
answered `405 not allowed to merge [reason: Not all required status checks successful]` even though
`mergeable` is `true` at the git level. Fix the check; do not reach for `force_merge`. An instance
admin with `force_merge: true` clears the protection error (`CheckPullMergeable`,
`services/pull/check.go`) because `apply_to_admins` only withholds that override from *repository*
admins. That is break-glass for the only operator, never a way to land a red change.

**Revisit only if these become true** (do not act on them now): switch to named contexts when one
always-running `pull_request` workflow becomes the sole merge gate, or when Forgejo starts reporting
a status for path-filtered workflows; raise `required_approvals` when a second account exists —
`kungraseri` is the only account and an author cannot approve their own pull request, so any
non-zero value blocks every merge.

**Update, 2026-11-03 18:56Z — the stored rules no longer match the bullets above.** At the
operator's request the rules were PATCHed to `apply_to_admins: false` on both branches, and `main`
alone was given `enable_push: true` with `push_whitelist_usernames: ["kungraseri"]`. Reason: `main`
pinned the `wiki` submodule at a commit `Veldrath.wiki` never had, so all four Coolify deploys died
at `git clone --recurse-submodules`, and the pull request that fixed it (#31, closed) could not
merge while its statuses were pending — the pin was pushed straight to `main` as `d7c19f6`. Two
things measured then, worth keeping: `apply_to_admins: false` does **not** lift `enable_push: false`
(the pre-receive hook refuses the administrator too, so the whitelist is a second switch), and
`development` still refuses every push. `scripts/apply-branch-protection.sh` and the doc table
encode the current shape, so a rule reporting `apply_to_admins: true`, or `enable_push: false` on
`main`, is now drift rather than a repair. Both rules also pin `push_whitelist_teams: []` and
`push_whitelist_deploy_keys: false` in the payload and in the read-back — the bypass is one named
account, never a team or a deploy key; the API validates a team name and round-trips the deploy-key
switch (measured). `scripts/tests/apply-branch-protection.test.sh` covers those drift cases offline
(stubbed API, no token) and runs in the docs workflow.

### Session-93 (2026-11-03) — Web coverage gate green: branch tail closed

`npm run coverage --workspace client` now **passes** (exit 0), which is the first time the gate the
user chose has held:

| Metric | Value |
|---|---|
| Lines | 99.19% (4702/4740) |
| Statements | 99.24% (7596/7654) |
| Functions | 98.83% (1614/1633) |
| Branches | 95.54% (2937/3074) |

80 test files, 1096 tests. Per-glob floors also clear: `src/lib/server` 99.67 lines / 100 branches,
`src/lib/game/**/*.ts` 99.73 / 99.47, `src/lib/foundry` 100 / 98.08, `src/routes/**/*.ts` 99.49 /
97.83, `src/**/*.svelte` 98.95 / 92.66. The `src/lib/game/**/*.ts` and `src/routes/**/*.ts` rows are
the globs the thresholds actually match; the directory totals that also include the `.svelte` files
beside them are lower (99.88 / 95.68 and 98.70 / 94.77) and are not the floored numbers. PR #13. The
path there (PRs 1–13 of the coverage pass) is
recorded in [`plans/web-feature-parity-and-coverage.md`](../../plans/web-feature-parity-and-coverage.md),
which is the live document for this work — the earlier "still red at 76.33% / 58.05%" notes in this
file are historical.

What future coverage work on this app needs to know:

- **Svelte compiles `{expr}` to `expr ?? ''`.** Most "missing branches" in a `.svelte` file are that
  fallback, not logic: render the same page with the field nullish and it is covered.
- **`<select value={…}>` compiles to `if (select_value !== (select_value = …))`**, whose false side
  cannot run in a static render. Those branches are not reachable from a test; do not chase them and
  do not exclude the whole file to hide them.
- **Stateful components need a fresh render per case.** The overlays, the character sheet, and the
  creation wizard keep pending state; a shared `beforeEach` render leaks it, and two mounted panels
  make `getByRole` ambiguous.

**.NET still has no coverage gate.** (*Superseded on 2026-11-03: PR #21 landed the gate — see
Session-96 for the enforcing step, the merged floors and the settlement measurement.*)
[`coverage.runsettings`](../../coverage.runsettings) collects
cobertura and every .NET workflow uploads a `CoverageReport/` artifact, but nothing compares it to a
floor, so `dotnet test` stays green at any coverage level. The SDK is present on kung-lab-hermes —
`~/.dotnet/dotnet`, version 10.0.403 — it is only missing from `PATH`, so
`export PATH="$HOME/.dotnet:$PATH"` gives a local number without waiting for CI. Tracked as its own
card (gate contract: which projects, which floors, cobertura-parse script vs
`coverlet.msbuild` threshold) — do not describe the 90% goal as met for .NET until that lands.

### Session-94 (2026-11-03) — `main`'s wiki pin was dangling, and a sync merge is not a content merge

Clearing the `development → main` pull request (#3) meant merging `main` back into `development`.
Two traps, both worth keeping:

- **The wiki submodule pin on `main` pointed at a commit the wiki repository does not have.** `main`
  pinned `d8d1edf` (and `90ea8f8` before it), but the wiki's own `origin/main` is `725950fd` — the
  same commit `development` pins. `git -C wiki cat-file -t d8d1edf` fails, so `git submodule update`
  on `main` dies with `reference is not a tree`. The wiki is a *separate repository* whose history
  was rewritten when it moved to KungGit, so a pin recorded in the superproject is not evidence the
  object still exists. Check with `git -C wiki cat-file -t <sha>` before bumping the pin.
- **`CONTRIBUTING.md` on `main` still had the pre-migration links** — GitHub issues and discussions,
  plus a `kungraseri.github.io` MkDocs site that no longer exists. `development`'s Forgejo
  equivalents won; main's wiki-link intent was already present identically in both versions.

Resolution kept `development`'s tree, so the sync merge changes no files: it exists only to make
`main` an ancestor of `development`. Until it lands, PR #3 reports `mergeable: false` no matter how
clean the content is — Forgejo compares histories, not just trees.

**Direct pushes to `development` are refused for everyone**, including the only admin:
`Forgejo: Not allowed to push to protected branch development` from the pre-receive hook. The rule
from Session-92 sets `enable_push: false` with no whitelist (and, since the 2026-11-03 18:56Z update
recorded under Session-92, no admin exemption either), so a sync like this cannot be pushed
directly. Forgejo also calls the resulting pull request an "empty commit"
when the trees match — give it a real change (this note) or loosen the rule deliberately.

### Session-95 (2026-11-03) — A root-config pull request reports no status, and `["*"]` then blocks it

Session-92 recorded the `["*"]` rule as "every check that reported must be green". The other half of
that sentence — *at least one status must exist* — is a real deadlock for a change that no workflow
watches, and it is not hypothetical: PR #27 (`wt/t_7db54bc9`, `4e8a2311`) touches only `.gitignore`,
which appears in none of the six `on.pull_request.paths` lists, so every workflow is filtered
out, no run is created, and the head commit carries zero statuses.

Read from the source rather than inferred (`services/pull/commit_status.go`, tag `v15.0.11`, the
version this instance reports): in `MergeRequiredContextsCommitStatus`, when a required pattern
matches no status the loop sets `returnedStatus = Pending` on `targetStatus == ""` and `break`s; the
`matchedCount == 0` fallback on line 65 only runs while `returnedStatus` is still `Success`, so with
an empty status list the state stays `Pending`, `IsPullCommitStatusPass` returns false, and the merge
is refused. `mergeable: true` on the pull request does not predict this — it is git-level only.

What that means in practice:

- The pre-protection pull requests (#1, #2, #5, #7, #9) did merge with zero statuses, so "it merged
  before" is not evidence; the rule has only existed since Session-92.
- A pull request reports if it touches **anything** in a list already: `web/**`, `**/*.cs`,
  `**/*.md`, `.github/agent-memory/**`, `docs/**`, `plans/**`, the engine/server/discord trees, or
  the coverage lists. Anything outside all of them — `.gitignore`, `.editorconfig`, `LICENSE` —
  reports nothing and waits.
- Forgejo 15.0.11 has **no workflow-dispatch API** (`GET .../actions/workflows` is 404), so a run
  cannot be started from a worker; only the web UI or a matching path triggers one. The operator's
  `force_merge` is break-glass, not a routine unblock.
- Posting a commit status by hand to satisfy the gate is not an acceptable unblock: it would let any
  agent self-certify a merge.

**Decided (2026-11-03, card `t_a6502114`): `["*"]` stays and one workflow reports on every pull
request.** `.github/workflows/merge-gate.yml` triggers on `pull_request` for `main` and `development`
with **no `paths` filter**, and runs `scripts/ci-merge-gate.sh --lint` — L1 every workflow has a
`name:`, L2 every `uses:` is one of the six actions mirrored on this instance, L3 every
`pull_request` workflow triggers on both `main` and `development`, L4 at least one `pull_request`
workflow has no `paths`/`paths-ignore` (the invariant that keeps the zero-status case impossible).
The decision, the rejected alternatives and the inventory are in `docs/ci-forgejo.md` under
"Branch protection: what can block a merge".

Why not the `paths`-list alternative: measured on the tree, **39 tracked paths in seven classes** match
no list — root dotfiles and editor config (10, `LICENSE` included), `NuGet.config` + `dotnet-tools.json`
+ `toc.yml` (3), the four root `.slnx` (4), `config/grafana/**` + `config/prometheus/**` +
`docker-compose.{coolify,dev,prod}.yml` (10), scripts no workflow names (10, after `docs.yml` claimed
`scripts/apply-branch-protection.sh` and `scripts/lib/forgejo-api.sh`), the `wiki` submodule pin (1),
and the merge gate's own workflow (1). Adding three names would leave 36 deadlocked and reopen on the
next root file. Those paths now merge on the merge gate alone — no build, no test — which is written
down there so nobody mistakes "green" for "built".

The deadlock itself was reproduced end to end before deciding, rather than taken from the Session-95
inference: probe PR #32 (branch `probe-x`, head `b59e841d`, only `.gitignore` changed) answered
`{"state":"","total_count":0}` and `[]` for statuses, `mergeable: true` for the pull request, and
`405 not allowed to merge [reason: Not all required status checks successful]` for
`POST /pulls/32/merge {"force_merge": false}`. The PR was closed and the branch deleted. The
workflow and script landed in card `t_48d8d3c1` (PR #38).

One correction to the force_merge reasoning recorded above: the token every worker runs with is the
instance administrator (`GET /user` → `kungraseri`, `is_admin: true`), so `force_merge` **is**
available to an agent — `CheckPullMergeable` clears the protection error for an admin. The gate is
therefore enforced by discipline, not capability: never use `force_merge` or a hand-posted status to
satisfy `["*"]`, because either one lets a worker certify its own merge. The API's
`GET .../commits/{sha}/statuses` also lists every state transition of a context, while the gate reads
the latest row per context (`GetLatestCommitStatus`, `services/pull/commit_status.go:128`) — a stale
`pending` row beside a `success` is not a red gate.

**Landed (2026-11-03, card `t_48d8d3c1`, PR #38).** `.github/workflows/merge-gate.yml` and
`scripts/ci-merge-gate.sh` exist, so the context
`CI - Merge gate / Merge gate reports on every pull request (pull_request)` now reports on every pull
request into `development` or `main` and `["*"]` always has a status to evaluate. Measured on PR #38
head `cf0c3cc6`: run 1357 `success` in **5 s** (`duration` on `GET /actions/runs/1357`; `pending` row
19:31:44Z → `success` row 19:31:50Z), combined commit status `{"state":"success","total_count":2}`
next to `Deploy Documentation / Build Docs (pull_request)`. The job is one depth-1 checkout plus the
lint; it must stay offline and dependency-free. `--uncovered` is the follow-up inventory — it
reproduces the runner's `PatternToRegex` translation (`**/` → `(.+/)?`, so `**/*.md` matches a root
`AGENTS.md`; the naive `**` → `.*` would over-report), and it now reports **39 paths** because the
gate and its script are themselves tracked and named by no `paths` list (`merge-gate.yml` declares no
paths at all). A change confined to one of those 39 still merges on the merge-gate status alone, with
no component build.

### Session-96 (2026-11-03) — The .NET coverage gate landed, and settling it moved no floor

PR #21 landed `scripts/coverage-floors.json`, `scripts/check-dotnet-coverage.mjs` and
`ci-coverage.yml` on `development` (merge `ab5dfe72`, after #20 `7d81b2ae` and #22 `4e713b08`). The
"no coverage gate" note in Session-93 is therefore superseded — but the enforcer is **not**
`dotnet test`. Plain `dotnet test`, even with `--collect "XPlat Code Coverage"`, only produces the
Cobertura report and stays green at any coverage level. The comparison is a separate step:
`ci-coverage.yml`'s `coverage-gate` job ("Enforce .NET Coverage Floors") runs `Test with coverage`,
then `Generate coverage report` (ReportGenerator merge), and only then `Enforce the coverage
contract`, which is

```bash
node scripts/check-dotnet-coverage.mjs \
  --report ./CoverageReport/Cobertura.xml \
  --floors scripts/coverage-floors.json \
  --suggest
```

That node script is what fails on a per-assembly or global regression, on an assembly missing from
the merged report, and on a shipping assembly carrying no floor. Reproduce the gate locally as
those three steps in that order — not by reading the exit code of `dotnet test`.

**Settling the floors on the merged base was a no-op, by the contract's own arithmetic.** Three full
runs on `0efb7c7c` with the CI-pinned SDK 10.0.402, each merged with ReportGenerator, are identical at
every package on line *and* branch: total 75.3806% / 55.5100% — **24,213 of 32,121 covered lines and
5,823 of 10,490 covered branches** — with 4,019 tests passing in every run.
`check-dotnet-coverage.mjs --suggest` names nothing on any of the three reports, because the largest
lift over a floor is +2.22 pp (Core branch) against D5 rule 2's 3-point trigger. (The "24,183 covered
lines" written in PR #22's body was a prose slip; ReportGenerator's summary says 24213 of 32121.)

**Two traps for whoever touches this next:**

- Do **not** "fix" the floors to the deterministic three-run rates. `measured` in
  `coverage-floors.json` is the six-run *lowest* observation that produced each floor (D4: floor =
  `trunc(lowest) − 1`), which is why Core's floor is 69 against a 70.49 low. Setting Core to 71
  because three clean runs said 71.12 puts the floor above the lowest observation and makes the gate
  fail intermittently — the exact failure the one-point subtraction exists to prevent.
- The `measured` block is the baseline, not a live field: no D5 rule refreshes it, so leave it alone
  when no floor moves. Live counts have drifted with the code since the baseline (+52 coverable lines:
  Core 13,459, Shared 1,488, Server 9,029), which makes its `lines` values read slightly low —
  informational, never a gate input.

### Session-97 (2026-11-04) — `Veldrath.Assets` deleted, and the tracker shrinks by half

The project and its test project are gone. It was **dead, not merely unused**: no static files were
ever served from it, and the directory held 1,837 art files (98 MB) that no code path read.

What actually proved it dead, in order of strength:

- `Veldrath.Server.csproj` carried a `<ProjectReference>` to it, but **no** `.cs` file under
  `Veldrath.Server/` referenced any type from the `Veldrath.Assets` namespace — a reference with no
  importer compiles and does nothing.
- `AddAssetStore` (the DI extension) was never called from any composition root, so `AssetStore` was
  never constructed at runtime.
- No `UseStaticFiles`/`MapStaticAssets` mount pointed at `GameAssets/`, so the tree was not reachable
  over HTTP either.
- No CI workflow referenced it, so nothing was even compiling it in the gate `Realm.Full.slnx` covered.

The nuance worth keeping: `AssetStore` was a generic content-addressed store, and the **desktop client
never used it either**. It was not killed by the web-first pivot — it was never wired up. Do not read
the deletion as "the pivot removed an asset pipeline"; there was no pipeline.

**Removed:** `Veldrath.Assets/` (1,837 files, 98 MB) and `Veldrath.Assets.Tests/`;
`versions/assets.props`; `scripts/download-assets.ps1`, `scripts/sync-assets.ps1`,
`scripts/package-assets.ps1`.

**References swept** (the first pass missed several; a wide `grep -ri` is mandatory for a project
removal): the three `.slnx` files that listed it (`Realm.Full`, `Veldrath.Libraries`,
`Veldrath.Server`), `Veldrath.Server/Veldrath.Server.csproj`, `Directory.Build.props`,
`Veldrath.Server/Dockerfile` and `Dockerfile.dev`, `docker-compose.dev.yml` (the source mount),
`scripts/coverage-floors.json` (its floor was line 45), the `GameAssets` binary-ignore block in
`.gitignore`, three tasks in `.vscode/tasks.json` (`build-veldrath-assets`, `test-veldrath-assets`,
`download-assets`), `.github/instructions/{testing,xml-documentation,build-strategy}.md`,
`.roo/skills/{run-all-tests,run-tests-by-component}/SKILL.md`, `scripts/generate-version.{sh,ps1}`,
`AGENTS.md`, `README.md`, and both path lists in `ci-coverage.yml`.

**Verification:** `dotnet build Realm.Full.slnx` → 0 warnings, 0 errors.
`dotnet test Realm.Full.slnx --no-build` → **4,011 passed, 0 failed** across five projects:
Core 2,040, Shared 795, Data 301, Server 846, Discord 29. The test-project count everywhere reads
**five**, not six.

**Inventory after the deletion** (`bash scripts/ci-merge-gate.sh --uncovered` on this tree): tracked
files **1,618** (was 3,472) and uncovered paths **36** (was 39); the "scripts no workflow names" class
fell 10 → 7 because the three asset scripts left with the project. `docs/ci-forgejo.md` records the new
numbers with the date, so anyone re-running `--uncovered` sees the same counts or knows the tree moved.

**Recovery:** the art tree still exists in history — `git log --diff-filter=D -- Veldrath.Assets/`
finds the deletion commit, and `git checkout <parent> -- Veldrath.Assets/GameAssets` restores any
subtree. Nothing needs re-downloading; the old download script pointed at third-party packs, not a
canonical source. `world-lore-plan.md` marks the TMX maps the same way.

### Session-98 (2026-11-04) — The wiki pin conflicts a second time, and why git cannot resolve it

Two pull requests were blocked on the same afternoon, both on the `wiki` gitlink and neither on code.

**PR #48 (`docs/memory-coverage-reporter`) conflicted with a change that did the same thing.**
PR #47 (`docs(coverage): drop the last ReportGenerator instructions`, `75a19a07`) landed the
`reportgenerator` → `scripts/coverage-report.sh` swap while the branch carrying the same swap was
still open, so the two edits met on one line. Resolved by taking development's text as the base and
keeping three things from the branch: the workflow's own `-reporttypes` list, the blank line removed
from inside the code block, and the paragraph recording that the new tool's class/method counts
differ from ReportGenerator's. Merge commit `846c082b`.

**PR #29 (`development` → `main`) reported `mergeable: false` again, for the same reason as
Session-94 and by a different mechanism.** The facts, all verified against the tree:

- merge base of `main` and `development` is `0efb7c7c` (PR #26)
- `main` holds exactly two commits `development` does not: `f3661a34` (PR #3's merge, which lives
  only on `main` because merges flow one way) and `d7c19f67` (wiki pin → `725950f`)
- `development` pins `3f41462` ("Coverage: document the repository's coverage reporter"), and
  `git -C wiki merge-base --is-ancestor 725950f 3f41462` succeeds — development's pin **contains**
  main's

So the answer is obvious to a human and impossible for git: the merge base's own wiki pin is
`d8d1edf`, the dangling commit that Session-94 fixed on `main`. `git merge-tree` prints "commits
don't follow merge-base" and refuses, because it cannot compute a submodule merge base from an
object that is not in the submodule repository. **A dangling pin poisons every later merge, long
after it was fixed** — the fix has to travel backwards as a sync merge or the conflict recurs.

Resolution, which is Session-94's recipe exactly: branch off `development`, `git merge origin/main`,
and set the gitlink with
`git update-index --cacheinfo 160000,3f414629274dfeffe39436871e302d5786114574,wiki`. After that the
superproject tree is **identical** to `development`'s — `git diff --cached --stat origin/development`
is empty — which is the point of a sync merge: it carries no content, only the ancestry that makes
`main` an ancestor so PR #29 stops reporting a conflict.

**The trap this sets, recorded so it is not rediscovered:** Forgejo will describe that pull request as
an empty commit, and a PR with no diff has nothing to review or merge. This note is the change that
rides along, which is why it exists in a session log rather than only in the merge message.

**Operational note:** Forgejo's read API is open on this instance — `GET .../pulls?state=open`
returns `number`, `head.ref`, `base.ref` and `mergeable` without a token, which is how PR numbers were
identified here instead of guessed from branch dates. Only writes (`POST .../pulls`, merges) answer
`401 {"message":"token is required"}`.

### Session-99 (2026-11-04) — .NET coverage 75.4% → 95.1%, and the floors move with it

**PR #53 (open, head 50646481) raises .NET coverage to 95.1% line / 82.7% branch** (30,443 / 31,995
lines), up from 75.4% / 55.8% measured on `development`. Six assemblies: Core 92.0%, Server 95.6%,
Data 99.8%, Shared 98.8%, Contracts 100.0%, Discord 89.6%. `scripts/coverage-floors.json` is ratcheted
in the same PR: global 94/81, Core 90/79, Server 94/82, Data 98/83, Shared 97/94, Contracts 99/—,
Discord 88/91. The delivered-on-`development` figure only becomes "merged" once the PR lands.

- **The 0.41-point oscillation is gone in practice.** Four consecutive full runs of the final
  tree spanned at most **0.02 points** on any assembly (Core 91.968 ±0.00, Server 95.57–95.59),
  because the RNG-dependent paths are now driven through seeded `IRandomSource` values or
  range/invariant assertions instead of retry loops. Floors are still `trunc(lowest run) − 1`,
  never `trunc(measured)`, and were derived from those four runs.
- **`--suggest` proposes `floor(measured)`, not `floor(measured) − 1`.** The gate note says
  "raise it to 91" for a 91.97% measurement; the policy in the skill/plan is one point of
  headroom below the truncated minimum. Do not copy the suggestion verbatim or the ratchet
  will sit inside the noise band.
- **Test-only seams added.** `Veldrath.Discord.Tests/Infrastructure/
  DiscordInteractionHarness.cs` emits a `SocketInteraction` subclass in a dynamic assembly
  (`IgnoresAccessChecksToAttribute`) and assigns it through `InteractionModuleBase<T>.Context`'s
  private setter, so the slash-command bodies actually run offline and their embeds are asserted;
  `RealmEngine.Core.Tests/Infrastructure/ThrowingInformationLogger.cs` reaches handler catch arms.
  Both live in test projects and are excluded from the report by `coverage.runsettings`. Session-100
  then added four deliberate production seams (`IRandomSource` parameters on `EnemySpellCastingService`,
  `CraftingService`, `PartyCombatTurnHandler` and `EnemyGenerator`, `InMemoryInventoryService
  .GetDurability`, `SmtpEmailSender.ResolvePort`) plus one real reward-persistence fix, all listed in
  the PR body.
- **A persisted-state assertion exposed a product bug.** `UseAbilityInCombatHubCommandHandler` awarded
  XP/gold by writing the character row and then overwrote its attribute blob with the pre-reward copy,
  silently dropping the gold. The handler now re-reads the row before writing the decremented cooldowns
  (as `AttackEnemyHubCommandHandler` already did); the test asserts the persisted `Gold`, not just the
  returned `GoldEarned`.
- **What is still uncovered, and why** (1,552 lines): Discord 41 (`BotWorker`,
  `InteractionHandlingService` need a live gateway/REST event), Server 398 (Postgres-only
  `ILike`/`APPLY`/`SKIP LOCKED` arms, `internal` `DotEnvConfiguration` without
  `InternalsVisibleTo`, sealed `AccountProvisioningService`), Core ~1,081 (defensive switch
  default arms over closed enums, dead private members such as
  `CreateCharacterHandler.GetClassIdFromName`, `GameHub`'s ban-on-connect path), Data 14.
- **Test count**: 4,011 → 6,004 across the five test projects.

### Session-100 (2026-11-04) — Review corrections on PR #53: determinism, culture, real assertions

Round-1 review (`REQUEST_CHANGES`, comment #748) blocked on six test-quality points; all six are fixed on
the same branch (6,004 → 6,011 tests, still no test touches a boundary it does not assert).

- **Scripted, not sampled.** `EnemySpellCastingService`, `CraftingService`, `PartyCombatTurnHandler` and
  `EnemyGenerator` now take `IRandomSource? randomSource = null` (defaulting to `SystemRandomSource
  .Shared`) so `CombatService`, crafting and party-turn tests script the exact rolls and assert exact
  damage/heal/quality/target outcomes. That deleted every retry loop the review named; the location
  test's ten-enemy candidate sample is scripted too (0 failures in 30 repeats, where the old test failed
  at repeat 44).
- **Culture-pinned formatting tests.** `RealmEngine.Shared.Tests/Infrastructure/UseCultureAttribute.cs`
  (a `BeforeAfterTestAttribute`) pins `en-US` for the tests that hard-code dot decimals. The whole
  Shared suite now passes under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8` (1,158/1,158); the review had
  reproduced 9 failures there.
- **Assertions that can fail for the right reason.** Durability is observed through the new
  `InMemoryInventoryService.GetDurability` (null → 100 start, accumulation, zero floor, per-character
  isolation); the SMTP default port is asserted through the new protected `SmtpEmailSender.ResolvePort`
  instead of dialling an ambient `localhost:587`; eviction tests set `LastUpdatedAt` explicitly rather
  than assuming `TimeSpan.Zero` expires a fresh session; the ability-reward test seeds nonzero XP/gold
  and asserts the persisted row (which is what exposed the overwritten-gold bug above).
- **Verified:** Release build 0 warnings / 0 errors on SDK 10.0.402 (the review's CS8602 is gone);
  6,011 tests pass in three consecutive full runs; three merged reports all print "Coverage contract
  satisfied" with no `--suggest` output; per-assembly spread ≤0.02 points (Core 91.95, Server
  95.57–95.59, Data 99.78, Shared 98.79, Contracts 100.00, Discord 89.59); `dotnet format
  --verify-no-changes` and `git diff --check` clean. Floors unchanged — nothing is 3+ points stale.

### Session-101 (2026-11-04) — Kill rewards and the killing cast both persist (PR #53 round 3)

Round-2 review (REQUEST_CHANGES, comment #754) proved the Session-100 "re-read the rewarded row" fix was
itself a regression: `UseAbilityInCombatHubCommandHandler` kept the current cast (mana spent and the
`AbilityCooldown_<ability>` this cast started) only in memory, then `DistributeRewardsAsync` re-read the
stored row, so the write-back preserved the reward but dropped the cast — a killing fireball persisted
mana 50 with no cooldown instead of mana 40 / cooldown 2.

- **Order is the fix, not another re-read.** Persist the cast (including the per-turn `DecrementCooldowns`
  tick) *before* the death transition runs. `DistributeRewardsAsync` then re-reads a row that already has
  the cast and adds XP/gold on top of it. `AttackEnemyHubCommand` needs no change: it never mutates mana
  or a new cooldown before the kill branch.
- **The test now pins all four persisted values** through a fresh `DbContext`: XP, gold, `CurrentMana`,
  the new cooldown (2 = 3 − the cast turn's tick) and an unrelated existing cooldown (2 → 1), plus a
  second test that wins no death transition (`TryMarkDeadAsync` already claimed) and still keeps the
  mana/cooldown it spent while awarding nothing.
- **Final counts:** 6,012 tests (4,011 on `development`); 95.1% line (30,444–30,446 of 32,000),
  82.7% branch (8,628–8,629 of 10,435) across three merged runs whose 4-decimal per-assembly rates are
  identical except a ≤0.004-point blip in Core branch / Server line. Floors unchanged (global 94/81)
  because nothing measures 3+ points above them.
- **CI delivery:** the exact-head `.NET Coverage` job failed only in the sticky-comment step (duplicate
  comment id from `scripts/forgejo-pr-comment.sh`), tracked separately as t_2814041b (dev-debugger).
  PR #53's next push must be rebased onto `development` after that fix lands so the workflow runs it.

### Standing rule — tunables are configuration, not constants (2026-11-13)

Written down in [`plans/dungeons.md` §9](../../plans/dungeons.md) and
[`docs/coolify-deployment.md`](../../docs/coolify-deployment.md) (server env reference). Short version:

- A number a designer can argue about is either **content** (a row in the content DB, edited in Foundry)
  or **configuration** (a host option). Never a `const` in a handler, never a value that needs a rebuild
  to change.
- `EnemyWorldOptions` (`Settings/`, section `Enemies`) is the current host surface: `RespawnDelaySeconds`
  60, `SweepIntervalSeconds` 5, `RespawnBatchSize` 50, `SessionGraceSeconds` 60,
  `ClearedRoomRespawnMultiplier` 3. `[Range]` + `ValidateDataAnnotations().ValidateOnStart()` at
  [`Program.cs:174`](../../Veldrath.Server/Program.cs). No `Enemies` section exists in
  `appsettings.json`, so the class defaults are what actually runs — a bad env var stops the host rather
  than being overridden.
- **`GameConfig` (`RealmEngine.Data/Entities/Config/GameConfig.cs`) is the intended runtime home** —
  `ConfigKey` + JSONB `Data` + `Version` + `UpdatedAt`, one row per config file, already seeded by
  migration — but **nothing reads it at runtime today**; a grep only finds migrations and snapshots.
- When that pass happens: options class keeps its shape and validation (it becomes the typed view of the
  row), the DB row overrides appsettings and appsettings stays the fallback so an empty DB still boots,
  one `ITunableSource` reads it (never per-attack), and Foundry is the editing surface, not a DB console.
- Known placeholders to fix in that pass: the cleared-room rule is one global multiplier (should be
  per-zone or per-difficulty) and instance expiry reuses `SessionGraceSeconds` (should be its own key).
  Level gates (`Zone.MinLevel`, `ZoneLocation.Stats.MinLevel`) stay content and must never become
  appsettings.

### Milestone G — the Foundry map editor (2026-11-11/12, PR #71)

Plan: [`plans/foundry-map-editor.md`](../../plans/foundry-map-editor.md). Durable description:
[`docs/client-ui-architecture.md` §5](../../docs/client-ui-architecture.md). Branch
`feat/milestone-g-foundry-map-editor`; commits `7bd1a220` … `6e8565c6`.

**What it is.** `/maps` lists graphs with a state badge and a create form; `/maps/[mapId]` is a canvas,
a metadata panel, a node inspector and an edge list. The route group strips from the URL, so links are
`/maps`, `/content`, `/submissions` — **never** `/foundry/...` (three of those were 404s until G0).

**The rules live in TypeScript, not markup** (`web/client/src/lib/foundry/`):
`map-canvas.ts` (positions, unplaced lane, collision, tidy), `map-edges.ts` (direction inference,
self-reference/duplicate-direction refusals, reciprocal edges), `map-nodes.ts` (suggested ids, defaults,
delete refusals), `map-editor.ts` (`issuesByNode`, `mapState`, `parseDraftGraph`, `normalizeDraft`).
Components render and dispatch.

**One draft, one writer.** The page holds a `MapGraphDto` in state; the hidden `graph` field posts it
whole. The old write-through `save` action, `applyNodeEdits`, `readNodeEdit` and `NodeEdit` are **deleted** —
two writers silently discard canvas gestures.

**Lifecycle (the gate).** `IsActive` + `ApprovedVersion` on `Map` (content DB; migrations
`AddMapReviewState`, `AddMapReviewOutcome`). `CreateDraftAsync` → inactive, one entry node, no fabricated
exit; `SubmitAsync` → refuses a graph with errors; `ReviewAsync` (`Curator` policy) → approval needs a
submission **and** a clean validate, sets `IsActive` + `ApprovedVersion`; rejection clears the submission
and keeps `LastReviewNotes`. `DeactivateAsync`/`DeleteDraftAsync` refuse while another map's node still
targets the map; a live map is retired, never deleted. **`Apply` never writes `IsActive`** — a crafted save
payload must not activate a draft. Every lifecycle change writes an `AdminAuditEntry`.

**Access matrix.** GET `/api/content/maps*` → `content.view` (the graph leaks hidden nodes and unlock
keys); PUT/POST authoring → `content.manage`; review/deactivate → the `Curator` **role** (a permission is
not enough). The endpoints used to require `ManageContent` group-wide, which made read-only unreachable.

**Notifications.** `FoundryNotification` gained `Kind` (`Submission`/`MapReview`), an optional
`SubmissionId`, a `MapId` (plain string — maps are in the content DB, so no cross-context FK) and a
`Title` snapshot. `FoundryNotificationWriter` is the only place a row is built. Storing the title removed a
**required** join that was silently hiding rows. Migration `AllowMapReviewNotifications`.

**Validator.** `MapGraphValidator.ValidateWorld` gained `target_node_unknown` (an exit must name a node the
target map has). The entry rules (`entry_missing`, `entry_not_found`, `entry_flag_mismatch`) already
existed — do not "add" them again.

**Gotchas worth remembering:**
- A local variable named `state` in a `.svelte` that uses `$state` breaks rune lexing with ~24 confusing
  errors ("`$state` used before its declaration"). The `MapHeader` prop is therefore `status`.
- `WebAppFactory` content DB starts empty: seed with `MapGraphSeeder`/`ZoneLocationsSeeder` before an HTTP
  test that needs a graph (`MapEditorEndpointTests.EnsureAMapAsync`).
- `MapEditorService` only has `ContentDbContext`; audit and notifications need `ApplicationDbContext`, so
  the **endpoint** writes them. `ReviewAsync` returns `MapReviewOutcome` (response + author + map name)
  for exactly that reason.
- Client DTOs keep the review fields **optional** (`isActive?`, `approvedVersion?`) because fixtures
  predate the gate; `mapState` treats `undefined` as live.
- Coverage: `vite.config.ts` now gates `src/lib/foundry/**/*.ts` (rules) at 92 and leaves the `.svelte`
  components to the 85/80 `.svelte` floor — the same reasoning already applied to `src/lib/game/**/*.ts`.
  Global branches sit at exactly 90 after G; adding component branches will push it under again.
- The Foundry authoring e2e (`tests/foundry-maps.e2e.ts`) is opt-in (`E2E_FOUNDRY=1` + staff credentials):
  a test cannot grant itself `content.manage` or `Curator`.

**Counts at the end of G:** 6,058 .NET tests, 1,303 web unit tests, coverage green globally (90 branches).

## Milestone H — Foundry portal overhaul

Plan: [`plans/foundry-overhaul.md`](../../plans/foundry-overhaul.md). **H0 is shipped** (shell, home,
grouped sidebar, group error page, `/languages` redirect); H1–H6 follow.

**The portal home is `/foundry`, not `/`.** `web/client/src/routes/+page.svelte` is the public site's landing
page, and a `(foundry)/+page.svelte` would resolve to the same URL — a SvelteKit route conflict, not
something to work around. `FOUNDRY_HOME_HREF` in `web/client/src/lib/foundry/nav.ts` is the single place the
home route is spelled.

**Access split.** Root `+layout.server.ts`: `staff.foundry = hasPermission(content.view)` and
`staff.submit = Boolean(user)`. `SiteHeader` renders "Foundry" → `/foundry` for the former and
"Submit content" → `/submissions/new` for the latter, never both. The Foundry home and every content loader
call `requirePermission(access, Permissions.ViewContent)`; `/submissions*` and `/notifications` stay
session-only.

**`canReview` is the Curator role, not the `content.review` permission.** The decision endpoints are
`.RequireAuthorization("Curator")`, so a hand-granted permission would render a Review entry that answers
403. Note `hasPermission` returns `true` for `Admin`, which does **not** hold the Curator role — the role
check is deliberate, and reconciling the two models is H5 work.

**Unread badge.** `GET /api/foundry/notifications/unread-count` → `FoundryUnreadNotificationCountDto(int
Count)`, served by `FoundryService.CountUnreadAsync` with the same predicate as `GetNotificationsAsync`. The
group layout fetches it once and degrades a failure to `0` rather than taking the whole group down.

**Shell components** live in `web/client/src/lib/components/foundry/`: `FoundrySidebar`,
`FoundryPageHeader`, `FoundryBreadcrumbs`, `FoundryEmptyState`, `FoundrySection`. `(foundry)/+layout.svelte`
owns the frame (`max-w-6xl`, one padding scale, sticky sidebar that becomes a chip row below `lg`); no page
may reintroduce its own `mx-auto max-w-*`.

**Gotchas:**
- `resolve()` cannot target a dynamic route with a literal — `resolve('/(foundry)/submissions/new')` is a
  typecheck error. Use the plain path string for `/submissions/new`.
- `/languages/+page.server.ts` is a `redirect(308, '/content/language')` that preserves the query; its
  `+page.svelte` is deleted. `/languages/new` stays until H4.
- `(foundry)/+error.svelte` renders inside the shell, so a 403 names the missing capability without dropping
  the reader out of the portal. It does not cover a failure in the group layout's own `load` — that still
  falls through to the root boundary, which is why the unread count degrades instead of throwing.
- The legacy pages (`content/*`, `submissions/*`, `notifications`, `languages/new`) lost their frames in H0
  but keep their own `<header>` blocks; they adopt `FoundryPageHeader`/`FoundryBreadcrumbs` as each is
  rewritten in H1+.
- Adding a route means adding it to the Playwright guard list (`tests/staff-guards.e2e.ts`) or the parity
  suite fails: `/foundry` and `/review` were added there, and the old `/languages` exemption is gone.

**Counts after H0:** 1,319 web unit tests, 26 Playwright tests (2 skipped), coverage gate green.

### H1 — the content editor became a form

`web/client/src/lib/foundry/content-editor.ts` gained five pure helpers, all unit tested: `slugify`,
`anchorId`, `isDirty`, `missingRequired`, `parseMarkdown`.

- **Derived slug** behind a "Derive from display name" checkbox. Locked by default on an existing entity,
  because the slug is what other content links to.
- **`parseMarkdown` returns blocks and spans, never HTML.** The page renders them with a `{#snippet inline}`
  and Svelte markup, so there is no `{@html}` and `svelte/no-at-html-tags` stays satisfied. Do not
  "simplify" it back to an HTML string: the write tooling decodes HTML entities in file content, which is
  how the first escaping renderer ended up with a no-op `.replace(/&/g, '&')`.
- **Required blocking happens client-side too**: `missingRequired` disables Save and labels the field. A
  boolean is never blank; an empty number is blank rather than `0`.
- **Sticky bar** Save / Validate / Discard, with `isDirty` driving a `beforeunload` guard and an
  "Unsaved changes" note. Discard reseeds from the loaded payload; it is not a form action.
- The page uses `FoundryPageHeader` and `FoundryBreadcrumbs`, and the required asterisk is `aria-hidden`
  so a field's accessible label stays exactly its name.
- A sparse record with neither display name nor slug used to render `Edit null` — a template literal
  stringifies `null`, unlike Svelte's `{x}` interpolation. The heading is now built conditionally.
- `(foundry)/+error.svelte` had zero coverage until H1's coverage pass; it now has three render tests
  (403 naming the capability, 500 with a retry, 404 without).

**Counts after H1:** 1,350 web unit tests, coverage 90.04% branches against the 90 global gate,
typecheck/lint/build clean.

### H2 — collections are first-class in the schema

`ContentFieldType` gained `StringList`, `KeyValue`, `ObjectList` and `Reference`; `ContentFieldDescriptor`
gained `Items` (the row shape), `RefType` and `Multiple`. `quest.objectives`, `quest.rewards`,
`recipe.ingredients` and `loottable.entries` are now `ObjectList`s, and recipe's `outputItemSlug` and
`craftingSkill` are `Reference`s. **The stored JSON did not change** — only how it is authored.

- **Form state is a union now**: `FieldValue = string | boolean | string[] | ContentRow[]` and
  `FieldValues = Record<string, FieldValue>`. `isDirty` compares collections with `JSON.stringify`
  because the editors rebuild arrays on every edit; a reference comparison would mark the form dirty on
  first render.
- `coerceFieldValue` passes collections through, except `KeyValue`, which is edited as rows and stored as
  an object via `pairsToObject`.
- `Recipe`/`LootTable`/`Quest` row fields stay `Text` for slugs *inside* rows: `writePath` addresses
  dot-paths, not array indices, so a `Reference` inside a row only gets a datalist, not a dot-path value.
  Top-level Reference fields are the ones the loader preloads options for.
- The edit loader now calls `browseContent(refType, null, 1, 50)` per distinct reference type, including
  ones nested in `Items`; a failure yields an empty option list rather than a broken form.
- `src/lib/parity/field-kinds.test.ts` reads `ContentFieldType` out of `Veldrath.Contracts` and compares
  it with `CONTENT_FIELD_KINDS`, so a kind added on one side fails a test.
- **`write_to_file`/`apply_diff` decode HTML entities in file content.** That silently turned an
  `&` escape into a no-op in H1 and it is why the markdown preview now returns spans, not HTML. Do not
  write entity text into source through these tools.
- The global branch gate sits right on 90, so every new branch counts: H2 needed explicit tests for an
  enum option labelled `(empty)`, a Reference with no `refType`, a `KeyValue` holding a scalar, and a
  schema with a slug but no display name before it went green again (90.02%).

**Counts after H2:** 1,382 web unit tests, 6,060 .NET tests, coverage 90.02% branches, typecheck/lint/build
clean.

### H3 — the read paths render fields, not JSON

`web/client/src/lib/components/foundry/ContentRenderer.svelte` renders a payload through the schema: groups
as cards, a `<dl>` of label → value, booleans as Yes/No, empties as a dash, `StringList` as chips, `KeyValue`
as a small table, `ObjectList` as a table whose columns are the row descriptors, and a `Reference` as a link
to `/content/{refType}/{slug}`. Keys the schema does not describe are listed under "Other fields"; the raw
JSON lives behind an "Advanced — raw JSON" disclosure. **That disclosure is now the only `<pre>` under
`(foundry)`** — the H3 done-when.

- The content detail loader now loads the schema too (`getContentTypeSchema`), best-effort: a missing schema
  is not fatal, the renderer just lists every key.
- Submission create is schema-driven. The type picker is a **GET** (`/submissions/new?type=quest`) because
  the server renders the chosen schema; the POST action rebuilds the payload with `buildPayload` and refuses
  with `Fill in: <fields>` when a required field is blank. **Collections are not authorable there yet** — the
  form names them and points at the content editor; H4 renders the editors in the create form.
- The submission view parses the stored payload and renders it with the same component. A payload that is
  not valid JSON is shown verbatim with a warning rather than swallowed.
- `ContentTypeInfo` is `{ contentType, displayLabel, description }` — **not** `{ key, label }`. Several older
  Foundry test mocks used `key`/`label`; the real fields are the longer ones.
- Watch for duplicate accessible labels: the submission form has its own "Description" textarea, so a schema
  field also labelled "Description" makes `getByLabelText` ambiguous.

**Counts after H3:** 1,397 web unit tests, coverage 90.01% branches, typecheck/lint/build clean.

### H4 — create for every content type

`/content/[type]/new` renders the type's own schema, so a new content type needs no new page. It is
deliberately a **plain form with no client-side state**: it creates an entity from its scalar fields and then
sends the author to the editor, which owns collections. Demanding collections at create time would have made
every collection-bearing type uncreatable, because a stateless form has no list/row editors.

- `referenceOptions`/`referenceTypes` moved to `#lib/server/content-refs.ts` (the edit and create loaders
  both need them). `isScalarKind(field)` in `content-editor.ts` is the one definition of "a field a plain
  control can author" — do not re-derive `fieldType !== 'StringList' && …` locally.
- The browse page's loader gained `canEdit`, and the page shows a "New entry" link only for an author.
- Submission create still cannot author collections; that is a deliberate deferral, not an oversight.
- **A malformed `apply_diff` with a SEARCH but no `=======` silently deletes the matched text.** One such
  block removed a plan bullet during H4; it was caught by reading the region afterwards, not by the tool.

**Counts after H4:** 1,412 web unit tests, coverage 90.01% branches, typecheck/lint/build clean.

### H5 — an approval lands content, and the reviewer sees the diff

`ContentWriteService` (server) is the one write path onto the catalog, used by Foundry's review gate. It runs
the same primitives as `ContentWriteEndpoints.CreateAsync` — `ContentEntityTypes.Deserialize`,
`ContentReferenceCatalog`, `ValidatorRegistry` — so a payload the editor refuses is refused here for the same
reason; only the orchestration is repeated. `ReviewAsync` promotes **before** flipping the status, so a failed
write leaves the submission `Pending` with the reason. `FoundrySubmission.PromotedSlug` records where it
landed (migration `AddSubmissionPromotion`), the DTO carries it, and the approval notification names it.

- **A content payload needs `typeKey`.** "Item has no type key, so content may not load it" is the error a
  payload gets without it; a minimal valid item is
  `{"slug","displayName","typeKey":"material","itemType":"material","rarityWeight":50,"isActive":true}`.
  `FoundryEndpointTests.PromotableItem` is the fixture.
- **Approval is stricter on purpose**: every existing approve test had to gain a promotable payload, because
  `{}` cannot be written. Do not "fix" that by skipping promotion when the payload is empty.
- The web diff is `diffFields(schema, previous, next)` in `content-editor.ts` plus `ContentDiff.svelte`. The
  submission loader fetches `getContentDetail(type.toLowerCase(), slug)` **only while the decision is open**;
  after that the diff is history.
- The same value appears in both the payload renderer and the diff table, so page tests must scope with
  `within(screen.getByRole('table'))` instead of `screen.getByText`.

**Counts after H5:** 6,062 .NET tests, 1,421 web unit tests, coverage 90.06% branches, lint/typecheck/build and
dotnet format clean.

### H6 — world health, schemas, and the defect the audit endpoint exposed

- `GET /api/content/audit` runs `ContentAuditService.RunAsync` and returns `ContentAuditIssueDto` rows; it
  requires `content.view`. The service is registered as **both** a singleton and a hosted service
  (`AddHostedService(sp => sp.GetRequiredService<ContentAuditService>())`) so startup and the endpoint share
  one instance — `AddHostedService<T>` alone registers only `IHostedService`, so injecting the concrete type
  would fail.
- **The audit never ran on SQLite.** It projected nested collections (zone actor pools, quest objectives, map
  nodes), which needs SQL `APPLY`; SQLite cannot translate it, the startup run swallows exceptions, and
  `ContentAuditServiceTests` asserted that translation failure as expected behaviour. It now materialises and
  flattens in memory. If you add a rule that projects a collection, keep it out of the query.
- `/world-health` and `/schemas` require `content.view`, are in the sidebar's Reference group, and are in
  `tests/staff-guards.e2e.ts` (that is what satisfies the route-parity suite — no exemption needed).
- **`ContentIssue` has no content type**, only `Identifier` (a slug), so `/world-health` cannot deep-link a
  finding. Adding `ContentType` to the record is the follow-up.
- Cross-type Foundry search is **not** built: `browseContent` searches one type, and a global search needs an
  endpoint over every resolver.

**Counts after H6:** 6,062 .NET tests, 1,431 web unit tests, coverage 90.01% branches, 28 Playwright tests,
lint/typecheck/build and dotnet format clean.

### I — the access model, and navigation v2 (2026-11-16)

The H0 decision (§5.4 of `plans/foundry-overhaul.md`) made the portal staff-only, and the first real account to
use it got 403s on the home and every sidebar item. The replacement model is §8 of that plan; this is the list
of traps.

- **`AddPolicy("Curator", …)` is a role policy and it was on three decision endpoints.** `Permissions.All` is
  granted to Admin as permission *claims*, so an Admin passed `RequireAuthorization(permission)` but failed
  `RequireAuthorization("Curator")`. It now uses `Permissions.ReviewSubmissions` on submission review, map
  review and map deactivate. Any new "who may decide" check goes through the permission.
- **The submission feed had no authorization.** `GET /api/foundry/submissions` and `/{id}` were anonymous,
  payloads included. They now require a token, and `FoundrySubmissionScope` (`Everything`, `Own`,
  `OwnPlusApproved`) decides what is visible; `mine=true` narrows for everyone including a reviewer, and a
  non-reviewer's default feed is `OwnPlusApproved`, not `Approved` — the first version dropped the caller's own
  rows and a test caught it.
- **`GET /api/content/maps*` now requires a session, not `content.view`.** The graph carries hidden nodes and
  unlock keys; that spoiler cost is accepted and the mitigation, if needed, is rendering hidden nodes
  distinctly rather than closing the editor.
- **The `(foundry)` group still requires a user** (`requireUser`), so "public" means "signed in". The redirect
  carries `?redirectTo=` and [`web/client/src/lib/navigation.ts`](../../web/client/src/lib/navigation.ts)
  rejects anything but a local path — do not inline that check, it is the open-redirect guard.
- **Layout flags are `canManageContent`, `canReview` (permission), `canAudit` (`content.view`).** There is no
  "can read" flag any more; if you find yourself adding one, the page probably should not be gated.
- **Nav v2:** one primary action (`/new`, the type picker) then Explore / Contribute / Curate / Maintain, with
  Notifications in a separate `account` list below a rule. `FOUNDRY_PATH_ROOTS` + `isFoundryPath` exist because
  the group has no URL prefix — the site header marks "Foundry" from any portal page through them.
- **`FoundrySidebar`'s icon map is exhaustive by construction and has no fallback icon.** A nav entry without an
  icon fails the sidebar tests rather than rendering a wrong glyph. That also removes an uncoverable branch.
- **Test gotchas:** `parentData` fixtures for the home loader must include `canReview` (a missing key is
  `undefined`, not false, and `toMatchObject({ canReview: false })` fails); the submissions page needs
  `scope` (`'mine' | 'all'`) and `canReview` in its fixtures, because the empty-state copy changed with the
  scope.
- **Coverage lands exactly on the gate.** Removing two branches from the sidebar left 4,249/4,721 = 90.00%. Any
  new branch in a Foundry file needs a test with it.

Deferred, recorded in the plan: `/submissions/new` is still `params.id === 'new'` inside `/[id]` (works, links
resolve, refactor is separate), and the type picker has no search.

**Counts after I:** 1,666 server tests, 1,442 web unit tests, coverage 90.00% branches, lint/typecheck/build
clean. Playwright was not run locally this pass; `tests/staff-guards.e2e.ts` gained `/new`.

### J — community map authoring (2026-11-17)

Plan: [`plans/community-map-authoring.md`](../../plans/community-map-authoring.md). Branch
`feat/milestone-j-community-map-authoring`, cut from `development` at `836198b5` (PR #73, which is where
milestone I actually shipped — there is no `feat/milestone-i-*` branch).

- **Map writes are decided per map, not per endpoint.** `MapCaller(AccountId, CanManageAny, CanReview)` — with
  `SeesEveryDraft` and `MayEdit(createdByAccountId)` — is passed into `ListAsync`, `GetAsync`, `SaveAsync`,
  `SubmitAsync`, `DeleteDraftAsync`. An endpoint-level `content.manage` made every map an editor's map and left
  an author unable to write their own draft; do not put a permission back on those routes. `validate` is a
  session-only read.
- **A stranger's draft reads as 404, not 403.** `GetAsync(mapId, caller)` returns null for a draft the caller
  cannot see, so the endpoint's existing 404 covers it. A 403 would confirm the map exists.
- **`canEdit` rides on `MapSummaryDto` and `MapGraphDto`** and the editor disables its controls from it. Do not
  re-derive it from `content.manage` in the page — that is the bug the flag exists to prevent.
- **`map_without_exit` is a warning on save and a refusal at approval.** `ReviewAsync` calls
  `BlocksPublication(issues)` (the one rule that refuses publication). A test that expects a *save* to return 400
  for a map with no exit is wrong — it returns 200 and persists. This cost a test rewrite during J0.
- **`ValidateWorld` grew two inputs:** `locationZones` (slug → zone) and `liveMapIds`. New codes:
  `node_reference_zone_mismatch` (error), `location_missing_node` and `map_unreachable` (warnings). A `Location`
  node's `refId` is a **zone-location** slug, so a test helper wanting "a way out" must use `RegionExit` with a
  **region** slug (`cinderplain`) — a `ZoneExit` with a location slug fails `node_reference_unknown`.
- **Authored coordinates are ranks, not distances.** `MapEditorService.ToDto` densifies each axis
  (`DenseRanks`) so `x=2` beside `x=34` renders adjacent, and the canvas fixed the cell at
  `CARD = 80` + `GAP = 24`. The spread-out-map complaint was authored slack, not a CSS bug; do not "fix" it in
  `map-canvas.ts`.
- **The review queue validates each submitted map before a reviewer opens it** (`getMap` + `validateMap`), and a
  failed check reports `unknown`, not `blocked` — an unreachable validator must never read as "this map is
  broken". `reviewVerdict` / `reviewVerdictLabel` in `map-editor.ts` own that wording, including "refused · the
  map has no exit" for a blocked verdict with zero errors.
- **A deactivation notification needs a second read.** `MapLifecycleResponse` carries neither the author nor the
  name, so `MapEditorService.AuthorOfAsync` returns `MapAuthor(AccountId, Name)` (submitter, else creator) and
  `DeactivateMapAsync` titles the inbox row with the map's name. A rejection already notifies through
  `MapReviewOutcome`.
- **Test gotchas:** `getByRole('link' | 'button', { name: /^Create/ })` matches more than one control on the
  maps pages — index into `getAllByRole(...)`; `parentData` fixtures must carry every flag the layout returns
  (a missing key is `undefined`, not `false`); the `src/lib/server/**` function floor (92) fails when a loader
  test mocks `content-api`, so a new API function needs a case in `content-api.test.ts` as well.

**Counts after J:** 6,089 .NET tests (1,677 server, 2,744 core, 510 data, 1,158 shared), 1,463 web unit tests,
30 Playwright tests (2 skipped), coverage 90.03% branches, lint/typecheck/build and
`dotnet format --verify-no-changes` clean.

### K — web e2e: user workflows (2026-11-17)

Plan: [`plans/web-e2e-workflows.md`](../../plans/web-e2e-workflows.md). The ask was blunt: "I can't always spend
time manually testing the UI." Before this, e2e proved *routes* (a spec mentioned every `+page.svelte`) and one
gated map smoke test — not one Foundry journey.

- **A browser mock cannot test these pages.** Every Foundry loader calls `gameApiFetch` on the SvelteKit server,
  so `page.route()` never sees the request. The specs need Postgres + `Veldrath.Server` + the preview: see
  [`scripts/e2e-stack.sh`](../../scripts/e2e-stack.sh) (`start|stop|status`) and
  [`web/client/tests/support/stack.ts`](../../web/client/tests/support/stack.ts).
- **Accounts are real, roles are written directly.** There is no bootstrap admin, so `grantRoles` inserts the
  `AspNetUserRoles` row (`AspNetUsers`/`AspNetRoles`, PascalCase quoted identifiers, no snake_case) after the
  first `/foundry` visit provisions the Identity row. Roles already carry their permission claims from startup
  seeding. Accounts are created through `/register`, so Better Auth owns the session.
- **JWT key mismatch is the #1 first-run failure** (`401 The signature key was not found`): the stack script
  reads `JWT_KEY`/`JWT_ISSUER`/`JWT_AUDIENCE`/`INTERNAL_BRIDGE_TOKEN` from `web/client/.env`. Do not hardcode them.
- **The API port is not 9000 by default** in Development: `appsettings.Development.json` says `:5000`, so the
  script pins `ASPNETCORE_URLS=http://127.0.0.1:9000` to match `PUBLIC_API_URL`.
- **Gating:** `E2E_STACK=1` enables the specs; `Auth__AccessCacheSeconds=0` is required (see below).
  `npm run test:e2e:stack` runs them; plain `npm run test` stays stack-free.
- **What CI does now:** the `e2e-game` job of `ci-web.yml` sets `E2E_STACK=1` and runs
  `game-loop` + `foundry-content` + `foundry-maps`.

Four defects these specs found (each only visible when the real app meets the real API):

1. `GET /api/account/profile` reported **no permissions** for a role holder — it read only per-user claims, while
   a role carries its permissions as *role* claims. Every ContentEditor and Curator looked permissionless, so the
   web app hid the authoring surface and the review queue from them. Fixed via `RolePermissionResolver` in
   `AccountService`, with two `AccountEndpointTests`.
2. A granted role did not apply for up to 60s: `AccountProvisioningService` caches the access snapshot. The four
   admin role/permission endpoints now call `AccountProvisioningService.Invalidate(accountId)`, and the TTL is
   `Auth:AccessCacheSeconds` (0 disables the cache).
3. A refused map approval surfaced as **"Request failed (400)."** — `game-request.ts#extractError` only read
   `error|detail|title`. It now reads `message` too, which is the shape every Veldrath operation result uses.
4. **Foundry could not create any content**: `ContentShapeRules.ValidateShared` requires `ContentBase.TypeKey` and
   no schema exposed it (`type_key_missing`); abilities also had the field named `abilityType` while the model and
   validator read `powerType`. `ContentSchema.cs` now authors `typeKey` in the shared Identity group and
   `powerType` (including `innate`) for abilities and spells.

Second pass (same day) closed the write half and added three more journeys. Five stack specs now:
`foundry-content` (create → edit → save → duplicate refused → deactivate → 404), `foundry-maps` (the map
lifecycle), `foundry-submissions` (a role-less player submits, a Curator approves, the catalog gains the entry,
the inbox reports it, Mark read clears it), `staff-areas` (editorial/admin/mod lists, a real user filter, and a
player refused all three) and `public-details` (`/lore`, `/patch-notes`, `/leaderboard`, `/community`, plus the
404 paths). Three more defects they found:

5. `ContentFieldType` and `ContentIssueSeverity` were serialized as **numbers** while the web switches on
   `'Integer'`/`'Boolean'`/`'Error'`. Every coercion missed, so the form posted `"50"` where an integer belonged and
   the write refused the body; the audit's severity badges were wrong too. Both enums now carry
   `[JsonConverter(typeof(JsonStringEnumConverter))]` — keep it if either enum moves.
6. A checkbox with no `value` submits `on`, and `coerceFieldValue` only accepted `'true'`: every save wrote
   `isActive: false`, the catalog hid the entry and the redirect target 404'd. Fixed in `coerceFieldValue`
   (`'on'` is true) and the edit form now sets `value="true"`.
7. The create form ignored the schema's boolean default, so new entries were born inactive. Fixed with
   `checked={field.default === 'true'}` on the create page.

**Trap worth remembering:** Playwright's `webServer` has `reuseExistingServer: !process.env.CI`, so a local rerun
silently reuses the previous `vite preview` build and your Svelte changes never reach the browser — the symptom
is a fixed bug that keeps failing. Run stack specs with `CI=true`. Likewise `scripts/e2e-stack.sh` kills a stray
`bin/**/Veldrath.Server` process before starting; without that, an old build keeps the port and every later run
tests it.

Third pass added the staff verbs and publishing. **Seven stack specs** now: `foundry-content`, `foundry-maps`,
`foundry-submissions`, `staff-areas`, `staff-actions` (warn → "1 warnings", mute 30 min → "Muted" chip, unmute →
cleared, on a real registered player), `editorial-publishing` (announcement → Publish → `/community`; patch note →
Publish → its public article and index link; empty body refused) and `public-details`.

- **The editorial create routes DO exist** — as `[id]` with `id === 'new'`, the same deferral as
  `/submissions/new`. `/editorial/announcements/new` is served by `[id]/+page.server.ts`, which switches on that
  id. Do not add a `new/` directory; you would shadow it.
- **The admin-action rate limit throttled the suite** (20/min, `RateLimit:AdminActionsPerMinute`). Loading an
  editorial page costs several admin calls, so `scripts/e2e-stack.sh` now sets
  `RateLimit__AdminActionsPerMinute=600` and `RateLimit__FoundryWritesPerMinute=600`. If a spec starts seeing
  429s, that is the knob.
- **A 429 used to read as "not found".** The three editorial `[id]` loaders did `error(404, result.error)`
  whenever data was missing, so a throttled or forbidden admin call sent the editor hunting for a record that
  exists. They now propagate the upstream status (`result.status === 0 ? 502 : result.status`). The same pattern
  is worth checking in any new admin loader.

Fourth pass closed E7. **Eight stack specs**; the newest is `admin-console` — an Admin grants Curator through
`/admin/users/[id]`, the *player's own context* (no re-login) sees the review queue and gets 200 on `/review`,
revoking returns 403 — the regression test for the snapshot-invalidation fix. `staff-actions` now also closes the
report loop (a second player files one via `POST /api/reports` with its own minted token, the moderator sees it
unresolved and resolves it), and `editorial-publishing` publishes a lore article too.

- **`parity/routes.test.ts` is down to two exemptions**: `/api/**` and `/languages/**`. The admin, mod, editorial,
  foundry, players and leaderboard exemptions are gone because a spec now drives each page.
- **The parity suite reads spec *source*, so a dynamic route needs its literal in the spec.** For
  `/admin/users/[id]` and `/mod/users/[id]` the specs record the real URL and re-`goto` it (a genuine
  deep-linkable assertion) with the route pattern named in a comment; do that rather than re-adding an exemption.
- **`/mod/reports`'s resolve action redirects to the *unfiltered* queue**, so a resolved row does not disappear
  from the page — assert its status chip flips to "Resolved", not that the row vanishes.
- **SvelteKit runs `+page.server.ts` and `+layout.server.ts` concurrently**, so a page load used to call the API
  before its layout's guard refused the request — a 403 line in the log and a wasted round trip per blocked
  page. Fixed: the 11 admin/mod page loaders `await parent()` first, and a full authenticated run now logs zero
  refused `/api/mod…`/`/api/admin…` calls. **Do this in any new gated page load**; the layout remains the only
  place the permission is decided.

Fifth pass closed E9: **kick is covered end to end.** `staff-actions` puts a player in the world (character via
`POST /api/characters` with a minted token, then `/play/{id}` waiting for `.map-node-cell button` — the hub's first
snapshot), the moderator kicks with a reason from `/mod/users/{id}`, and the player's own screen shows that reason
in the `session.error` banner (`GameScreen.svelte`). The seventh pass below gave `phase === 'disconnected'` its own
screen; the spec now asserts that instead of the banner.

Sixth pass (E11–E16) made the suite trustworthy rather than merely broad. **Twelve stack specs, fourteen tests,
~4.5 minutes**, plus thirty stack-free specs in ~18s.

- **`scripts/e2e-stack.sh start --fresh`** drops the compose volume so a run starts from the seed (the server
  migrates and seeds on boot). Use it when a failure smells like stale data. The same pass added
  `api-stop` / `api-start`, which manage **only** the API process — `upstream-failure.e2e.ts` needs them, and it
  self-skips when `.e2e-server.pid` is absent (CI runs `dotnet run` itself, so there is nothing to stop).
- **`session-states.e2e.ts`** is the session truth: clearing cookies mid-flow sends the visitor to
  `/login?redirectTo=` with the **whole** nested target (`/content/ability?page=1`), and signing in lands back there.
  If you touch the guard, `navigation.ts#localPathFrom` or the login action, that spec is the one to watch.
- **`game-journeys.e2e.ts`** drives the seven-step wizard (`Continue` until the last step, where the button reads
  `Create character`). Two traps: the wizard accepts **letters and spaces only** in a name, so a digit stamp is
  rejected before the availability check; and creating a character navigates to `/play/{id}`, **not** the roster.
- **`review-diff.e2e.ts` found a real defect:** the review page's diff says "this would replace the stored entry",
  but promotion called a create-only path and refused any submission whose slug existed — so no reviewer could
  accept a correction. `ContentWriteService.PromoteAsync` now upserts (row id survives, version bumps), with a
  server test that asserts the replace. Keep that in mind when reading `FoundryService.ReviewAsync`: approval is an
  upsert, not a create.
- **`playwright.config.ts`** now sets `retries: 1` on CI with `trace: 'on-first-retry'` and
  `video: 'retain-on-failure'`, and a 10s expect budget. A retry without a trace is a flake nobody can diagnose.

Seventh pass closed the two §6 leftovers.

- **`phase === 'disconnected'` has its own screen.** A kick used to leave the map, the travel buttons and the bag on
  screen, each firing a command into a closed connection. `GameScreen.svelte` now renders a `Disconnected` region
  (the moderator's reason plus "Back to characters") and drops the game shell and `<GameOverlays>` while that phase
  holds. Component tests are in `shell.test.ts` (`playing({ phase: 'disconnected' })`); the e2e kick test asserts the
  region, the reason, zero `.map-node-cell button`, and the landing on `/characters`. **Watch the coverage gate when
  adding `.svelte` branches**: this screen put branches at 89.95% until its component tests landed.
- **The review queue's N+1 is gone.** "Would approval accept this map?" is now answered server-side in
  `MapEditorService.ListAsync` from **one** world snapshot and returned on `MapSummaryDto.Review`
  (`MapReviewVerdictDto`), using the same `BlocksPublication`/error rule the decision applies, so a row cannot
  promise an approval the decision would refuse. `ValidateWorldAsync` became `LoadWorldAsync` plus a static
  `ValidateWorld(world, …)`. The web loader reads `map.review` and words a missing verdict as `unknown`, never
  `ready`. Do not reintroduce a `getMap`/`validateMap` pair per row on `/review`.

- **Running the stack specs locally needs the preview origin too.** `E2E_STACK=1` alone was not enough: Better Auth
  rejected the sign-up with `Invalid origin: http://localhost:4173`, because `web/client/.env` sets
  `ORIGIN=http://localhost:5173` (the dev server). That is now handled for you — `playwright.config.ts` starts the
  preview with `ORIGIN`/`BETTER_AUTH_URL`/`PUBLIC_*`/`DATABASE_URL` set, so
  `./scripts/e2e-stack.sh start --fresh` + `npm run test:e2e:stack --workspace client` is the whole recipe.
  **Never export those by hand**: a hand-set `PUBLIC_*` on the command line is ignored (they are inlined at build
  time), which is how the dev hub URL leaked into a "stack" run.

Eighth pass — **the .NET coverage gate was red, and it was `Veldrath.Server` branches**: 80.6% against a floor of 82
(measured 2026-11-17 with the pinned SDK 10.0.402, `dotnet test Realm.Full.slnx -c Release --collect "XPlat Code
Coverage"` → `scripts/coverage-report.sh` → `scripts/check-dotnet-coverage.mjs`). Driver: whole features landed with
no tests, and the gate only sees a regression once the branch count grows. Closed to **82.5% branch / 95.2% line**
(total 82.7% / 95.1%) with two suites:

- [`GetCraftableRecipesQueryTests`](../../Veldrath.Server.Tests/Features/GetCraftableRecipesQueryTests.cs) — the
  craft panel's entire data source had **zero** tests, so `GetCraftableRecipesQuery.cs` sat at 0%. Eleven tests now
  cover the three gates in order (level → skill rank → materials), the joined shortfall sentence, ranked attribute
  parsing (`Skill_{id}_Rank`, unreadable JSON, missing rank = 0), the informational normalisation and the panel's
  ordering.
- [`DotEnvConfigurationTests`](../../Veldrath.Server.Tests/Infrastructure/DotEnvConfigurationTests.cs) — the
  `.env` loader was at 5.9%. It is `internal`, so `Veldrath.Server.csproj` now has
  `<InternalsVisibleTo Include="Veldrath.Server.Tests" />`. **The class is in a `DisableParallelization`
  collection** because it writes process environment variables; do the same for any future env-mutating test.

The remaining large gaps are `CharacterCreationSessionEndpoints.cs` (65%), `GetInventoryHubCommand` (67%) and the
`GameHub` partials — worth ~2 more points if the floor ever needs margin. The floors themselves were not touched.
**Still open — and it is a game defect, not only a test gap:** combat and death (E13c/d) need a known encounter,
and there is none. **No seeded zone location has an `ActorPool`** (verified with
`jsonb_array_length("ActorPool")` over `ZoneLocations`: every row is 0), and `ActorPoolResolver.SpawnRosterAsync`
returns an empty roster for an empty pool *by design* — the old `"common"` fallback was removed so a missing pool
could not be silently faked. So nothing spawns anywhere, the HUD's "Local" panel can never list a hostile, and
`CombatPanel` never mounts for a real player. The content audit only **warns** on an empty pool ("safe locations
are legitimate"), so a wholly empty world passes. Also: only **4 actor archetypes** exist (two of them NPCs —
`tavern-keeper`, `village-blacksmith`), the 4 loot tables are referenced by **no** archetype, and two of them name
content that does not exist (`wolf-common-drops` has no wolf, `bandit-boss-drops` has no boss).

**Measurement trap, and a correction.** I first reported that no location had `Traits.SafeRespawn = true` and that
every death logged `Zone {ZoneId} has no location flagged SafeRespawn`. That was **my query, not the code**:
`ContentModelConfiguration` stores traits with `ToJson()`, so the JSON keys are the property names —
`"Traits"->>'SafeRespawn'`, `IsDungeon`, `IsTown` — and I had used camelCase (`'safeRespawn'`), which matches
nothing. The seeders do set it (8 towns/waypoints plus one `SafeRespawn`/`IsDungeon = false` lobby in every dungeon
zone), so respawn and the lobby lookup work. **Always query these columns in PascalCase.**

Everything is written up in [`plans/content-seeding-pass.md`](../../plans/content-seeding-pass.md): measured
counts, P0/P1/P2 priorities, the guardrail test to add to `SeededContentValidationTests`, and what each item
unblocks. **Return here after the content pass**, then write E13c (engage → victory) and E13d (die → respawn).

Ninth pass — **the workflow specs run against their own isolated stack, and getting that green exposed four
cross-wirings**. `scripts/e2e-stack.sh` is a standalone compose project
([`docker-compose.e2e.yml`](../../docker-compose.e2e.yml), `veldrath-e2e`, own volume, own container name) on
**Postgres 55432 / API 9100 / preview 4273**. Nothing is shared with a development environment, `start --fresh`
drops the volume first (a green run then means "works against the seed"), a port it did not take is reported
instead of stolen, and it never `pkill`s — an earlier version killed a developer's own `dotnet run` on :9000.

Each failure below was a **configuration** defect that a single-spec run hid:

1. **`PUBLIC_HUB_URL` was never injected.** `PUBLIC_*` are inlined at `vite build`; setting only
   `PUBLIC_API_URL` left the browser's SignalR connection pointed at `.env`'s dev hub on :9000, so
   `.map-node-cell` never rendered and encounters/game-journeys/staff-actions all failed looking like client
   bugs. `playwright.config.ts`'s `webServer.env` now sets `PUBLIC_API_URL` **and** `PUBLIC_HUB_URL`, `ORIGIN`,
   `BETTER_AUTH_URL`, `DATABASE_URL` (the auth role — `.env` points at the dev DB on 5433) and
   `AUTH_RATE_LIMIT_DISABLED`.
2. **Better Auth rate-limited the run.** ~40 sign-ups a minute from loopback answered
   `Too many requests. Please try again later.` on `/register`: the client address cannot be resolved, so one
   bucket is shared per path. [`auth.ts`](../../web/client/src/lib/server/auth.ts) reads
   `AUTH_RATE_LIMIT_DISABLED` (test stacks only, never the image) **and** now sets
   `advanced.ipAddress.ipAddressHeaders = ['x-forwarded-for']` — that second part is the production fix behind
   Coolify/Traefik, where without it every player shares a single bucket per path.
3. **`upstream-failure.e2e.ts` stops the API**, so it cannot share a run with anything. `test:e2e:stack` is now
   `:specs` (parallel) then `:outage` (`--workers=1`), and CI mirrors the split in two steps. The spec's own doc
   comment already said "run with one worker"; the script was what ignored it.
4. **Every sign-up paid a 10s SMTP timeout.** Development settings point `Email:SmtpHost` at a mail container
   this stack does not run, so the bridge call hung until the web app's `AbortSignal.timeout(10_000)` fired.
   The e2e API now starts with `Email__SmtpHost=""` → `NullEmailSender` → immediate answer.

Also: `tests/game-loop.e2e.ts` stopped hard-coding `http://localhost:9000` and imports `apiBase`/`gameToken`
from [`tests/support/stack.ts`](../../web/client/tests/support/stack.ts) like every other spec, so `E2E_API_PORT`
reaches it too.

Proof: `scripts/e2e-stack.sh start --fresh` then `npm run test:e2e:stack --workspace client` → **17 passed in
about a minute** (16 in parallel, then the outage spec), with 1,470 web unit tests and a clean typecheck/prettier.
Documented for humans in [`web/client/README.md`](../../web/client/README.md).

Tenth pass — **two defects found by a person using the wizard, and the layout harness that now catches the class.**

- **`.btn` is a single-line control.** Skeleton sets `white-space: nowrap` on it, and a grid item defaults to
  `min-width: auto`. Put a sentence in a `.btn` inside a grid and the row becomes as wide as the *unwrapped*
  sentence: measured on the creation wizard, a **1290px row in a 574px track**, document 1643px in a 1280px
  viewport, so every page gained a horizontal scrollbar. Fixing it needs **both** `white-space: normal` and
  `min-width: 0` (wrapping alone still cannot shrink below the longest word). The primitive is
  `.choice-option` in [`brand.css`](../../web/client/src/routes/brand.css) — imported after Skeleton, so it wins
  on order, no `!important`, no layer fighting. **Do not put a description inside a `.btn`.**
- **The point-buy budget is 27, stats 8–15, cost `{8:0, 9:1, 10:2, 11:3, 12:4, 13:5, 14:7, 15:9}`** — so
  15/15/15 is exactly 27 and 15/14/12/11/10/10 is exactly 27. `AllocateCreationAttributesHandler` used to accept
  any allocation *under* budget and store it, and `FinalizeCreationSessionCommand` passed it to
  `CreateCharacterCommand`, so the API could create a minimum-stats character with 27 points unused. Both refuse
  now (missing / partial / out-of-range), and the wizard disables Continue with a hint while points remain.
  **Tests that walk the wizard must spend the budget**: `spendAttributePoints` in
  [`tests/support/creation.ts`](../../web/client/tests/support/creation.ts).
- **Layout is asserted, not photographed.** [`tests/support/layout.ts`](../../web/client/tests/support/layout.ts)
  `expectNoHorizontalOverflow` checks `document.scrollingElement.scrollWidth` and every descendant rect against its
  container, skipping descendants inside an `overflow-x` clipper (the map cells truncate deliberately).
  [`tests/layout.e2e.ts`](../../web/client/tests/layout.e2e.ts) drives the seven steps at 1280/768/390 plus
  `/`, `/lore`, `/patch-notes`, `/community`, `/register`, and signed in `/characters` and `/account`. **Negative
  control run before keeping it**: removing `choice-option` from the species row fails it with "the page scrolls
  sideways by 949px". jsdom has no layout, so a Vitest test can never catch this class.
- **Sweep of the other 196 `.btn` sites**: the wizard was the only sentence-in-a-button, and the only one in a
  single-column grid track. Account/mod/admin labels are two words in `flex flex-wrap` rows (they wrap instead of
  overflowing); the game panels keep dynamic text in a `<span>` beside short verbs. `/account` is the riskiest
  variable-content surface and is in the harness.

Eleventh pass — **the forest can now be foraged from, and the engine's harvesting command cannot be reused.**

- `LootTablesSeeder` gained `forest-forage` (`TypeKey = "harvesting"`, `IsHarvesting = true`, guaranteed
  `glowcap-mushroom` 1–2). Until this there was **no harvesting loot table at all**, so a node could be
  harvested for nothing — and `gather-mushrooms` had no possible source.
- [`HarvestableNodesSeeder`](../../Veldrath.Server/Data/Seeders/HarvestableNodesSeeder.cs) authors five nodes in
  `wood-road` and `old-clearing` (the Ashlen Wood), seeded from `Program.cs` against `GameDbContext`, idempotent
  by `NodeId` so **a restart never restores a depleted node**. `LootTableRef` is the engine's reference form:
  `@loot-tables/harvesting/forest:forest-forage` (the roll uses the part after the last colon).
- **`HarvestNodeCommand` is single-player and must not be wired to the hub.** It reads
  `ISaveGameService.GetCurrentSave()` (one global "current save") and writes the bag through the save-game
  store, not the character blob the client reads. The multiplayer path composes the *stateless* pieces —
  `HarvestCalculatorService`, `CriticalHarvestService`, `ToolValidationService`,
  `LootTableService.RollHarvestingDrops(node.LootTableRef, yield, isCritical)` (returns `ItemDrop` with an
  `@items/general:<slug>` ref) — plus `IInventoryService.AddItemAsync(name, itemRef, qty)`, exactly as combat
  loot does. All four services are registered by `AddRealmEngineCore`.
- `HarvestableNodes` lives in **`GameDbContext`** (live state), while `DatabaseSeeder.SeedAsync` is content-only
  and is reused by `SeededContentProbe` with just a `ContentDbContext` — so node seeding belongs in `Program.cs`,
  never inside `SeedAsync`.
- Design decisions for the hub slice (recorded in [`plans/content-seeding-pass.md`](../../plans/content-seeding-pass.md)):
  authored nodes, `node.LocationId == character.CurrentZoneLocationSlug` as the guard, MinToolTier 0 only,
  skill rank 0 and no skill XP yet, `collect` advanced with both slug and ref, `LocationNodes` pushed on
  `EnterLocation` and refreshed after failures, and the web consumer in the same commit (parity suites).
- **The hub slice is shipped and the loop is green** (`GetLocationNodes`/`HarvestNode`, `LocationNodes`,
  `Harvested`, a Gatherable panel section). Getting the spec green exposed four things worth remembering:
  - **`IInventoryService` (`EfCoreInventoryService`) writes save-game records, not `Characters.InventoryBlob`** —
    the bag the client reads. Both harvest and combat loot now write the blob through `CharacterBag`
    (twelfth pass below).
  - **Region → zone travel was broken**: region `Zone` nodes had no `TargetMapId`, and
    `TravelToNodeHubCommand` only switches maps when a node names one. `MapGraphSeeder.LinkRegionZonesToZoneMaps`
    now sets it, guarded by a seeder test. Needs `--fresh` because the seeder skips an existing graph.
  - **Zone maps chain `entry → first location → second …`**, so the second location is two hops from arrival.
  - **Nodes never regenerate** (`NodeSpawnerService.RespawnNode` has no caller), and `session.zone` goes stale on
    a map travel that changes zone (banner shows the old zone with the new location). Both closed in the twelfth
    pass below.

Twelfth pass — **the three follow-ups the harvest loop left behind are closed.**

- **One home for the bag.** [`CharacterBag`](../../Veldrath.Server/Features/Characters/CharacterBag.cs) owns
  `Read`/`Add`/`Serialize` over the `[{ "ItemRef", "Quantity" }]` JSON. `AttackEnemyHubCommand.RollLootAsync` now
  takes the `Character` entity, writes the blob and saves the row — `IInventoryService` is gone from the handler,
  and the test asserts the stored blob rather than a fake inventory. The harvest handler dropped its private
  `ReadBag`/`AddToBag` for the same helper.
- **Lazy node regrowth, no scheduler.** [`HarvestingOptions`](../../Veldrath.Server/Settings/HarvestingOptions.cs)
  (`Harvesting:RegrowMinutes`, default 30, `[Range(1, 1440)]`, `ValidateOnStart`) is read by
  `GetLocationNodesHubCommandHandler`: a node that cannot be harvested and whose `LastHarvestedAt` is older than
  the cutoff is reset to full health with `TimesHarvested = 0` and saved on read. Guarded by
  `LocationNodesRegrowthTests` (comes back after the window, stays empty inside it).
- **A walk is an arrival.** `NodeTraveled` carries `ZoneId` and nothing else — no name, inn/merchant flags or
  occupants — so a cross-zone walk left the banner reading the old zone. `GameHub.TravelToNode` now calls
  `SyncArrivedZoneAsync`, which updates `Context.Items` (`CurrentZoneId`/`CurrentMapId`/`CurrentNodeId`) and sends
  the same `ZoneEntered` snapshot `EnterZone` sends; the client clears its zone when an arrival reports none
  (`handleNodeTraveled`). Three hub tests (`GameHubZoneTests`) cover crossing, staying and leaving for the region
  map; two session tests cover the client half.
- **Known gap this leaves:** a walk out of a zone changes the session row's zone but never sends `PlayerLeft` to
  the zone it left, so peers in the old zone can keep showing the walker until they refresh. Travel also never
  sets `Context.Items["CurrentZoneId"]` before this pass, which is now fixed for the arrival's zone.

Thirteenth pass — **chests exist, and they do not stand still.**

- **Where the row lives.** [`LootChest`](../../Veldrath.Server/Data/Entities/LootChest.cs) is in
  `ApplicationDbContext` (not `GameDbContext` like `HarvestableNodes`) because nothing in the engine consumes it —
  only `LootTableService.RollChestDrops` — so it needs no `INodeRepository`-style abstraction, and it belongs
  beside `GroundItems`/`ZoneInstances`, which are the other per-location live rows. It has a nullable
  `ZoneInstanceId` with cascade delete, so a delve's chests die with the run. Migration:
  `Veldrath.Server/Migrations/*_AddLootChests.cs` (deploy needs it; tests use `EnsureCreated`).
- **Authored vs roaming, one table.** `Kind` discriminates them. `LootChestsSeeder.Authors()` seeds two authored
  chests in the Ashlen Wood (`old-clearing-strongbox`, `wood-road-cache`), idempotent by `ChestId` and preserving
  opened state. Roaming rows are inserted by `ChestService` from the zone's `ZoneLocation` rows: not hidden, not
  safe respawn, not instanced, `MinLevel >= Chests:MinSiteDangerLevel`, location not already occupied, zone is
  not a town. Site and loot table are both drawn by `RarityWeight`.
- **Swept, not timed.** `ChestService : BackgroundService` mirrors `EnemyRespawnService`. `ReopenAt` drives the
  reopen window (`Chests:ReopenAfterMinutes`, 180); a zone gains a roaming chest when its newest roaming
  `SpawnedAt`/`OpenedAt` is older than `Chests:SpawnEveryMinutes` (90) and it holds fewer than
  `Chests:MaxRoamingPerZone` (1). Opened roaming rows are tombstones on purpose: they are the clock. Every write
  is conditional (`ExecuteUpdateAsync`), so two sweeper instances cannot double-reopen a row.
- **Node regrowth moved onto the same sweep.** The lazy rule I shipped hours earlier is gone;
  `GetLocationNodesHubCommand` is a pure read again, and `ChestService.RegrowNodesAsync` resets depleted nodes
  whose `LastHarvestedAt` is older than `HarvestingOptions.RegrowMinutes`. `LocationNodesRegrowthTests` was
  replaced by `ChestSweeperTests` (reopen window, roaming site/town/cap/cadence filters, node regrowth).
- **First open wins.** `OpenChestHubCommandHandler` guards position, instance and `IsOpened`, rolls, then claims
  with `WHERE IsOpened = false`; a rows-affected of zero answers "someone else got there first" instead of paying
  twice. Items go into `Characters.InventoryBlob` through `CharacterBag`; `roll.Gold` is routed through
  `AddGoldHubCommand` so the attributes gold key keeps one writer.
- **Transport + web.** `GetLocationChests`/`OpenChest` on the hub, `LocationChests` on pull and on `EnterLocation`,
  `ChestOpened` to the current zone group (a shared chest disappears for the party, not just the opener). Web:
  `LocationChest`/`LocationChestsPayload`/`ChestOpenedPayload`, `session.chests`, a **Chests** section in the Local
  panel, pulls on `LocationEntered` and on `NodeTraveled`. `harvest.e2e.ts` now opens the strongbox and asserts the
  guaranteed potion lands in the bag.
- **Gaps worth remembering:** the sweeper does not broadcast (a chest appearing under a player's feet only shows
  on their next arrival — the difficulty-scoped group name lives per connection, not on the row); the seeded
  `forest-chest-loot` has no `gold` entry, so chests pay items only; the e2e stack exports
  `Chests__ReopenAfterMinutes=1` + `Chests__SweepIntervalSeconds=5` so an isolated re-run recovers the authored
  chests, and the spec walks on to the road's cache and then skips with that reason if both were emptied.

Fourteenth pass — **people exist, and the world cannot be fooled into handing them a knife.**

- **Placement is a row, not a pool entry.** [`LocationNpc`](../../Veldrath.Server/Data/Entities/LocationNpc.cs) in
  `ApplicationDbContext` (`NpcId`, `ZoneId`, `LocationSlug`, `ArchetypeSlug`, optional `DisplayName`, `SortOrder`),
  seeded by [`LocationNpcsSeeder`](../../Veldrath.Server/Data/Seeders/LocationNpcsSeeder.cs): blacksmith at
  `aurelian-market`, tavern keeper at `hearthbound-inn`, elder at `crestfall-square`. Migration
  `*_AddLocationNpcs.cs`. A pool entry means "may spawn here, weighted" and every pool entry becomes an attackable
  `SpawnedEnemy`, which is exactly why the seeder comment had kept NPCs out of pools — and why placement needed to
  be explicit.
- **The bug that made this urgent:** the main-story quest in `QuestsSeeder` has `talk:village-blacksmith` and no
  blacksmith existed anywhere, so the quest was unfinishable — the same shape as `gather-mushrooms` without nodes.
- **Read + talk.** `GetLocationNpcs` → `LocationNpcs` on location entry (pushed next to nodes and chests). The DTO
  carries the archetype slug, name, occupation and role flags read from `ContentDbContext.ActorArchetypes`, because
  the shared `NPC` projection has no roles. `TalkToNpc(slug)` verifies the placement is at the character's location,
  answers with a line from `IDialogueRepository.GetBySpeakerAsync(slug)` filtered to greetings (neutral fallback),
  sends `NpcTalked`, then advances `talk:<slug>` — the archetype slug is the speaker key and the objective key.
- **Hostile filter.** `EfCoreEnemyRepository.MapToModel` now projects `traits["hostile"] =
  e.Traits.Hostile ?? true`, and `ActorPoolResolver.SpawnRosterAsync` skips a non-hostile entry with a warning, so
  content that puts a shopkeeper in a pool cannot turn them into a target. `ResolveArchetypeAsync` is untouched, so
  rebuilding existing enemy rows still works.
- **Client-side gap this exposed:** location lists were only pulled on a *move*, so a resumed character saw no
  nodes, chests or people until they walked. `handleCharacterSelected` now pulls ground items, nodes, chests and
  people once after `EnterZone`. Session tests assert those four invocations.
- **Web:** `LocationNpc`/`LocationNpcsPayload`/`NpcTalkedPayload`, `session.npcs`, a **People** section with a Talk
  button (shows `· quests` / `· trades` from the flags), the line logged as `Name: “line”`. The `TalkToNpc` entry was
  removed from the hub-methods parity allow-list because it now has a real caller.
- **Tests:** `NpcPlacementTests` (seed guards, listing with roles, talk line, refusals, hostile/non-hostile roster),
  four reworked `TalkToNpc` hub tests (`NpcTalked` goes to `Clients.Caller`; the journal push goes to the connection
  id, so assertions must use the right proxy), two session tests, and the elder-then-wood walk in `harvest.e2e.ts`.

### Session-102 (2026-11-19) — Design documentation restructured for a creative team

Started as "clean up the stale Avalonia references", ended as a rebuild of the design set plus a guard
against the drift that caused them.

- **The document was not merely stale, it was wrong.** `docs/design-system.md` documented a component class
  vocabulary (`.btn-primary`, `.btn-ghost`, `.badge-accent`, `.health`, `.mana`) that has never existed in the
  codebase — buttons are Skeleton `btn preset-filled-primary-500` / `preset-tonal-surface` / `preset-tonal-error`
  and badges are `badge preset-tonal-*`. It also claimed `.card` was Layer 2 with 24px padding (it is
  `--vds-bg-1` with no padding), claimed the app does not use `--vds-space-*` (it does, throughout `brand.css`),
  and documented the heading scale as `--vds-text-*` when headings are `.h1`/`.h2`/`.h3` in `rem`.
- **`--vds-gold*` was undocumented.** The metal the whole identity is built from — logo, panel-header icons,
  nav glyphs, hero kicker, equipped marks, prose emphasis — appeared in `theme.css` and 18 call sites in
  `brand.css`/`layout.css` but in no token table. Documented now, as a first-class accent with its own role:
  gold is the metal, Seal is the action.
- **Contrast table was numerically wrong**, not just dated. Recomputed with the WCAG formula: text primary
  18→16.6, seal-light 5.8→5.1, success 5.4→7.0, warning 4.6→7.6, info 5.2→6.7, danger 4.9→5.1. The old numbers
  were inflated for accents and deflated for greys, which is why seal-light looked safe for body text when it is
  not.
- **New set:** `docs/design/brand-guide.md` (the mark described for the first time, file-to-surface map, clear
  space, minimum sizes, raster-logo-vs-Cinzel rule, art direction), `docs/design/asset-specs.md` (canvas sizes,
  SVG-first, weight budgets, naming, icon grid, item-art contract), `docs/design/asset-inventory.md` (every file
  with its consumer and a proposed verdict), `docs/design/tokens.json` (machine-readable export).
- **`docs/client-ui-architecture.md`** rewritten around the surface inventory: every panel, block, dialog and
  HUD widget, what opens it, and the four states each must define. The two structural decisions and the Foundry
  review gate survive; the retired-surface framing is one line.
- **Guard:** `web/client/src/lib/parity/design-tokens.test.ts` — `theme.css` `:root` ↔ `tokens.json` ↔
  `docs/design-system.md`, four assertions (missing, extra, value mismatch, undocumented). It passes; it would
  have caught the gold omission.
- **Published.** `docfx.json` now includes the design set in `content` and maps `docs/design/images` (optimised
  2× previews only — the live 1–2 MB originals must never be copied) plus `docs/design/tokens.json` as a
  resource. `docs/toc.yml` gains a **Design** section. AGENTS.md's allowlist paragraph, the `web-ui` skill and
  `web/README.md` point at the new set.
- **`web-ui` skill had drifted too:** it named `--vds-crimson` and `--vds-emberfall` (both nonexistent; the real
  tokens are `--vds-seal` and `--vds-ember`), listed background layers as `#14151D/#1B1D28/#232533`, body text as
  `#E8E6E3`, muted as `#9A96A0`, border as `#2E2F42`, and called Lora the body face. Corrected against `theme.css`.
- **Dead tokens found:** `--vds-bg-deeper`, `--vds-accent`, `--vds-indicator-player|occupant|enemy` and
  `--vds-game-footer-h` are declared and never consumed. Listed in design-system §8.3 — give each a job or delete
  it, but do not document it as live.
- **Asset reality, measured:** 17 PNGs, ≈31.8 MB, of which only 3 are referenced anywhere. The crest is 1.61 MB
  rendered at 76px (and 44–48px elsewhere), the header wordmark 1.01 MB rendered at 30px tall, the footer lockup
  1.81 MB at 64px, the 512 install icon 583 KB. Fourteen files (≈27.2 MB) are referenced by nothing: ten alternate
  lockups and V studies, plus four herald emblems at 1254–1669px with no consumer. **No vector or layered master
  is committed** — that, not the file sizes, is the blocking gap.
- **Decision on the 14: left in place (2026-11-19).** No move, no deletion. They were reviewed visually
  and are not duplicates of the live marks — they are an unassigned lockup family, and two of them fill
  roles the docs already assume exist: `veldrath-v-clean-…` is the V-only monogram the brand guide names
  for anything under 48px, and `veldrath-icon-big-…` is the rounded-square install tile that the
  180/192/512 icons have no master for. Both stay unassigned and unwired. The four `herald/*` emblems
  point at nothing: no seeded organisation, no faction record in content, no emblem slot in the UI.
  Recorded in `asset-inventory.md` §4 with a role-by-role table; the re-export work is unaffected.
- **Docs link gate was already red** on `plans/content-seeding-pass.md` (8 links: `../../Veldrath.Server/…` from
  `plans/`, and `Features/Shops/` where the directory is `Features/Shop/`). Fixed, because it is the same CI job
  that builds the new pages. `node scripts/check-markdown-links.mjs` now reports 57 files clean.

### Session-103 (2026-11-20) — E2E reporting, and the spec that had stopped running

Asked for "reporting for e2e like we do for coverage". Investigating it turned up two defects worse than the
missing report.

- **No Playwright reporter was ever configured.** `playwright.config.ts` set `webServer`, `retries`, `use` and
  `testMatch` and no `reporter`, so the default was console `list` and `web/client/playwright-report/` **never
  existed**. Both web jobs upload that path with `if-no-files-found: ignore`, so the step was green and empty.
  There was no JSON or JUnit either: no history, no flaky count, no way to notice a spec that stopped running.
- **`tests/encounters.e2e.ts` did not run in CI.** The spec list existed twice — `web/client/package.json` (15
  specs) and `.github/workflows/ci-web.yml` (14) — and the workflow's copy was missing the combat and death
  journey that the content pass had just unblocked. Two hand-maintained lists, one short.

What landed (detail and decisions in [`plans/e2e-reporting.md`](../../plans/e2e-reporting.md)):

- **One manifest, one runner.** `web/client/tests/e2e-manifest.json` names every spec once across the runs
  `stack-free`, `stack` and `outage`; `web/client/tests/run-e2e.mjs` expands a run into the Playwright
  invocation, applies the run's env (manifest values are **defaults** — a variable already in the environment
  wins, which is how CI keeps its own ports), and points the reporters at per-run files. `--list`/`--dry-run`
  work without `npm ci`; a run with an unmet gate refuses to start instead of producing an all-skipped report.
- **Reports per run**, because one job runs Playwright twice and a fixed output file would be truncated by the
  second: `html` (never opens), `json`, `junit`, and `outputDir` for traces, all from `PW_*` variables.
- **`scripts/e2e-summary.mjs`** renders `E2EReport/Summary.md` from one or more JSON reports: totals, per-spec
  table slowest-first, failures with their first error line and trace path, skips with their reason, plus the
  commit in the header (a reused preview is `reuseExistingServer` outside CI, so provenance matters).
- **Flaky means a *failed* attempt, not merely a second attempt.** A test is flaky only when at least one
  attempt is `failed`/`timedOut` and the last one passed. A `skipped` attempt is not a failure: in a serial group
  a timed-out test marks the rest of the group `skipped` on attempt 1, and the group retry passes them all — so
  `[skipped, passed]` is **passed**, which is what Playwright's own tally says. Counting it flaky is why comment
  1086 reported 2 flaky / 19 passed for run 2194 while Playwright's console said 1 flaky / 20 passed
  (`encounters.e2e.ts:134`). `Duration` stays the sum of attempts across workers, not wall clock, and the
  summary says so under the totals table.
- **`scripts/check-e2e-manifest.mjs`** is the gate, and it is deliberately **not** a pass/fail gate: it fails
  when a spec on disk is claimed by no run, when a claimed spec produced no result (a suite with zero specs is
  collected from `suite.file` for exactly this), when a spec ran that the run does not claim, when a skip has no
  allow-listed reason, or when a run falls below its floor in `web/client/tests/e2e-baseline.json` (empty today
  on purpose — `--suggest` prints the number to commit). Flaky is counted and named, never gated.
- **A separate `<!-- ci-e2e -->` comment**, not a section of the coverage comment: `merge-coverage-sections.awk`
  hardcoded `## Coverage summary`, and test counts under a coverage heading are the wrong document. The script
  now takes `--marker` **with** `--section`/`--title` and a `--heading`, and the awk takes its marker and heading
  as parameters — coverage's output stays byte-identical, which its 102-check test pins.
- **Two skips are allow-listed with reasons**: `harvest.e2e.ts` (chests already emptied on a non-fresh stack) and
  `upstream-failure.e2e.ts` (it needs an API started by `scripts/e2e-stack.sh`; CI starts `dotnet run` itself, so
  the spec self-skips there and runs locally).
- **Tests**: `node --test scripts/tests/e2e-reporting.test.mjs` (22 tests: summary rendering, flaky (including
  `[skipped, passed]` → passed and `[failed, passed]` → flaky), merged runs, missing report, and five negative
  controls for the gate) and the extended
  [`scripts/tests/forgejo-pr-comment.test.sh`](../../scripts/tests/forgejo-pr-comment.test.sh) (case I covers the
  e2e marker, its heading, the kept sibling section, and that the coverage comment is neither patched nor
  deleted).
- **Worth knowing when touching this**: `require.resolve('playwright/cli.js')` throws
  `ERR_PACKAGE_PATH_NOT_EXPORTED` — resolve the package entry and take `cli.js` beside it. And the checker's
  default manifest path is resolved **from the script**, not the working directory, because the same command has
  to work from the repo root, `web/` and `web/client/`.

Still open after this pass: the baseline floors are empty until a green CI run records them; the outage spec
only executes locally (CI has no API for it to stop); and the e2e comment has no artifact deep-link, because the
artifact URL shape is instance-specific.

### Session-104 (2026-11-20) — The coverage comment now quotes the gate, not the reporter

The web coverage **check** and the web coverage **comment** have never been the same measurement, and
on PR #88 the difference surfaced as a contradiction: the gate logged `Branches : 90.33% ( 4428/4902 )`
while comment 1063 carried `Branch coverage | 89.8% (3,752 of 4,180)`, which reads as a gate failure
beside a green check.

- **Mechanism, nailed down at the source.** `codecov/codecov-report`'s `CoberturaReader.ReadLines`
  (`src/CoverageReport/CoberturaReader.cs:119-160`) sums the per-line `condition-coverage="x% (c/t)"`
  attributes and never reads the root `<coverage branches-valid=… branches-covered=…>`. istanbul
  writes a line-level condition only for a branch site it can attach to a source line, so that sum is
  a *subset* of v8's branch map — 4,180 of 4,902 sites on that run. The root attributes **do** agree
  with v8 exactly (`branches-valid="4902" branches-covered="4383"` on `development@44422ac2`), so the
  lossy step is the per-line conversion, not the file.
- **Decision: the comment quotes the gate** (option 1 of three; the plan records why 2 and 3 lost).
  `scripts/web-coverage-gate.mjs` heads the `web` section with the totals parsed from the coverage
  step's log — that is also the only place vitest's `ERROR: Coverage for …` threshold lines exist, so
  per-glob floor breaches now reach the comment — and labels the reporter's per-directory table as
  Cobertura accounting with both numbers for the run. Tests:
  `node --test scripts/tests/web-coverage-gate.test.mjs` (8 cases).
- **Two reporting defects fixed alongside it.** `coverage.reportOnFailure` was off, so a red gate wrote
  no Cobertura at all (vitest's `text` lines went to the log, nothing else) and the comment kept the
  previous head's numbers; and the comment step had no `if: always()`, so a red job never posted. Both
  are on now. Verified locally: a failing gate at 89.41% still writes `coverage-summary.json` and
  `cobertura-coverage.xml`.
- **Do not "fix" this by reconciliation.** The gate is v8's branch *sites*; the table is istanbul's
  line conditions. Lowering the gate to 4,180 or subtracting the 722 conditionless sites from the
  numerator would each be a real weakening. The remaining fix belongs upstream (read the root
  attributes when present, fall back to the per-line sum for coverlet) plus a `COVERAGE_REPORT_REF`
  bump — recorded as residual in the plan.
- **Gotcha for local reproduction.** `npm run coverage --workspace client` needs `PUBLIC_API_URL` and
  `PUBLIC_HUB_URL` set or SvelteKit aborts with `env_invalid`; `src/lib/testing/harness.test.ts:84`
  asserts the checkout path contains `Veldrath`, so a worktree named otherwise fails one test (and,
  with `reportOnFailure` now on, still writes the report). The pinned reporter needs
  `PATH="$HOME/.dotnet:$PATH"` if the SDK is not installed system-wide.

### Session-105 (2026-11-20) — A script the web job runs now triggers the web job

`scripts/check-e2e-manifest.mjs` and `scripts/e2e-summary.mjs` were executed by `ci-web.yml` but named
by neither `paths` list, so a PR that edited one without its test ran no web CI (PR #92, head
`968f0cf6`: merge gate and Build Docs only). Both are now named in the `push` and `pull_request` lists,
next to `scripts/web-coverage-gate.mjs` and `scripts/tests/**` — the rule is "a script the job executes
is in the job's paths". `scripts/e2e-stack.sh` stays unnamed on purpose: it is the local equivalent of
the authenticated e2e job, and CI starts its own stack. `bash scripts/ci-merge-gate.sh --uncovered` now
reports 37 paths, 8 in the scripts class.

### Session-106 (2026-11-20) — The coverage reporter now reads the root counters (v1.0.4)

The residual Session-104 recorded is closed upstream, not in this repository.

- **Upstream.** `codecov/codecov-report` PR #1 merged as `f6a5681` and tagged **`v1.0.4`**: the reader
  takes the root `<coverage branches-valid=… branches-covered=…>` attributes when a file carries them
  and falls back to the per-line `condition-coverage` sum for coverlet (whose per-line conditions are
  complete). The Total row therefore reports the gate's branch pair; only the formatting differs —
  one decimal (`90.3%`) against the gate's two (`90.33%`).
- **Pinned in both on-demand paths.** `scripts/coverage-report.sh` (`COVERAGE_REPORT_REF` default) and
  `scripts/generate-coverage-report.ps1` both default to `v1.0.4`. The tool clones into
  `.coverage-report/v1.0.4/` (gitignored) on first use.
- **The contrast paragraph reads the root counters.** `scripts/web-coverage-gate.mjs` has three
  mutually exclusive states: root counters equal to the gate → "the two accountings agree on this run"
  plus the per-directory caveat (those rows are still istanbul's line conditions and do not sum to the
  Total); root counters present but different → "a Cobertura file that does not describe this run";
  no root counters → the previous per-line paragraph verbatim. The `Warning — accounting mismatch`
  note still fires in the stale-file case. Tests:
  `node --test scripts/tests/web-coverage-gate.test.mjs` (9 cases).
- **Verified against real artifacts, not only fixtures.** Replaying the PR #88 Cobertura file through
  the bumped pin printed `Fetching coverage reporter v1.0.4` and `Branch coverage: 90.3% (4,428 of
  4,902)`; the gate paragraph on the same file says the two accountings agree.
- **Not a defect to "fix".** The `90.33` vs `90.3` precision difference is deliberate (the tool
  formats percentages with one decimal); the contract is that the gate row and the reporter's Total
  row carry the same two counts. `scripts/coverage-floors.json`, `coverage.runsettings` and
  `web/client/vite.config.ts` thresholds did not move.

### Session-107 (2026-11-20) — Dialogue depth: E4 is shipped

Milestone E's last phase. The content schema is in [`foundry-content-schema.md`](foundry-content-schema.md); this is
the runtime and the panel.

- **Two bridges, one shared evaluation.** `GetNpcDialogue` and `ChooseNpcOption`
  ([`DialogueHubCommands.cs`](../../Veldrath.Server/Features/Characters/Npcs/DialogueHubCommands.cs)) both go through
  `NpcDialogueReader.BlockedReason`, so the reason a button is disabled and the reason the server refuses are the same
  computation. The read payload carries `Enabled`, `BlockedReason` and the grant preview; `Response` is deliberately
  **not** sent until the choice is made.
- **One-shot is claimed, not checked and then granted.** `ClaimAsync` inserts the `CharacterDialogueChoice` row first;
  a duplicate key means "already spoken about that" and pays nothing. Grabbing the gold before the claim would have
  made a double click pay twice.
- **Grants go through their owners**: items into `Characters.InventoryBlob` via `CharacterBag`, gold through
  `AddGoldHubCommand`, quests through `AcceptQuestHubCommand`, objectives through `QuestJournal.Advance`. Quest
  enrolment is applied **first** because it reloads and saves the character, then the journal/bag are written once,
  then gold — the order in `ApplyGrantsAsync`.
- **Entry selection**: the named `entrySlug` must belong to that speaker, otherwise the speaker's entry with options
  wins. Chaining is one `NextSlug` hop, and the chained conversation is rebuilt against the **reloaded** character so
  a granted item changes what the next entry offers.
- **Client**: `DialogueOverlay.svelte` + `dialogue.ts` (pure display rules, `dialogue.test.ts`), `session.dialogue`,
  and the People block's Talk now calls `startConversation`, which still runs `TalkToNpc` first — that is what advances
  `talk:` objectives, and dropping it would silently break the quest path.
- **Modal consequences, twice**: `harvest.e2e.ts` had to close the panel after talking, and the paywall of a modal
  panel over the map is the failure mode to remember for any future "Talk" affordance.
- `dialogue.e2e.ts` asserts the whole transport today (People entry → hub → panel → greeting still logged). It asserts
  "nothing more to say" rather than a choice because no conversation is authored in the seed — the choose path is
  covered by 16 handler tests. **When the content team authors the elder's options, extend that spec with a choice and
  its consequence.**

### Session-108 (2026-11-20) — The authenticated e2e job was red: one failure, three traps

CI reported `CI - Web (SvelteKit) / Authenticated e2e (real server)` failing on `development` after ~3 minutes.
Reproduced with the isolated stack. Four defects: one was the failure, three were the reasons local reproduction
kept lying about it.

- **The failure: a spec that assumes a character arrives at a location.** `dialogue.e2e.ts` expected `Wayfarer Elder`
  the moment the wizard lands on `/play/{id}`. Every zone map chains `entry → first location → …`, so the square is a
  hop away and the People block lists nobody. The spec now walks there with `walkTo`, which moved to
  [`web/client/tests/support/map.ts`](../../web/client/tests/support/map.ts) and is shared with `harvest.e2e.ts`.
  This is the trap the content pass already recorded for the Ashlen Wood; it is now a helper, not folklore.
- **`scripts/e2e-stack.sh` was committed without the executable bit** (mode 100644) while the docs and
  `upstream-failure.e2e.ts` both *execute* it. `execFileSync` therefore died with `EACCES` in 535 ms — and because that
  spec stops the API, the **next** run failed in a dozen unrelated places. Fixed with
  `git update-index --chmod=+x`. If a spec fails instantly with a spawn error, check the mode before the code.
- **The runner's gate read `process.env` rather than the manifest's defaults**, so `npm run test:e2e:stack` — the
  documented local command — refused to start, and CI (which exports both gates at job level) was the only place it
  worked. The gate now evaluates the *effective* env; asking for the refusal is an explicitly empty `E2E_STACK=`,
  which is what the tooling test now does.
- **`harvest.e2e.ts` asserted "no Open button remains"** after opening a chest. A location can hold a reopened
  authored chest beside a roaming one the sweeper placed, so the assertion was false on any database used twice — it
  passes on a fresh stack and fails on the second run, which is the worst shape a test defect can have. It now asserts
  what the comment always claimed: the list shrank.

Verified: the full `npm run test:e2e:stack` green twice in a row (22 specs + the outage leg), 23 tooling tests, web
lint clean. In CI the outage leg still self-skips — it needs an API the stack script started, and the job starts its
own — so the mode bug could never have shown up there.
- **`node --test` passing says nothing about `svelte-check`.** `web/client/tests/run-e2e.mjs` is type-checked (`checkJs`
  is on for the client tsconfig), so `env[gate]` on an object literal is a TS7053 implicit-any index error and the
  `Build and Test SvelteKit Web` job died in `typecheck` — while the 23 tooling tests that *execute* the same file were
  green. The env object is annotated `@type {Record<string, string | undefined>}` now. Two habits follow: run
  `npm run typecheck` after touching anything under `web/client/tests/`, and treat the tooling tests as a runtime
  check, never a compile check.

### Session-109 (2026-11-20) — The shop became a person: T1 lands the merchant band and the per-zone shelf

The approved `merchants:` band in `game-context/budget.yaml` is now enforced by the shop path, and the per-zone stock
table exists. Four facts for the next session:

- **A merchant is a placed shopkeeper, not a zone flag.** `ShopMerchantResolver` requires the character's
  `CurrentZoneLocationSlug`, a `LocationNpc` at that exact zone+location, and an `ActorArchetype` with
  `Traits.Shopkeeper == true`; `Zone.HasMerchant` is still checked, and `RequestedZoneId != character.CurrentZoneId` is
  refused as `ZoneMismatch`. `VisitShop`, `GetShopCatalog`, `BuyItem` and `SellItem` all go through it and return a
  typed `ShopRefusal` next to the message ("There is no one to trade with here."). **Consequence to remember:** only
  crestfall has placements today (`LocationNpcsSeeder`), so aldenmere / tolvaren / skarhold refuse to trade until the
  D3 rosters are seeded, and `playground` refuses because it has no authored stock. That is the intended consequence,
  not a regression — the per-zone stock was authored anyway so the towns are ready when their people arrive.
- **Stock is `ZoneStockItems` in `ContentDbContext`** (migration `20261121004324_AddZoneStockItems`, unique on
  `(ZoneId, ItemSlug)`), authored by `RealmEngine.Data/Seeders/ZoneStockSeeder` — crestfall 9, aldenmere 12,
  tolvaren 11, skarhold 12 of the 24 purchasable items. The counts are load-bearing, not cosmetic: a shelf's
  dearest row is the ceiling that sizes its town's purse (`merchants.gold_basis` in `game-context/budget.yaml`), so
  `MerchantBandTests.Gold_Basis_Reproduces_Each_Towns_Band_From_Its_Authored_Shelf` now fails the moment a shelf
  edit re-bases a band. It is deliberately *not* a `ContentBase` family: a row is a
  zone↔item join with no slug or display name, so it is absent from `ContentEntityTypes`, the Foundry schema and the
  pack importer. Sell does not consult stock — a merchant buys back anything it is offered.
- **Pricing is one place:** `ShopPricing` applies `MerchantBands.PriceMarkupPercent` (100 = the band floor) to buy and
  `BuybackRatePercent` (50 = the band ceiling) to sell, and clamps sell below buy so even a 1-gold item cannot be
  bought and sold for the same coin. `MerchantBandTests` parses `budget.yaml` and fails when the C# copy drifts.
- **The blacksmith's purse moved 500 → 250 through the importer, not the seeder.** `ArchetypeSeeder` is insert-only, so
  `content/archetypes/village-blacksmith.yaml` is the delivery path: import an old (500) copy, then the real pack,
  reported `created 1` → `updated 1` → `unchanged 1` against Postgres 17, row `Version 2`, `TradeGold 250`. Change both
  in future (the seeder for fresh installs, the pack for existing rows) or a new database and an upgraded one disagree.
- **A shelf edit is a data migration — decided in `t_3231a2ea`, not deferred.** `ZoneStockItems` has no pack family and
  is not getting one. The reason is its shape, not its size: a row is a `(ZoneId, ItemSlug)` pair of soft string
  references with no slug (`ZoneStockItem.cs:22-31`), while the importer's whole update path is
  `FindBySlugAsync` on a `ContentBase` (`ContentPackImporter.cs:119`) and its child reconciliation hangs off a parent
  navigation (`ContentPackImporter.cs:275-306`); the `Zone` being named lives in `ApplicationDbContext`
  (`Veldrath.Server/Data/Entities/Zone.cs:7`, `ApplicationDbContext.cs:34`), a context an import never opens — one
  Postgres database, four contexts (`Program.cs:97-108`, `:240`) — so a pack family would need a `ContentBase` parent
  row (a shelf keyed by the zone id, not a phantom zone: `ZoneLocation` is the existing precedent of a content family
  carrying a loose `ZoneId`) purely to give the importer something to hang the list off, and it would still buy no
  validation, because the pack validator cannot resolve a gameplay zone id or read the live `Zone` table. Option 3 (a
  reconciling seeder) stays rejected: two writers to one table disagreeing in silence is the exact failure this
  decision prevents. **Ownership:** `Authors()` is the truth — the seeder materialises it on a database that has never
  been seeded, a migration carries the delta on one that has, nothing else writes these rows. Adding a row still needs
  no migration (the seeder inserts what is missing); removing one, or changing a `SortOrder`, does. Shape, verification
  query and the "who owns a shelf" statement: [`docs/content-packs.md`](../../docs/content-packs.md); the standing rule
  for future sessions: [`engine-codebase.md`](engine-codebase.md), "Content packs — the editable catalogue".

Test gotchas found on the way: a `ContentDbContext` sharing a SQLite connection after another context created tables
needs `Database.GetService<IRelationalDatabaseCreator>().CreateTables()` (EnsureCreated is a no-op); and a fixture that
seeds a context from its constructor must keep that context in a field — `using var db` there disposes it before the
first test runs.

### Session-110 (2026-11-20) — The shelves were re-based onto the approved band, and a guard now pins the arithmetic

T1's two-row shelf defect, settled by `t_056aa527` and fixed by `t_a49d7693`. The band is the authority: it landed at
15:22 (`e691eba2`), the shelves 94 minutes later (`3b7ac2dc`, 16:56), and the newer artifact failed to obey the table
twice.

- **crestfall lost `hunters-bow` (35 gold) — 10 rows → 9.** The town's prose calls it "a starter blade", and
  `iron-sword` is that blade; the bow was a second weapon dearer than the 30-gold `rune-of-fire` ceiling the L0 band is
  sized from, which had pushed the shelf to [100, 300] against the approved [100, 250]. Nine rows still clears
  `MinimumStockSize` (8) — do not invent a tenth row to restore the old count, that is how the ceiling drifted.
- **tolvaren swapped `orb-of-force` (300) for `fire-ruby` (200)**, 11 rows unchanged. 16 x 0.50 x 300 = 2400 blew the
  approved 1600 ceiling; `fire-ruby` is the ceiling the L10 comment already named and was the one item it named that
  the shelf lacked. Skarhold's prose ("the two orbs no lower town will handle") now matches the data.
- **The gap that let it through is closed.** Nothing tied `gold_basis`'s arithmetic to the authored shelves, so
  `MerchantBandTests.Gold_Basis_Reproduces_Each_Towns_Band_From_Its_Authored_Shelf` now recomputes every merchant
  town's band from `ZoneStockSeeder.Authors()` ∩ the priced catalogue ∩ `budget.yaml`. It fails on the pre-fix shelves
  with "…crestfall's L0 ceiling from its shelf ceiling (35 gold)… but found 300 (difference of 50)". Its fixture seeds
  `ItemsSeeder` too — the values come from the catalogue, not from a copy of the prices.
- **A shelf edit is inert on an existing database until a migration deletes the old row.** `ZoneStockSeeder` is
  insert-only (`:40-60`) and `ZoneStockItems` has no importer or pack path, so `20261121025507_ReconcileZoneStockShelves`
  deletes `crestfall/hunters-bow` and `tolvaren/orb-of-force`; the next start inserts `tolvaren/fire-ruby` in the freed
  slot. Verified against Postgres 17 (Up removes exactly two rows, Down restores them at SortOrder 100, Down repeatable).
  The authoring gap itself is still open — see the parked card on `ZoneStockItems` having no authoring path.

### Session-D3-t_d65b853d (2026-11-21) — The people arrive: D3's roster is seeded, and the three towns can trade again

T1 left the shop honest and three towns unshoppable. The fix was content, not code: `LocationNpcsSeeder` now places all
thirteen rows of `docs/design/content/towns.md` §4, and `ArchetypeSeeder` carries an archetype for each.

- **Placement ids come from towns.md, not from the follow-up card.** The card listed `aldenmere-marketplace-cellar`
  for the cellar keeper; the approved table places `aldenmere-greycup-keeper` at `grey-cup`. The doc wins — it is the
  approved source and the card says so. Follow the roster table, not the prose summary of it.
- **Who trades is the "sells / teaches / refuses" column.** Eleven of the thirteen carry `Traits.Shopkeeper`; the two
  who do not are `wayfarer-elder` (teaches, no coin) and `harbour-master` (teaches the tide line, refuses berth).
  A shopkeeper archetype at a location the town does not flag is harmless: the resolver gates on the *zone's*
  `HasMerchant`, so crestfall's tavern keeper and the new carter trade from the town shelf wherever they stand (C6).
- **Every purse is inside its town's band, chosen there rather than caught by the test.** L0 150, L5 300/400/500,
  L10 900/800, L18 1600/2400/1800; `MerchantBandTests.Every_Seeded_Shopkeeper_Purse_Is_Inside_Its_Towns_Band` runs over
  the real `LocationNpcs` rows, so a new placement with a purse outside its town's tier reds the build.
- **New archetypes are named people.** `DisplayName` on a new archetype is the person (`Raekmeld`), because each is
  exactly one person; the three pre-existing generic archetypes keep role names and their three `DisplayName`
  overrides are still unseeded — towns.md §8 records that as owed with the wave B greetings.
- **The regression guard is the invariant, not a list.** `ShopMerchantResolverTests` resolves every `ZoneLocation`
  flagged `HasMerchant` in a non-dev merchant zone (six today) and fails when one has no shopkeeper or the zone has no
  shelf. The client offers its shop button from that same flag, so this is the test that keeps the button and the
  server agreeing.
- **Playground stays shopless on purpose.** `ZoneStockSeeder` has no `playground` rows and `LocationNpcsSeeder` places
  nobody there: the sandbox is `IsDevOnly`, its locations are fixtures, and inventing a roster would be inventing a
  settlement. Its shop refuses with `NoMerchant`, which is the documented decision.
