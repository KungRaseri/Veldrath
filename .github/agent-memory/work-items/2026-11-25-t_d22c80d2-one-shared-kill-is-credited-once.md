---
kind: work-item
date: 2026-11-25
area: engine
card: t_d22c80d2
title: "One shared kill is credited once: the death transition owns the participant set"
---

### Session t_d22c80d2 (2026-11-25) — One shared kill advances each participant's kill objective once

Follow-up to [`2026-11-24-t_c1139655-a-shared-kill-ends-every-fight.md`](2026-11-24-t_c1139655-a-shared-kill-ends-every-fight.md)
and its successor `t_83308434` (PR #304), which read the credited set from `enemy.Participants` under the enemy lock
**before** `TryMarkDeadAsync` and returned it even when the transition was lost. The melee handler had always done
that; #304 gave the ability path the same shape, and `t_0d9521bd` (PR #319) then routed loot and the room clear
through the same branch. Two callers in one shared fight — each holding its own `SpawnedEnemy`, because
`EfCoreEnemyInstanceStore.TryGetAsync` materialises a fresh view per read — therefore each reported
`EnemyDefeated = true` with a full `ParticipantIds`, and `GameHub.AdvanceKillObjectivesAsync` advanced
`kill:<archetype-slug>` and the legacy display-name key **twice** for every participant of one kill. The loser also
rolled a second copy of the loot and re-broadcast a clear it did not own.

**Decision: the death transition owns the credit.** `IEnemyInstanceStore.TryMarkDeadAsync` now returns
`Task<IReadOnlyList<Guid>?>` — the participant ids the kill credits, or `null` when another caller already won.
`EfCoreEnemyInstanceStore` runs the conditional `UPDATE … WHERE IsAlive` and the `EnemyParticipants` read inside one
transaction and commits them together, so the credited set is the durable set at the transition. Both handlers call
one new helper, `CombatHelpers.SettleKillAsync`, which returns a `CombatKillSettlement`:

- winner: credited ids, its own `CombatRewardShare`, the released session ids, the rolled loot, the room clear;
- loser: `CombatKillSettlement.Uncredited` — no ids, no share, no release, no loot, no clear. It still reports
  `EnemyDefeated = true`, because its own client's fight is over.

Rewards, loot and the clear move inside the same win gate, so the third asymmetry the review of `t_0d9521bd`
warned about cannot appear: one kill pays, drops, clears and credits once, in both paths.

**The card's edge, proved at the store.** If a participant's `SaveAsync` commits after the winner loaded the enemy,
the winner's in-memory copy does not know them — "credit the winner's snapshot" would drop that participant.
Reading the set at the transition avoids it: the outcome UPDATE holds the row lock until commit, and a concurrent
`SaveAsync` writes health and participants on that same row in one transaction, so a participant is either committed
before the transition (and credited) or committed after the death (and never part of the kill). That is also the set
`DistributeRewardsAsync` can pay, so credit and reward cannot disagree. `EfCoreEnemyInstanceStoreTests.
TryMarkDeadAsync_Credits_The_Participants_Committed_At_The_Transition` pins it with a view taken before the other
turn's save.

**Tests.**

- `SharedKillCreditTests` (new file) drives the **real** ability and melee handlers behind `GameHub` over the fake
  store, with two stale copies of one enemy (`FakeEnemyInstanceStore.PendingReads`, added for this): the ability
  wins, the melee caller loses, and the hub issues exactly **four** `AdvanceQuestObjectivesHubCommand` calls (two
  participants × slug + legacy name) and one drop, in the winner's bag.
- Per path, a losing-caller test: `EnemyDefeated` true, `ParticipantIds` empty, no loot, no clear, and the loser's
  bag untouched.
- `FakeEnemyInstanceStore.TryMarkDeadAsync` now mirrors the store: it returns the stored row's participants on the
  one transition it allows, `null` after.

**Negative controls, run in a detached worktree at the commit (`git worktree remove`d afterwards).**

- Losing caller credits its stale copy's participants (the pre-fix shape): all three `SharedKillCreditTests` fail;
  the hub test reports eight advances, four extraneous.
- Credit stays gated but the loser still rolls loot and records the clear (the post-#319 shape): all three fail —
  `Expected result.ItemsDropped to be empty … but found health-potion` and the loser's bag carries one.
- Store returns an empty participant list: exactly the transition-time test fails (`misses {latecomer}`).

**Gates.** `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors); `dotnet test Veldrath.Server.slnx` (Shared
1158, Core 2748, Data 697, Server 2004 — 6607 passed, 0 failed); `dotnet format Veldrath.Server.slnx
--verify-no-changes`; `node scripts/memory/memory-index.mjs --check`; `node scripts/check-markdown-links.mjs`;
`node scripts/check-content-names.mjs`; `node scripts/check-e2e-manifest.mjs`.

**No web payload change.** `ParticipantIds` is an internal hub-command result field; nothing under `web/` reads it,
and the loot/clear fields keep their shape on the same `CombatTurn`/`LocationCleared` payloads #319 shipped.

**Noticed and deliberately not changed.**

- The losing caller still broadcasts `EnemyDefeated` to its zone group, so the group can log the death twice. It is
  cosmetic (the client clears the enemy idempotently) and gating it on `ParticipantIds.Count > 0` would suppress a
  legitimate broadcast if a winner ever legitimately reported no participants. The kill fan-out that matters —
  advances, rewards, loot, clear — is winner-only.
- A stale `SaveAsync` that lands after the death still writes health and participation onto the dead row (the
  pre-existing lost-update window D4/#35 owns). It can no longer affect credit, because the transition already read
  and committed its set.
- The loser's turn carries `XpEarned = 0`/`GoldEarned = 0` even when the winner's split paid them; that was true
  before this card (the loser never had a reward share) and the row is updated for the next snapshot.
