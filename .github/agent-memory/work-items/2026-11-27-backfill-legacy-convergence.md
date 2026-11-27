---
kind: work-item
date: 2026-11-27
area: map
title: "The map-graph backfill converges on a legacy seed instead of refusing to boot"
---

### Session 2026-11-27 — a legacy seed must migrate, not crash-loop

`BackfillSeededMapDoorsAndArrivalHops` aborted the A1 deploy on 2026-11-26 and left
`api.veldrath.com` in a restart loop (1263 restarts, `P0001` on every boot). The database was
not corrupt and nothing was half-applied — EF runs the body as one transaction and
`__EFMigrationsHistory` still ended at 2026-11-21 — but the migration demanded two rows that
a much older seed never wrote.

The two shapes, and why a plain pre-#270 seed never produces them:

- **The walk is absent, not misplaced.** The declared re-home moves Aldenmere's
  marketplace→Grey Cup walk from `East` to `North` because the new moor-road door claims
  `East`. Production predates the seeder deriving that walk at all, so neither slot was
  filled; "missing" was listed as a conflict next to "duplicate", and the body raised
  `Refusing to guess which walk the door should replace`.
- **The region markers have no target.** `region:cinderplain:zone:skarhold` and
  `region:saltcliff:zone:saltcliff-heights` carried `TargetMapId = NULL` (all 18 region Zone
  markers did), because `LinkRegionZonesToZoneMaps` only landed in #282. The Arrival hop's
  destination did not exist, so the marker guard raised
  `Refusing to add an Arrival hop that would strand the board`.

Both are now decided by evidence rather than by preference. The walk is **inserted** at the
seeded `North` direction only when *both* slots are empty; a walk in either slot is left
alone, and a destination direction holding another edge still aborts. An unset marker is
**linked** to `'zone:' || "RefId"` (the seeder's own rule) only when that map is live;
a marker pointing somewhere else still aborts. Both writes join `_backfill_changed`, so each
touched map is still version-bumped exactly once, and the notice reports the counts.

`scripts/verify-map-graph-upgrade.sh` gained a `legacy` case: clone the pre-#270 seed, delete
the marketplace walk outright, null every region marker, then boot the head server on it. It
asserts twelve doors, one walk at `North`, the moor-road door in `East`, both markers linked,
nine maps bumped once, and an idempotent restart. The previous cases (plain, main predecessor,
fresh, equivalent, two conflicts) are unchanged, so the fix cannot have loosened them.

Known limit, deliberately not widened here: on a pre-#282 database the migration links only
the two markers it declares. The other region markers stay unset until someone runs the
seeder's rule over them — a separate one-time backfill, not part of this patch.
