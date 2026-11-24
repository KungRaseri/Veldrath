---
kind: work-item
date: 2026-11-24
area: engine
card: t_e02774b6
title: "A level-up banks the attribute points the engine awards"
---

### Session-D6-t_e02774b6 (2026-11-24) — A level-up banks the attribute points the engine awards

`CharacterProgression.AwardExperience` wrote `Level`, `Experience` and the recomputed `MaxHealth`/`MaxMana`, and
nothing else. `UnspentAttributePoints` therefore had exactly one writer in `Veldrath.Server` — the decrement in
`AllocateAttributePointsHubCommandHandler` — so every character carried zero for life and the allocation command
could only ever answer "not enough points", whatever the client showed. The engine authors the award
(`RealmEngine.Shared.Models.Character.LevelUp()`: 3 points per level, plus 2 more on every fifth); the server was
simply not recording it.

- **The grant lands in the same blob write as the pool recompute.** `CharacterProgression.KeyUnspentAttributePoints`
  is the key the allocate handler matches, `AttributePointsForLevel(level)` is `3 + (level % 5 == 0 ? 2 : 0)`, and
  `AttributePointsForLevels(oldLevel, newLevel)` sums every level crossed (3 → 7 pays 3+5+3+3 = 14, not four times the
  starting rate). The write sits inside the existing `if (level > oldLevel)` branch, added to whatever balance the
  blob already holds, so an award that crosses no level still leaves the attributes JSON untouched — the pre-existing
  fact `Handle_AwardInsideTheLevel_DoesNotRewriteTheAttributesBlob` stays green and pins that.
- **The points are not delegated to the engine object.** That object's `LevelUp()` also rewrites the pools from its
  `Constitution × 10` formulas, which this path deliberately does not adopt (see the D5 note: the class curve from
  `CharacterClass` is the authority, with the engine formula as fallback). The rate is reproduced instead, and
  `GainExperienceHubCommandHandlerTests.Handle_LevelUp_BanksExactlyWhatTheEngineLevelUpBanks` drives a real
  `Character.GainExperience` for eight levels (2, 3, 4, 5, 6, 10, 15, 20) and compares — the two rules cannot drift
  apart silently.
- **The loop closes handler-to-handler.** `Handle_LevelUp_BanksPointsTheAllocateAttributePointsCommandCanSpend` levels
  a character, then runs `AllocateAttributePointsHubCommand` for `{Strength: 3}` against the same repository: success,
  0 remaining points, Strength 10 → 13, two persisted updates. The other facts cover a single crossing (3), a
  three-level award onto an existing 4-point balance (13), the fifth-level bonus alone (5) and a multi-level award
  that crosses the fifth (11). Totals live in
  `Veldrath.Server.Tests/Features/GainExperienceHubCommandHandlerTests.cs`; this note does not restate a count.
- `AllocateAttributePointsHubCommand`'s remarks now name the source of the points it spends (a level-up) and the blob
  key, so the command no longer reads as if some other subsystem were expected to fund it.
- **Still open, filed or noted:** the client never writes the new balance into `session.stats` after a level-up, so the
  allocation panel stays disabled until the character is re-selected even though the server now has the points
  (`t_dad908f7`); `PendingLevelUps` and `UnspentSkillPoints` remain unpersisted — the engine queues and awards both, and
  no server path spends skill points; and allocating into `Constitution`/`Wisdom` still does not recompute
  `MaxHealth`/`MaxMana`, because the allocate command only bumps the attribute ints.
