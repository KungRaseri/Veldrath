---
kind: work-item
date: 2026-11-24
area: content
card: t_21bf1ee4
title: "The five tail hostiles take a silhouette class, and R1's count closes at 49"
---

### Session-D-content (2026-11-24) — The five tail hostiles take a silhouette class

Josh decided the silhouette class of the five Cinderplain tail hostiles on 2026-11-25 (`t_21bf1ee4`,
superseding the T1 proposal on `t_23f83ab4` comment #515 for those five rows only). The class is what
`docs/design/content/archetype-visuals.md` §2 R1 draws proportion and facing from and §2 R2 draws the
outline break from, so until it existed no tail portrait could be specified, and R1's `Rows classified`
column stopped at 44 of the roster's 49 rows.

- **The values are `archetypes.md`'s now, not this document's.** The *tail roster* table gained a ninth
  column, `class`, after `combat role`; the approved eight are unchanged and in order. `vent-stalker`,
  `crust-crawler` and `maw-serpent` are `beast`; `ash-picker` is `humanoid`; `ash-eater` is `spirit`. The
  decision is recorded as **decision 6** under `## Tail decisions` in `archetypes.md`, which is the block
  that governs that section.
- **`ash-eater` is the one row that moved off the original proposal.** It was proposed `beast`; Josh chose
  `spirit`, so it is drawn as one non-limbed mass — no neck, no narrow waist, no feet — with R2's single
  break a column, plume or funnel in the 70°–110° sector. Both files now say why, in the row's own reason
  and in a paragraph that calls a later `beast` read a redraw rather than a correction. Anyone who
  "fixes" it back to beast has overwritten a human decision.
- **R1's column is counted, and it now equals the roster.** beast 11 → 14, humanoid 8 → 9, spirit 4 → 5,
  unquiet 4 and townsfolk 17 unchanged: 14 + 9 + 4 + 5 + 17 = 49. The column is the class column of
  §4.1 (34 rows) + §4.1b (10) + the new **§4.1c** (5) tallied by slug against the roster rows. §4.1's
  own 34-row table is untouched — it is approved text — which is why the five live in a companion table
  rather than five new rows inside it.
- **§4.1c is a companion, and it stays out of the request lane.** It states slug, class and a one-line
  reason, assigns no P0/P1/P2 asset class, and opens no request: which request the five join is a §5
  decision. §8's *Left open on purpose* bullet was rewritten to say that, because the old bullet said the
  class itself was open and would have contradicted §4.1c on the next read.
- **Two stale statements were repaired rather than left to rot.** `archetypes.md`'s *How this section was
  checked* said the tail table passed an "eight-column check" (it is nine now, and the note says both);
  `archetype-visuals.md` §0 said `archetypes.md` "names the five it cannot [classify]", which stopped
  being true. The corrections table got a row instead of a silent edit, and the previous row was marked
  superseded for the five rows rather than rewritten.
- **Measured, not read.** A script over the roster rows (`archetypes.md` 1–20 table, 8 columns, plus the
  tail, 9) and over §4.1/§4.1b/§4.1c confirms roster 49 (32 hostile / 17 non-hostile), 34 + 10 + 5 = 49
  classified with no duplicate and no unclassified slug, and the R1 column per class equal to the sum.
  `node scripts/check-markdown-links.mjs` exits 0 over 220 files. Both scripts live in the worker's
  scratch dir, not the repo.
