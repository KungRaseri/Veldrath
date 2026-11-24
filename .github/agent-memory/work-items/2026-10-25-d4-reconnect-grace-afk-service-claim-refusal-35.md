---
kind: work-item
date: 2026-10-25
area: content
pr: 35
title: "D4: reconnect grace, AFK service, claim refusal (#35)"
---

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
