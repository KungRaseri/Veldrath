---
kind: work-item
date: 2026-11-24
area: content
card: t_58ccac38
title: "Twelve zones got a door: the seeded world has one inbound-door gap left, and it is the sandbox"
---

### Session-t_58ccac38 (2026-11-24) — Twelve zones got a door

PR #241 made `map_unreachable` ignore a region board's `Zone` marker, which left 14 seeded zones flagged as having no
door on another map. This card closed 12 of them — everything but the dev sandbox (`zone:playground`, entered by the
test harness) and `the-halrow`, whose door landed with PR #240. Every door is a `ZoneExit` on a neighbouring zone map
plus one `ExitToZone` walk pair (`origin → exit`, `exit → origin`), the shape of
`zone:grevenmire:exit:zone:drowning-pits`.

- **The 12 are exactly the three regions that had no zone-level doors at all:** Greymoor (4), Saltcliff (4),
  Cinderplain (4). Varenmark already had them. `MapGraphSeederTests.SeedAsync_EveryLiveZoneCanBeEnteredThroughADoorRatherThanItsRegionNode`
  now flags one live zone, `zone:playground`.
- **Adjacency comes from the region board, not from a new claim.** Each door follows a `Walk`/`ExitToRegion` label the
  region map already carries (`aldenmere-barrowdeeps`, `tolvaren-sunken-name`, `ashfields-smoldering-reach`, …), so the
  door display names and descriptions add no geography: the name is `Road to X` / `Track to X` / `Descent into X` and the
  description is the destination zone's own description, verbatim, as every existing `ZoneExit` does. No canon proposal
  was needed.
- **Where each region is entered on foot is what set the chains.** The region entry node is adjacent to one zone marker
  each — `aldenmere` in Greymoor, `saltcliff-heights` in Saltcliff, `skarhold` in Cinderplain — so those three are the
  bootstrap zones, and the doors run outward from them: Aldenmere → Barrow Deeps and Pale Moor, Pale Moor → Soddenfen;
  Tolvaren (the port, adjacent to all three coast zones on the board) → heights, Sunken Name, Tidewrack Flats;
  Skarhold → Ashfields → Smoldering Reach → the Maw.
- **The seed's generated edges did not move.** Nodes 136 → 148 and edges 306 → 330 is exactly +12 and +2 per door:
  `Walk` stays 246, `ExitToRegion` stays 35, `ExitToZone` goes 25 → 49, `ZoneExit` nodes 11 → 23. The two test count
  assertions carry the new totals.
- **Direction slots are the constraint, and the seeder's own chain is the reason a door hangs off a location.** Adding an
  authored edge to a node annexes one cardinal direction before `ConnectZoneEntriesToLocations` runs; if that leaves a
  chain hop nowhere to go, the hop is silently dropped and every later location falls off Arrival's walk. Each of the
  seven touched maps therefore places its door on a node that keeps the chain intact: `zone:smoldering-reach`'s vent
  fields only had East free (South is the authored walk to Arrival, West is the chain's hop back to the Pyreling Den),
  and Aldenmere/Pale Moor/Tolvaren took the last free direction on Arrival. The comments beside each block record which
  slot was taken and why — change a direction there and the next chain hop moves with it.
- **All 12 doors walked from Arrival**, verified with a throwaway probe (since deleted) that ran the zone walk from each
  origin map's entry: `reachableFromArrival=True` for every one, `node_unreachable` empty across all 23 maps,
  `zone:playground` the only `map_unreachable`, all 18 region `Zone` nodes still carry `TargetMapId = zone:<ref>`.
- **Two zones still have no way out of any kind:** `zone:soddenfen` and `zone:tidewrack-flats` keep the
  `map_without_exit` warning (9 zones carried it before this card). That is unchanged behaviour, not this card's debt:
  the nine exit-less zones are left through `LeaveZone` → `LeaveCurrentZoneAsync`, and both now have an inbound door.
