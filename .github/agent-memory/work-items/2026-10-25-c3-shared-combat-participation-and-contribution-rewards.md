---
kind: work-item
date: 2026-10-25
area: content
title: "C3: shared combat participation and contribution rewards"
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
