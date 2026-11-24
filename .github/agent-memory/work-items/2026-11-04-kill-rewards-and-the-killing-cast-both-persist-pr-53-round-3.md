---
kind: work-item
date: 2026-11-04
area: engine
pr: 53
title: "Kill rewards and the killing cast both persist (PR #53 round 3)"
---

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
