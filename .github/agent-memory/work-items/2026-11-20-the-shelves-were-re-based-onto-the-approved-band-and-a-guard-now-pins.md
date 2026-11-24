---
kind: work-item
date: 2026-11-20
area: content
title: "The shelves were re-based onto the approved band, and a guard now pins the arithmetic"
---

### Session-110 (2026-11-20) — The shelves were re-based onto the approved band, and a guard now pins the arithmetic

T1's two-row shelf defect, settled by `t_056aa527` and fixed by `t_a49d7693`. The band is the authority: it landed at
15:22 (`e691eba2`), the shelves 94 minutes later (`3b7ac2dc`, 16:56), and the newer artifact failed to obey the table
twice.

- **crestfall lost `hunters-bow` (35 gold) — 10 rows → 9.** The town's prose calls it "a starter blade", and
  `iron-sword` is that blade; the bow was a second weapon dearer than the 30-gold `rune-of-fire` ceiling the L0 band is
  sized from, which had pushed the shelf to [100, 300] against the approved [100, 250]. Nine rows still clears
  `MinimumStockSize` (8) — do not invent a tenth row to restore the old count, that is how the ceiling drifted.
- **tolvaren swapped `orb-of-force` (300) for `fire-ruby` (200)**, 11 rows unchanged. 16 x 0.50 x 300 = 2400 blew the
  approved 1600 ceiling; `fire-ruby` is the ceiling the L10 comment already named and was the one item it named that
  the shelf lacked. Skarhold's prose ("the two orbs no lower town will handle") now matches the data.
- **The gap that let it through is closed.** Nothing tied `gold_basis`'s arithmetic to the authored shelves, so
  `MerchantBandTests.Gold_Basis_Reproduces_Each_Towns_Band_From_Its_Authored_Shelf` now recomputes every merchant
  town's band from `ZoneStockSeeder.Authors()` ∩ the priced catalogue ∩ `budget.yaml`. It fails on the pre-fix shelves
  with "…crestfall's L0 ceiling from its shelf ceiling (35 gold)… but found 300 (difference of 50)". Its fixture seeds
  `ItemsSeeder` too — the values come from the catalogue, not from a copy of the prices.
- **A shelf edit is inert on an existing database until a migration deletes the old row.** `ZoneStockSeeder` is
  insert-only (`:40-60`) and `ZoneStockItems` has no importer or pack path, so `20261121025507_ReconcileZoneStockShelves`
  deletes `crestfall/hunters-bow` and `tolvaren/orb-of-force`; the next start inserts `tolvaren/fire-ruby` in the freed
  slot. Verified against Postgres 17 (Up removes exactly two rows, Down restores them at SortOrder 100, Down repeatable).
  The authoring gap itself is still open — see the parked card on `ZoneStockItems` having no authoring path.
