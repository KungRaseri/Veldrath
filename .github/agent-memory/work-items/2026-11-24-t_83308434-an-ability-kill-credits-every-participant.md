---
kind: work-item
date: 2026-11-24
area: engine
card: t_83308434
title: "An ability kill credits every participant the melee blow would"
---

### Session-t_83308434 (2026-11-24) — An ability kill credits every participant the melee blow would

`GameHub.AdvanceKillObjectivesAsync` says it credits "each character the caller credits with a kill", and the
melee call site passed `AttackEnemyHubResult.ParticipantIds` — the enemy's participant set. The ability call
site passed `[characterId]`, because `UseAbilityInCombatHubResult` exposed only `ReleasedCombatCharacterIds`.
A shared fight killed with an ability therefore advanced `kill:<slug>` for the caster alone, while the same
kill by melee advanced it for everyone who engaged the enemy. XP and gold were never wrong: both paths pay
`CombatHelpers.DistributeRewardsAsync` from `DamageContributions`. Objective progress was the whole gap.

- **The credited set comes from `enemy.Participants`, read under `enemy.SyncRoot` before the death transition.**
  That is the same set the melee branch reads, so the two blows now credit identically by construction. Reading
  it before `TryMarkDeadAsync` also means a caller that loses the reward race still credits its participants —
  `DistributeRewardsAsync` returns empty rewards to the losing caller, so sourcing the ids from
  `distribution.Rewards` would have credited nobody on that turn and regressed the caster.
- **`distribution.Rewards` is not the credited set, and neither is `ReleasedCombatCharacterIds`.**
  `Rewards` holds only the damaged contributors, while `EngageEnemyHubCommand` documents the opposite intent for
  progress: "A member who never acts earns no XP or gold — only progress." `ReleasedCombatCharacterIds` comes
  from `ICombatSessionStore.RemoveByEnemyIdAsync` and is wider still — it can carry a defender with a fight
  pointed at the enemy who never engaged it — so it stays the answer to `EndReleasedFightsAsync` only.
  `UseAbilityInCombatHubResult.ParticipantIds` names the distinction in its remarks.
- **No payload or signature changed.** `CombatTurn` is built field by field in the hub, so the new result field
  does not reach the client; the parity suites are untouched. `AdvanceKillObjectivesAsync`'s doc comment no
  longer says an ability kill credits the caster only, and `QuestObjectiveKinds.Kill`'s "advanced for every
  combat participant" is true of both paths now — no edit was needed there, only the fix.
- **Tests.** `UseAbilityInCombatHubCommandHandlerTests.Handle_Reports_Every_Participant_Credited_With_The_Kill`
  arranges an ally that damaged the enemy and a bystander that only holds a session, and asserts
  `ParticipantIds` is the caster plus the ally while `ReleasedCombatCharacterIds` is the caster plus the
  bystander. `GameHubPartyCombatTests.UseAbilityInCombat_On_A_Shared_Kill_Advances_The_Kill_Objective_For_Every_
  Participant` asserts one advance per participant per key for caster and ally. Two negative controls were run
  and reverted: reverting the call site to `[characterId]` fails the hub test, and populating `ParticipantIds`
  from the released set fails the handler test — they pin the defect and the wrong-set trap, not the mocks.
