---
kind: work-item
date: 2026-10-25
area: engine
title: "Milestone A kickoff + A1: gold-first death cost"
---

### Session-84 (2026-10-25) — Milestone A kickoff + A1: gold-first death cost

**Issues filed before any code** (the lesson from C): A1 #44, A2 #45, A3 #46, A4 #47, A5 #48, A6 #49, and
`plans/death-and-first-hour.md` carries those numbers in its tracker line.

**A1.** `DeathPenaltyOptions` (section `Death`, `ValidateOnStart`): `GoldLossPercent` 10, `GoldLossCap` 100,
`GoldFloor` 10, `XpLossPercent` 5. `DeathPenalties.Calculate(carriedGold, experience, options)` is pure and returns
`DeathPenalty(GoldLost, XpLost)`: gold when `gold >= GoldFloor`, experience only when the character is effectively
broke, nothing when it holds neither; the share rounds **up** (minimum 1) so a small balance still notices, and the gold
loss is capped. `HandleDeathIfNeededAsync` now returns `DeathOutcome(IsDead, IsHardcore, Penalty)`; hardcore still
soft-deletes with **no** penalty (there is nothing left to punish, and the client is told 0/0 rather than a lie).

**Wiring cost, stated honestly:** all four handlers that can kill a player (`AttackEnemy`, `Defend`, `Flee`,
`UseAbility`) take `IOptions<DeathPenaltyOptions>` and report `GoldLost`/`XpLost`; the hub adds both to every
`CombatTurn` variant. That meant touching four handler tests' `MakeHandler` helpers — the mechanical churn is the price
of not hiding options in a static.

**Client:** `CombatTurnPayload` gained `goldLost`/`xpLost` (the payload-contract test enforces the mirror), and
`GameSession.applyDeathPenalty` folds the loss into the stat block and logs "You lost N gold." / "… experience."

**Trap:** `Character.Experience` is a `long`, so the calculator takes `long` and clamps internally; an `int` parameter
fails to compile (CS1503) rather than truncating silently.

**Tests:** `DeathPenaltiesTests` (5 pure cases), two integration deaths through `AttackEnemy` (gold-first with a
stored-blob assertion, and broke → XP), one Vitest for the client mirror. Server 771 → **778**, full solution **3,923**,
Vitest **178**, typecheck/lint/format clean.

---
