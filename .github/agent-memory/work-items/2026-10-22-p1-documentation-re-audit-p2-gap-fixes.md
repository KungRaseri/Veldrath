---
kind: work-item
date: 2026-10-22
area: content
title: "P1 documentation re-audit + P2 gap fixes"
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
