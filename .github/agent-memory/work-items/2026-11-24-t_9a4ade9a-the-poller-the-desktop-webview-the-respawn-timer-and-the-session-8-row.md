---
kind: work-item
date: 2026-11-24
area: engine
card: t_9a4ade9a
title: "The poller, the desktop WebView, the respawn timer and the session-8 row"
---

### Session t_9a4ade9a (2026-11-24) — The poller, the desktop WebView, the respawn timer and the session-8 row

Six claims from the `t_d3eef21a` review's follow-up list still described retired things as current fact. Every
replacement was read out of the source by method body rather than by the doc comment above it: at
`development@dee6b0ad`, where this branch is cut, and re-checked unchanged on `development@6e9d6caf`, which moved
under the review when #314 landed and touches neither the combat nor the announcements code.

- `areas/server-reconnect-polling-session-20.md` was **kept as a closed record with a banner**, the same judgement
  `p3-stubs-status.md` got in #297: the poller half is a session-20 Avalonia story, and deleting it would lose the
  reasoning while the surviving half still ships the same file. The banner says the poller went with the client in
  session-68 and that **no web counterpart was written or should be invented** — the shell reads a dead connection
  off the hub it already has (`session.phase` → `connecting` / `reconnecting` / `failed` / `disconnected` in
  `session.svelte.ts`), and `Program.cs:536`/`:546` keep `MapHealthChecks("/health")` and `/health/detail` for
  probes. The announcements half is recorded as current with its real pointers: contracts, entity, repository
  (`GetActiveAsync(CancellationToken)`, the `var now = DateTimeOffset.UtcNow` local still required for SQLite,
  `AddScoped` at `Program.cs:162`), `AnnouncementEndpoints` (`GET /api/announcements`, `AllowAnonymous`, mapped
  `Program.cs:515`), and the `Announcements` table in `Veldrath.Server/Migrations/20260416172040_InitialCreate.cs`
  — **there is no `AddAnnouncements` migration and no `Migrations/Application/` directory**, two things the note
  had claimed existed. It also records that nothing calls the endpoint: the web reads `/api/editorial/announcements`
  (`editorial-api.ts`), and `rest-routes.test.ts` allow-lists the legacy route as a removal candidate. The
  announcement test-structure guidance is still exactly what ships (`AnnouncementsFixture` + the separate
  `AnnouncementEmptyEndpointTests` class in `Veldrath.Server.Tests/Features/AnnouncementEndpointTests.cs`). Every
  client-only claim is struck through under its own heading.
- `areas/sveltekit-migration-phase-5-desktop-webview-session-60-2026-10-23.md` is a **retirement banner, not a
  rewrite**. Checked at `development`: no `Veldrath.Client`, no `VELDRATH_DESKTOP_UI`, no
  `web/client/build-desktop`, no `desktop` mode in `vite.config.ts`, no `build:desktop` script, `package.json`
  builds with `@sveltejs/adapter-node` only, and `web/client/src/routes/` has no `desktop` route. The banner names
  the one survivor and it is where it always was: `GameScreen.svelte` at `web/client/src/lib/game/`, rendered by
  `/play/[id]` alone now that the second route went with the shell.
- `auth-and-character-creation-plan.md` no longer lists `LoginViewModel` external OAuth flow tests as remaining.
  What remains is web-side OAuth link-mode coverage: `pages-account.test.ts` and `pages-account-branches.test.ts`
  drive `authClient.linkSocial` / `unlinkAccount`, and `auth-pages.test.ts` covers `linkableProviders` and the
  OAuth error mapping.
- `combat-loop-plan.md`'s `Scope:` bullet now scopes work to `Veldrath (Server/Contracts)`, with the client
  deletion dated and the SvelteKit app named as the client.
- `combat-loop-plan.md` no longer presents `ScheduleRespawn` as the implementation. It is nowhere in the source
  (`git grep ScheduleRespawn --include=*.cs` returns nothing); the only surviving mentions are records of its
  removal, and the note now points at a new "Enemy respawn — what ships (no per-enemy timer)" section: death is
  one conditional `UPDATE` in `TryMarkDeadAsync` that stamps `RespawnAt` from `EnemyWorldOptions.RespawnDelay`;
  `EnemyRespawnService`
  (`BackgroundService`, `Program.cs:197`) sweeps every `SweepIntervalSeconds` (default 5),
  `SweepExpiredRespawnsAsync` claims due rows with `FOR UPDATE SKIP LOCKED` and returns them,
  `EnemySpawned { Id, Name, Level, CurrentHealth, MaxHealth }` goes to `enemy.ZoneGroup` after the commit, a
  location read also settles due rows lazily through `GetAliveOrSpawnAsync` → `SettleLocationAsync`, and a cleared
  room's rows are pushed out by `ExtendClearedRespawnsAsync`. No `SpawnRosterAsync` call in the respawn path.
- `areas/session-log.md`'s session-8 row says "EquipmentBlob column (later folded into `InitialCreate`)" — the
  column's creator is `Veldrath.Server/Migrations/20260416172040_InitialCreate.cs:267`, and the corrected
  `areas/equipment-blob-schema-session-8.md` already records that there is no `AddEquipmentBlob` migration.

Two edits beyond the card's six items, both in `combat-loop-plan.md` and both to stop the file contradicting itself:
its header no longer claims to be "current as of Session-39", and the session-39 "ACTUAL IMPLEMENTATION" section
carries a banner saying `ZoneLocationEnemyStore` and `CombatSessionStore` were deleted by D2
(`EfCoreEnemyInstanceStore` over `EnemyInstances`, `EfCoreCombatSessionStore` over `CombatSessions`), that
`SpawnedEnemy.cs`, `ActiveCombatSession.cs` and `CombatCharacterHydrator.cs` survive, and that "Client Build Status"
refers to the deleted Avalonia build — without it the new respawn section contradicted text twenty lines above it.

**Verified:** `node scripts/memory/memory-index.mjs --write` then `--check` exit 0,
`node scripts/check-markdown-links.mjs` exits 0, `veldrath-memory.md` untouched (it is the template verbatim since
#301). A grep for `ScheduleRespawn`, `DesktopUiServer`, `VELDRATH_DESKTOP_UI`, `build-desktop` and `LoginViewModel`
over the six files finds only records of their removal — struck-through lines, banner-scoped text and lines that
say the thing went with the client.

Round-1 review corrections, all applied: the `Scope:` bullet is named rather than cited by line number (its number
moves with every edit above it); the grep claim reads "nowhere in the source (`git grep --include=*.cs` returns
nothing)" instead of "nowhere in the tree"; and the three nits — `ServerStatusService` is qualified as the client's
(the Discord bot still ships a class of that name in `Veldrath.Discord/Services/ServerStatusService.cs`), the
announcements line says "no client calls it — only the endpoint's own integration tests do", and the respawn
heading says "no per-enemy timer" so it does not sit oddly above a `PeriodicTimer` sweeper.
