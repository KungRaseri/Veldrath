---
kind: work-item
date: 2026-10-23
area: content
title: "Web-first pivot and RCL action correctness"
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
