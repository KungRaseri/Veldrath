---
kind: work-item
date: 2026-11-24
area: engine
card: t_a2b409f8
title: "A kill levels the character: combat XP goes through CharacterProgression"
---

### Session-D7-t_a2b409f8 (2026-11-24) — A kill levels the character

Combat was the one remaining writer of stored experience that bypassed the level. `CombatHelpers.DistributeRewardsAsync`
did `character.Experience += share.Xp` and nothing else, so a player who only killed filled the bar past `Level × 100`
and `Character.Level` never moved — the quest turn-in and the dev experience endpoint level through
`GainExperienceHubCommand` → `CharacterProgression`, combat did not. Found while landing #244 (`t_d97538ea`), which is
why that PR's remark said this gap was tracked separately; the remark in `CharacterProgression` now names both callers.

- **The award is the same helper, with the class row.** `DistributeRewardsAsync` takes an `ICharacterClassRepository`
  (both combat handlers inject it) and calls `CharacterProgression.AwardExperience(character, share.Xp, attrs, class)`
  after adding gold to the blob, so a crossing kill rewrites `Level`, banks the XP remainder, recomputes
  `MaxHealth`/`MaxMana` along the class curve (stored pool as the floor) and banks `UnspentAttributePoints` — exactly
  what a quest turn-in does. The class read is wrapped: a catalogue failure logs and falls back to the engine formulas
  rather than swallowing the reward.
- **The reply carries the new state.** `DistributeRewardsAsync` returns `CombatRewardShare` records (id, xp, gold,
  progression) instead of `(id, xp, gold)` tuples, and `AttackEnemyHubResult`/`UseAbilityInCombatHubResult` expose a
  `CombatProgression` (old/new level, remaining experience, `LeveledUp`, the four pools, unspent points). Both
  `CombatTurn` sends in `GameHub` project those as flat `NewLevel`/`NewExperience`/`LeveledUp`/`MaxHealth`/
  `CurrentHealth`/`MaxMana`/`CurrentMana`/`UnspentAttributePoints` members, null on a turn that paid no XP.
- **The reducer takes the server's numbers instead of adding.** `applyCombatTurnToStats` used to do
  `experience = stats.experience + xpEarned`; with a real level-up that is wrong twice (the award is banked as a
  remainder, and the level was never set). When `newLevel` is present it now sets `level` and `experience` from the
  payload and applies the pools; without it (an older server, a non-kill turn) the arithmetic stays the fallback.
  `describeCombatTurn` adds a "Level up! You are now level N." line so the fight log says what the sheet shows.
- **Pinned by tests.** `AttackEnemyHubCommandHandlerTests.Handle_LevelsTheCharacter_When_TheKillCrossesTheThreshold`
  seeds 90 XP at level 1 against the seeded warrior curve (120/20, +12/+2) and a 10 XP kill: the stored row ends at
  level 2 with 0 banked XP, `MaxHealth` 132, `CurrentHealth` 100 (a bigger bar is not a heal) and 3 banked points;
  `Handle_Solo_Kill_Still_Pays_The_Whole_Reward` now also pins a non-crossing kill's progression (level 1, 10 banked).
  `reducers.test.ts` covers the levelling turn, the non-crossing turn and the log line.
- **Measured.** `dotnet build Veldrath.Server.slnx` and `Realm.Full.slnx` 0/0; `dotnet test Veldrath.Server.slnx`
  6558 passed / 0 failed (Shared 1158, Core 2748, Data 694, Server 1958); `dotnet format --verify-no-changes` exit 0;
  web typecheck 0/0, lint clean, 1586 unit tests, build clean; fresh isolated stack e2e 25 passed with the halrow
  journey failing for its own reason (it invokes the `GameHub.GainExperience` method #244 removed — carded as
  `t_fbe098ab`) and the encounters death journey passing when run alone (parallel-run flake, not a regression).
- **Still open.** A party member credited with a share also levels server-side, but only the caller's sheet hears about
  it: participants have never received their XP in any payload (`EnemyDefeated` carries no numbers), so their sheet
  still waits for the next character load. That is the pre-existing party-XP gap, not new here. A second gap surfaced
  while proving the stack run: a shared kill deletes every session pointed at the enemy, and the other fighter is only
  told "Not in combat" (carded as `t_c1139655`; it makes the encounters death journey fail in a full parallel run and
  pass alone, with the failing character at `Level 1, Experience 0` — never this code path).
