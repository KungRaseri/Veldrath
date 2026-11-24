---
kind: area
date: 2026-11-23
area: content
title: "The Halrow Import Pack and Its Way In (2026-11-23)"
---

## The Halrow Import Pack and Its Way In (2026-11-23)

- `content/zonelocations/the-halrow.yaml` is the pool-matrix §8 translation: `rootbound-cellars` bandit-ruffian 5 /
  sump-crawler 5, `collapsed-vault` sump-crawler 6 / bandit-ruffian 4, `halrow-threshold` `[]` (the `SafeRespawn` +
  `IsDungeon=false` dungeon lobby). Every other authored value is the `ZoneLocationsSeeder` row, so a re-import is
  0 created / 0 updated / 3 unchanged.
- **The zone had no way in on foot**: the only node naming it was the region board's `TargetMapId = null` zone node.
  `zone:the-droveway` now carries `zone:the-droveway:exit:zone:the-halrow` on `weathered-waypost` — "Descent into the
  Halrow", the two-way pair labelled `halrow-stair` — and `MapGraphSeederTests` moves with it. The
  waypost's West (drove-road) and the entry chain's East (sunken-fields) are taken, so the exit takes North; check the
  chain's free slots before authoring another exit on a location node.
- Proof on a throwaway Postgres (`rv-content-pg` :55433, database `rv_halrow_a1`, migrated and seeded by running the
  API once against it): loottables 30/2/0 → archetypes 0/30/3 → this file alone 0/2/1 → rerun 0/0/3; stored pools
  entry for entry, `halrow-threshold` `[]` with zero `EnemyInstances`.
- E2E `web/client/tests/a1-the-halrow.e2e.ts` (registered in the manifest stack run, now 27 specs): Crestfall Square
  → the Droveway → the Drove Road → the Weathered Waypost → the halrow stair → the Threshold → the Rootbound Cellars,
  a pool kill pays its imported loot and the bag shows it, home through Varenmark. Focused run 11.7s, non-skipped.
