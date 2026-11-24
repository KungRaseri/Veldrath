---
kind: work-item
date: 2026-11-24
area: engine
card: t_d3eef21a
title: "The retired client leaves the last seven notes"
---

### Session t_d3eef21a (2026-11-24) — The retired client leaves the last seven notes

Seven notes still named the Avalonia client's view models as current wiring. Six were scoped to a flagged line
each; `p3-stubs-status.md` was judged as a whole. Every replacement claim was read out of the source at
`development@dabc40aa` (the merge of #293), not inferred.

- `areas/p3-stubs-status.md` was **kept and given a banner rather than deleted**: `engine-codebase.md:323` points
  here for the full P3 status, so deletion would have broken a live cross-reference. The banner says the six items
  are closed Avalonia-client fixes, and names the two halves that survive — item 5's `VisitShopHubCommand` /
  `GameHub.VisitShop` (client half: the shop dialog in `web/client/src/lib/game/GameOverlays.svelte`) and item 6's
  `Veldrath.Server/Features/Announcements` endpoints. `session-log.md` already carries the session-3/5/9/17 dates,
  and item 6's "(see below)" is gone because there was nothing below it.
- `areas/clientsettings-new-session-5.md` got a retirement banner plus the mechanism that replaced it: no settings
  singleton and no runtime server picker; `PUBLIC_API_URL`/`PUBLIC_HUB_URL` declared in `web/client/src/env.ts` and
  inlined at build time; the hub built in `lib/hub.ts` (throws on an empty variable, refuses an `http:` hub from an
  `https:` page, token from `accessTokenFactory` → `/api/game-token`); SvelteKit server calls through
  `gameApiBaseUrl()` (`INTERNAL_BRIDGE_URL` then `PUBLIC_API_URL`); `Discord:ServerBaseUrl` the only surviving key
  of that name. `engine-codebase.md` mentions neither `ClientSettings` nor `ServerBaseUrl` any more, so nothing
  depended on this note.
- `areas/equipment-blob-schema-session-8.md` kept the blob schema and replaced the client callback with
  `session.svelte.ts`'s `equipItem` / `unequip` and its `ItemEquipped` subscriber. Three handler facts were wrong
  and are corrected: there is no `AddEquipmentBlob` migration (the column ships in `20260416172040_InitialCreate`
  and there is no `Migrations/Application/` directory), the valid slots are the twelve `EquipmentSlots` constants
  rather than eight, and the payload also carries `ClearedSlots` and the refreshed `Equipped` map — and it is
  broadcast to the zone group, not only to the caller.
- `areas/dbcontext-separation-established-2026-03-19-session-6.md` lost the `AvailableClasses` /
  `FakeContentService` gotcha (a `Task.Delay(50)` race in a deleted client test) and the `FakeServerConnectionService`
  bullet — both fixtures are gone from the tree — and now points at the engine tests that cover the path with plain
  awaits: `NewCharacterClassQueryHandlerTests.Handle_ReturnsFailure_WhenNoClassesExist` and
  `BeginCreationSessionHandlerTests.Handle_ReturnsAvailableClasses_FromMediator`.
- `areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md` got a banner separating what
  landed (`PlayerSession` replaced `ZoneSession`, and it is still the live session entity) from what died (the TMX
  region map, removed 2026-10-22 in session-46). `PlayerSession`'s current fields and the eleven-method
  `IPlayerSessionRepository` are recorded; phases 3–8 are marked never built, and the "Phase 7: Client hub wiring in
  `CharacterSelectViewModel` + `GameViewModel`" item is replaced by what travel actually uses — `GameHub.GetMapGraph`
  / `TravelToNode`, called from `session.svelte.ts` and drawn by `CardinalMap.svelte`.
- `auth-and-character-creation-plan.md`: the `ConnectionLost` fix now describes the web path (per-connection token
  from `/api/game-token`, SignalR automatic reconnect, `failed` and `disconnected` phases, the Disconnected panel in
  `GameScreen.svelte`), and the P1-4 decision returns the player to `/characters` rather than a "main menu".
- `combat-loop-plan.md`: the client section, the Phase 8 client test line, the Phase 9 UI section and the `Final`
  command now name `session.svelte.ts`, `CombatPanel.svelte`, `GameScreen.svelte`, `session.test.ts` /
  `combat-panel.test.ts`, and `dotnet test Realm.Full.slnx` plus `cd web && npm run test`. The `Veldrath.slnx`
  command is gone with the solution.

**Verified:** `node scripts/memory/memory-index.mjs --check` exits 0, `node scripts/check-markdown-links.mjs` exits
0, and a grep for `GameViewModel`, `CharacterSelectViewModel`, `axaml` and `Avalonia` over the seven files finds no
mention that is not either struck through or sitting under a banner that names the client deleted.

- **Still stale, outside this card's scope:** `areas/server-reconnect-polling-session-20.md` (`App.axaml.cs`,
  `serverStatus.StartPollingAsync`) and `areas/sveltekit-migration-phase-5-desktop-webview-session-60-2026-10-23.md`
  still describe the desktop client; `auth-and-character-creation-plan.md:39` still lists `LoginViewModel` OAuth
  tests as a remaining item; `combat-loop-plan.md:189` still scopes work to `Veldrath (Server/Client/Contracts)`.
