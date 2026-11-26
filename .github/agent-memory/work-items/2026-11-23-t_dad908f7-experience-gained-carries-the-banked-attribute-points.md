---
kind: work-item
date: 2026-11-23
area: web
card: t_dad908f7
title: "ExperienceGained carries the banked attribute points, so the panel follows a level-up"
---

### t_dad908f7 (2026-11-23) — ExperienceGained carries the banked attribute points

A level-up banks attribute points into the attributes blob (`CharacterProgression`, PR #244's follow-up), but the
client never heard about the new balance: `GainExperienceHubResult` carried the level, the XP and the four pools,
`GameHub.TurnInQuest` mirrored exactly those fields into `ExperienceGained`, and `session.svelte.ts` wrote only
`level`/`experience`/the pools into `stats`. The allocation panel budgets from `stats.unspentAttributePoints`
(`GameOverlays.svelte`), whose only writers were `CharacterSelected` and `AttributePointsAllocated`, so a level
earned mid-session left the panel reading "0 of 0 unspent points left" with every allocate button disabled until a
reload or a re-select.

- **The result reads the blob back.** `GainExperienceHubResult.UnspentAttributePoints` is
  `attributes.GetValueOrDefault("UnspentAttributePoints")` on the dictionary the award already parsed and (on a
  level-up) rewrote — 0 when the blob carries no balance, which is also what an unreadable blob reports, because
  `ParseAttributes` returns an empty dictionary rather than throwing. The literal is spelled in the handler (the
  allocation command writes the same key) so the handler does not depend on the banking constant landing later.
- **The client writes only when the field is present.** `ExperienceGainedPayload` (types.ts) now exists, so
  `payload-contract.test.ts` compares the hub's anonymous payload against it instead of skipping the event; the
  session handler spreads `unspentAttributePoints` when `!== undefined` and leaves the current balance alone
  otherwise. 0 is a real balance, so the guard is `undefined`, not truthiness.
- **Tests:** `GainExperienceHubCommandHandlerTests` — an award inside the level reports the stored balance without
  rewriting the blob, a level-up reports the balance the persisted blob holds, a corrupt blob reports 0;
  `GameHubQuestItemTests.TurnInQuest_Grants_Gold_And_Experience_Then_Announces_The_Rewards` asserts the whole
  `ExperienceGained` payload; `session-events.test.ts` — the event adopts a reported balance without a re-select and
  keeps the current one when the payload omits it.
- **Negative controls:** the handler fact was re-run on the merged tree with the value pinned to 0 and reports
  "Expected result.UnspentAttributePoints to be 7, but found 0" (4 already banked + 3 for the level); removing the
  hub's payload field reddens the turn-in fact; removing the session's spread reddens the adopt fact. The two
  patches (this one and the banking PR) merge in either order to the same tree.
